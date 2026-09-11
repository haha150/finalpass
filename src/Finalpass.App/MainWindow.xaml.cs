using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Finalpass.App;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const uint WmPowerBroadcast = 0x0218;
    private const uint WmWtsSessionChange = 0x02B1;
    private const nuint PbtApmSuspend = 0x0004;
    private const nuint PbtApmStandby = 0x0005;
    private const nuint WtsSessionLock = 0x0007;
    private const uint NotifyForThisSession = 0;
    private const nuint SubclassId = 0x46504153;

    private bool _closeConfirmed;
    private bool _initialFileHandled;
    private readonly NativeMethods.SubclassProc _subclassProc;
    private nint _windowHandle;
    private bool _subclassInstalled;
    private bool _sessionNotificationsRegistered;

    public MainWindow()
    {
        InitializeComponent();
        _subclassProc = WindowSubclassProc;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        AppWindow.SetIcon(iconPath);

        AppTitleBar.IconSource = new ImageIconSource
        {
            ImageSource = new BitmapImage(
                new Uri("ms-appx:///Assets/Square44x44Logo.scale-200.png")),
        };

        RootFrame.Navigate(typeof(MainPage));
        AppWindow.Closing += AppWindow_Closing;
        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;
        RegisterSystemLockNotifications();
    }

    public void ApplyTheme(string theme)
    {
        RootGrid.RequestedTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    private void RegisterSystemLockNotifications()
    {
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _subclassInstalled = NativeMethods.SetWindowSubclass(
            _windowHandle,
            _subclassProc,
            SubclassId,
            0);
        if (_subclassInstalled)
        {
            _sessionNotificationsRegistered = NativeMethods.WTSRegisterSessionNotification(
                _windowHandle,
                NotifyForThisSession);
        }
    }

    private nint WindowSubclassProc(
        nint windowHandle,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData)
    {
        if (message == WmWtsSessionChange && wParam == WtsSessionLock)
        {
            QueueSystemLock("was locked");
        }
        else if (message == WmPowerBroadcast &&
            (wParam == PbtApmSuspend || wParam == PbtApmStandby))
        {
            QueueSystemLock("is suspending");
        }

        return NativeMethods.DefSubclassProc(windowHandle, message, wParam, lParam);
    }

    private void QueueSystemLock(string reason)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (RootFrame.Content is MainPage page)
            {
                _ = page.LockForSystemEventAsync(reason);
            }
        });
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_sessionNotificationsRegistered)
        {
            _ = NativeMethods.WTSUnRegisterSessionNotification(_windowHandle);
            _sessionNotificationsRegistered = false;
        }

        if (_subclassInstalled)
        {
            _ = NativeMethods.RemoveWindowSubclass(_windowHandle, _subclassProc, SubclassId);
            _subclassInstalled = false;
        }
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (_initialFileHandled)
        {
            return;
        }

        _initialFileHandled = true;
        string? path = Environment.GetCommandLineArgs()
            .Skip(1)
            .FirstOrDefault(argument =>
                string.Equals(Path.GetExtension(argument), ".fpass", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(argument));
        if (path is not null && RootFrame.Content is MainPage page)
        {
            if (page.IsLoaded)
            {
                await page.OpenVaultPathAsync(path);
                return;
            }

            RoutedEventHandler? loadedHandler = null;
            loadedHandler = async (_, _) =>
            {
                page.Loaded -= loadedHandler;
                await page.OpenVaultPathAsync(path);
            };
            page.Loaded += loadedHandler;
        }
    }

    private async void AppWindow_Closing(
        Microsoft.UI.Windowing.AppWindow sender,
        Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_closeConfirmed)
        {
            return;
        }

        args.Cancel = true;
        if (RootFrame.Content is MainPage page && await page.PrepareToCloseAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }

    private static class NativeMethods
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate nint SubclassProc(
            nint windowHandle,
            uint message,
            nuint wParam,
            nint lParam,
            nuint subclassId,
            nuint referenceData);

#pragma warning disable SYSLIB1054 // Delegate-based subclass callbacks require classic P/Invoke marshalling.
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("comctl32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowSubclass(
            nint windowHandle,
            SubclassProc callback,
            nuint subclassId,
            nuint referenceData);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("comctl32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RemoveWindowSubclass(
            nint windowHandle,
            SubclassProc callback,
            nuint subclassId);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("comctl32.dll")]
        internal static extern nint DefSubclassProc(
            nint windowHandle,
            uint message,
            nuint wParam,
            nint lParam);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("wtsapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WTSRegisterSessionNotification(
            nint windowHandle,
            uint flags);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("wtsapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WTSUnRegisterSessionNotification(nint windowHandle);
#pragma warning restore SYSLIB1054
    }
}
