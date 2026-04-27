using Microsoft.Win32;
using GameLauncher.Client.Services;

namespace GameLauncher.Client.Forms;

public sealed partial class MainForm : Form
{
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupRegistryValueName = "GameLauncher.Client";
    private const string CafeDisplayName = "Cyber Game";
    public string ThemeFontFamily { get; set; } = "Segoe UI";

    private readonly SettingsService _settingsService;
    private readonly CatalogReaderService _catalogService;
    private readonly GameLaunchService _launchService;
    private readonly GamePrewarmService _prewarmService;

    private bool _enableCloseAppHotKeyFromServer = true;
    private bool _isCloseAppHotKeyRegistered;

    public MainForm(
        SettingsService settingsService,
        CatalogReaderService catalogService,
        GameLaunchService launchService,
        GamePrewarmService prewarmService)
    {
        _settingsService = settingsService;
        _catalogService = catalogService;
        _launchService = launchService;
        _prewarmService = prewarmService;

        Text = "Menu trò chơi";
        Width = 1320;
        Height = 860;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BodyBackColor;
        MinimumSize = new Size(1000, 680);

        if (File.Exists("app.ico"))
        {
            Icon = new Icon("app.ico");
        }

        BuildLayout();

        _statusHeartbeatTimer.Interval = 45_000;
        _statusHeartbeatTimer.Tick += (_, _) => WriteClientStatusSafe();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _ = Task.Run(EnsureStartupWithWindows);
        await LoadCatalogOnStartupAsync();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateCloseAppHotKeyRegistration();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterCloseAppHotKey();
        base.OnHandleDestroyed(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _statusHeartbeatTimer.Stop();
        CancelBackgroundPrewarm();
        WriteClientStatusSafe(clearPlayingGame: true);
        _headerLogoImage?.Dispose();
        _headerLogoImage = null;
        base.OnFormClosed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotKey && m.WParam.ToInt32() == CloseLaunchedAppHotKeyId)
        {
            HandleCloseRunningApplicationHotKey();
            return;
        }

        base.WndProc(ref m);
    }

    private void HandleCloseRunningApplicationHotKey()
    {
        if (!_enableCloseAppHotKeyFromServer)
        {
            return;
        }

        _launchService.TryCloseLastLaunchedApplication(out _);
    }

    private async Task ExecuteWithErrorHandlingAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void EnsureStartupWithWindows()
    {
        try
        {
            var executablePath = Application.ExecutablePath;
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return;
            }

            var startupValue = $"\"{Path.GetFullPath(executablePath)}\"";
            using var runKey = Registry.CurrentUser.CreateSubKey(StartupRegistryPath, writable: true);
            if (runKey is null)
            {
                return;
            }

            var existingValue = runKey.GetValue(StartupRegistryValueName)?.ToString();
            if (!string.Equals(existingValue, startupValue, StringComparison.Ordinal))
            {
                runKey.SetValue(StartupRegistryValueName, startupValue, RegistryValueKind.String);
            }
        }
        catch
        {
            // Ignore autostart registration failures on restricted environments.
        }
    }

    private void RegisterCloseAppHotKey()
    {
        if (_isCloseAppHotKeyRegistered || !IsHandleCreated)
        {
            return;
        }

        _isCloseAppHotKeyRegistered = RegisterHotKey(Handle, CloseLaunchedAppHotKeyId, ModControl | ModAlt, (uint)Keys.K);
    }

    private void UnregisterCloseAppHotKey()
    {
        if (!_isCloseAppHotKeyRegistered || !IsHandleCreated)
        {
            return;
        }

        UnregisterHotKey(Handle, CloseLaunchedAppHotKeyId);
        _isCloseAppHotKeyRegistered = false;
    }

    private void UpdateCloseAppHotKeyRegistration()
    {
        if (_enableCloseAppHotKeyFromServer)
        {
            RegisterCloseAppHotKey();
            return;
        }

        UnregisterCloseAppHotKey();
    }

    private void SendLauncherToDesktop()
    {
        if (WindowState != FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Minimized;
        }

        SendToBack();
    }
}
