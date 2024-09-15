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

        // 解析情報
        // 受信状況
        enum RecvStateWaitFor
        {
            Direction,
            Command,
            Size,
            Value,
            Data,

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

            Init();
        }

        public void Init()
        {
            // 受信待ち状態初期化:Direction待機状態
            waitFor = RecvStateWaitFor.Direction;
            recvCount = 0;
            //
            Packet.Init();
        }

        public bool Analyze(ref RecvInfo rx)
        {
            bool recieved = false;

            int pos;
            for (pos = rx.RxBuffTgtPos; pos < rx.RxBuffOffset; pos++)
            {
                var data = rx.RxBuff[pos];

                switch (waitFor)
                {
                    case RecvStateWaitFor.Direction:
                        // Directionチェック
                        if (data == 0x01)
                        {
                            waitFor = RecvStateWaitFor.Command;
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
                        Packet.Value |= (UInt32)(data << (recvCount*8));
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

        public bool CheckResult(ref RecvInfo rx)
        {
            switch (rx.Type)
            {
                case RxDataType.Match:
                    // Packet受信正常終了
                    break;
            }

            // 受信情報初期化
            Init();

            return false;
        }
    }
}
