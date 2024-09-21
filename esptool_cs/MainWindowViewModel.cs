using esptool_cs.Utility;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;

namespace esptool_cs
{
    internal class MainWindowViewModel : BindableBase
    {
        // COMポート選択
        public ReactiveCollection<Utility.WindowsApi.ComPortInfo> ComPorts { get; set; }
        public ReactivePropertySlim<int> ComPortsSelectedIndex {  get; set; }
        public ReactiveCommand OnUpdateComPorts {  get; set; }

        // ファームウェア選択

        // ファームウェア書き込み操作
        public ReactiveCommand OnFirmwareWrite { get; set; }

        // 手動操作


        // ESP Serial Bootloader Protocol
        public EspBootloader.Protocol Protocol { get; set; }

        // Logコンテナ参照
        public ReactiveCollection<string> ProtocolLog { get; set; }
        public ReactiveCollection<string> RawLog { get; set; }

        public MainWindowViewModel()
        {
            // Protocol
            Protocol = new EspBootloader.Protocol();

            // COMポートリスト
            ComPorts = new ReactiveCollection<WindowsApi.ComPortInfo>();
            ComPorts.AddTo(Disposables);
            ComPortsSelectedIndex = new ReactivePropertySlim<int>();
            ComPortsSelectedIndex.AddTo(Disposables);
            OnUpdateComPorts = new ReactiveCommand();
            OnUpdateComPorts.Subscribe(x =>
            {
                UpdateComPortList();
            })
            .AddTo(Disposables);

            OnFirmwareWrite = new ReactiveCommand();
            OnFirmwareWrite.Subscribe(async x =>
            {
                await WriteFirmware();
            })
            .AddTo(Disposables);

            // Log
            ProtocolLog = Log.ProtocolLog.Data;
            RawLog = Log.RawLog.Data;

            //ProtocolLog.Add("test1");
            //ProtocolLog.Add("test2aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            //RawLog.Add("test3");
            //RawLog.Add("test4");
        }

        public void Init()
        {
            UpdateComPortList();
        }

        private void UpdateComPortList()
        {
            ComPorts.Clear();
            Utility.WindowsApi.GetComPortList(ComPorts);
            if (ComPorts.Count > 0)
            {
                ComPortsSelectedIndex.Value = 0;
            }
        }

        private async Task WriteFirmware()
        {
            try
            {
                bool result;

                // COMポートが選択されていない場合は異常終了
                if (ComPortsSelectedIndex.Value < 0)
                {
                    ProtocolLog.Add("COMポートが選択されていません");
                    return;
                }

                // COMポート取得
                var port = ComPorts[ComPortsSelectedIndex.Value];
                // Bootloader接続
                result = await Protocol.Open(port.ComPort);
                if (!result)
                {
                    // 異常メッセージを出力して終了
                    ProtocolLog.Add(Protocol.Error);
                    return;
                }
                else
                {
                    // Bootloader接続時の受信内容を出力
                    ProtocolLog.Add(Protocol.Header);
                }

                // SYNC送信
                result = await Protocol.SendSync();
                if (!result)
                {
                    // 異常メッセージを出力して終了
                    ProtocolLog.Add(Protocol.Error);
                    return;
                }

                //
                var is_init = await Protocol.Send(EspBootloader.Command.READ_REG);
                if (is_init)
                {
                    ProtocolLog.Add($"MAC addr: {Protocol.EfuseMacAddr:X12}");
                    ProtocolLog.Add($"CHIP ID : {Protocol.ChipId:X4}");
                }
                else
                {
                    ProtocolLog.Add(Protocol.Error);
                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                await Protocol.Close();
            }
        }
    }
}
