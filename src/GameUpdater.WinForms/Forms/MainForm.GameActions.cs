using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using GameUpdater.Core.Abstractions;
using GameUpdater.Core.Services;
using GameUpdater.Shared.Localization;
using GameUpdater.Shared.Models;

namespace GameUpdater.WinForms.Forms;

public sealed partial class MainForm
{    private async void AddGameButton_Click(object? sender, EventArgs e)
    {
        using var editor = new GameEditorForm();
        if (editor.ShowDialog(this) != DialogResult.OK || editor.EditedGame is null)
        {
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            var gameId = await _gameService.SaveGameAsync(editor.EditedGame);
            await AutoExportCatalogAsync();
            await ReloadAllAsync(gameId);
        });
    }

    private async void EditGameButton_Click(object? sender, EventArgs e)
    {
        if (SelectedGame is null)
        {
            ShowInfo(I18n.Server.NeedSelectGameFirst);
            return;
        }

        using var editor = new GameEditorForm(SelectedGame);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.EditedGame is null)
        {
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            var gameId = await _gameService.SaveGameAsync(editor.EditedGame);
            await AutoExportCatalogAsync();
            await ReloadAllAsync(gameId);
        });
    }

    private async void DeleteGameButton_Click(object? sender, EventArgs e)
    {
        if (SelectedGame is null)
        {
            ShowInfo(I18n.Server.NeedSelectGameFirst);
            return;
        }

        var game = SelectedGame;
        var result = MessageBox.Show(
            this,
            I18n.Server.DeleteGameConfirm(game.Name),
            I18n.Common.ConfirmTitle,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
        {
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            await _gameService.DeleteGameAsync(game);
            await AutoExportCatalogAsync();
            await ReloadAllAsync();
        });
    }

    private async void ScanManifestButton_Click(object? sender, EventArgs e)
    {
        if (SelectedGame is null)
        {
            ShowInfo(I18n.Server.NeedSelectGameFirst);
            return;
        }

        var game = SelectedGame;
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            ToggleGameControls(false);
            await _gameService.ScanGameAsync(game);
            await AutoExportCatalogAsync();
            await ReloadAllAsync(game.Id);
        }, () => ToggleGameControls(true));
    }

    private async void RefreshButton_Click(object? sender, EventArgs e)
    {
        await ExecuteWithErrorHandlingAsync(() => ReloadAllAsync());
    }

    private async void ExportCatalogButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = I18n.Server.JsonFileFilter,
            Title = I18n.Server.ExportClientCatalogButton,
            FileName = Path.GetFileName(_autoCatalogPath)
        };

        var initialDirectory = Path.GetDirectoryName(_autoCatalogPath);
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            _autoCatalogPath = dialog.FileName;
            await _catalogService.ExportToFileAsync(_autoCatalogPath, BuildClientPolicy());
            await SaveUiSettingsAsync();
            MessageBox.Show(this, I18n.Server.ExportCatalogDone(_autoCatalogPath), I18n.Common.InfoTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private void BrowseClientWallpaperButton_Click(object? sender, EventArgs e)
    {
        using var openDialog = new OpenFileDialog
        {
            Filter = I18n.Server.ImageFileFilter,
            CheckFileExists = true,
            Title = I18n.Server.SettingWallpaper
        };

        if (openDialog.ShowDialog(this) == DialogResult.OK)
        {
            _clientWallpaperPathTextBox.Text = openDialog.FileName;
        }
    }

    private void ClearClientWallpaperButton_Click(object? sender, EventArgs e)
    {
        _clientWallpaperPathTextBox.Text = string.Empty;
    }

    private async void SaveSettingsButton_Click(object? sender, EventArgs e)
    {
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            _clientWindowsWallpaperPath = _clientWallpaperPathTextBox.Text.Trim();
            _enableClientCloseApplicationHotKey = _enableClientCloseAppHotKeyCheckBox.Checked;
            _clientHeartbeatIntervalSeconds = NormalizeClientHeartbeatIntervalSeconds(Decimal.ToInt32(_clientHeartbeatIntervalNumeric.Value));
            _dashboardRefreshIntervalSeconds = NormalizeDashboardRefreshIntervalSeconds(Decimal.ToInt32(_dashboardRefreshIntervalNumeric.Value));
            ApplyRuntimeIntervals();
            if (_clientThemeFontComboBox.SelectedItem != null)
            {
                _clientThemeFontFamily = _clientThemeFontComboBox.SelectedItem.ToString() ?? I18n.Server.DefaultThemeFontFamily;
            }
            await SaveUiSettingsAsync();
            ApplyUiFontSize(_uiFontSizeMode);
            await AutoExportCatalogAsync();
            ShowInfo(I18n.Server.SettingsSavedAndCatalogSynced);
        });
    }

    private async void RefreshLogsButton_Click(object? sender, EventArgs e)
    {
        await ExecuteWithErrorHandlingAsync(LoadLogsAsync);
    }

    private async void ExportLogsCsvButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = I18n.Server.CsvFileFilter,
            Title = I18n.Common.CsvButton,
            FileName = $"update-logs-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            var logs = (await _logRepository.GetRecentAsync())
                .OrderByDescending(item => item.CreatedAt)
                .ToList();

            var builder = new StringBuilder();
            builder.AppendLine(I18n.Server.LogsCsvHeader);
            foreach (var log in logs)
            {
                builder.AppendLine(string.Join(",",
                    EscapeCsv(log.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
                    EscapeCsv(log.GameName),
                    EscapeCsv(log.Action),
                    EscapeCsv(log.Status),
                    EscapeCsv(log.Message)));
            }

            await File.WriteAllTextAsync(dialog.FileName, builder.ToString(), new UTF8Encoding(true));
            ShowInfo(I18n.Server.CsvExportDone(dialog.FileName));
        });
    }

}






