using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;

namespace Finalpass.App;

internal enum AutoTypeTargetKind
{
    /// <summary>Minimize Finalpass so Windows activates the previously used window.</summary>
    PreviousWindow,

    /// <summary>Leave everything alone and let the user switch windows during the countdown.</summary>
    ManualSwitch,

    /// <summary>Activate a specific window picked by the user.</summary>
    ChosenWindow,
}

internal sealed record OpenWindowInfo(nint Handle, string Title, string ProcessName);

internal enum AutoTypeOutcome
{
    Typed,
    Cancelled,
    FinalpassStillActive,
    Blocked,
    TargetGone,
}

/// <summary>
/// Types a credential into whichever window is in the foreground using synthesized
/// Unicode keystrokes, so nothing is placed on the clipboard.
/// </summary>
internal static class AutoTyper
{
    private const int SettleMilliseconds = 500;
    private const int ModifierReleaseTimeoutMilliseconds = 2_000;
    private const int InterEventDelayMilliseconds = 3;
    private const ushort VkShift = 0x10;
    private const ushort VkControl = 0x11;
    private const ushort VkMenu = 0x12;
    private const ushort VkTab = 0x09;
    private const ushort VkReturn = 0x0D;
    private const uint InputKeyboard = 1;
    private const uint KeyEventUnicode = 0x0004;
    private const uint KeyEventKeyUp = 0x0002;
    private static readonly int[] ModifierKeys = [0x10, 0x11, 0x12, 0x5B, 0x5C];

    private static readonly object Sync = new();
    private static CancellationTokenSource? _pending;

    public static void Cancel()
    {
        lock (Sync)
        {
            _pending?.Cancel();
        }
    }

    /// <summary>
    /// Prepares the target, waits <paramref name="delay"/> so the user can click the right field,
    /// then types <paramref name="username"/> (when given), a Tab, and <paramref name="password"/>.
    /// Only one request can be pending; a new one replaces it.
    /// </summary>
    public static async Task<AutoTypeOutcome> RunAsync(
        string? username,
        string password,
        bool pressEnter,
        AutoTypeTargetKind targetKind,
        nint targetWindow,
        TimeSpan delay,
        Action<string> progress)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(progress);
        ValidateText(username);
        ValidateText(password);

        CancellationTokenSource cancellation = new();
        lock (Sync)
        {
            _pending?.Cancel();
            _pending = cancellation;
        }

