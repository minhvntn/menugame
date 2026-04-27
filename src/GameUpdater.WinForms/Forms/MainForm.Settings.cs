using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using GameUpdater.Core.Abstractions;
using GameUpdater.Core.Services;
using GameUpdater.Shared.Models;

namespace GameUpdater.WinForms.Forms;

public sealed partial class MainForm
{    private async Task LoadUiSettingsAsync()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<ServerUiSettings>(json);
            if (settings is not null)
            {
                if (!string.IsNullOrWhiteSpace(settings.ClientCatalogPath))
                {
                    _autoCatalogPath = settings.ClientCatalogPath;
                }

                if (!string.IsNullOrWhiteSpace(settings.ResourceSourceRootPath))
                {
                    _resourceSourceRootPath = settings.ResourceSourceRootPath;
                }

                if (!string.IsNullOrWhiteSpace(settings.ResourceTargetRootPath))
                {
                    _resourceTargetRootPath = settings.ResourceTargetRootPath;
                }

                _resourceBandwidthLimitMbps = Math.Max(0, settings.ResourceBandwidthLimitMbps);
                _clientWindowsWallpaperPath = settings.ClientWindowsWallpaperPath?.Trim() ?? string.Empty;
                _clientCafeDisplayName = string.IsNullOrWhiteSpace(settings.ClientCafeDisplayName) ? _clientCafeDisplayName : settings.ClientCafeDisplayName.Trim();
                _clientBannerMessage = settings.ClientBannerMessage?.Trim() ?? string.Empty;
                _clientThemeAccentColor = string.IsNullOrWhiteSpace(settings.ClientThemeAccentColor) ? _clientThemeAccentColor : settings.ClientThemeAccentColor.Trim();
                _clientThemeFontFamily = string.IsNullOrWhiteSpace(settings.ClientThemeFontFamily) ? _clientThemeFontFamily : settings.ClientThemeFontFamily.Trim();
                _clientStatusFolderPath = settings.ClientStatusFolderPath?.Trim() ?? string.Empty;
                _enableClientCloseApplicationHotKey = settings.EnableClientCloseApplicationHotKey;
                _enableClientFullscreenKioskMode = settings.EnableClientFullscreenKioskMode;

                if (!string.IsNullOrWhiteSpace(settings.UiFontSizeMode) &&
                    Enum.TryParse<UiFontSizeMode>(settings.UiFontSizeMode, true, out var parsedFontSizeMode))
                {
                    _uiFontSizeMode = parsedFontSizeMode;
                }
            }

            _resourceSourceRootTextBox.Text = _resourceSourceRootPath;
            _resourceTargetRootTextBox.Text = _resourceTargetRootPath;
            _resourceBandwidthLimitNumeric.Value = Math.Min(_resourceBandwidthLimitNumeric.Maximum, _resourceBandwidthLimitMbps);
            _clientWallpaperPathTextBox.Text = _clientWindowsWallpaperPath;
            _clientCafeNameTextBox.Text = _clientCafeDisplayName;
            _clientBannerMessageTextBox.Text = _clientBannerMessage;
            _clientThemeAccentColorTextBox.Text = _clientThemeAccentColor;
            if (_clientThemeFontComboBox.Items.Contains(_clientThemeFontFamily))
            {
                _clientThemeFontComboBox.SelectedItem = _clientThemeFontFamily;
            }
            else
            {
                _clientThemeFontComboBox.SelectedItem = "Segoe UI";
            }
            _clientStatusFolderTextBox.Text = _clientStatusFolderPath;
            _enableClientCloseAppHotKeyCheckBox.Checked = _enableClientCloseApplicationHotKey;
            _enableClientFullscreenKioskCheckBox.Checked = _enableClientFullscreenKioskMode;
            SetFontSizeSelection(_uiFontSizeMode);
            ApplyUiFontSize(_uiFontSizeMode);
        }
        catch
        {
            // Keep default path when settings file is invalid.
        }
    }

    private async Task SaveUiSettingsAsync()
    {
        var settings = new ServerUiSettings
        {
            ClientCatalogPath = _autoCatalogPath,
            ResourceSourceRootPath = _resourceSourceRootPath,
            ResourceTargetRootPath = _resourceTargetRootPath,
            ResourceBandwidthLimitMbps = _resourceBandwidthLimitMbps,
            ClientWindowsWallpaperPath = _clientWindowsWallpaperPath,
            ClientCafeDisplayName = _clientCafeDisplayName,
            ClientBannerMessage = _clientBannerMessage,
            ClientThemeAccentColor = _clientThemeAccentColor,
            ClientThemeFontFamily = _clientThemeFontFamily,
            ClientStatusFolderPath = _clientStatusFolderPath,
            EnableClientCloseApplicationHotKey = _enableClientCloseApplicationHotKey,
            EnableClientFullscreenKioskMode = _enableClientFullscreenKioskMode,
            UiFontSizeMode = _uiFontSizeMode.ToString()
        };

        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_settingsFilePath, json, Encoding.UTF8);
    }
}

