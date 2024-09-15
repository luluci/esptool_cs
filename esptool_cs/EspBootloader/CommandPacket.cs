using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace esptool_cs.EspBootloader
{
    internal class CommandPacket
    {
        const int DataLength = 1024;

        enum PacketOffset
        {
            Direction = 0,
            Command = 1,
            Size = 2,
            Checksum = 5,
            Data = 8,
        }

        // Cmd
        public Command Command { get; set; }
        // Size
        public UInt16 Size { get; set; }
        // Value
        public UInt32 Checksum { get; set; }
        // Data

        // Dataは送信用バッファに直接展開する
        public byte[] Packet { get; set; }
        public int PacketLength { get; set; }

        public CommandPacket()
        {
            Command = Command.None;
            Size = 0;
            Checksum = 0;
            Packet = new byte[DataLength];
        }

        public void Init()
        {
            Command = Command.None;
            Size = 0;
            Checksum = 0;
        }

        public void SetReadReg(UInt32 addr)
        {
            // READ_REG
            Command = Command.READ_REG;
            Size = 4;
            Checksum = 0;
            // Packet作成
            MakePacket(addr);
        }

        public void MakePacket(UInt32 data)
        {
            MakePacket();
            MakePacket((int)PacketOffset.Data, data);
            //
            PacketLength = (int)PacketOffset.Data + sizeof(UInt32);
        }

        public void MakePacket()
        {
            // Data以外をPacketバッファに展開する

            // 
            Packet[0] = 0x00;
            Packet[1] = (byte)Command;
            // Size
            MakePacket((int)PacketOffset.Size, Size);
            // Checksum
            MakePacket((int)PacketOffset.Checksum, Checksum);
        }

        public void MakePacket(int packetOffset, UInt16 data)
        {
            Packet[packetOffset + 0] = (byte)(data & 0x00FF);
            Packet[packetOffset + 1] = (byte)((data & 0xFF00) >> 8);
        }
        public void MakePacket(int packetOffset, UInt32 data)
        {
            Packet[packetOffset + 0] = (byte)(data & 0x000000FF);
            Packet[packetOffset + 1] = (byte)((data & 0x0000FF00) >> 8);
            Packet[packetOffset + 2] = (byte)((data & 0x00FF0000) >> 16);
            Packet[packetOffset + 3] = (byte)((data & 0xFF000000) >> 24);
        }
    }
}
