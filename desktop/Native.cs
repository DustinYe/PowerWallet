using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PowerWallet {
    public static class ScreenPolicy {
        public static Size Fit(double workWidth,double workHeight) {
            return new Size(Math.Max(240,Math.Min(920,workWidth-24)),Math.Max(220,Math.Min(760,workHeight-24)));
        }
        public static bool RequiresPageScroll(double height) {return height<400;}
    }
    internal static class Native {
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct MONITORINFO { public int Size;public RECT Monitor,Work;public uint Flags; }
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor,ref MONITORINFO info);
        [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd,int command);
        public static void FocusExisting() {
            foreach(var process in System.Diagnostics.Process.GetProcesses()) {
                try{if(process.MainWindowTitle=="PowerWallet · 电源键钱包"){ShowWindow(process.MainWindowHandle,9);SetForegroundWindow(process.MainWindowHandle);return;}}catch(InvalidOperationException){}
            }
        }
        public static void DarkTitle(Window window,bool dark) {int value=dark?1:0;var hwnd=new WindowInteropHelper(window).Handle;if(hwnd!=IntPtr.Zero)DwmSetWindowAttribute(hwnd,20,ref value,4);}
        public static void Fit(Window window,bool initial) {
            var hwnd=new WindowInteropHelper(window).Handle;
            var info=new MONITORINFO();info.Size=Marshal.SizeOf(info);
            if(!GetMonitorInfo(MonitorFromWindow(hwnd,2),ref info))return;
            double scale=Math.Max(1,GetDpiForWindow(hwnd))/96.0;
            double width=(info.Work.Right-info.Work.Left)/scale,height=(info.Work.Bottom-info.Work.Top)/scale;
            var fit=ScreenPolicy.Fit(width,height);
            window.MinWidth=Math.Min(480,fit.Width);window.MinHeight=Math.Min(420,fit.Height);
            if(initial || window.Width>fit.Width)window.Width=fit.Width;
            if(initial || window.Height>fit.Height)window.Height=fit.Height;
        }
    }
}
