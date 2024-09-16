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
            StartByte = 0,
            Direction = 1,
            Command = 2,
            Size = 3,
            Checksum = 6,
            Data = 9,
        }

        // Cmd
        public Command Command { get; set; }
        // Size
        public UInt16 Size { get; set; }
        // Value
        public UInt32 Checksum { get; set; }
        // Data
        public static readonly byte[] SyncData = {
            0x07, 0x07, 0x12, 0x20,
            0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55,
            0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55,
            0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55,
            0x55, 0x55
        };

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

        public void SetSync()
        {
            // SYNC
            Command = Command.SYNC;
            Size = 36;
            Checksum = 0;
            // Packet作成
            MakePacket(SyncData, SyncData.Length);
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

        public void MakePacket(byte[] data, int length)
        {
            // Base作成
            MakePacketImpl();
            // Data作成
            MakePacketImpl((int)PacketOffset.Data, data, length);
            PacketLength = (int)PacketOffset.Data + length;
            // StopByte作成
            MakePacketImplStopByte(PacketLength);
            PacketLength++;
        }

        public void MakePacket(UInt32 data)
        {
            // Base作成
            MakePacketImpl();
            // Data作成
            MakePacketImpl((int)PacketOffset.Data, data);
            PacketLength = (int)PacketOffset.Data + sizeof(UInt32);
            // StopByte作成
            MakePacketImplStopByte(PacketLength);
            PacketLength++;
        }

        public void MakePacketImplStopByte(int offset)
        {
            Packet[offset] = 0xC0;
        }

        public void MakePacketImpl()
        {
            // Data以外をPacketバッファに展開する

            // 
            Packet[(int)PacketOffset.StartByte] = 0xC0;
            Packet[(int)PacketOffset.Direction] = 0x00;
            Packet[(int)PacketOffset.Command] = (byte)Command;
            // Size
            MakePacketImpl((int)PacketOffset.Size, Size);
            // Checksum
            MakePacketImpl((int)PacketOffset.Checksum, Checksum);
        }

        public void MakePacketImpl(int packetOffset, UInt16 data)
        {
            Packet[packetOffset + 0] = (byte)(data & 0x00FF);
            Packet[packetOffset + 1] = (byte)((data & 0xFF00) >> 8);
        }
        public void MakePacketImpl(int packetOffset, UInt32 data)
        {
            Packet[packetOffset + 0] = (byte)(data & 0x000000FF);
            Packet[packetOffset + 1] = (byte)((data & 0x0000FF00) >> 8);
            Packet[packetOffset + 2] = (byte)((data & 0x00FF0000) >> 16);
            Packet[packetOffset + 3] = (byte)((data & 0xFF000000) >> 24);
        }
        public void MakePacketImpl(int packetOffset, byte[] data, int len)
        {
            Buffer.BlockCopy(data, 0, Packet, packetOffset, len);
        }
    }
}
