using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace esptool_cs.EspBootloader
{
    internal class Protocol
    {
        //
        SerialPort serialPort;

        // 送信パケット
        CommandPacket commandPacket;
        // 受信解析
        ResponseAnalyzer respAnlyzr;
        Serial.Reciever<ResponseAnalyzer> reciever;

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

        }

        public async Task Open(string port)
        {
            // COMポートを開く
            serialPort.PortName = port;
            serialPort.Open();
            // Bootloader起動
            await Reset.RunBootloader(serialPort);
            //
            await reciever.Run();
            await reciever.Run();
            await reciever.Run();
            await reciever.Run();
            await reciever.Run();
        }

        public async Task Close()
        {
            await Reset.RunUserApp(serialPort);
            serialPort.Close();
        }

        public async Task Send(Command cmd)
        {
            // CommandPacket作成、送信
            commandPacket.SetReadReg(0x00);
            serialPort.Write(commandPacket.Packet, 0, commandPacket.PacketLength);
            // ResponsePacket待機
            await reciever.Run();
        }
    }
}
