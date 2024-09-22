using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace esptool_cs.EspBootloader
{
    enum EspRegMap : UInt32
    {
        EFUSE_BASE = 0x60007000,
        EFUSE_BLOCK1_ADDR = EFUSE_BASE + 0x0044,
        EFUSE_BLOCK2_ADDR = EFUSE_BASE + 0x005C,
        EFUSE_RD_MAC_SPI_SYS_0_REG = EFUSE_BASE + 0x0044,
        EFUSE_RD_MAC_SPI_SYS_1_REG = EFUSE_BASE + 0x0048,
    }

    internal class Protocol
    {
        //
        SerialPort serialPort;

        // 送信パケット
        CommandPacket commandPacket;
        // 受信解析
        ResponseAnalyzer respAnlyzr;
        Serial.ProtocolHelper<ResponseAnalyzer> protocol;

        // ESP32情報
        public UInt64 EfuseMacAddr;
        public UInt16 ChipId;

        public string Header { get; set; }
        public string Error { get; set; }

        //
        static int RetryCountSync = 5;
        static int TimeoutSync = 500;

        public Protocol()
        {
            var cmd = CommandHelper.CommandConvertTable[0];

            // SerialPort作成
            serialPort = Reset.MakeSerial();
            //
            commandPacket = new CommandPacket();
            //
            respAnlyzr = new ResponseAnalyzer();
            // 受信解析
            protocol = new Serial.ProtocolHelper<ResponseAnalyzer>(serialPort, respAnlyzr);

            //
            EfuseMacAddr = 0;
        }

        public async Task<bool> Open(string port)
        {
            if (serialPort.IsOpen) serialPort.Close();

            // COMポートを開く
            serialPort.PortName = port;
            serialPort.Open();

            // USB-JTAG方式でBootloader起動トライ
            await Reset.RunBootloaderUsbJtag(serialPort);
            // Header取得
            respAnlyzr.Init();
            await protocol.Run();

            // 受信データ無し(タイムアウトが発生)の場合、Bootloader起動できていない
            if (respAnlyzr.RecieveType != RecieveType.Header)
            {
                // Classic方式でBootloader起動トライ
                await Reset.RunBootloaderClassic(serialPort);
                // Header取得
                respAnlyzr.Init();
                await protocol.Run();
            }

            // 
            if (respAnlyzr.RecieveType != RecieveType.Header)
            {
                serialPort.Close();
                Error = "Bootloader起動に失敗";
            }

            Header = respAnlyzr.Header;

            return respAnlyzr.RecieveType == RecieveType.Header;
        }

        public async Task Close()
        {
            await Reset.RunUserApp(serialPort);
            serialPort.Close();
        }

        public async Task Discard()
        {
            // 読み捨て処理
            respAnlyzr.StartDiscard();
            // 受信タイムアウトまで繰り返す
            do
            {
                await protocol.Run();
                if (respAnlyzr.RecieveType != RecieveType.None)
                {
                    Log.RawLog.Data.Add($"[Discard] {respAnlyzr.ToString()}");
                }
            } while (respAnlyzr.RecieveType != RecieveType.None);
            //
            respAnlyzr.StopDiscard();
        }


        public async Task<bool> GetHardwareInfo()
        {
            bool success = false;

            // Chip情報取得
            // READ_REG作成
            commandPacket.SetReadReg((UInt32)EspRegMap.EFUSE_RD_MAC_SPI_SYS_0_REG);
            // SYNC送信
            success = await SendPacket(500);
            // 通信結果
            if (!success)
            {
                Error = respAnlyzr.Error;
                return false;
            }
            // 
            EfuseMacAddr = respAnlyzr.Packet.Value;

            commandPacket.SetReadReg((UInt32)EspRegMap.EFUSE_RD_MAC_SPI_SYS_1_REG);
            // SYNC送信
            success = await SendPacket(500);
            // 通信結果
            if (!success)
            {
                Error = respAnlyzr.Error;
                return false;
            }
            //
            EfuseMacAddr |= ((UInt64)(respAnlyzr.Packet.Value & 0x0000FFFF) << 32);
            ChipId = (UInt16)respAnlyzr.Packet.Value;

            return true;
        }

        public async Task<bool> SendSync()
        {
            bool success = false;

            // SYNCコマンド作成
            commandPacket.SetSync();
            // SYNC送信
            success = await SendPacket(TimeoutSync, RetryCountSync);
            // 通信結果
            if (!success)
            {
                Error = "初期通信(SYNC)に失敗。";
            }

            return success;
        }

        private async Task<bool> SendPacket(int timeout, int retry = 1)
        {
            bool success = false;

            // Packet送信
            // Packetを作成して空本関数をコールする
            for (int i = 0; i < retry && !success; i++)
            {
                // 送信
                serialPort.Write(commandPacket.Packet, 0, commandPacket.PacketLength);
                // 送信ログ
                Log.RawLog.Data.Add($"[Tx] {commandPacket.ToString()}");

                // 受信処理
                // 何らかの受信がある限り繰り返す
                do
                {
                    // 応答待機
                    await protocol.Run(timeout);
                    // 受信結果チェック
                    // 受信成功で終了、失敗したらリトライ
                    if (respAnlyzr.RecieveType == RecieveType.Protocol && respAnlyzr.Packet.Command == commandPacket.Command)
                    {
                        success = true;
                    }
                    // 応答ログ
                    if (success)
                    {
                        Log.RawLog.Data.Add($"[Accept] {respAnlyzr.ToString()}");
                    }
                    else
                    {
                        Log.RawLog.Data.Add($"[Error] {respAnlyzr.ToString()}");
                    }
                } while (!success && respAnlyzr.RecieveType != RecieveType.None);

                // 受信バッファクリア
                await Discard();
            }

            return success;
        }
    }
}
