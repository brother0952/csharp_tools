using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ScreenshotTool
{
    public static class HotKeyManager
    {
        private const int WM_HOTKEY = 0x0312;
        private static int currentId;
        private static Action hotkeyAction;
        private static HwndSource source;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public static void Initialize(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            source = HwndSource.FromHwnd(handle);
            if (source != null)
            {
                source.AddHook(WndProc);
            }
        }

        public static void RegisterHotKey(Key key, KeyModifier modifiers, Action action)
        {
            if (source == null) return;
            var handle = source.Handle;
            currentId = currentId + 1;
            
            if (!RegisterHotKey(handle, currentId, (uint)modifiers, (uint)KeyInterop.VirtualKeyFromKey(key)))
            {
                throw new InvalidOperationException("无法注册热键");
            }

            hotkeyAction = action;
        }

        public static void UnregisterHotKey()
        {
            if (source == null) return;
            var handle = source.Handle;
            UnregisterHotKey(handle, currentId);
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == currentId)
            {
                if (hotkeyAction != null)
                {
                    hotkeyAction.Invoke();
                }
                handled = true;
            }
            return IntPtr.Zero;
        }
    }

    [Flags]
    public enum KeyModifier
    {
        None = 0x0000,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Windows = 0x0008
    }
} 