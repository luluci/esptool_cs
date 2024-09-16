using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace esptool_cs.EspBootloader
{
    // 参考
    // https://docs.espressif.com/projects/esptool/en/latest/esp32/advanced-topics/boot-mode-selection.html
    // https://github.com/espressif/esptool/blob/master/esptool/reset.py

    internal static class Reset
    {
        public static SerialPort MakeSerial()
        {
            // Bootloader設定でSerialPortを開く
            // https://docs.espressif.com/projects/esptool/en/latest/esp32/esptool/serial-connection.html#serial-port-settings

            try
            {
                var serial = new SerialPort
                {
                    BaudRate = 115200,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Parity = Parity.None,
                    // フロー制御
                    DtrEnable = false,
                    RtsEnable = false,
                    Handshake = Handshake.None,
                    // Timeout
                    WriteTimeout = 100,
                    ReadTimeout = 0,
                };

                return serial;
            }
            catch (Exception ex) 
            {
                return null;
            }
        }

        public static async Task RunUserApp(SerialPort serial)
        {
            // 通常モード(ユーザfirmware起動モード)でリセットする
            // RTS/DTRによる操作は Hardware CDC and JTAGモード のときのみ
            // USB-OTGモードでもリセットはかかる

            // 自動ブートローダー回路とUART制御線の関係は共通と想定
            // C# SerialPortのRtsEnable/DtrEnableはRTS/DTR出力レベルとイコールでない点に注意
            // RtsEnable/DtrEnableいずれもfalseのときは常時通信許可となるActiveレベルになっているはず
            // RtsEnable/DtrEnableいずれもtrueのときは通信時にレベルがスイッチングされるはずであり、
            // 非通信時には通信不可のNegativeレベルになっているはず
            // USB-UART変換基盤によりESPマイコンにはUARTに従いActiveLoでポート出力される
            // よってC#上での設定とESPへのポート出力レベルの関係は下記のようになる想定
            // 
            // C#(Windows)            USB-UART   ESP
            // RtsEnable DtrEnable -> RTS DTR -> EN IO0
            // false     false        1   1      1  1
            // true      true         0   0      1  1
            // true      false        0   1      0  1
            // false     true         1   0      1  0


            // 初期状態(RTS,DTR)=(1,1)とする
            serial.RtsEnable = false;
            serial.DtrEnable = false;

            await Task.Delay(100);

            // (RTS,DTR)=(0,1)によりリセット
            // USB-OTGモードではリセットはかかるが、リセット状態が維持されない
            serial.RtsEnable = true;

            await Task.Delay(100);

            // (RTS,DTR)=(1,1)でリセット解除して通常モードで起動
            serial.RtsEnable = false;
            serial.DtrEnable = false;
        }
        public static async Task RunBootloaderUsbJtag(SerialPort serial)
        {
            // 通常モード(ユーザfirmware起動モード)でリセットする

            // 自動ブートローダー回路とUART制御線の関係は共通と想定
            // C# SerialPortのRtsEnable/DtrEnableはRTS/DTR出力レベルとイコールでない点に注意
            // RtsEnable/DtrEnableいずれもfalseのときは常時通信許可となるActiveレベルになっているはず
            // RtsEnable/DtrEnableいずれもtrueのときは通信時にレベルがスイッチングされるはずであり、
            // 非通信時には通信不可のNegativeレベルになっているはず
            // USB-UART変換基盤によりESPマイコンにはUARTに従いActiveLoでポート出力される
            // よってC#上での設定とESPへのポート出力レベルの関係は下記のようになる想定
            // 
            // C#(Windows)            USB-UART   ESP
            // RtsEnable DtrEnable -> RTS DTR -> EN IO0
            // false     false        1   1      1  1
            // true      true         0   0      1  1
            // true      false        0   1      0  1
            // false     true         1   0      1  0


            //// 初期状態(RTS,DTR)=(1,1)とする
            //serial.RtsEnable = true;
            //serial.DtrEnable = true;

            //// (RTS,DTR)=(0,1)によりリセット
            //serial.RtsEnable = false;

            //// (RTS,DTR)=(1,1)でリセット解除して通常モードで起動
            //serial.RtsEnable = true;

            serial.RtsEnable = false;
            serial.DtrEnable = false;

            await Task.Delay(100);

            serial.DtrEnable = true;
            serial.RtsEnable = false;

            await Task.Delay(100);

            serial.RtsEnable = true;
            serial.DtrEnable = false;
            serial.RtsEnable = true;

            await Task.Delay(100);

            serial.DtrEnable = false;
            serial.RtsEnable = false;
        }

        public static async Task RunBootloaderClassic(SerialPort serial)
        {
            // 通常モード(ユーザfirmware起動モード)でリセットする

            // 自動ブートローダー回路とUART制御線の関係は共通と想定
            // C# SerialPortのRtsEnable/DtrEnableはRTS/DTR出力レベルとイコールでない点に注意
            // RtsEnable/DtrEnableいずれもfalseのときは常時通信許可となるActiveレベルになっているはず
            // RtsEnable/DtrEnableいずれもtrueのときは通信時にレベルがスイッチングされるはずであり、
            // 非通信時には通信不可のNegativeレベルになっているはず
            // USB-UART変換基盤によりESPマイコンにはUARTに従いActiveLoでポート出力される
            // よってC#上での設定とESPへのポート出力レベルの関係は下記のようになる想定
            // 
            // C#(Windows)            USB-UART   ESP
            // RtsEnable DtrEnable -> RTS DTR -> EN IO0
            // false     false        1   1      1  1
            // true      true         0   0      1  1
            // true      false        0   1      0  1
            // false     true         1   0      1  0


            //// 初期状態(RTS,DTR)=(1,1)とする
            //serial.RtsEnable = true;
            //serial.DtrEnable = true;

            //await Task.Delay(100);

            //// (RTS,DTR)=(0,1)によりリセット
            //serial.RtsEnable = false;

            //// (RTS,DTR)=(1,1)でリセット解除して通常モードで起動
            //serial.RtsEnable = true;

            serial.RtsEnable = false;
            serial.DtrEnable = false;

            await Task.Delay(100);

            serial.RtsEnable = true;

            await Task.Delay(100);

            serial.DtrEnable = true;
            serial.RtsEnable = false;

            await Task.Delay(100);

            serial.DtrEnable = false;
        }
    }
}
