using esptool_cs.Serial;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace esptool_cs.EspBootloader
{
    internal class ResponsePacket
    {
        const int DataLength = 1024;

        // Cmd
        public Command Command { get; set; }
        // Size
        public UInt16 Size { get; set; }
        // Value
        public UInt32 Value { get; set; }
        //
        public byte[] Data { get; set; }

        public ResponsePacket() 
        {
            Command = Command.None;
            Size = 0;
            Value = 0;
            Data = new byte[DataLength];
        }

        public void Init()
        {
            Command = Command.None;
            Size = 0;
            Value = 0;
        }
    }

    internal class ResponseAnalyzer : Serial.IAnalyzer
    {
        public ResponsePacket Packet { get; set; }
        StringBuilder msgBuffer;
        public string Header { get; set; }
        public string Error { get; set; }
        public bool HasError { get; set; }
        //
        public RecieveResult Result {  get; set; }

        // 解析情報
        // 解析モード
        public enum Mode
        {
            Header,     // Bootloader接続時に自動で送られてくる情報
            Protocol,   // Protocolでのやりとり
        }
        Mode mode;
        // 受信状況
        enum RecvStateWaitFor
        {
            //
            Header,
            HeaderDummyRead,//読み捨て
            //
            StartByte,
            Direction,
            Command,
            Size,
            Value,
            Data,
            StopByte,
            DummyRead,  //読み捨て

            //
            Timeout,    // 不正データ受信でタイムアウトするまで読み捨てる
        }
        RecvStateWaitFor waitFor;
        // 受信データ数:複数バイトデータ用
        int recvCount;

        public ResponseAnalyzer()
        {
            // 受信データバッファ
            Packet = new ResponsePacket();

            Init(Mode.Header);
        }

        public void Init(Mode mode_)
        {
            mode = mode_;
            recvCount = 0;
            HasError = false;
            msgBuffer = new StringBuilder();

            switch (mode)
            {
                case Mode.Protocol:
                    // 受信待ち状態初期化:Direction待機状態
                    waitFor = RecvStateWaitFor.StartByte;
                    //
                    Packet.Init();
                    break;

                case Mode.Header:
                default:
                    waitFor = RecvStateWaitFor.Header;
                    break;
            }
        }

        public bool Analyze(ref RecvInfo rx)
        {
            switch (mode)
            {
                case Mode.Header:
                    return AnalyzeHeader(ref rx);

                case Mode.Protocol:
                    return AnalyzeProtocol(ref rx);

                default:
                    return true;
            }
        }

        public bool CheckResult(ref RecvInfo rx)
        {
            Result = rx.Result;
            switch (rx.Result)
            {
                case RecieveResult.Match:
                    // Packet受信正常終了
                    break;
            }

            switch (mode)
            {
                case Mode.Header:
                    Header = msgBuffer.ToString();
                    break;

                default:
                    break;
            }

            if (HasError)
            {
                Error = msgBuffer.ToString();
            }

            // 受信情報初期化
            //Init(mode);

            return false;
        }

        private bool AnalyzeHeader(ref RecvInfo rx)
        {
            bool recieved = false;

            switch (waitFor)
            {
                case RecvStateWaitFor.Header:
                    var str = System.Text.Encoding.UTF8.GetString(rx.RxBuff, rx.RxBuffTgtPos, rx.RxBuffOffset);
                    rx.RxBuffTgtPos = rx.RxBuffOffset;
                    msgBuffer.Append(str);
                    if (str.IndexOf("waiting for download") != -1)
                    {
                        waitFor = RecvStateWaitFor.HeaderDummyRead;
                        recieved = true;
                    }
                    break;

                case RecvStateWaitFor.HeaderDummyRead:
                default:
                    // タイムアウトするまで読み捨て
                    break;
            }

            return recieved;
            //return false;
        }

        private bool AnalyzeProtocol(ref RecvInfo rx)
        {
            bool recieved = false;

            // エラーチェック
            // 先頭が 0xC0 以外のときはエラーメッセージとみなす
            if (rx.RxBuff[0] != 0xC0)
            {
                var str = System.Text.Encoding.UTF8.GetString(rx.RxBuff, rx.RxBuffTgtPos, rx.RxBuffOffset);
                msgBuffer.Append(str);
                HasError = true;
                return true;
            }

            int pos;
            for (pos = rx.RxBuffTgtPos; !recieved && pos < rx.RxBuffOffset; pos++)
            {
                var data = rx.RxBuff[pos];

                switch (waitFor)
                {
                    case RecvStateWaitFor.StartByte:
                        // StartByteチェック
                        if (data == 0xC0)
                        {
                            waitFor = RecvStateWaitFor.Direction;
                        }
                        break;

                    case RecvStateWaitFor.Direction:
                        // Directionチェック
                        if (data == 0x01)
                        {
                            waitFor = RecvStateWaitFor.Command;
                        }
                        else
                        {
                            // 未定義Commandを受信した
                            // 一連のシーケンスは無効にする
                            waitFor = RecvStateWaitFor.Timeout;
                        }
                        break;

                    case RecvStateWaitFor.Command:
                        Packet.Command = CommandHelper.CommandConvertTable[data];
                        if (Packet.Command != Command.None)
                        {
                            waitFor = RecvStateWaitFor.Size;
                            recvCount = 0;
                        }
                        else
                        {
                            // 未定義Commandを受信した
                            // 一連のシーケンスは無効にする
                            waitFor = RecvStateWaitFor.Timeout;
                        }
                        break;

                    case RecvStateWaitFor.Size:
                        // Size情報取り込み
                        Packet.Size |= (UInt16)(data << recvCount);
                        recvCount++;
                        // 2バイト受信したらOK
                        if (recvCount >= 2)
                        {
                            waitFor = RecvStateWaitFor.Value;
                            recvCount = 0;
                        }
                        break;

                    case RecvStateWaitFor.Value:
                        // Value情報取り込み
                        Packet.Value |= (UInt32)(data << (recvCount * 8));
                        recvCount++;
                        // 4バイト受信したらOK
                        if (recvCount >= 4)
                        {
                            waitFor = RecvStateWaitFor.Data;
                            recvCount = 0;
                        }
                        break;

                    case RecvStateWaitFor.Data:
                        // Data取り込み
                        Packet.Data[recvCount] = data;
                        recvCount++;
                        // Sizeで示された分だけDataを受信する
                        if (recvCount >= Packet.Size)
                        {
                            waitFor = RecvStateWaitFor.StopByte;
                        }
                        break;

                    case RecvStateWaitFor.StopByte:
                        // StopByteチェック
                        if (data == 0xC0)
                        {
                            // 1パケット受信完了
                            recieved = true;
                        }
                        break;

                    case RecvStateWaitFor.Timeout:
                    default:
                        // 読み捨て
                        break;
                }
            }

            rx.RxBuffTgtPos = pos;

            return recieved;
        }
    }
}
