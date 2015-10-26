using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace LCDLightControl
{
    public static class MonitorPower
    {
        const int SC_MONITORPOWER = 0xF170;
        const int WM_SYSCOMMAND = 0x0112;
        const int MONITOR_ON = -1;
        const int MONITOR_OFF = 2;
        const int MONITOR_STANBY = 1;

        static int HWND_BROADCAST = 0xffff;
        //the message is sent to all 
        //top-level windows in the system

        static int HWND_TOPMOST = -1;
        //the message is sent to one 
        //top-level window in the system

        static int HWND_TOP = 0; //
        static int HWND_BOTTOM = 1; //limited use
        static int HWND_NOTOPMOST = -2; //


        [DllImport("msvcrt.dll")]
        public static extern int puts(string c);
        [DllImport("msvcrt.dll")]
        internal static extern int _flushall();

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg,
        IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        //Or 
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr hwndParent,
        IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        [DllImport("user32.dll")]
        static extern IntPtr PostMessage(int hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        static extern void mouse_event(Int32 dwFlags, Int32 dx, Int32 dy, Int32 dwData, UIntPtr dwExtraInfo);

        private const int MOUSEEVENTF_MOVE = 0x0001;

        public static void Off()
        {
            //SendMessage(ValidHWND, WM_SYSCOMMAND, SC_MONITORPOWER, MONITOR_OFF);
            //IntPtr intTemp = GetConsoleWindow
            //Thread.Sleep(500);
            PostMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, MONITOR_OFF);
        }

        public static void On()
        {
            // This apparently only turns the monitor back on for a few seconds...
            // PostMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, MONITOR_ON);
            // So I guess we can just jump the mouse instead:
            mouse_event(MOUSEEVENTF_MOVE, 0, 1, 0, UIntPtr.Zero);
            Thread.Sleep(40);
            mouse_event(MOUSEEVENTF_MOVE, 0, -1, 0, UIntPtr.Zero);
        }
        public static void Standby()
        {
            PostMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, MONITOR_STANBY);
        }
    }
}
