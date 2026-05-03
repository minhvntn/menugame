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
{    private void BrowseSourceButton_Click(object? sender, EventArgs e)
    {
        if (CurrentSourceKind == UpdateSourceKind.Folder)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = I18n.Server.UpdateSourceFolderPickerDescription,
                UseDescriptionForTitle = true,
                SelectedPath = _updateSourceTextBox.Text
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _updateSourceTextBox.Text = dialog.SelectedPath;
            }
        }
        else
        {
            using var dialog = new OpenFileDialog
            {
                Filter = I18n.Server.UpdateSourceZipFilter,
                CheckFileExists = true,
                InitialDirectory = File.Exists(_updateSourceTextBox.Text)
                    ? Path.GetDirectoryName(_updateSourceTextBox.Text)
                    : string.Empty
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _updateSourceTextBox.Text = dialog.FileName;
            }
        }
    }

    private async void ApplyUpdateButton_Click(object? sender, EventArgs e)
    {
        if (SelectedGame is null)
        {
            ShowInfo(I18n.Server.NeedSelectGameFirst);
            return;
        }

        var game = SelectedGame;
        var request = new UpdateRequest
        {
            Game = game,
            SourceKind = CurrentSourceKind,
            SourcePath = _updateSourceTextBox.Text.Trim(),
            TargetVersion = _updateVersionTextBox.Text.Trim(),
            CreateBackup = _backupCheckBox.Checked
        };

        var monitorRow = StartDownloadMonitor(game.Name, game.Id, resourceKey: null);

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            ToggleUpdateControls(false);
            _updateOutputTextBox.Clear();
            _updateProgressBar.Value = 0;
            AppendUpdateMessage(I18n.Server.UpdateStarted(game.Name));

            try
            {
                var progress = new Progress<UpdateProgressInfo>(info =>
                {
                    _updateProgressBar.Value = Math.Clamp(info.Percent, 0, 100);
                    AppendUpdateMessage(info.Message);
                    UpdateDownloadMonitor(monitorRow, info.Percent, I18n.Server.UpdateRunningStatus, info.Message, info);
                });

                var backupPath = await _updateService.ApplyUpdateAsync(request, progress);
                if (!string.IsNullOrWhiteSpace(backupPath))
                {
                    AppendUpdateMessage(I18n.Server.BackupSaved(backupPath));
                }

                UpdateDownloadMonitor(monitorRow, 100, I18n.Server.UpdateSuccessStatus, I18n.Server.UpdateCompleted);
                await AutoExportCatalogAsync();
                await ReloadAllAsync(game.Id);
            }
            catch (Exception exception)
            {
                UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, I18n.Server.UpdateFailedStatus, exception.Message);
                throw;
            }
        }, () => ToggleUpdateControls(true));
    }

}



