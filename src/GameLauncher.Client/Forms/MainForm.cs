using Microsoft.Win32;
using GameLauncher.Client.Services;
using GameUpdater.Shared.Localization;
using GameLauncher.Client.Extensions;

namespace GameLauncher.Client.Forms;

public sealed partial class MainForm : Form
{
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupRegistryValueName = "GameLauncher.Client";
    private const string CafeDisplayName = I18n.Launcher.DefaultCafeName;
    public string ThemeFontFamily { get; set; } = I18n.Launcher.DefaultFontFamily;

    private readonly SettingsService _settingsService;
    private readonly CatalogReaderService _catalogService;
    private readonly GameLaunchService _launchService;

    private bool _enableCloseAppHotKeyFromServer = true;
    private bool _isCloseAppHotKeyRegistered;

    public MainForm(
        SettingsService settingsService,
        CatalogReaderService catalogService,
        GameLaunchService launchService)
    {
        _settingsService = settingsService;
        _catalogService = catalogService;
        _launchService = launchService;

        Text = I18n.Launcher.WindowTitle;
        AutoScaleMode = AutoScaleMode.Dpi;
        
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Width = Math.Min(this.ScaleDpi(1570), workingArea.Width);
        Height = Math.Min(this.ScaleDpi(950), workingArea.Height);
        
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BodyBackColor;
        MinimumSize = this.ScaleSize(1000, 680);

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

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateCloseAppHotKeyRegistration();
        ApplyImmersiveDarkMode();
    }

    private void ApplyImmersiveDarkMode()
    {
        try
        {
            if (Environment.OSVersion.Version.Major >= 10)
            {
                int useDarkMode = 1;
                int result = DwmSetWindowAttribute(Handle, DwmwaUseImmersiveDarkMode, ref useDarkMode, sizeof(int));
                if (result != 0)
                {
                    DwmSetWindowAttribute(Handle, DwmwaUseImmersiveDarkModeBefore20h1, ref useDarkMode, sizeof(int));
                }
            }
        }
        catch
        {
            // Fallback on restricted or older Windows environments.
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterCloseAppHotKey();
        base.OnHandleDestroyed(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _statusHeartbeatTimer.Stop();
        _clockTimer.Stop();
        _slideTimer.Stop();
        _slideTimer.Dispose();
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
            MessageBox.Show(this, exception.Message, I18n.Common.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
