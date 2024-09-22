using esptool_cs.Serial;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static esptool_cs.EspBootloader.ResponseAnalyzer;

namespace esptool_cs.EspBootloader
{
    internal class ProtocolPacket
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

        public ProtocolPacket() 
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

        public override string ToString()
        {
            return $"Command={Command.ToString()} Size={Size} Value={Value} Data={BitConverter.ToString(Data)}";
        }
    }

    internal enum RecieveType
    {
        None,   // 受信データなし

        //
        Ascii,  // 未定義ASCIIデータ受信
        Binary, // 未定義バイナリデータ受信

        // 確定データ
        Header, // Bootloader接続時の通知ASCII受信
        Protocol,   // SerialProtocolデータ受信

    }

    internal class ResponseAnalyzer : Serial.IAnalyzer
    {
        public ProtocolPacket Packet { get; set; }
        ProtocolPacket RecievePacket { get; set; }
        ProtocolPacket DiscardPacket { get; set; }
        StringBuilder msgBuffer;
        public string Header { get; set; }
        public string Log {  get; set; }
        public string Error { get; set; }
        //
        public RecieveType RecieveType { get; set; }
        //
        public bool IsBufferEmpty { get; set; }

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
            FirstByte,

            //
            Header,
            //
            StartByte,
            Direction,
            Command,
            Size,
            Value,
            Data,
            StopByte,

            //
            Timeout,    // 不正データ受信でタイムアウトするまで読み捨てる
        }
        RecvStateWaitFor waitFor;
        // 受信データ数:複数バイトデータ用
        int recvCount;

        public ResponseAnalyzer()
        {
            // 受信データバッファ
            RecievePacket = new ProtocolPacket();
            DiscardPacket = new ProtocolPacket();
            Packet = RecievePacket;

            Init();
        }

        public void Init()
        {
            recvCount = 0;
            msgBuffer = new StringBuilder();
            Packet.Init();
            RecieveType = RecieveType.None;
            waitFor = RecvStateWaitFor.FirstByte;
            IsBufferEmpty = false;
        }

        public void StartDiscard()
        {
            Packet = DiscardPacket;
        }
        public void StopDiscard()
        {
            Packet = RecievePacket;
        }

        public bool Analyze(ref RecvInfo rx)
        {
            // 最初の受信データによってバイナリかASCIIか判定する
            if (waitFor == RecvStateWaitFor.FirstByte)
            {
                AnalyzeRecvMode(ref rx);
            }

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
            switch (rx.Result)
            {
                case RecieveResult.Match:
                    // Packet受信正常終了
                    break;
            }

            //
            if (rx.RxBuffOffset == rx.RxBuffTgtPos)
            {
                IsBufferEmpty = true;
            }

            //
            switch (RecieveType)
            {
                case RecieveType.Header:
                    Header = msgBuffer.ToString();
                    //Log = Header;
                    break;

                case RecieveType.Protocol:
                    break;

                case RecieveType.Ascii:
                    //Log = msgBuffer.ToString();
                    break;

                case RecieveType.Binary:

                    break;

                case RecieveType.None:
                    //Log = "";
                    break;

                default:
                    //Log = "<error unknown recieve>";
                    break;
            }

            // 受信情報初期化
            //Init();

            return false;
        }

        public override string ToString()
        {
            switch (RecieveType)
            {
                case RecieveType.Header:
                    return Header;

                case RecieveType.Ascii:
                    return msgBuffer.ToString();

                case RecieveType.Protocol:
                    return Packet.ToString();

                case RecieveType.Binary:
                    return Packet.Data.ToString();

                case RecieveType.None:
                    return "<Recieved NoData>";

                default:
                    return "<error unknown recieve>";
            }
        }

        private void AnalyzeRecvMode(ref RecvInfo rx)
        {
            // 最初のバイトを取得
            var firstByte = rx.RxBuff[rx.RxBuffTgtPos];
            // 0xC0の場合はBootloaderのSerial Protocolのコマンド
            // それ以外はASCIIと見なすがチェックするか？
            if (firstByte == 0xC0)
            {
                mode = Mode.Protocol;
                RecieveType = RecieveType.Binary;
                waitFor = RecvStateWaitFor.StartByte;
            }
            else
            {
                mode = Mode.Header;
                RecieveType = RecieveType.Ascii;
                waitFor = RecvStateWaitFor.Header;
            }
        }

        private bool AnalyzeHeader(ref RecvInfo rx)
        {
            bool recieved = false;

            var str = System.Text.Encoding.UTF8.GetString(rx.RxBuff, rx.RxBuffTgtPos, rx.RxBuffOffset);
            rx.RxBuffTgtPos = rx.RxBuffOffset;
            msgBuffer.Append(str);
            if (str.IndexOf("waiting for download") != -1)
            {
                RecieveType = RecieveType.Header;
                recieved = true;
            }

            return recieved;
            //return false;
        }

        private bool AnalyzeProtocol(ref RecvInfo rx)
        {
            bool recieved = false;

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
                            RecieveType = RecieveType.Protocol;
                            recieved = true;
                        }
                        break;

                    case RecvStateWaitFor.Timeout:
                    default:
                        // 未定義データ
                        // 0xC0はデータの切れ目と見なす
                        if (data == 0xC0)
                        {
                            // 1パケット受信完了
                            recieved = true;
                        }
                        break;
                }
            }

            rx.RxBuffTgtPos = pos;

            return recieved;
        }
    }
}
