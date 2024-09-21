using esptool_cs.EspBootloader;
using esptool_cs.Utility;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace esptool_cs.Serial
{
    public enum RecieveResult
    {
        Empty,
        Match,
        Timeout,
        IntervalTimeout,    // バイト間タイムアウト
        Cancel,
    }

    // 受信データ情報
    public class RecvInfo
    {
        public RecieveResult Result { get; set; }
        public const int BuffSize = 1024;
        public const int MatchBuffSize = 1024;
        public byte[] RxBuff { get; set; }
        public int RxBuffOffset { get; set; }
        public int RxBuffTgtPos { get; set; }
        public DateTime TimeStamp { get; set; }

        public RecvInfo()
        {
            RxBuff = new byte[BuffSize];
            RxBuffOffset = 0;
            RxBuffTgtPos = 0;
        }

        public void Init()
        {
            // 受信バッファはリセットして受信開始する
            RxBuffOffset = 0;
            RxBuffTgtPos = 0;
        }

        public void Restart()
        {
            // 受信バッファ残りを継続して解析を再開する
            Result = RecieveResult.Empty;
        }
    }

    internal interface IAnalyzer
    {
        // SerialPortからのデータ取り出しのためにバッファを使い捨てにしてると諸々気になるため
        // 受信データ管理データを使いまわすためにRxDataを介してやりとりを行う。
        bool Analyze(ref RecvInfo rx);
        bool CheckResult(ref RecvInfo rx);
    }

    internal class ProtocolHelper<T> : BindableBase
        where T : IAnalyzer
    {
        public ReactivePropertySlim<string> State {  get; set; }

        SerialPort serialPort;
        bool IsRunning;
        int PollingCycle;

        // タイムアウト判定
        int RxCompleteTimeout;//受信完了タイムアウト:一定期間内に意図したデータを受信できなかった
        int RxIntervalTimeout;//バイト間タイムアウト:最後の受信から一定期間内に受信が無かった

        // 時間計測タイマ
        Utility.CycleTimer PollingTimer = new Utility.CycleTimer();
        Utility.CycleTimer RxCompleteTimer = new Utility.CycleTimer();
        Utility.CycleTimer RxIntervalTimer = new Utility.CycleTimer();
        // 中断token
        CancellationTokenSource CancelTokenSource;

        // 受信解析器
        T rxAnlzr;

        // 受信ハンドラ
        bool HasRecieve;

        // 解析結果
        public RecvInfo RecvData;

        public ProtocolHelper(SerialPort serial, T rxanlyzer) 
        {
            State = new ReactivePropertySlim<string>("test");

            // SerialPortへの参照だけ持つ
            serialPort = serial;
            // 受信ハンドラ登録
            serialPort.DataReceived += Serial_DataReceived;

            PollingCycle = 100;
            RxCompleteTimeout = 100;
            RxIntervalTimeout = 100;

            // 受信解析器
            rxAnlzr = rxanlyzer;
            RecvData = new RecvInfo();
        }

        public async Task<RecieveResult> Run(int compTimeout = -1, int itvlTimeout = -1)
        {
            // 通信プロトコル起動
            // Stop()するまでデータ受信やタイムアウト、それらに付随する処理を継続する。
            // 本関数はGUIスレッドから呼び出すこと。

            try
            {
                // タイムアウト設定
                if (compTimeout != -1) RxCompleteTimeout = compTimeout;
                if (itvlTimeout != -1) RxIntervalTimeout = itvlTimeout;
                // 中断token作成
                CancelTokenSource = new CancellationTokenSource();
                InitBeforeTaskStart();

                IsRunning = true;
                while (IsRunning)
                {
                    // 受信開始前に解析情報系を初期化
                    InitBeforeRecvAnalyze();
                    // イベントポーリング開始
                    // 通信フレーム受信完了、通信タイムアウト等のイベント発生したら処理を返す
                    // 通信フレーム受信完了したら対応する処理を実施し、再度受信ポーリング処理に戻る、という流れが基本になる。
                    await PollingEvent(CancelTokenSource.Token);
                    // 発生イベントに応じて処理を実施
                    CheckEvent();
                }
            }
            catch
            {
                IsRunning = false;
            }

            return RecvData.Result;
        }

        public void Stop()
        {
            // 通信終了通知
            CancelTokenSource.Cancel();
        }

        public void InitBeforeTaskStart()
        {
        }

        public void InitBeforeRecvAnalyze()
        {
            // 受信結果バッファをリスタート
            RecvData.Restart();
            // 受信処理開始
            RxCompleteTimer.Start();
            RxIntervalTimer.Stop();
        }

        public async Task PollingEvent(CancellationToken cancel)
        {
            var isRxEvent = false;

            while (true)
            {
                PollingTimer.Start();

                // Task Cancel判定
                if (cancel.IsCancellationRequested)
                {
                    //throw new OperationCanceledException("Cancel Requested");
                    RecvData.Result = RecieveResult.Cancel;
                    return;
                }
                // Rx
                isRxEvent = PollingRecv();
                if (isRxEvent)
                {
                    // 受信イベントがあればループ終了して受信結果を処理する
                    return;
                }

                await PollingTimer.WaitAsync(PollingCycle);
            }
        }

        public bool PollingRecv()
        {
            // タイムアウト判定
            // 受信完了タイムアウト
            if (RxCompleteTimer.WaitForMsec(RxCompleteTimeout) <= 0)
            {
                RecvData.Result = RecieveResult.Timeout;
                return true;
            }
            // バイト間タイムアウト
            // 何かしらのデータ受信後、指定時間経過でタイムアウトする
            if (RxIntervalTimer.IsRunning)
            {
                if (RxIntervalTimer.WaitForMsec(RxIntervalTimeout) <= 0)
                {
                    RecvData.Result = RecieveResult.IntervalTimeout;
                    return true;
                }
            }

            // 受信バッファ読み出し
            if (HasRecieve || RecvData.RxBuffOffset != RecvData.RxBuffTgtPos || serialPort?.BytesToRead > 0)
            {
                // 最後に受信した時間を更新
                // 厳密にはHasRecieveのときのみ
                RxIntervalTimer.Start();

                try
                {
                    // 受信バッファに未解析分があるときはシリアル受信の読み出しをしない
                    // 受信バッファが空で読み出しをしてしまうと例外が飛ぶので、無駄な処理をしないためにケアする
                    if (RecvData.RxBuffOffset == RecvData.RxBuffTgtPos)
                    {
                        // すぐに受信フラグを下す
                        // フラグを下した後にシリアル受信を読み出すことで取得漏れは無くなるはず
                        HasRecieve = false;
                        // 受信バッファ読み出し
                        // データ取り出し先バッファを大きくすることで処置しているが、
                        // 厳密にはSerialPort側にデータが残っている可能性があることに注意
                        var len = serialPort.Read(RecvData.RxBuff, RecvData.RxBuffOffset, RecvInfo.BuffSize - RecvData.RxBuffOffset);
                        RecvData.RxBuffOffset += len;
                    }
                    // 受信解析
                    var result = rxAnlzr.Analyze(ref RecvData);
                    // 受信バッファケア
                    // すべて読み出していたらクリアして領域を確保する
                    // 受信バッファサイズ以上の受信があっても順繰りに処理できるようにするケア
                    // 解析した分は受信解析マッチバッファに移してある
                    if (RecvData.RxBuffOffset == RecvData.RxBuffTgtPos)
                    {
                        RecvData.RxBuffOffset = 0;
                        RecvData.RxBuffTgtPos = 0;
                    }
                    // 何かしらマッチしていたら通知
                    if (result)
                    {
                        RecvData.Result = RecieveResult.Match;
                        RecvData.TimeStamp = RxIntervalTimer.GetTime();
                        return true;
                    }
                }
                catch (TimeoutException)
                {
                    // HasRecieveを下してからReadするまでに受信した場合、
                    // 受信バッファが空でHasRecieveが立っている可能性があるが、
                    // そのケースはスルー
                }
            }
            else
            {
                //if (AnalyzeTimeout(RxEndTimer))
                //{
                //    RecvData.Type = RxDataType.Match;
                //    return true;
                //}
            }

            // 受信イベント無し
            return false;
        }

        public void CheckEvent()
        {

            switch (RecvData.Result)
            {
                case RecieveResult.Cancel:
                    // Cancelにより通信終了
                    IsRunning = false;
                    break;

                case RecieveResult.Timeout:
                    // 受信タイムアウトによる受信シーケンス終了
                case RecieveResult.Match:
                    // 受信解析で定義したルールにマッチ
                    // 受信解析器に通知してポーリング終了をチェック
                    if (!rxAnlzr.CheckResult(ref RecvData))
                    {
                        IsRunning = false;
                    }
                    break;

                default:
                    break;
            }
        }

        private void Serial_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // 受信ハンドラ
            // awaitでポーリング処理しているタスクから受信解析結果を返却する必要があるため、
            // 受信解析もタスクで実施するように、タスクへ受信あり通知のみ行う。
            HasRecieve = true;
        }

    }
}
