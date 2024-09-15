using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace esptool_cs.Utility
{
    static public class WindowsApi
    {
        public const int FALSE = 0;
        public const int TRUE = 1;

        public const int WM_SYSCOMMAND = 0x0112;

        public enum WindowMessage : int
        {
            WM_NULL = 0,
            WM_SETTEXT = 0x000C,
            WM_GETTEXT = 0x000D,
            WM_GETTEXTLENGTH = 0x000E,
            WM_PAINT = 0x000F,
            WM_CLOSE = 0x0010,
            WM_QUERYENDSESSION = 0x0011,
            WM_QUIT = 0x0012,
            WM_QUERYOPEN = 0x0013,
            WM_ERASEBKGND = 0x0014,
            WM_SYSCOLORCHANGE = 0x0015,
            WM_ENDSESSION = 0x0016,

            WM_SYSCOMMAND = 0x0112,

            WM_USER = 0x0400,
        }

        public const int SC_MONITORPOWER = 0xF170;

        public const int MONITOR_OFF = 2;

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_FLAG_MOVEONLY = (SWP_NOSIZE | SWP_NOZORDER);

        public const int SW_SHOWNORMAL = 1;
        public const int SW_SHOWMINIMIZED = 2;
        public const int SW_SHOWMAXIMIZED = 3;

        public const int GW_CHILD = 5;

        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;

        public const int WS_CAPTION = 0x00C00000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;

        public enum SystemMetric : int
        {
            SM_CXSCREEN = 0,
            SM_CYSCREEN = 1,
            SM_CYVSCROLL = 2,
            SM_CXVSCROLL = 3,
            SM_CYCAPTION = 4,
            SM_CXBORDER = 5,
            SM_CYBORDER = 6,
            SM_CXDLGFRAME = 7,
            SM_CYDLGFRAME = 8,
            SM_CYVTHUMB = 9,
            SM_CXHTHUMB = 10,
            SM_CXICON = 11,
            SM_CYICON = 12,
            SM_CXCURSOR = 13,
            SM_CYCURSOR = 14,
            SM_CYMENU = 15,
            SM_CXFULLSCREEN = 16,
            SM_CYFULLSCREEN = 17,
            SM_CYKANJIWINDOW = 18,
            SM_MOUSEWHEELPRESENT = 75,
            SM_CYHSCROLL = 20,
            SM_CXHSCROLL = 21,
            SM_DEBUG = 22,
            SM_SWAPBUTTON = 23,
            SM_RESERVED1 = 24,
            SM_RESERVED2 = 25,
            SM_RESERVED3 = 26,
            SM_RESERVED4 = 27,
            SM_CXMIN = 28,
            SM_CYMIN = 29,
            SM_CXSIZE = 30,
            SM_CYSIZE = 31,
            SM_CXFRAME = 32,
            SM_CYFRAME = 33,
            SM_CXMINTRACK = 34,
            SM_CYMINTRACK = 35,
            SM_CXDOUBLECLK = 36,
            SM_CYDOUBLECLK = 37,
            SM_CXICONSPACING = 38,
            SM_CYICONSPACING = 39,
            SM_MENUDROPALIGNMENT = 40,
            SM_PENWINDOWS = 41,
            SM_DBCSENABLED = 42,
            SM_CMOUSEBUTTONS = 43,
            SM_CXFIXEDFRAME = SM_CXDLGFRAME,
            SM_CYFIXEDFRAME = SM_CYDLGFRAME,
            SM_CXSIZEFRAME = SM_CXFRAME,
            SM_CYSIZEFRAME = SM_CYFRAME,
            SM_SECURE = 44,
            SM_CXEDGE = 45,
            SM_CYEDGE = 46,
            SM_CXMINSPACING = 47,
            SM_CYMINSPACING = 48,
            SM_CXSMICON = 49,
            SM_CYSMICON = 50,
            SM_CYSMCAPTION = 51,
            SM_CXSMSIZE = 52,
            SM_CYSMSIZE = 53,
            SM_CXMENUSIZE = 54,
            SM_CYMENUSIZE = 55,
            SM_ARRANGE = 56,
            SM_CXMINIMIZED = 57,
            SM_CYMINIMIZED = 58,
            SM_CXMAXTRACK = 59,
            SM_CYMAXTRACK = 60,
            SM_CXMAXIMIZED = 61,
            SM_CYMAXIMIZED = 62,
            SM_NETWORK = 63,
            SM_CLEANBOOT = 67,
            SM_CXDRAG = 68,
            SM_CYDRAG = 69,
            SM_SHOWSOUNDS = 70,
            SM_CXMENUCHECK = 71,
            SM_CYMENUCHECK = 72,
            SM_SLOWMACHINE = 73,
            SM_MIDEASTENABLED = 74,
            SM_MOUSEPRESENT = 19,
            SM_XVIRTUALSCREEN = 76,
            SM_YVIRTUALSCREEN = 77,
            SM_CXVIRTUALSCREEN = 78,
            SM_CYVIRTUALSCREEN = 79,
            SM_CMONITORS = 80,
            SM_SAMEDISPLAYFORMAT = 81,
            SM_IMMENABLED = 82,
            SM_CXFOCUSBORDER = 83,
            SM_CYFOCUSBORDER = 84,
            SM_TABLETPC = 86,
            SM_MEDIACENTER = 87,
            SM_CMETRICS_OTHER = 76,
            SM_CMETRICS_2000 = 83,
            SM_CMETRICS_NT = 88,
            SM_REMOTESESSION = 0x1000,
            SM_SHUTTINGDOWN = 0x2000,
            SM_REMOTECONTROL = 0x2001,
        }

        public enum SessionEnding : int
        {
            Cancel = FALSE,
            Ok = TRUE,
        }

        [DllImport("Kernel32.dll")]
        public static extern bool AttachConsole(int processId);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(SystemMetric smIndex);

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public System.Drawing.Point ptMinPosition;
            public System.Drawing.Point ptMaxPosition;
            public System.Drawing.Rectangle rcNormalPosition;
        }

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SendMessage(int hWnd, int hMsg, int wParam, int lParam);

        [DllImport("user32.dll")]
        public static extern bool LockWorkStation();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        public static extern int MoveWindow(IntPtr hwnd, int x, int y, int nWidth, int nHeight, int bRepaint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

        public delegate bool EnumWindowCallBack(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern int EnumWindows(EnumWindowCallBack lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowCallBack lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern int GetParent(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder s, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        public static IntPtr GetWindowHandle(System.Windows.Window window)
        {
            var helper = new WindowInteropHelper(window);
            return helper.Handle;
        }

        public static HwndSource GetHwndSource(System.Windows.Window window)
        {
            var source = HwndSource.FromHwnd(GetWindowHandle(window));
            // HwndSourceHook sourceHook;
            // source.AddHook(WndProcSample);
            return source;
        }

        public static IntPtr WndProcSample(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == (int)WindowMessage.WM_QUERYENDSESSION)
            {
                handled = true;
                if (true)
                {
                    // シャットダウンをキャンセルする
                    return (IntPtr)SessionEnding.Cancel;
                }
                else
                {
                    // シャットダウンをキャンセルしない
                    return (IntPtr)SessionEnding.Ok;
                }
            }
            return IntPtr.Zero;
        }

        public static string GetWindowText(IntPtr hWnd)
        {
            const int capacity = 256;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(capacity);
            GetWindowText(hWnd, sb, capacity);
            return sb.ToString();
        }

        public static void DisplayOff()
        {
            SendMessage(-1, WM_SYSCOMMAND, SC_MONITORPOWER, MONITOR_OFF);
        }


        public class ComPortInfo
        {
            public string ComPort { get; set; }
            public string DeviceName { get; set; }
        }
        public static void GetComPortList(IList<ComPortInfo> list)
        {
            try
            {
                // 
                var check = new System.Text.RegularExpressions.Regex("(COM[1-9][0-9]?[0-9]?)");

                // 
                ManagementClass mcPnPEntity = new ManagementClass("Win32_PnPEntity");
                ManagementObjectCollection manageObjCol = mcPnPEntity.GetInstances();
                //
                foreach (var manageObj in manageObjCol)
                {
                    // ManagementObject

                    // Nameプロパティを取得
                    var namePropertyValue = manageObj.GetPropertyValue("Name");
                    if (namePropertyValue is null)
                    {
                        continue;
                    }
                    // COMポートチェック
                    string name = namePropertyValue.ToString();
                    var match = check.Match(name);
                    if (match.Success)
                    {
                        list.Add(new ComPortInfo {
                            ComPort = match.Value,
                            DeviceName = match.Value + " : " + name,
                        });
                    }
                }
            }
            catch
            {
                list.Clear();
            }
        }
    }
}
