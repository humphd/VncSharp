/*
 * Based on code from Stephen Toub's MSDN blog at
 * http://blogs.msdn.com/b/toub/archive/2006/05/03/589423.aspx
 * 
 * Originally written by https://github.com/rmcardle for https://github.com/mRemoteNG/VncSharpNG
 * Additional fixes and porting to (upstream) https://github.com/humphd/VncSharp by https://github.com/kmscode
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VncSharp
{
    public class KeyboardHook
    {
        // ReSharper disable InconsistentNaming
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, HookKeyMsgData lParam);
        // ReSharper restore InconsistentNaming

        [Flags]
        public enum ModifierKeys
        {
            None = 0x0000,
            Shift = 0x0001,
            LeftShift = 0x002,
            RightShift = 0x004,
            Control = 0x0008,
            LeftControl = 0x010,
            RightControl = 0x20,
            Alt = 0x0040,
            LeftAlt = 0x0080,
            RightAlt = 0x0100,
            Win = 0x0200,
            LeftWin = 0x0400,
            RightWin = 0x0800
        }

        protected class KeyNotificationEntry: IEquatable<KeyNotificationEntry>
        {
            public IntPtr WindowHandle;
            public int KeyCode;
            public ModifierKeys ModifierKeys;
            public bool Block;

            public bool Equals(KeyNotificationEntry obj)
            {
                return obj != null && WindowHandle == obj.WindowHandle && KeyCode == obj.KeyCode && ModifierKeys == obj.ModifierKeys && Block == obj.Block;
            }
        }

        private const string HookKeyMsgName = "HOOKKEYMSG-{EC4E5587-8F3A-4A56-A00B-2A5F827ABA79}";
        private static uint _hookKeyMsg;
        public static uint HookKeyMsg
        {
            get
            {
                if (_hookKeyMsg != 0) return _hookKeyMsg;
                _hookKeyMsg = NativeMethods.RegisterWindowMessage(HookKeyMsgName);
                if (_hookKeyMsg == 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                return _hookKeyMsg;
            }
        }

        // this is a custom structure that will be passed to
        // the requested hWnd via a WM_APP_HOOKKEYMSG message
        [StructLayout(LayoutKind.Sequential)]
        public class HookKeyMsgData
        {
            public int KeyCode;
            public ModifierKeys ModifierKeys;
            public bool WasBlocked;
        }

        private static int _referenceCount;
        private static IntPtr _hook;
        private static readonly NativeMethods.LowLevelKeyboardProcDelegate LowLevelKeyboardProcStaticDelegate = LowLevelKeyboardProc;
        private static readonly List<KeyNotificationEntry> NotificationEntries = new List<KeyNotificationEntry>();

        public KeyboardHook()
        {
            _referenceCount++;
            SetHook();
        }

        ~KeyboardHook()
        {
            _referenceCount--;
            if (_referenceCount < 1) UnsetHook();
        }

        private static void SetHook()
        {
            if (_hook != IntPtr.Zero) return;

            var curProcess = Process.GetCurrentProcess();
            var curModule = curProcess.MainModule;

            var hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, LowLevelKeyboardProcStaticDelegate, NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
            if (hook == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            _hook = hook;
        }

        private static void UnsetHook()
        {
            if (_hook == IntPtr.Zero) return;

            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        private static IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, NativeMethods.KBDLLHOOKSTRUCT lParam)
        {
            var wParamInt = wParam.ToInt32();
            var result = 0;

            if (nCode != NativeMethods.HC_ACTION)
                return result != 0 ? new IntPtr(result) : NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (wParamInt)
            {
                case NativeMethods.WM_KEYDOWN:
                case NativeMethods.WM_SYSKEYDOWN:
                case NativeMethods.WM_KEYUP:
                case NativeMethods.WM_SYSKEYUP:
                    result = OnKey(wParamInt, lParam);
                    break;
            }

            return result != 0 ? new IntPtr(result) : NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private static int OnKey(int msg, NativeMethods.KBDLLHOOKSTRUCT key)
        {
            var result = 0;

            foreach (var notificationEntry in NotificationEntries)
                // It error code is Null, have to ignore the exception
                // For some unknow raison, sometime GetFocuseWindows throw an exception
                // Mainly when the station is unlocked, or after an admin password is asked
                try
                {
                    if (GetFocusWindow() != notificationEntry.WindowHandle || notificationEntry.KeyCode != key.vkCode)
                        continue;
                    var modifierKeys = GetModifierKeyState();
                    if (!ModifierKeysMatch(notificationEntry.ModifierKeys, modifierKeys)) continue;

                    var wParam = new IntPtr(msg);
                    var lParam = new HookKeyMsgData
                    {
                        KeyCode = key.vkCode,
                        ModifierKeys = modifierKeys,
                        WasBlocked = notificationEntry.Block
                    };

                    if (!PostMessage(notificationEntry.WindowHandle, HookKeyMsg, wParam, lParam))
                        throw new Win32Exception(Marshal.GetLastWin32Error());

                    if (notificationEntry.Block) result = 1;
                }
                catch (Win32Exception e)
                {
                    if (e.NativeErrorCode != 0)
                    {
                        throw;
                    }
                }

            return result;
        }

        private static IntPtr GetFocusWindow()
        {
            var guiThreadInfo = new NativeMethods.GUITHREADINFO();
            if (NativeMethods.GetGUIThreadInfo(0, guiThreadInfo))
                return NativeMethods.GetAncestor(guiThreadInfo.hwndFocus, NativeMethods.GA_ROOT);
            var except = Marshal.GetLastWin32Error();
            throw new Win32Exception(except);
        }

        private static readonly Dictionary<int, ModifierKeys> ModifierKeyTable = new Dictionary<int, ModifierKeys>
        {
            { NativeMethods.VK_SHIFT, ModifierKeys.Shift },
            { NativeMethods.VK_LSHIFT, ModifierKeys.LeftShift },
            { NativeMethods.VK_RSHIFT, ModifierKeys.RightShift },
            { NativeMethods.VK_CONTROL, ModifierKeys.Control },
            { NativeMethods.VK_LCONTROL, ModifierKeys.LeftControl },
            { NativeMethods.VK_RCONTROL, ModifierKeys.RightControl },
            { NativeMethods.VK_MENU, ModifierKeys.Alt },
            { NativeMethods.VK_LMENU, ModifierKeys.LeftAlt },
            { NativeMethods.VK_RMENU, ModifierKeys.RightAlt },
            { NativeMethods.VK_LWIN, ModifierKeys.LeftWin },
            { NativeMethods.VK_RWIN, ModifierKeys.RightWin }
        };

        public static ModifierKeys GetModifierKeyState()
        {
            var modifierKeyState = ModifierKeys.None;

            foreach (var pair in ModifierKeyTable)
            {
                if ((NativeMethods.GetAsyncKeyState(pair.Key) & NativeMethods.KEYSTATE_PRESSED) != 0) modifierKeyState |= pair.Value;
            }

            if ((modifierKeyState & ModifierKeys.LeftWin) != 0) modifierKeyState |= ModifierKeys.Win;
            if ((modifierKeyState & ModifierKeys.RightWin) != 0) modifierKeyState |= ModifierKeys.Win;

            return modifierKeyState;
        }

        private static bool ModifierKeysMatch(ModifierKeys requestedKeys, ModifierKeys pressedKeys)
        {
            if ((requestedKeys & ModifierKeys.Shift) != 0) pressedKeys &= ~(ModifierKeys.LeftShift | ModifierKeys.RightShift);
            if ((requestedKeys & ModifierKeys.Control) != 0) pressedKeys &= ~(ModifierKeys.LeftControl | ModifierKeys.RightControl);
            if ((requestedKeys & ModifierKeys.Alt) != 0) pressedKeys &= ~(ModifierKeys.LeftAlt | ModifierKeys.RightAlt);
            if ((requestedKeys & ModifierKeys.Win) != 0) pressedKeys &= ~(ModifierKeys.LeftWin | ModifierKeys.RightWin);
            return requestedKeys == pressedKeys;
        }

        public static void RequestKeyNotification(IntPtr windowHandle, int keyCode, bool block)
        {
            RequestKeyNotification(windowHandle, keyCode, ModifierKeys.None, block);
        }

        public static void RequestKeyNotification(IntPtr windowHandle, int keyCode, ModifierKeys modifierKeys = ModifierKeys.None, bool block = false)
        {
            var newNotificationEntry = new KeyNotificationEntry
            {
                WindowHandle = windowHandle,
                KeyCode = keyCode,
                ModifierKeys = modifierKeys,
                Block = block
            };

            foreach (var notificationEntry in NotificationEntries)
                if (notificationEntry == newNotificationEntry) return;

            NotificationEntries.Add(newNotificationEntry);
        }

        public static void CancelKeyNotification(IntPtr windowHandle, int keyCode, bool block)
        {
            CancelKeyNotification(windowHandle, keyCode, ModifierKeys.None, block);
        }

        private static void CancelKeyNotification(IntPtr windowHandle, int keyCode, ModifierKeys modifierKeys = ModifierKeys.None, bool block = false)
        {
            var notificationEntry = new KeyNotificationEntry
            {
                WindowHandle = windowHandle,
                KeyCode = keyCode,
                ModifierKeys = modifierKeys,
                Block = block
            };

            NotificationEntries.Remove(notificationEntry);
        }
    }
}