        try
        {
            CancellationToken token = cancellation.Token;
            switch (targetKind)
            {
                case AutoTypeTargetKind.PreviousWindow:
                    if (App.Window.AppWindow.Presenter is OverlappedPresenter presenter)
                    {
                        presenter.Minimize();
                    }

                    break;
                case AutoTypeTargetKind.ChosenWindow:
                    if (!NativeMethods.IsWindow(targetWindow))
                    {
                        return AutoTypeOutcome.TargetGone;
                    }

                    if (NativeMethods.IsIconic(targetWindow))
                    {
                        _ = NativeMethods.ShowWindow(targetWindow, 9);
                    }

                    _ = NativeMethods.SetForegroundWindow(targetWindow);
                    break;
            }

            string where = targetKind == AutoTypeTargetKind.ManualSwitch
                ? "switch to the target window and click its field"
                : "click the target field if needed";
            for (int remaining = (int)Math.Ceiling(delay.TotalSeconds); remaining > 0; remaining--)
            {
                progress($"Auto-typing in {remaining}… {where}.");
                await Task.Delay(TimeSpan.FromSeconds(1), token);
            }

            await Task.Delay(SettleMilliseconds, token);
            if (targetKind == AutoTypeTargetKind.ChosenWindow && !NativeMethods.IsWindow(targetWindow))
            {
                return AutoTypeOutcome.TargetGone;
            }

            if (IsFinalpassForeground())
            {
                return AutoTypeOutcome.FinalpassStillActive;
            }

            await WaitForModifiersReleasedAsync(token);
            token.ThrowIfCancellationRequested();
            if (IsFinalpassForeground())
            {
                return AutoTypeOutcome.FinalpassStillActive;
            }

            List<NativeMethods.Input> inputs = [];
            if (username is not null)
            {
                AppendText(inputs, username);
                AppendKey(inputs, VkTab);
            }

            AppendText(inputs, password);
            if (pressEnter)
            {
                AppendKey(inputs, VkReturn);
            }

            return await Task.Run(() => Send(inputs), CancellationToken.None)
                ? AutoTypeOutcome.Typed
                : AutoTypeOutcome.Blocked;
        }
        catch (OperationCanceledException)
        {
            return AutoTypeOutcome.Cancelled;
        }
        finally
        {
            lock (Sync)
            {
                if (ReferenceEquals(_pending, cancellation))
                {
                    _pending = null;
                }
            }

            cancellation.Dispose();
        }
    }

    public static IReadOnlyList<OpenWindowInfo> GetOpenWindows()
    {
        List<OpenWindowInfo> windows = [];
        _ = NativeMethods.EnumWindows(
            (handle, parameter) =>
            {
                if (!NativeMethods.IsWindowVisible(handle) ||
                    NativeMethods.GetWindow(handle, 4) != 0 ||
                    (NativeMethods.GetWindowLongPtr(handle, -20) & 0x80) != 0 ||
                    (NativeMethods.DwmGetWindowAttribute(handle, 14, out int cloaked, sizeof(int)) == 0 &&
                        cloaked != 0))
                {
                    return true;
                }

                int length = NativeMethods.GetWindowTextLength(handle);
                if (length == 0)
                {
                    return true;
                }

                char[] title = new char[length + 1];
                int copied = NativeMethods.GetWindowText(handle, title, title.Length);
                _ = NativeMethods.GetWindowThreadProcessId(handle, out uint processId);
                if (processId == Environment.ProcessId)
                {
                    return true;
                }

                string processName;
                try
                {
                    using Process process = Process.GetProcessById((int)processId);
                    processName = process.ProcessName;
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
                {
                    processName = "unknown";
                }

                windows.Add(new OpenWindowInfo(handle, new string(title, 0, copied), processName));
                return true;
            },
            0);
        return windows;
    }

    private static void ValidateText(string? value)
    {
        if (value is not null && value.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                "The value contains control characters that cannot be auto-typed safely.");
        }
    }

    private static bool IsFinalpassForeground()
    {
        nint foreground = NativeMethods.GetForegroundWindow();
        if (foreground == 0)
        {
            return false;
        }

        _ = NativeMethods.GetWindowThreadProcessId(foreground, out uint processId);
        return processId == Environment.ProcessId;
    }

    // The shortcut that started auto-type may still be held; stray Ctrl/Shift/Alt/Win would
    // turn the typed characters into shortcuts in the target application.
    private static async Task WaitForModifiersReleasedAsync(CancellationToken token)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(ModifierReleaseTimeoutMilliseconds);
        while (ModifierKeys.Any(key => (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0) &&
            DateTime.UtcNow < deadline)
        {
            await Task.Delay(50, token);
        }
    }

    // Characters the keyboard layout can produce are sent as real key presses (with the layout's
    // modifiers); only the rest fall back to Unicode packets. Some apps, notably the Windows 11
    // Notepad, stop processing injected input after a Unicode packet or a fast burst of events.
    private static void AppendText(List<NativeMethods.Input> inputs, string text)
    {
        foreach (char character in text)
        {
            short scan = NativeMethods.VkKeyScan(character);
            if (scan == -1)
            {
                inputs.Add(CreateInput(0, character, KeyEventUnicode));
                inputs.Add(CreateInput(0, character, KeyEventUnicode | KeyEventKeyUp));
                continue;
            }

            ushort virtualKey = (ushort)(scan & 0xFF);
            int modifiers = (scan >> 8) & 7;
            List<ushort> held = [];
            if ((modifiers & 1) != 0)
            {
                held.Add(VkShift);
            }

            if ((modifiers & 2) != 0)
            {
                held.Add(VkControl);
            }

            if ((modifiers & 4) != 0)
            {
                held.Add(VkMenu);
            }

            foreach (ushort modifier in held)
            {
                inputs.Add(CreateInput(modifier, 0, 0));
            }

            AppendKey(inputs, virtualKey);
            for (int index = held.Count - 1; index >= 0; index--)
            {
                inputs.Add(CreateInput(held[index], 0, KeyEventKeyUp));
            }
        }
    }

    private static void AppendKey(List<NativeMethods.Input> inputs, ushort virtualKey)
    {
        inputs.Add(CreateInput(virtualKey, 0, 0));
        inputs.Add(CreateInput(virtualKey, 0, KeyEventKeyUp));
    }

    private static NativeMethods.Input CreateInput(ushort virtualKey, ushort scanCode, uint flags) =>
        new()
        {
            Type = InputKeyboard,
            Keyboard = new NativeMethods.KeyboardInput
            {
                VirtualKey = virtualKey,
                ScanCode = scanCode,
                Flags = flags,
            },
        };

    // Events are sent one at a time with a short gap; a single burst is silently ignored by some apps.
    private static bool Send(List<NativeMethods.Input> inputs)
    {
        int size = Marshal.SizeOf<NativeMethods.Input>();
        NativeMethods.Input[] single = new NativeMethods.Input[1];
        foreach (NativeMethods.Input input in inputs)
        {
            single[0] = input;
            // A short count means input was blocked, typically by an elevated target window (UIPI).
            if (NativeMethods.SendInput(1, single, size) != 1)
            {
                return false;
            }

            Thread.Sleep(InterEventDelayMilliseconds);
        }

        return true;
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Explicit, Size = 40)]
        internal struct Input
        {
            [FieldOffset(0)]
            public uint Type;

            [FieldOffset(8)]
            public KeyboardInput Keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct KeyboardInput
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public nuint ExtraInfo;
        }

        internal delegate bool EnumWindowsProc(nint windowHandle, nint parameter);

#pragma warning disable SYSLIB1054 // Struct-array marshalling is simplest with classic P/Invoke.
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint SendInput(
            uint inputCount,
            [In] Input[] inputs,
            int inputSize);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll")]
        internal static extern nint GetForegroundWindow();

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(nint windowHandle);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(nint windowHandle);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(nint windowHandle);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(nint windowHandle, int command);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(nint windowHandle);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll")]
        internal static extern nint GetWindow(nint windowHandle, uint command);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        internal static extern nint GetWindowLongPtr(nint windowHandle, int index);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowTextLength(nint windowHandle);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(nint windowHandle, [Out] char[] text, int maxCount);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(
            nint windowHandle,
            int attribute,
            out int value,
            int valueSize);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern short VkKeyScan(char character);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(int virtualKey);
#pragma warning restore SYSLIB1054
    }
}
