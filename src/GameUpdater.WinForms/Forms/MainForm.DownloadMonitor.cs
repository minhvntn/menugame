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
{    private DownloadMonitorRow StartDownloadMonitor(string gameName, int? gameId = null, string? resourceKey = null)
    {
        var row = new DownloadMonitorRow
        {
            StartedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            GameName = gameName,
            GameId = gameId,
            GameIdDisplay = gameId.HasValue && gameId.Value > 0 ? gameId.Value.ToString() : "-",
            ResourceKey = resourceKey ?? string.Empty,
            ProgressPercent = 0,
            ProgressDisplay = "0.0%",
            Status = I18n.Server.UpdateRunningStatus,
            Message = I18n.Server.DownloadTaskInitMessage,
            TotalSizeGbDisplay = "-",
            RemainingMbDisplay = "-",
            RemainingTimeDisplay = "-",
            SpeedMbpsDisplay = "-"
        };

        _downloadMonitorRows.Insert(0, row);
        RefreshDownloadMonitorSerialNumbers();
        _downloadMonitorBinding.ResetBindings(false);

        if (_currentResourceFilter == ResourceFilterKind.DownloadMonitor)
        {
            UpdateDownloadSummary();
        }

        SyncResourceRowFromMonitor(row);
        return row;
    }

    private void UpdateDownloadMonitor(
        DownloadMonitorRow row,
        int progressPercent,
        string status,
        string message,
        UpdateProgressInfo? progressInfo = null)
    {
        row.ProgressPercent = Math.Clamp(progressPercent, 0, 100);
        row.Status = status;
        row.Message = message;
        row.UpdatedAt = DateTime.Now;
        UpdateDownloadMonitorDerivedColumns(row, progressInfo);
        _downloadMonitorBinding.ResetBindings(false);

        if (_currentResourceFilter == ResourceFilterKind.DownloadMonitor)
        {
            UpdateDownloadSummary();
        }

        SyncResourceRowFromMonitor(row);

        if (IsAutoRemovableCompletedStatus(status))
        {
            ScheduleAutoRemoveCompletedRow(row);
        }
        else
        {
            row.AutoRemoveScheduled = false;
        }
    }

    private void UpdateDownloadMonitorDerivedColumns(DownloadMonitorRow row, UpdateProgressInfo? progressInfo)
    {
        if (progressInfo?.TotalBytes is long totalBytes && totalBytes > 0)
        {
            row.TotalBytes = totalBytes;
        }

        if (progressInfo?.ProcessedBytes is long processedBytes && processedBytes >= 0)
        {
            row.ProcessedBytes = row.TotalBytes.HasValue
                ? Math.Clamp(processedBytes, 0L, row.TotalBytes.Value)
                : processedBytes;
        }

        if (progressInfo?.SpeedMbps is double speedMbps && speedMbps >= 0)
        {
            row.SpeedMbps = speedMbps;
        }
        else if (!string.Equals(row.Status, I18n.Server.UpdateRunningStatus, StringComparison.OrdinalIgnoreCase))
        {
            row.SpeedMbps = null;
        }

        if (IsAutoRemovableCompletedStatus(row.Status) && row.TotalBytes.HasValue)
        {
            row.ProcessedBytes = row.TotalBytes.Value;
        }

        var precisePercent = GetDownloadProgressPercent(row);
        row.ProgressPercent = (int)Math.Round(Math.Clamp(precisePercent, 0d, 100d));
        row.ProgressDisplay = $"{Math.Clamp(precisePercent, 0d, 100d):N1}%";

        row.TotalSizeGbDisplay = row.TotalBytes.HasValue && row.TotalBytes.Value > 0
            ? (row.TotalBytes.Value / 1024d / 1024d / 1024d).ToString("N2")
            : "-";

        if (row.TotalBytes.HasValue && row.TotalBytes.Value > 0)
        {
            var remainingBytes = Math.Max(0L, row.TotalBytes.Value - row.ProcessedBytes);
            var remainingMb = remainingBytes / 1024d / 1024d;
            row.RemainingMbDisplay = remainingMb.ToString("N2");

            if (remainingBytes <= 0)
            {
                row.RemainingTimeDisplay = "00:00:00";
            }
            else if (row.SpeedMbps.HasValue && row.SpeedMbps.Value > 0.05d)
            {
                var etaSeconds = remainingMb / row.SpeedMbps.Value;
                row.RemainingTimeDisplay = TimeSpan.FromSeconds(Math.Max(0d, etaSeconds)).ToString(@"hh\:mm\:ss");
            }
            else
            {
                row.RemainingTimeDisplay = "-";
            }
        }
        else
        {
            row.RemainingMbDisplay = "-";
            row.RemainingTimeDisplay = "-";
        }

        row.SpeedMbpsDisplay = row.SpeedMbps.HasValue && row.SpeedMbps.Value > 0
            ? row.SpeedMbps.Value.ToString("N1")
            : "-";
    }

    private void RefreshDownloadMonitorSerialNumbers()
    {
        for (var index = 0; index < _downloadMonitorRows.Count; index++)
        {
            _downloadMonitorRows[index].SerialNumber = index + 1;
        }
    }

    private async void ScheduleAutoRemoveCompletedRow(DownloadMonitorRow row)
    {
        if (row.AutoRemoveScheduled)
        {
            return;
        }

        row.AutoRemoveScheduled = true;

        const int maxChecks = 20;
        for (var attempt = 0; attempt < maxChecks; attempt++)
        {
            await Task.Delay(500);

            if (!_downloadMonitorRows.Contains(row))
            {
                row.AutoRemoveScheduled = false;
                return;
            }

            if (!IsAutoRemovableCompletedStatus(row.Status))
            {
                row.AutoRemoveScheduled = false;
                return;
            }

            if (IsResourceSyncRunning(row))
            {
                continue;
            }

            _downloadMonitorRows.Remove(row);
            RefreshDownloadMonitorSerialNumbers();
            _downloadMonitorBinding.ResetBindings(false);

            if (_currentResourceFilter == ResourceFilterKind.DownloadMonitor)
            {
                UpdateDownloadSummary();
            }

            row.AutoRemoveScheduled = false;
            return;
        }

        row.AutoRemoveScheduled = false;
    }

    private static bool IsAutoRemovableCompletedStatus(string status)
    {
        return string.Equals(status, I18n.Server.UpdateSuccessStatus, StringComparison.OrdinalIgnoreCase);
    }

    private void SyncResourceRowFromMonitor(DownloadMonitorRow monitorRow)
    {
        var resourceRow = FindResourceRowForMonitor(monitorRow);
        if (resourceRow is null)
        {
            return;
        }

        var previousDownloaded = resourceRow.IsDownloaded;

        if (string.Equals(monitorRow.Status, I18n.Server.UpdateRunningStatus, StringComparison.OrdinalIgnoreCase))
        {
            resourceRow.DownloadStatus = $"{I18n.Server.UpdateRunningStatus} {monitorRow.ProgressPercent}%";
            resourceRow.DownloadSpeedDisplay = ExtractDownloadSpeedDisplay(monitorRow.Message);
        }
        else if (string.Equals(monitorRow.Status, I18n.Server.UpdatePausedStatus, StringComparison.OrdinalIgnoreCase))
        {
            resourceRow.DownloadStatus = $"{I18n.Server.UpdatePausedStatus} {monitorRow.ProgressPercent}%";
            resourceRow.DownloadSpeedDisplay = "-";
        }
        else if (string.Equals(monitorRow.Status, I18n.Server.UpdateStoppingStatus, StringComparison.OrdinalIgnoreCase))
        {
            resourceRow.DownloadStatus = $"{I18n.Server.UpdateStoppingStatus} {monitorRow.ProgressPercent}%";
            resourceRow.DownloadSpeedDisplay = "-";
        }
        else if (IsAutoRemovableCompletedStatus(monitorRow.Status))
        {
            resourceRow.IsDownloaded = true;
            resourceRow.DownloadStatus = I18n.Server.DownloadStatusDownloaded;
            resourceRow.DownloadSpeedDisplay = "-";
            resourceRow.RunStatus = GetRunStatusAfterSync(resourceRow);
        }
        else if (string.Equals(monitorRow.Status, I18n.Server.UpdateStoppedStatus, StringComparison.OrdinalIgnoreCase))
        {
            resourceRow.DownloadStatus = resourceRow.IsDownloaded ? I18n.Server.DownloadStatusDownloaded : I18n.Server.DownloadStatusMissing;
            resourceRow.DownloadSpeedDisplay = "-";
        }
        else if (string.Equals(monitorRow.Status, I18n.Server.UpdateFailedStatus, StringComparison.OrdinalIgnoreCase))
        {
            resourceRow.DownloadStatus = resourceRow.IsDownloaded ? I18n.Server.DownloadStatusDownloaded : I18n.Server.DownloadStatusError;
            resourceRow.DownloadSpeedDisplay = "-";
        }

        if (previousDownloaded != resourceRow.IsDownloaded)
        {
            ApplyResourceFilter(_currentResourceFilter);
            return;
        }

        _resourcesBinding.ResetBindings(false);
    }

    private static string ExtractDownloadSpeedDisplay(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "-";
        }

        var marker = "MB/s";
        var markerIndex = message.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex <= 0)
        {
            return "-";
        }

        var start = markerIndex - 1;
        while (start >= 0)
        {
            var character = message[start];
            if (char.IsDigit(character) || character == '.' || character == ',' || char.IsWhiteSpace(character))
            {
                start--;
                continue;
            }

            break;
        }

        start++;
        if (start >= markerIndex)
        {
            return "-";
        }

        var numberPart = message.Substring(start, markerIndex - start).Trim();
        if (string.IsNullOrWhiteSpace(numberPart))
        {
            return "-";
        }

        return $"{numberPart} MB/s";
    }

    private ResourceGameRow? FindResourceRowForMonitor(DownloadMonitorRow monitorRow)
    {
        if (!string.IsNullOrWhiteSpace(monitorRow.ResourceKey))
        {
            var bySourceKey = _allResourceRows.FirstOrDefault(row =>
                string.Equals(row.SourceKey, monitorRow.ResourceKey, StringComparison.OrdinalIgnoreCase));
            if (bySourceKey is not null)
            {
                return bySourceKey;
            }
        }

        return _allResourceRows.FirstOrDefault(row =>
            string.Equals(row.Name, monitorRow.GameName, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetRunStatusAfterSync(ResourceGameRow row)
    {
        var launchPath = FindPreferredExecutablePath(row.InstallPath, row.Name);
        var runReady = !string.IsNullOrWhiteSpace(launchPath) && File.Exists(launchPath);
        if (runReady)
        {
            return I18n.Server.RunStatusReady;
        }

        return row.IsManaged ? I18n.Server.RunStatusMissingExe : I18n.Server.RunStatusNotConfiguredExe;
    }

    private void EnsureDownloadMonitorContextMenu()
    {
        if (_downloadMonitorContextMenuInitialized)
        {
            return;
        }

        _downloadMonitorContextMenuInitialized = true;
        EnsureDownloadBandwidthPresetMenuItems();

        _downloadMonitorContextMenu.Items.Add(_pauseDownloadMenuItem);
        _downloadMonitorContextMenu.Items.Add(_resumeDownloadMenuItem);
        _downloadMonitorContextMenu.Items.Add(_pauseAllDownloadsMenuItem);
        _downloadMonitorContextMenu.Items.Add(_resumeAllDownloadsMenuItem);
        _downloadMonitorContextMenu.Items.Add(_stopDownloadMenuItem);
        _downloadMonitorContextMenu.Items.Add(_setDownloadBandwidthMenuItem);
        _downloadMonitorContextMenu.Items.Add(_retryDownloadFromIdcMenuItem);
        _downloadMonitorContextMenu.Items.Add(_removeDownloadMenuItem);
        _downloadMonitorContextMenu.Items.Add(new ToolStripSeparator());
        _downloadMonitorContextMenu.Items.Add(_removeFinishedDownloadsMenuItem);

        _downloadMonitorContextMenu.Opening += DownloadMonitorContextMenu_Opening;
        _pauseDownloadMenuItem.Click += PauseDownloadMenuItem_Click;
        _resumeDownloadMenuItem.Click += ResumeDownloadMenuItem_Click;
        _pauseAllDownloadsMenuItem.Click += PauseAllDownloadsMenuItem_Click;
        _resumeAllDownloadsMenuItem.Click += ResumeAllDownloadsMenuItem_Click;
        _stopDownloadMenuItem.Click += StopDownloadMenuItem_Click;
        _retryDownloadFromIdcMenuItem.Click += RetryDownloadFromIdcMenuItem_Click;
        _removeDownloadMenuItem.Click += RemoveDownloadMenuItem_Click;
        _removeFinishedDownloadsMenuItem.Click += RemoveFinishedDownloadsMenuItem_Click;

        _downloadMonitorGrid.ContextMenuStrip = _downloadMonitorContextMenu;
        _downloadMonitorGrid.MouseDown += DownloadMonitorGrid_MouseDown;
    }

    private void EnsureDownloadBandwidthPresetMenuItems()
    {
        if (_downloadBandwidthPresetMenuItems.Count > 0)
        {
            return;
        }

        for (var mbps = 1; mbps <= 10; mbps++)
        {
            var item = new ToolStripMenuItem($"{mbps} MB/s")
            {
                Tag = mbps
            };

            item.Click += DownloadBandwidthPresetMenuItem_Click;
            _downloadBandwidthPresetMenuItems.Add(item);
            _setDownloadBandwidthMenuItem.DropDownItems.Add(item);
        }
    }

    private void DownloadMonitorGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        var column = _downloadMonitorGrid.Columns[e.ColumnIndex];
        if (!string.Equals(column.Name, DownloadProgressColumnName, StringComparison.Ordinal))
        {
            return;
        }

        e.Handled = true;
        e.PaintBackground(e.CellBounds, true);

        var graphics = e.Graphics;
        if (graphics is null)
        {
            return;
        }

        var row = _downloadMonitorGrid.Rows[e.RowIndex].DataBoundItem as DownloadMonitorRow;
        var percent = Math.Clamp(GetDownloadProgressPercent(row), 0d, 100d);
        var progressText = row?.ProgressDisplay ?? $"{percent:0.0}%";

        var barBounds = new Rectangle(
            e.CellBounds.X + 4,
            e.CellBounds.Y + 4,
            Math.Max(1, e.CellBounds.Width - 8),
            Math.Max(1, e.CellBounds.Height - 8));

        using (var borderPen = new Pen(Color.Silver))
        {
            graphics.DrawRectangle(borderPen, barBounds);
        }

        if (percent > 0)
        {
            var fillWidth = (int)Math.Round((barBounds.Width - 1) * (percent / 100d));
            if (fillWidth > 0)
            {
                var fillRect = new Rectangle(
                    barBounds.X + 1,
                    barBounds.Y + 1,
                    Math.Max(1, fillWidth),
                    Math.Max(1, barBounds.Height - 1));

                using var fillBrush = new SolidBrush(Color.FromArgb(16, 185, 129));
                graphics.FillRectangle(fillBrush, fillRect);
            }
        }

        var textColor = _downloadMonitorGrid.Rows[e.RowIndex].Selected ? Color.White : Color.Black;
        var textFont = e.CellStyle?.Font ?? _downloadMonitorGrid.Font;
        TextRenderer.DrawText(
            graphics,
            progressText,
            textFont,
            barBounds,
            textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        e.Paint(e.CellBounds, DataGridViewPaintParts.Border);
    }

    private static double GetDownloadProgressPercent(DownloadMonitorRow? row)
    {
        if (row is null)
        {
            return 0d;
        }

        if (row.TotalBytes.HasValue && row.TotalBytes.Value > 0)
        {
            return (row.ProcessedBytes * 100d) / row.TotalBytes.Value;
        }

        return row.ProgressPercent;
    }

    private void DownloadMonitorGrid_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var hit = _downloadMonitorGrid.HitTest(e.X, e.Y);
        if (hit.RowIndex < 0 || hit.RowIndex >= _downloadMonitorGrid.Rows.Count)
        {
            return;
        }

        _downloadMonitorGrid.ClearSelection();
        var row = _downloadMonitorGrid.Rows[hit.RowIndex];
        row.Selected = true;
        _downloadMonitorGrid.CurrentCell = row.Cells[0];
    }

    private void DownloadMonitorContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        var hasRunningTasks = _activeResourceSyncControls.Values.Any(control => !control.IsPaused);
        var hasPausedTasks = _activeResourceSyncControls.Values.Any(control => control.IsPaused);
        _pauseAllDownloadsMenuItem.Enabled = hasRunningTasks;
        _resumeAllDownloadsMenuItem.Enabled = hasPausedTasks;

        var selected = GetSelectedDownloadMonitorRow();
        if (selected is null)
        {
            _pauseDownloadMenuItem.Enabled = false;
            _resumeDownloadMenuItem.Enabled = false;
            _stopDownloadMenuItem.Enabled = false;
            _setDownloadBandwidthMenuItem.Enabled = false;
            _retryDownloadFromIdcMenuItem.Enabled = false;
            _removeDownloadMenuItem.Enabled = false;
            _removeFinishedDownloadsMenuItem.Enabled = _downloadMonitorRows.Count > 0;
            SetCheckedBandwidthPreset(0);
            return;
        }

        var isRunning = IsResourceSyncRunning(selected);
        var isPaused = IsResourceSyncPaused(selected);
        var selectedControl = TryGetResourceSyncToken(selected, out var syncControl) ? syncControl : null;
        var retryCandidate = !isRunning &&
                             string.Equals(selected.Status, I18n.Server.UpdateFailedStatus, StringComparison.OrdinalIgnoreCase) &&
                             FindResourceRowForMonitor(selected) is { HasSource: true };
        _pauseDownloadMenuItem.Enabled = isRunning && !isPaused;
        _resumeDownloadMenuItem.Enabled = isRunning && isPaused;
        _stopDownloadMenuItem.Enabled = isRunning;
        _setDownloadBandwidthMenuItem.Enabled = isRunning && selectedControl is not null;
        _retryDownloadFromIdcMenuItem.Enabled = retryCandidate;
        _removeDownloadMenuItem.Enabled = !isRunning;
        _removeFinishedDownloadsMenuItem.Enabled = _downloadMonitorRows.Any(row => !IsResourceSyncRunning(row));
        SetCheckedBandwidthPreset(selectedControl?.BandwidthLimitMbps ?? 0);
    }

    private void PauseDownloadMenuItem_Click(object? sender, EventArgs e)
    {
        var row = GetSelectedDownloadMonitorRow();
        if (row is null)
        {
            return;
        }

        if (!TryGetResourceSyncToken(row, out var syncControl))
        {
            ShowInfo(I18n.Server.TaskNoLongerRunning);
            return;
        }

        syncControl.Pause();
        UpdateDownloadMonitor(row, row.ProgressPercent, I18n.Server.UpdatePausedStatus, I18n.Server.TaskPaused);
    }

    private void ResumeDownloadMenuItem_Click(object? sender, EventArgs e)
    {
        var row = GetSelectedDownloadMonitorRow();
        if (row is null)
        {
            return;
        }

        if (!TryGetResourceSyncToken(row, out var syncControl))
        {
            ShowInfo(I18n.Server.TaskNoLongerRunning);
            return;
        }

        syncControl.Resume();
        UpdateDownloadMonitor(row, row.ProgressPercent, I18n.Server.UpdateRunningStatus, I18n.Server.TaskResumed);
    }

    private void PauseAllDownloadsMenuItem_Click(object? sender, EventArgs e)
    {
        var runningRows = _activeResourceSyncControls
            .Where(item => !item.Value.IsPaused)
            .Select(item => item.Key)
            .ToList();

        if (runningRows.Count == 0)
        {
            return;
        }

        foreach (var row in runningRows)
        {
            if (!TryGetResourceSyncToken(row, out var syncControl))
            {
                continue;
            }

            syncControl.Pause();
            UpdateDownloadMonitor(row, row.ProgressPercent, I18n.Server.UpdatePausedStatus, I18n.Server.BatchPaused);
        }
    }

    private void ResumeAllDownloadsMenuItem_Click(object? sender, EventArgs e)
    {
        var pausedRows = _activeResourceSyncControls
            .Where(item => item.Value.IsPaused)
            .Select(item => item.Key)
            .ToList();

        if (pausedRows.Count == 0)
        {
            return;
        }

        foreach (var row in pausedRows)
        {
            if (!TryGetResourceSyncToken(row, out var syncControl))
            {
                continue;
            }

            syncControl.Resume();
            UpdateDownloadMonitor(row, row.ProgressPercent, I18n.Server.UpdateRunningStatus, I18n.Server.BatchResumed);
        }
    }

    private void DownloadBandwidthPresetMenuItem_Click(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem { Tag: int mbps })
        {
            return;
        }

        var row = GetSelectedDownloadMonitorRow();
        if (row is null)
        {
            return;
        }

        if (!TryGetResourceSyncToken(row, out var syncControl))
        {
            ShowInfo(I18n.Server.TaskNoLongerRunning);
            return;
        }

        syncControl.SetBandwidthLimitMbps(mbps);
        SetCheckedBandwidthPreset(mbps);
        UpdateDownloadMonitor(row, row.ProgressPercent, row.Status, I18n.Server.BandwidthLimitSet(mbps));
    }

    private void SetCheckedBandwidthPreset(int mbps)
    {
        foreach (var item in _downloadBandwidthPresetMenuItems)
        {
            item.Checked = item.Tag is int value && value == mbps;
        }
    }

    private void StopDownloadMenuItem_Click(object? sender, EventArgs e)
    {
        var row = GetSelectedDownloadMonitorRow();
        if (row is null)
        {
            return;
        }

        if (!TryGetResourceSyncToken(row, out var syncControl))
        {
            ShowInfo(I18n.Server.TaskNoLongerRunning);
            return;
        }

        UpdateDownloadMonitor(row, row.ProgressPercent, I18n.Server.UpdateStoppingStatus, I18n.Server.TaskStopping);
        syncControl.Cancel();
    }

    private async void RetryDownloadFromIdcMenuItem_Click(object? sender, EventArgs e)
    {
        var monitorRow = GetSelectedDownloadMonitorRow();
        if (monitorRow is null)
        {
            return;
        }

        if (IsResourceSyncRunning(monitorRow))
        {
            ShowInfo(I18n.Server.RetryWhileRunningNotAllowed);
            return;
        }

        var resourceRow = FindResourceRowForMonitor(monitorRow);
        if (resourceRow is null || !resourceRow.HasSource)
        {
            ShowInfo(I18n.Server.RetrySourceNotFound);
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            ToggleResourceSyncControls(false);
            UpdateResourceRootsFromInputs();
            await SaveUiSettingsAsync();

            int? gameId = null;
            try
            {
                gameId = await SyncResourceRowAsync(resourceRow);
                await AutoExportCatalogAsync();
            }
            finally
            {
                await ReloadAllAsync(gameId ?? SelectedGame?.Id);
            }
        }, () => ToggleResourceSyncControls(true));
    }

    private void RemoveDownloadMenuItem_Click(object? sender, EventArgs e)
    {
        var row = GetSelectedDownloadMonitorRow();
        if (row is null)
        {
            return;
        }

        if (IsResourceSyncRunning(row))
        {
            ShowInfo(I18n.Server.NeedStopTaskBeforeRemoveRow);
            return;
        }

        _downloadMonitorRows.Remove(row);
        RefreshDownloadMonitorSerialNumbers();
        _downloadMonitorBinding.ResetBindings(false);

        if (_currentResourceFilter == ResourceFilterKind.DownloadMonitor)
        {
            UpdateDownloadSummary();
        }
    }

    private void RemoveFinishedDownloadsMenuItem_Click(object? sender, EventArgs e)
    {
        var removableRows = _downloadMonitorRows
            .Where(row => !IsResourceSyncRunning(row))
            .ToList();

        if (removableRows.Count == 0)
        {
            return;
        }

        foreach (var row in removableRows)
        {
            _downloadMonitorRows.Remove(row);
        }

        RefreshDownloadMonitorSerialNumbers();
        _downloadMonitorBinding.ResetBindings(false);

        if (_currentResourceFilter == ResourceFilterKind.DownloadMonitor)
        {
            UpdateDownloadSummary();
        }
    }

    private DownloadMonitorRow? GetSelectedDownloadMonitorRow()
    {
        return _downloadMonitorGrid.CurrentRow?.DataBoundItem as DownloadMonitorRow;
    }

    private void RegisterResourceSyncToken(DownloadMonitorRow row, ResourceSyncTaskControl syncControl)
    {
        _activeResourceSyncControls[row] = syncControl;
    }

    private void UnregisterResourceSyncToken(DownloadMonitorRow row)
    {
        if (_activeResourceSyncControls.TryGetValue(row, out var syncControl))
        {
            _activeResourceSyncControls.Remove(row);
            syncControl.Dispose();
        }
    }

    private bool TryGetResourceSyncToken(DownloadMonitorRow row, out ResourceSyncTaskControl syncControl)
    {
        return _activeResourceSyncControls.TryGetValue(row, out syncControl!);
    }

    private bool IsResourceSyncRunning(DownloadMonitorRow row)
    {
        return _activeResourceSyncControls.ContainsKey(row);
    }

    private bool IsResourceSyncPaused(DownloadMonitorRow row)
    {
        return _activeResourceSyncControls.TryGetValue(row, out var syncControl) && syncControl.IsPaused;
    }

    private void SeedDownloadMonitorRows(IReadOnlyList<UpdateLogEntry> logs)
    {
        if (_activeResourceSyncControls.Count > 0 ||
            _downloadMonitorRows.Any(row =>
                string.Equals(row.Status, I18n.Server.UpdateRunningStatus, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.Status, I18n.Server.UpdatePausedStatus, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.Status, I18n.Server.UpdateStoppingStatus, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _downloadMonitorRows.Clear();

        var recentUpdateLogs = logs
            .Where(log =>
                log.Action.Contains(I18n.Server.LogActionUpdateKeyword, StringComparison.OrdinalIgnoreCase) ||
                log.Action.Contains(I18n.Server.LogActionSyncKeyword, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(log => log.CreatedAt)
            .Take(30);

        foreach (var log in recentUpdateLogs)
        {
            var row = new DownloadMonitorRow
            {
                StartedAt = log.CreatedAt.ToLocalTime(),
                UpdatedAt = log.CreatedAt.ToLocalTime(),
                GameName = log.GameName,
                ProgressPercent = string.Equals(log.Status, I18n.Server.UpdateSuccessStatus, StringComparison.OrdinalIgnoreCase) ? 100 : 0,
                Status = log.Status,
                Message = log.Message,
                GameIdDisplay = "-",
                ProgressDisplay = string.Equals(log.Status, I18n.Server.UpdateSuccessStatus, StringComparison.OrdinalIgnoreCase) ? "100.0%" : "0.0%",
                TotalSizeGbDisplay = "-",
                RemainingMbDisplay = "-",
                RemainingTimeDisplay = "-",
                SpeedMbpsDisplay = "-"
            };

            UpdateDownloadMonitorDerivedColumns(row, null);
            _downloadMonitorRows.Add(row);
        }

        RefreshDownloadMonitorSerialNumbers();
        _downloadMonitorBinding.ResetBindings(false);

        if (_currentResourceFilter == ResourceFilterKind.DownloadMonitor)
        {
            UpdateDownloadSummary();
        }
    }

}





