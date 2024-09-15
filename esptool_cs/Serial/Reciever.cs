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
    public enum RxDataType
    {
        Empty,
        Match,
        Timeout,
        Cancel,
    }

    // 受信データ情報
    public class RecvInfo
    {
        public RxDataType Type { get; set; }
        public const int BuffSize = 1024;
        public const int MatchBuffSize = 1024;
        public byte[] RxBuff { get; set; }
        public int RxBuffOffset { get; set; }
        public int RxBuffTgtPos { get; set; }
        public DateTime TimeStamp { get; set; }
        public bool IsRecieving { get; set; }

        public RecvInfo()
        {
            RxBuff = new byte[BuffSize];
            RxBuffOffset = 0;
            RxBuffTgtPos = 0;
            IsRecieving = false;
        }

        public void Restart()
        {
            IsRecieving = false;
        }
    }

    internal enum TimeoutMode
    {
        Immediate,  // 受信待機を始めたら即時カウント開始
        AnyRecieve, // 何かしらデータ受信後にカウント開始
    }

    internal interface IAnalyzer
    {
        // SerialPortからのデータ取り出しのためにバッファを使い捨てにしてると諸々気になるため
        // 受信データ管理データを使いまわすためにRxDataを介してやりとりを行う。
        bool Analyze(ref RecvInfo rx);
        bool CheckResult(ref RecvInfo rx);
    }

    internal class Reciever<T> : BindableBase
        where T : IAnalyzer
    {
        public ReactivePropertySlim<string> State {  get; set; }

        SerialPort serialPort;
        bool IsRunning;
        int PollingCycle;
        int RxTimeout;
        Utility.CycleTimer PollingTimer = new Utility.CycleTimer();
        Utility.CycleTimer RxBeginTimer = new Utility.CycleTimer();
        Utility.CycleTimer RxEndTimer = new Utility.CycleTimer();
        CancellationTokenSource CancelTokenSource;

        // 受信解析器
        T rxAnlzr;
        TimeoutMode timeoutMode;

        // 受信ハンドラ
        bool HasRecieve;

        // 解析結果
        RecvInfo RecvData;

        public Reciever(SerialPort serial, T rxanlyzer) 
        {
            State = new ReactivePropertySlim<string>("test");

            // SerialPortへの参照だけ持つ
            serialPort = serial;
            // 受信ハンドラ登録
            serialPort.DataReceived += Serial_DataReceived;

            PollingCycle = 100;
            RxTimeout = 1000;

            // 受信解析器
            rxAnlzr = rxanlyzer;
            RecvData = new RecvInfo();
            timeoutMode = TimeoutMode.Immediate;
        }

        public async Task Run(TimeoutMode timeoutmode = TimeoutMode.Immediate)
        {
            // 通信プロトコル起動
            // Stop()するまでデータ受信やタイムアウト、それらに付随する処理を継続する。
            // 本関数はGUIスレッドから呼び出すこと。

            try
            {
                timeoutMode = timeoutmode;
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
            //
            if (timeoutMode == TimeoutMode.Immediate)
            {
                RecvData.IsRecieving = true;
                RxBeginTimer.Start();
            }
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
                    RecvData.Type = RxDataType.Cancel;
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
            // 何かしらのデータ受信後、指定時間経過でタイムアウトする
            if (RecvData.IsRecieving)
            {
                if (RxBeginTimer.WaitForMsec(RxTimeout) <= 0)
                {
                    RecvData.Type = RxDataType.Timeout;
                    return true;
                }
            }

            // 受信バッファ読み出し
            if (HasRecieve || RecvData.RxBuffOffset != RecvData.RxBuffTgtPos || serialPort?.BytesToRead > 0)
            {
                // 受信開始した時間を記憶
                if (!RecvData.IsRecieving)
                {
                    RecvData.IsRecieving = true;
                    RxBeginTimer.Start();
                }
                // 最後に受信した時間を更新
                RxEndTimer.Start();

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
                        RecvData.Type = RxDataType.Match;
                        RecvData.TimeStamp = RxEndTimer.GetTime();
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

            switch (RecvData.Type)
            {
                case RxDataType.Cancel:
                    // Cancelにより通信終了
                    IsRunning = false;
                    break;

                case RxDataType.Timeout:
                    // 受信タイムアウトによる受信シーケンス終了
                case RxDataType.Match:
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
