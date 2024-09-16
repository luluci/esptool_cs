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
        Serial.Reciever<ResponseAnalyzer> reciever;

        // ESP32情報
        public UInt64 EfuseMacAddr;
        public UInt16 ChipId;

        public string Header { get; set; }
        public string Error { get; set; }

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
            reciever = new Serial.Reciever<ResponseAnalyzer>(serialPort, respAnlyzr);

            //
            EfuseMacAddr = 0;
        }

        public async Task<bool> Open(string port)
        {
            // COMポートを開く
            serialPort.PortName = port;
            serialPort.Open();

            // USB-JTAG方式でBootloader起動トライ
            await Reset.RunBootloaderUsbJtag(serialPort);
            // Header取得
            respAnlyzr.Init(ResponseAnalyzer.Mode.Header);
            await reciever.Run();

            // タイムアウトが発生したらBootloader起動できていない
            if (respAnlyzr.Result == Serial.RecieveResult.Timeout)
            {
                // Classic方式でBootloader起動トライ
                await Reset.RunBootloaderClassic(serialPort);
                // Header取得
                respAnlyzr.Init(ResponseAnalyzer.Mode.Header);
                await reciever.Run();
            }

            Header = respAnlyzr.Header;

            return respAnlyzr.Result == Serial.RecieveResult.Match;
        }

        public async Task Close()
        {
            await Reset.RunUserApp(serialPort);
            serialPort.Close();
        }

        public async Task Send(Command cmd)
        {
            // CommandPacket作成、送信
            respAnlyzr.Init(ResponseAnalyzer.Mode.Protocol);
            // SYNC実行
            commandPacket.SetSync();
            serialPort.Write(commandPacket.Packet, 0, commandPacket.PacketLength);
            // ResponsePacket待機
            await reciever.Run();
            reciever.Discard();

            await Task.Delay(100);

            // Chip情報取得
            respAnlyzr.Init(ResponseAnalyzer.Mode.Protocol);
            commandPacket.SetReadReg((UInt32)EspRegMap.EFUSE_RD_MAC_SPI_SYS_0_REG);
            serialPort.Write(commandPacket.Packet, 0, commandPacket.PacketLength);
            await reciever.Run();
            if (respAnlyzr.HasError)
            {
                Error = respAnlyzr.Error;
                return;
            }
            reciever.Discard();
            EfuseMacAddr = respAnlyzr.Packet.Value;

            respAnlyzr.Init(ResponseAnalyzer.Mode.Protocol);
            commandPacket.SetReadReg((UInt32)EspRegMap.EFUSE_RD_MAC_SPI_SYS_1_REG);
            serialPort.Write(commandPacket.Packet, 0, commandPacket.PacketLength);
            await reciever.Run();
            EfuseMacAddr |= ((UInt64)(respAnlyzr.Packet.Value & 0x0000FFFF) << 32);
            ChipId = (UInt16)respAnlyzr.Packet.Value;
        }
    }
}
