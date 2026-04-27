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
            ShowInfo("Vui lòng chọn trò chơi trước.");
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
            ShowInfo("Vui lòng chọn trò chơi trước.");
            return;
        }

        var game = SelectedGame;
        var result = MessageBox.Show(
            this,
            $"Bạn có chắc muốn xóa {game.Name} khỏi danh sách quản lý? Dữ liệu trò chơi trên ổ đĩa sẽ không bị xóa.",
            "Xác nhận xóa",
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
            ShowInfo("Vui lòng chọn trò chơi trước.");
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
            Filter = "Tệp JSON (*.json)|*.json",
            Title = "Xuất danh mục trò chơi cho client",
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
            MessageBox.Show(this, $"Đã xuất danh mục:{Environment.NewLine}{_autoCatalogPath}", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private void BrowseClientWallpaperButton_Click(object? sender, EventArgs e)
    {
        using var openDialog = new OpenFileDialog
        {
            Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp|Tất cả tệp (*.*)|*.*",
            CheckFileExists = true,
            Title = "Chọn hình nền Windows cho client"
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
            if (_clientThemeFontComboBox.SelectedItem != null)
            {
                _clientThemeFontFamily = _clientThemeFontComboBox.SelectedItem.ToString() ?? "Segoe UI";
            }
            await SaveUiSettingsAsync();
            ApplyUiFontSize(_uiFontSizeMode);
            await AutoExportCatalogAsync();
            ShowInfo("Đã lưu thiết lập và đồng bộ catalog cho client.");
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
            Filter = "Tệp CSV (*.csv)|*.csv",
            Title = "Xuất lịch sử cập nhật",
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
            builder.AppendLine("Thời gian,Trò chơi,Hành động,Trạng thái,Nội dung");
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
            ShowInfo($"Đã xuất CSV: {dialog.FileName}");
        });
    }

}




