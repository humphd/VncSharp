using System;
using System.Runtime.InteropServices;

namespace VncSharp
{
    public static class NativeMethods
    {
        #region Functions

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProcDelegate lpfn, IntPtr hMod, int dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, KBDLLHOOKSTRUCT lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetGUIThreadInfo(int idThread, GUITHREADINFO lpgui);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern int MapVirtualKey(int uCode, int uMapType);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern int ToAscii(int uVirtKey, int uScanCode, byte[] lpKeyState, byte[] lpwTransKey, int fuState);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        #endregion

        #region Delegates

        public delegate IntPtr LowLevelKeyboardProcDelegate(int nCode, IntPtr wParam, KBDLLHOOKSTRUCT lParam);

        #endregion

        #region Structures

        [StructLayout(LayoutKind.Sequential)]
        public class KBDLLHOOKSTRUCT
        {
            internal int vkCode;
            internal int scanCode;
            internal int flags;
            internal int time;
            internal IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            internal int left;
            internal int top;
            internal int right;
            internal int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class GUITHREADINFO
        {
            public GUITHREADINFO()
            {
                cbSize = Convert.ToInt32(Marshal.SizeOf(this));
            }

            internal int cbSize;
            internal int flags;
            internal IntPtr hwndActive;
            internal IntPtr hwndFocus;
            internal IntPtr hwndCapture;
            internal IntPtr hwndMenuOwner;
            internal IntPtr hwndMoveSize;
            internal IntPtr hwndCaret;
            internal RECT rcCaret;
        }

        #endregion

        #region Constants
        // GetAncestor
        public const int GA_ROOT = 2;

        // SetWindowsHookEx
        public const int WH_KEYBOARD_LL = 13;

        // LowLevelKeyboardProcDelegate
        public const int HC_ACTION = 0;

        // SendMessage
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;

        // GetAsyncKeyState
        public const int KEYSTATE_PRESSED = 0x8000;

        #region Virtual Keys
        public const int VK_CANCEL = 0x0003;
        public const int VK_BACK = 0x0008;
        public const int VK_TAB = 0x0009;
        public const int VK_CLEAR = 0x000C;
        public const int VK_RETURN = 0x000D;
        public const int VK_PAUSE = 0x0013;
        public const int VK_ESCAPE = 0x001B;
        public const int VK_SNAPSHOT = 0x002C;
        public const int VK_INSERT = 0x002D;
        public const int VK_DELETE = 0x002E;
        public const int VK_HOME = 0x0024;
        public const int VK_END = 0x0023;
        public const int VK_PRIOR = 0x0021;
        public const int VK_NEXT = 0x0022;
        public const int VK_LEFT = 0x0025;
        public const int VK_UP = 0x0026;
        public const int VK_RIGHT = 0x0027;
        public const int VK_DOWN = 0x0028;
        public const int VK_SELECT = 0x0029;
        public const int VK_PRINT = 0x002A;
        public const int VK_EXECUTE = 0x002B;
        public const int VK_HELP = 0x002F;
        public const int VK_LWIN = 0x005B;
        public const int VK_RWIN = 0x005C;
        public const int VK_APPS = 0x005D;
        public const int VK_F1 = 0x0070;
        public const int VK_F2 = 0x0071;
        public const int VK_F3 = 0x0072;
        public const int VK_F4 = 0x0073;
        public const int VK_F5 = 0x0074;
        public const int VK_F6 = 0x0075;
        public const int VK_F7 = 0x0076;
        public const int VK_F8 = 0x0077;
        public const int VK_F9 = 0x0078;
        public const int VK_F10 = 0x0079;
        public const int VK_F11 = 0x007A;
        public const int VK_F12 = 0x007B;
        public const int VK_SHIFT = 0x0010;
        public const int VK_LSHIFT = 0x00A0;
        public const int VK_RSHIFT = 0x00A1;
        public const int VK_CONTROL = 0x0011;
        public const int VK_LCONTROL = 0x00A2;
        public const int VK_RCONTROL = 0x00A3;
        public const int VK_MENU = 0x0012;
        public const int VK_LMENU = 0x00A4;
        public const int VK_RMENU = 0x00A5;

        public const int VK_OEM_1 = 0x00BA;
        public const int VK_OEM_2 = 0x00BF;
        public const int VK_OEM_3 = 0x00C0;
        public const int VK_OEM_4 = 0x00DB;
        public const int VK_OEM_5 = 0x00DC;
        public const int VK_OEM_6 = 0x00DD;
        public const int VK_OEM_7 = 0x00DE;
        public const int VK_OEM_8 = 0x00DF;
        public const int VK_OEM_102 = 0x00E2;

        #endregion

        #endregion

        // ReSharper restore InconsistentNaming
    }
}
