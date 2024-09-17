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

        // Log
        public ReactivePropertySlim<string> Log {  get; set; }

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
            Log = new ReactivePropertySlim<string>("");
            Log.AddTo(Disposables);
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
                    Log.Value += "COMポートが選択されていません\n";
                    return;
                }

                // COMポート取得
                var port = ComPorts[ComPortsSelectedIndex.Value];
                // Bootloader接続
                result = await Protocol.Open(port.ComPort);
                if (!result)
                {
                    // 異常メッセージを出力して終了
                    Log.Value += Protocol.Error;
                    Log.Value += "\n";
                    return;
                }
                else
                {
                    // Bootloader接続時の受信内容を出力
                    Log.Value += Protocol.Header;
                }

                // SYNC送信
                result = await Protocol.SendSync();
                if (!result)
                {
                    // 異常メッセージを出力して終了
                    Log.Value += Protocol.Error;
                    Log.Value += "\n";
                    return;
                }

                //
                var is_init = await Protocol.Send(EspBootloader.Command.READ_REG);
                if (is_init)
                {
                    Log.Value += $"MAC addr: {Protocol.EfuseMacAddr:X12}\n";
                    Log.Value += $"CHIP ID : {Protocol.ChipId:X4}\n";
                }
                else
                {
                    Log.Value += Protocol.Error;
                    Log.Value += "\n";
                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                await Protocol.Close();
            }

            Log.Value += "fin!";
        }
    }
}
