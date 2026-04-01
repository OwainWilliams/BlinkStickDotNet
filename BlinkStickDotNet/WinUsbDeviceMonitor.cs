#region License
// Copyright 2013 by Agile Innovative Ltd
//
// This file is part of BlinkStick.HID library.
//
// BlinkStick.HID library is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by the Free
// Software Foundation, either version 3 of the License, or (at your option) any
// later version.
//
// BlinkStick.HID library is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
// FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License along with
// BlinkStick.HID library. If not, see http://www.gnu.org/licenses/.
#endregion

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace BlinkStickDotNet
{
    public class WinUsbDeviceMonitor : IDisposable
    {
        /// <summary>
        /// Occurs when device list changed.
        /// </summary>
        public event EventHandler DeviceListChanged;

        /// <summary>
        /// Raises the device list changed event.
        /// </summary>
        protected void OnDeviceListChanged()
        {
            if (Enabled && DeviceListChanged != null)
            {
                DeviceListChanged(this, new EventArgs());
            }
        }

        public Boolean Enabled { get; set; }

        private IntPtr mHwnd = IntPtr.Zero;
        private Thread mMessageThread;
        private readonly ManualResetEventSlim mReadyEvent = new ManualResetEventSlim(false);
        private WndProcDelegate mWndProcDelegate;
        private GCHandle mGCHandle;
        private string mClassName;

        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        private const int DBT_DEVNODES_CHANGED = 0x0007;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int ptX;
            public int ptY;
        }

        public WinUsbDeviceMonitor()
        {
            this.Enabled = false;
            mMessageThread = new Thread(MessageLoop) { IsBackground = true, Name = "WinUsbDeviceMonitor" };
            mMessageThread.Start();
            mReadyEvent.Wait();
        }

        private void MessageLoop()
        {
            mClassName = "WinUsbDeviceMonitor_" + Guid.NewGuid().ToString("N");
            mWndProcDelegate = WndProc;
            mGCHandle = GCHandle.Alloc(mWndProcDelegate);

            var wc = new WNDCLASSEX();
            wc.cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX));
            wc.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(mWndProcDelegate);
            wc.lpszClassName = mClassName;
            RegisterClassEx(ref wc);

            mHwnd = CreateWindowEx(0, mClassName, "WinUsbDeviceMonitor", 0, -200, -200, 10, 10, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            mReadyEvent.Set();

            MSG msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_DEVICECHANGE)
            {
                int wp = wParam.ToInt32();
                if (wp == DBT_DEVICEARRIVAL || wp == DBT_DEVICEREMOVECOMPLETE || wp == DBT_DEVNODES_CHANGED)
                {
                    OnDeviceListChanged();
                }
            }
            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        public void Dispose()
        {
            if (mHwnd != IntPtr.Zero)
            {
                DestroyWindow(mHwnd);
                mHwnd = IntPtr.Zero;
            }
            if (mGCHandle.IsAllocated) mGCHandle.Free();
            if (mClassName != null)
            {
                UnregisterClass(mClassName, IntPtr.Zero);
                mClassName = null;
            }
        }
    }
}
