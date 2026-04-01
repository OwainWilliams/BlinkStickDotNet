// Copyright © 2006-2010 Travis Robinson. All rights reserved.
//
// website: http://sourceforge.net/projects/libusbdotnet
// e-mail:  libusbdotnet@gmail.com
//
// This program is free software; you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the
// Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
// for more details.
//
// You should have received a copy of the GNU General Public License along
// with this program; if not, write to the Free Software Foundation, Inc.,
// 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA. or
// visit www.gnu.org.
//
//
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace LibUsbDotNet.DeviceNotify.Internal
{
    internal sealed class DevNotifyNativeWindow : IDisposable
    {
        private const string WINDOW_CAPTION = "{18662f14-0871-455c-bf99-eff135425e3a}";
        private const int WM_DEVICECHANGE = 0x219;
        private const string WINDOW_CLASS_NAME = "DevNotifyNativeWindowClass";

        private readonly OnDeviceChangeDelegate mDelDeviceChange;
        private readonly OnHandleChangeDelegate mDelHandleChanged;

        private IntPtr mHwnd = IntPtr.Zero;
        private GCHandle mGCHandle;
        private WndProcDelegate mWndProcDelegate;
        private Thread mMessageThread;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

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

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

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

        private readonly ManualResetEventSlim mReadyEvent = new ManualResetEventSlim(false);
        private string mClassName;

        internal DevNotifyNativeWindow(OnHandleChangeDelegate delHandleChanged, OnDeviceChangeDelegate delDeviceChange)
        {
            mDelHandleChanged = delHandleChanged;
            mDelDeviceChange = delDeviceChange;

            mMessageThread = new Thread(MessageLoop) { IsBackground = true, Name = "DevNotifyNativeWindow" };
            mMessageThread.Start();
            mReadyEvent.Wait();
        }

        private void MessageLoop()
        {
            mClassName = WINDOW_CLASS_NAME + "_" + Guid.NewGuid().ToString("N");
            mWndProcDelegate = WndProc;
            mGCHandle = GCHandle.Alloc(mWndProcDelegate);

            var wc = new WNDCLASSEX();
            wc.cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX));
            wc.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(mWndProcDelegate);
            wc.lpszClassName = mClassName;
            RegisterClassEx(ref wc);

            mHwnd = CreateWindowEx(0, mClassName, WINDOW_CAPTION, 0, -100, -100, 50, 50, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            mDelHandleChanged(mHwnd);
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
                // Build a Message-like struct to pass to delegate
                var m = new FakeMessage { Msg = (int)msg, WParam = wParam, LParam = lParam };
                mDelDeviceChange(ref m);
            }
            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        public void DestroyHandle()
        {
            if (mHwnd != IntPtr.Zero)
            {
                DestroyWindow(mHwnd);
                mHwnd = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            DestroyHandle();
            if (mGCHandle.IsAllocated) mGCHandle.Free();
            if (mClassName != null)
            {
                UnregisterClass(mClassName, IntPtr.Zero);
                mClassName = null;
            }
        }

        #region Nested Types

        internal delegate void OnDeviceChangeDelegate(ref FakeMessage m);
        internal delegate void OnHandleChangeDelegate(IntPtr windowHandle);

        /// <summary>Replaces System.Windows.Forms.Message for passing Win32 message data.</summary>
        internal struct FakeMessage
        {
            public int Msg;
            public IntPtr WParam;
            public IntPtr LParam;
        }

        #endregion
    }
}
