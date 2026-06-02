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
{
    private Image? _resourceStatusActiveIcon;
    private Image? _resourceStatusInactiveIcon;

    private int _resourceDownloadedCount = 0;
    private int _resourceMissingCount = 0;

    private string _resourceSearchQuery = string.Empty;
    private System.Windows.Forms.Timer _filterResourceDebounceTimer = new System.Windows.Forms.Timer { Interval = 300 };

    private void ResourceTree_DrawNode(object? sender, DrawTreeNodeEventArgs e)
    {
        var g = e.Graphics;
        var node = e.Node;
        if (node == null || _resourceTree.Font == null) return;

        bool isSelected = (e.State & TreeNodeStates.Selected) != 0;
        bool isHovered = false; // TreeView doesn't easily provide hover state for nodes without mouse tracking, but standard selection is fine

        var backColor = isSelected ? Color.FromArgb(243, 232, 255) : _resourceTree.BackColor; // purple-100 for selection
        var textColor = isSelected ? Color.FromArgb(126, 34, 206) : Color.FromArgb(71, 85, 105); // purple-700 or slate-600

        // Draw Background
        var bounds = e.Bounds;
        using (var brush = new SolidBrush(backColor))
        {
            g.FillRectangle(brush, bounds);
        }

        // Draw Text
        var textFont = isSelected ? new Font(_resourceTree.Font, FontStyle.Bold) : _resourceTree.Font;
        
        // Calculate indent
        int indent = node.Level * 20 + 24; 
        
        // If it's a root node, make it bold and slate-800
        if (node.Level == 0)
        {
            textColor = Color.FromArgb(30, 41, 59); // slate-800
            textFont = new Font(_resourceTree.Font, FontStyle.Bold);
            indent = 8;
        }

        var textRect = new Rectangle(bounds.Left + indent, bounds.Top, bounds.Width - indent - 40, bounds.Height);
        TextRenderer.DrawText(g, node.Text, textFont, textRect, textColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

        // Draw Badge
        if (node.Level > 0 && node.Tag is ResourceFilterKind filterKind)
        {
            int count = -1;
            if (filterKind == ResourceFilterKind.Missing) count = _resourceMissingCount;
            if (filterKind == ResourceFilterKind.Downloaded) count = _resourceDownloadedCount;

            if (count >= 0)
            {
                string countText = count.ToString();
                var badgeFont = new Font(_resourceTree.Font.FontFamily, _resourceTree.Font.Size - 1.5f, FontStyle.Bold);
                var textSize = TextRenderer.MeasureText(countText, badgeFont);
                
                int badgeWidth = Math.Max(24, textSize.Width + 12);
                int badgeHeight = 20;
                int badgeX = bounds.Right - badgeWidth - 12;
                int badgeY = bounds.Top + (bounds.Height - badgeHeight) / 2;

                var badgeRect = new Rectangle(badgeX, badgeY, badgeWidth, badgeHeight);
                var badgeBackColor = isSelected ? Color.FromArgb(233, 213, 255) : Color.FromArgb(241, 245, 249); // purple-200 or slate-100
                var badgeTextColor = isSelected ? Color.FromArgb(126, 34, 206) : Color.FromArgb(100, 116, 139); // purple-700 or slate-500

                using (var path = GameUpdater.WinForms.Controls.CardPanel.GetRoundedRectPath(badgeRect, 10))
                {
                    using (var badgeBrush = new SolidBrush(badgeBackColor))
                    {
                        g.FillPath(badgeBrush, path);
                    }
                }

                TextRenderer.DrawText(g, countText, badgeFont, badgeRect, badgeTextColor, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
                badgeFont.Dispose();
            }
        }
        
        if (isSelected && node.Level == 0)
        {
           textFont.Dispose(); 
        }
    }

    private void BuildResourceTree()
    {
        _resourceTree.AfterSelect -= ResourceTree_AfterSelect;
        _resourceTree.DrawMode = TreeViewDrawMode.OwnerDrawAll;
        _resourceTree.DrawNode -= ResourceTree_DrawNode;
        _resourceTree.DrawNode += ResourceTree_DrawNode;

        _filterResourceDebounceTimer.Tick -= FilterResourceDebounceTimer_Tick;
        _filterResourceDebounceTimer.Tick += FilterResourceDebounceTimer_Tick;

        _resourceTree.Nodes.Clear();

        var resourceRoot = new TreeNode(I18n.Server.ResourceTreeRoot)
        {
            Tag = ResourceFilterKind.All
        };
        resourceRoot.Nodes.Add(new TreeNode(I18n.Server.ResourceTreeMissing)
        {
            Tag = ResourceFilterKind.Missing
        });
        resourceRoot.Nodes.Add(new TreeNode(I18n.Server.ResourceTreeDownloaded)
        {
            Tag = ResourceFilterKind.Downloaded
        });

        var monitorRoot = new TreeNode(I18n.Server.ResourceTreeMonitorRoot)
        {
            Tag = ResourceFilterKind.DownloadMonitor
        };
        monitorRoot.Nodes.Add(new TreeNode(I18n.Server.ResourceTreeMonitor)
        {
            Tag = ResourceFilterKind.DownloadMonitor
        });

        _resourceTree.Nodes.Add(resourceRoot);
        _resourceTree.Nodes.Add(monitorRoot);
        _resourceTree.ExpandAll();

        _resourceTree.AfterSelect += ResourceTree_AfterSelect;
        _resourceTree.SelectedNode = resourceRoot;
    }

    private void ResourceTree_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is ResourceFilterKind filterKind)
        {
            if (_resourceWorkspaceTabControl.TabPages.Count > 0 && _resourceWorkspaceTabControl.SelectedIndex != 0)
            {
                _resourceWorkspaceTabControl.SelectedIndex = 0;
            }
            ApplyResourceFilter(filterKind);
        }
    }

    private async void RefreshResourcesButton_Click(object? sender, EventArgs e)
    {
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            UpdateResourceRootsFromInputs();
            await ReloadAllAsync(SelectedGame?.Id);
        });
    }

    private void BrowseResourceSourceButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = I18n.Server.ResourceSourcePickerDescription,
            UseDescriptionForTitle = true,
            SelectedPath = _resourceSourceRootTextBox.Text
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _resourceSourceRootTextBox.Text = dialog.SelectedPath;
        }
    }



    private async void SaveResourceSettingsButton_Click(object? sender, EventArgs e)
    {
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            UpdateResourceRootsFromInputs();
            await SaveUiSettingsAsync();
            await ReloadAllAsync(SelectedGame?.Id);
            ShowInfo(I18n.Server.ResourceConfigSaved);
        });
    }

    private async void CheckResourceHealthButton_Click(object? sender, EventArgs e)
    {
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            UpdateResourceRootsFromInputs();
            await RebuildResourceRowsAsync(_gamesBinding.List.OfType<GameRecord>().ToList());
            var missingSource = _allResourceRows.Count(row => !row.HasSource);
            var needSync = _allResourceRows.Count(row => row.RequiredAdditionalBytes.GetValueOrDefault() > 0);
            var missingRunFile = _allResourceRows.Count(row => row.IsDownloaded && !string.Equals(row.HealthStatus, I18n.Server.ResourceHealthOk, StringComparison.OrdinalIgnoreCase));
            UpdateResourceSummary(_allResourceRows);
            ShowInfo(I18n.Server.ResourceHealthCheckDone(missingSource, needSync, missingRunFile, BuildResourceHealthSummary()));
        });
    }

    private async void SyncSelectedResourceButton_Click(object? sender, EventArgs e)
{
    if (_resourcesGrid.Visible == false)
    {
        ShowInfo(I18n.Server.NeedSwitchToResourceList);
        return;
    }

    var selectedRows = GetSelectedOrCurrentResourceRows()
        .Where(row => row.HasSource)
        .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (selectedRows.Count == 0)
    {
        ShowInfo(I18n.Server.NeedSelectResourceWithSource);
        return;
    }

    await RunResourceSyncForRowsAsync(selectedRows, ResourceSyncMode.Incremental);
}

    private async Task RunResourceSyncForRowsAsync(
        IReadOnlyList<ResourceGameRow> rows,
        ResourceSyncMode syncMode,
        string? overrideTargetRoot = null)
    {
        if (rows.Count == 0)
        {
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            ToggleResourceSyncControls(false);
            UpdateResourceRootsFromInputs();
            await SaveUiSettingsAsync();

            int? selectedGameId = SelectedGame?.Id;
            foreach (var row in rows)
            {
                if (FindActiveMonitorRowForResource(row) is not null)
                {
                    AppendUpdateMessage(I18n.Server.ResourceSkipBecauseTaskRunning(row.Name));
                    continue;
                }

                try
                {
                    var gameId = await SyncResourceRowAsync(row, syncMode, overrideTargetRoot);
                    if (gameId.HasValue)
                    {
                        selectedGameId = gameId.Value;
                    }
                }
                catch (OperationCanceledException)
                {
                    AppendUpdateMessage(I18n.Server.ResourceTaskStoppedByRequest(row.Name));
                    break;
                }
            }

            await AutoExportCatalogAsync();
            await ReloadAllAsync(selectedGameId);
        }, () => ToggleResourceSyncControls(true));
    }

    private async Task SyncGameFromResourceLegacyAsync(GameRecord game)
    {
        var targetRoot = GetConfiguredResourceTargetRoots().FirstOrDefault(r => game.InstallPath.StartsWith(r, StringComparison.OrdinalIgnoreCase)) ?? GetConfiguredResourceTargetRoots().FirstOrDefault() ?? "E:\\GameOnline";
        
        var monitorRow = StartDownloadMonitor(game.Name, game.Id > 0 ? game.Id : null, resourceKey: ResolveSourceKeyForGame(game));
        var syncControl = new ResourceSyncTaskControl();
        var syncMode = ResourceSyncMode.Incremental;
        var actionName = I18n.Server.ResourceSyncAction;

        try
        {
            var progress = new Progress<UpdateProgressInfo>(info =>
            {
                if (syncControl.IsPaused)
                {
                    return;
                }

                UpdateDownloadMonitor(monitorRow, info.Percent, I18n.Server.UpdateRunningStatus, info.Message, info);
            });

            var result = await _resourceSyncService.SyncGameAsync(
                game,
                _resourceSourceRootPath,
                targetRoot,
                progress);

            var successMessage = syncMode == ResourceSyncMode.MissingOnly
                ? I18n.Server.ResourceSyncMissingSuccess(game.Name, result.CopiedFiles, result.TotalFiles)
                : I18n.Server.ResourceSyncDownloadedSuccess(game.Name, result.CopiedFiles, result.TotalFiles, result.SourcePath, result.TargetPath);

            UpdateDownloadMonitor(monitorRow, 100, I18n.Server.UpdateSuccessStatus, successMessage);
            AppendUpdateMessage(successMessage);

            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = game.Id > 0 ? game.Id : null,
                GameName = game.Name,
                Action = actionName,
                Status = I18n.Server.UpdateSuccessStatus,
                Message = successMessage,
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception exception)
        {
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, I18n.Server.UpdateFailedStatus, exception.Message);

            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = game.Id > 0 ? game.Id : null,
                GameName = game.Name,
                Action = I18n.Server.ResourceSyncAction,
                Status = I18n.Server.UpdateFailedStatus,
                Message = exception.Message,
                CreatedAt = DateTime.UtcNow
            });

            throw;
        }
    }

    private async Task SyncGameFromResourceAsync(
        GameRecord game,
        string targetRootPath,
        ResourceSyncMode syncMode = ResourceSyncMode.Incremental,
        IReadOnlyList<string>? sourceRoots = null,
        string? resourceKey = null)
    {
        var sourceRootCandidates = sourceRoots is { Count: > 0 }
            ? sourceRoots
            : GetConfiguredResourceSourceRoots();
        var monitorRow = StartDownloadMonitor(
            game.Name,
            game.Id > 0 ? game.Id : null,
            resourceKey: resourceKey ?? ResolveSourceKeyForGame(game));
        var syncControl = new ResourceSyncTaskControl();
        syncControl.SetBandwidthLimitMbps(_resourceBandwidthLimitMbps);
        RegisterResourceSyncToken(monitorRow, syncControl);
        var actionName = syncMode == ResourceSyncMode.MissingOnly ? I18n.Server.ResourceSyncMissingAction : I18n.Server.ResourceSyncAction;

        try
        {
            var progress = new Progress<UpdateProgressInfo>(info =>
            {
                if (syncControl.IsPaused)
                {
                    return;
                }

                UpdateDownloadMonitor(monitorRow, info.Percent, I18n.Server.UpdateRunningStatus, info.Message, info);
            });

            var result = await SyncGameWithMirrorFallbackAsync(
                game,
                sourceRootCandidates,
                targetRootPath,
                progress,
                syncMode,
                syncControl);

            var successMessage = syncMode == ResourceSyncMode.MissingOnly
                ? I18n.Server.ResourceSyncMissingSuccess(game.Name, result.CopiedFiles, result.TotalFiles)
                : I18n.Server.ResourceSyncDownloadedSuccess(game.Name, result.CopiedFiles, result.TotalFiles, result.SourcePath, result.TargetPath);

            UpdateDownloadMonitor(monitorRow, 100, I18n.Server.UpdateSuccessStatus, successMessage);
            AppendUpdateMessage(successMessage);

            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = game.Id > 0 ? game.Id : null,
                GameName = game.Name,
                Action = actionName,
                Status = I18n.Server.UpdateSuccessStatus,
                Message = successMessage,
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (OperationCanceledException)
        {
            var canceledMessage = I18n.Server.ResourceDownloadStopped(game.Name);
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, I18n.Server.UpdateStoppedStatus, canceledMessage);
            AppendUpdateMessage(canceledMessage);

            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = game.Id > 0 ? game.Id : null,
                GameName = game.Name,
                Action = actionName,
                Status = I18n.Server.UpdateStoppedStatus,
                Message = canceledMessage,
                CreatedAt = DateTime.UtcNow
            });

            throw;
        }
        catch (Exception exception)
        {
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, I18n.Server.UpdateFailedStatus, exception.Message);

            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = game.Id > 0 ? game.Id : null,
                GameName = game.Name,
                Action = actionName,
                Status = I18n.Server.UpdateFailedStatus,
                Message = exception.Message,
                CreatedAt = DateTime.UtcNow
            });

            throw;
        }
        finally
        {
            UnregisterResourceSyncToken(monitorRow);
        }
    }

    private async Task<ResourceSyncResult> SyncGameWithMirrorFallbackAsync(
        GameRecord game,
        IReadOnlyList<string> sourceRoots,
        string targetRootPath,
        IProgress<UpdateProgressInfo> progress,
        ResourceSyncMode syncMode,
        ResourceSyncTaskControl syncControl)
    {
        if (sourceRoots.Count == 0)
        {
            throw new InvalidOperationException(I18n.Server.ResourceSourceConfigInvalid);
        }

        Exception? lastException = null;
        for (var index = 0; index < sourceRoots.Count; index++)
        {
            var sourceRoot = sourceRoots[index];
            if (string.IsNullOrWhiteSpace(sourceRoot))
            {
                continue;
            }

            progress.Report(UpdateProgressInfo.Create(
                5,
                I18n.Server.ResourceTrySourceProgress(index + 1, sourceRoots.Count, sourceRoot)));

            try
            {
                return await Task.Run(
                    () => _resourceSyncService.SyncGameAsync(
                        game,
                        sourceRoot,
                        targetRootPath,
                        progress,
                        maxBytesPerSecond: null,
                        waitIfPausedAsync: syncControl.WaitIfPausedAsync,
                        syncMode: syncMode,
                        cancellationToken: syncControl.CancellationToken,
                        getMaxBytesPerSecond: () => syncControl.BandwidthLimitBytesPerSecond),
                    syncControl.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (index < sourceRoots.Count - 1)
                {
                    progress.Report(UpdateProgressInfo.Create(
                        5,
                        I18n.Server.ResourceSourceError(sourceRoot, ex.Message)));
                }
            }
        }

        throw lastException ?? new InvalidOperationException("Sync failed without exceptions");
    }

    private GameRecord? FindGameById(int gameId)
    {
        var games = (_gamesBinding.DataSource as IEnumerable<GameRecord>)?.ToList();
        return games?.FirstOrDefault(game => game.Id == gameId);
    }

    private GameRecord? FindGameByInstallPath(string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(installPath);
        var games = (_gamesBinding.DataSource as IEnumerable<GameRecord>)?.ToList();
        return games?.FirstOrDefault(game =>
        {
            if (string.IsNullOrWhiteSpace(game.InstallPath))
            {
                return false;
            }

            var gameInstallPath = Path.GetFullPath(game.InstallPath);
            return string.Equals(gameInstallPath, fullPath, StringComparison.OrdinalIgnoreCase);
        });
    }

    private GameRecord BuildTransientGameRecordFromResourceRow(ResourceGameRow row)
    {
        return new GameRecord
        {
            Id = 0,
            Name = row.Name,
            Category = string.IsNullOrWhiteSpace(row.Category) ? I18n.Server.ResourceDefaultCategoryIdc : row.Category,
            InstallPath = row.InstallPath,
            Version = I18n.Server.GameEditorDefaultVersion,
            LaunchRelativePath = string.Empty,
            LaunchArguments = string.Empty,
            Notes = I18n.Server.ResourceAutoCreatedFromSourceNote
        };
    }

    private async Task<int?> SyncResourceRowAsync(ResourceGameRow row, ResourceSyncMode syncMode = ResourceSyncMode.Incremental, string? overrideTargetRoot = null)
    {
        if (!await ConfirmDiskSpaceForResourceSyncAsync(row))
        {
            AppendUpdateMessage(I18n.Server.ResourceSkipDueLowDisk(row.Name));
            return null;
        }

        var existingGame = row.ManagedGameId.HasValue
            ? FindGameById(row.ManagedGameId.Value)
            : FindGameByInstallPath(row.InstallPath);

        // Figure out the correct target root:
        // If it's an update, use the existing InstallPath's root.
        // If it's new, use overrideTargetRoot OR the auto-balanced one from ResolveTargetPathForSourceKey.
        string targetRoot;
        if (row.IsDownloaded)
        {
            targetRoot = GetConfiguredResourceTargetRoots().FirstOrDefault(r => row.InstallPath.StartsWith(r, StringComparison.OrdinalIgnoreCase)) ?? GetConfiguredResourceTargetRoots().FirstOrDefault() ?? "E:\\GameOnline";
        }
        else
        {
            targetRoot = overrideTargetRoot ?? GetConfiguredResourceTargetRoots().FirstOrDefault(r => row.InstallPath.StartsWith(r, StringComparison.OrdinalIgnoreCase)) ?? GetConfiguredResourceTargetRoots().FirstOrDefault() ?? "E:\\GameOnline";
        }

        var game = existingGame ?? BuildTransientGameRecordFromResourceRow(row);
        var sourceRoots = GetCandidateSourceRootsForRow(row);
        await SyncGameFromResourceAsync(game, targetRoot, syncMode, sourceRoots, resourceKey: row.SourceKey);
        return await EnsureManagedGameRegistrationAsync(game, row);
    }

    private async Task<bool> ConfirmDiskSpaceForResourceSyncAsync(ResourceGameRow row)
    {
        if (!row.HasSource ||
            string.IsNullOrWhiteSpace(row.SourcePath) ||
            string.IsNullOrWhiteSpace(row.InstallPath) ||
            !Directory.Exists(row.SourcePath))
        {
            return true;
        }

        var estimate = await Task.Run(() => TryEstimateRequiredDiskSpace(row.SourcePath, row.InstallPath));
        if (estimate is null)
        {
            return true;
        }

        var (requiredAdditionalBytes, availableBytes) = estimate.Value;
        const long reserveBytes = 1L * 1024 * 1024 * 1024; // 1 GB safety margin.
        if (availableBytes >= requiredAdditionalBytes + reserveBytes)
        {
            return true;
        }

        var requiredGb = requiredAdditionalBytes / 1024d / 1024d / 1024d;
        var availableGb = availableBytes / 1024d / 1024d / 1024d;
        var reserveGb = reserveBytes / 1024d / 1024d / 1024d;

                var result = MessageBox.Show(
            this,
            I18n.Server.DiskSpaceWarning(row.Name, requiredGb, availableGb, reserveGb),
            I18n.Server.DiskSpaceWarningTitle,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        return result == DialogResult.Yes;
    }

    private static (long RequiredAdditionalBytes, long AvailableBytes)? TryEstimateRequiredDiskSpace(string sourcePath, string targetPath)
    {
        try
        {
            if (!Directory.Exists(sourcePath))
            {
                return null;
            }

            var sourceBytes = CalculateDirectorySizeSafe(sourcePath);
            var targetBytes = Directory.Exists(targetPath) ? CalculateDirectorySizeSafe(targetPath) : 0L;
            var requiredAdditionalBytes = Math.Max(0L, sourceBytes - targetBytes);

            var fullTargetPath = Path.GetFullPath(targetPath);
            var root = Path.GetPathRoot(fullTargetPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            var drive = new DriveInfo(root);
            return (requiredAdditionalBytes, drive.AvailableFreeSpace);
        }
        catch
        {
            return null;
        }
    }

    private static long? EstimateRequiredAdditionalBytes(string sourcePath, string targetPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(targetPath) || !Directory.Exists(sourcePath))
        {
            return null;
        }

        return TryEstimateRequiredDiskSpace(sourcePath, targetPath)?.RequiredAdditionalBytes;
    }

    private static string BuildResourceHealthStatus(bool hasSource, bool hasDownloadedData, bool runReady, long? requiredAdditionalBytes)
    {
        if (!hasSource)
        {
            return I18n.Server.ResourceHealthMissingSource;
        }

        if (!hasDownloadedData)
        {
            return I18n.Server.ResourceHealthNotDownloaded;
        }

        if (!runReady)
        {
            return I18n.Server.ResourceHealthMissingRunFile;
        }

        if (requiredAdditionalBytes.GetValueOrDefault() > 0)
        {
            return I18n.Server.ResourceHealthNeedSync;
        }

        return I18n.Server.ResourceHealthOk;
    }

    private string BuildResourceHealthSummary()
    {
        var messages = new List<string>();
        var sourceRoots = GetConfiguredResourceSourceRoots();
        var sourceOk = sourceRoots.Any(root => IsHttpSourceRootConfigured(root) || Directory.Exists(root));
        if (sourceRoots.Count > 1)
        {
            messages.Add(I18n.Server.ResourceMirrorSummary(sourceRoots.Count, sourceOk));
        }
        else
        {
            messages.Add(sourceOk ? I18n.Server.ResourceSourceStatusOk : I18n.Server.ResourceSourceStatusUnavailable);
        }

        var targetRoots = GetConfiguredResourceTargetRoots();
        var targetWritableCount = targetRoots.Count(root => Directory.Exists(root) && CanWriteToFolder(root));
        var targetWritable = targetWritableCount > 0;
        messages.Add(targetWritable ? I18n.Server.ResourceTargetStatusOk : I18n.Server.ResourceTargetStatusNotWritable);

        if (targetWritable)
        {
            double totalFreeGb = 0;
            double totalTotalGb = 0;
            foreach (var rootStr in targetRoots.Where(r => Directory.Exists(r)))
            {
                var root = Path.GetPathRoot(Path.GetFullPath(rootStr));
                if (!string.IsNullOrWhiteSpace(root))
                {
                    try
                    {
                        var drive = new DriveInfo(root);
                        if (drive.IsReady)
                        {
                            totalFreeGb += drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
                            totalTotalGb += drive.TotalSize / 1024d / 1024d / 1024d;
                        }
                    } catch {}
                }
            }
            if (totalTotalGb > 0)
            {
                var usedPercent = (totalTotalGb - totalFreeGb) * 100d / totalTotalGb;
                var warning = totalFreeGb < 100 || usedPercent >= 90 ? " !" : string.Empty;
                messages.Add(I18n.Server.ResourceDiskFreeSummary(totalFreeGb, totalTotalGb, usedPercent, warning));
            }
        }

        return string.Join(" • ", messages);
    }

    private static bool CanWriteToFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var testFile = Path.Combine(folder, $".write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testFile, "ok");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static long CalculateDirectorySizeSafe(string path)
    {
        var total = 0L;
        var stack = new Stack<string>();
        stack.Push(path);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    try
                    {
                        total += new FileInfo(file).Length;
                    }
                    catch
                    {
                        // Ignore individual file access errors.
                    }
                }
            }
            catch
            {
                // Ignore folder access errors.
            }

            try
            {
                foreach (var directory in Directory.EnumerateDirectories(current))
                {
                    stack.Push(directory);
                }
            }
            catch
            {
                // Ignore folder access errors.
            }
        }

        return total;
    }

    private async Task<int?> EnsureManagedGameRegistrationAsync(GameRecord game, ResourceGameRow row)
    {
        var launchRelativePath = FindPreferredLaunchRelativePath(game.InstallPath, row.Name);
        if (!string.IsNullOrWhiteSpace(launchRelativePath))
        {
            game.LaunchRelativePath = launchRelativePath;
        }

        if (string.IsNullOrWhiteSpace(game.Version))
        {
            game.Version = I18n.Server.GameEditorDefaultVersion;
        }

        var gameId = await _gameService.SaveGameAsync(game);
        game.Id = gameId;

        try
        {
            await _gameService.ScanGameAsync(game);
        }
        catch
        {
            // Ignore manifest scan failure to avoid blocking sync flow.
        }

        return gameId;
    }

    private IReadOnlyList<string> GetConfiguredResourceSourceRoots()
    {
        if (string.IsNullOrWhiteSpace(_resourceSourceRootPath))
        {
            return Array.Empty<string>();
        }

        return _resourceSourceRootPath
            .Split(['\r', '\n', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<string> GetCandidateSourceRootsForRow(ResourceGameRow row)
    {
        var configured = GetConfiguredResourceSourceRoots().ToList();
        if (configured.Count == 0)
        {
            return configured;
        }

        if (string.IsNullOrWhiteSpace(row.SourceRoot))
        {
            return configured;
        }

        configured.RemoveAll(item => string.Equals(item, row.SourceRoot, StringComparison.OrdinalIgnoreCase));
        configured.Insert(0, row.SourceRoot);
        return configured;
    }

    private void UpdateResourceRootsFromInputs()
    {
        _resourceSourceRootPath = _resourceSourceRootTextBox.Text.Trim();
        UpdateResourceTargetRootPathFromUi();
        _resourceBandwidthLimitMbps = Decimal.ToInt32(_resourceBandwidthLimitNumeric.Value);

        if (GetConfiguredResourceSourceRoots().Count == 0)
        {
            throw new InvalidOperationException(I18n.Server.NeedAtLeastOneIdcSource);
        }

        if (string.IsNullOrWhiteSpace(_resourceTargetRootPath))
        {
            throw new InvalidOperationException(I18n.Server.NeedResourceTargetFolder);
        }
    }

    private void ConfigureResourcesGrid()
    {
        EnsureResourceStatusIconsLoaded();
        _resourcesGrid.AutoGenerateColumns = false;
        _resourcesGrid.AllowUserToAddRows = false;
        _resourcesGrid.AllowUserToDeleteRows = false;
        _resourcesGrid.MultiSelect = true;
        _resourcesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _resourcesGrid.ReadOnly = true;
        _resourcesGrid.RowHeadersVisible = false;
        _resourcesGrid.DataSource = _resourcesBinding;
        _resourcesGrid.CellFormatting -= ResourcesGrid_CellFormatting;
        _resourcesGrid.CellFormatting += ResourcesGrid_CellFormatting;

        _resourcesGrid.Columns.Add(new DataGridViewImageColumn
        {
            Name = "ResourceStatusIcon",
            HeaderText = I18n.Server.ResourceGridHeaderStatus,
            DataPropertyName = nameof(ResourceGameRow.HealthStatus),
            Width = 84,
            ImageLayout = DataGridViewImageCellLayout.Zoom
        });
        _resourcesGrid.Columns.Add(CreateTextColumn(I18n.Server.ResourceGridHeaderGameName, nameof(ResourceGameRow.Name), 180));
        _resourcesGrid.Columns.Add(CreateTextColumn(I18n.Server.ResourceGridHeaderCategory, nameof(ResourceGameRow.Category), 120));
        _resourcesGrid.Columns.Add(CreateTextColumn(I18n.Server.ResourceGridHeaderSource, nameof(ResourceGameRow.SourceStatus), 110));
        _resourcesGrid.Columns.Add(CreateTextColumn(I18n.Server.ResourceGridHeaderDownloadStatus, nameof(ResourceGameRow.DownloadStatus), 160));
        _resourcesGrid.Columns.Add(CreateTextColumn(I18n.Server.ResourceGridHeaderSpeed, nameof(ResourceGameRow.DownloadSpeedDisplay), 100));
        _resourcesGrid.Columns.Add(CreateTextColumn(I18n.Server.ResourceGridHeaderRunStatus, nameof(ResourceGameRow.RunStatus), 130));
        _resourcesGrid.Columns.Add(CreateTextColumn(I18n.Server.ResourceGridHeaderFileCount, nameof(ResourceGameRow.FileCountDisplay), 90));
        _resourcesGrid.Columns.Add(CreateTextColumn(I18n.Server.ResourceGridHeaderSizeGb, nameof(ResourceGameRow.SizeGbDisplay), 110));
        _resourcesGrid.Columns.Add(CreateTextColumn(I18n.Server.ResourceGridHeaderRequiredGb, nameof(ResourceGameRow.RequiredAdditionalGbDisplay), 110));
        _resourcesGrid.Columns.Add(CreateTextColumn(I18n.Server.ResourceGridHeaderLastUpdated, nameof(ResourceGameRow.LastUpdatedAt), 150, "yyyy-MM-dd HH:mm:ss"));
        _resourcesGrid.Columns.Add(CreateTextColumn(I18n.Server.ResourceGridHeaderSourcePath, nameof(ResourceGameRow.SourcePath), 260));
        _resourcesGrid.Columns.Add(CreateTextColumn(I18n.Server.ResourceGridHeaderInstallPath, nameof(ResourceGameRow.InstallPath), 400, fill: true));
    }

    private void EnsureResourceStatusIconsLoaded()
    {
        if (_resourceStatusActiveIcon is not null && _resourceStatusInactiveIcon is not null)
        {
            return;
        }

        var assembly = typeof(MainForm).Assembly;
        _resourceStatusActiveIcon ??= TryLoadEmbeddedImage(assembly, "GameUpdater.WinForms.Resources.active.png")
            ?? TryLoadEmbeddedImage(assembly, "GameUpdater.WinForms.Resources.online_icon.png");
        _resourceStatusInactiveIcon ??= TryLoadEmbeddedImage(assembly, "GameUpdater.WinForms.Resources.inactive.png")
            ?? TryLoadEmbeddedImage(assembly, "GameUpdater.WinForms.Resources.offline_icon.png");
    }

    private static Image? TryLoadEmbeddedImage(System.Reflection.Assembly assembly, string resourceName)
    {
        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            return stream is null ? null : Image.FromStream(stream);
        }
        catch
        {
            return null;
        }
    }

    private void ResourcesGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _resourcesGrid.Columns[e.ColumnIndex].Name != "ResourceStatusIcon")
        {
            return;
        }

        var status = e.Value?.ToString()?.Trim();
        var isActive = string.Equals(status, I18n.Server.ResourceHealthOk, StringComparison.OrdinalIgnoreCase);
        e.Value = isActive ? _resourceStatusActiveIcon : _resourceStatusInactiveIcon;
        e.FormattingApplied = e.Value is not null;
    }

    private void EnsureResourcesContextMenu()
    {
        if (_resourcesContextMenuInitialized)
        {
            return;
        }

        _resourcesContextMenuInitialized = true;
        EnsureResourceBandwidthPresetMenuItems();

        _resourcesContextMenu.Items.Add(_downloadSelectedResourcesMenuItem);
        _resourcesContextMenu.Items.Add(new ToolStripSeparator());
        _resourcesContextMenu.Items.Add(_pauseSelectedResourcesMenuItem);
        _resourcesContextMenu.Items.Add(_resumeSelectedResourcesMenuItem);
        _resourcesContextMenu.Items.Add(_stopSelectedResourcesMenuItem);
        _resourcesContextMenu.Items.Add(_setResourceBandwidthMenuItem);
        _resourcesContextMenu.Items.Add(_retrySelectedResourcesMenuItem);
        _resourcesContextMenu.Items.Add(new ToolStripSeparator());
        _resourcesContextMenu.Items.Add(_syncMissingFromIdcMenuItem);

        _resourcesContextMenu.Opening += ResourcesContextMenu_Opening;
        _downloadSelectedResourcesMenuItem.Click += DownloadSelectedResourcesMenuItem_Click;
        _pauseSelectedResourcesMenuItem.Click += PauseSelectedResourcesMenuItem_Click;
        _resumeSelectedResourcesMenuItem.Click += ResumeSelectedResourcesMenuItem_Click;
        _stopSelectedResourcesMenuItem.Click += StopSelectedResourcesMenuItem_Click;
        _retrySelectedResourcesMenuItem.Click += RetrySelectedResourcesMenuItem_Click;
        _syncMissingFromIdcMenuItem.Click += SyncMissingFromIdcMenuItem_Click;

        _resourcesGrid.ContextMenuStrip = _resourcesContextMenu;
        _resourcesGrid.MouseDown += ResourcesGrid_MouseDown;
    }

    private void EnsureResourceBandwidthPresetMenuItems()
    {
        if (_resourceBandwidthPresetMenuItems.Count > 0)
        {
            return;
        }

        for (var mbps = 1; mbps <= 10; mbps++)
        {
            var item = new ToolStripMenuItem($"{mbps} MB/s")
            {
                Tag = mbps
            };

            item.Click += ResourceBandwidthPresetMenuItem_Click;
            _resourceBandwidthPresetMenuItems.Add(item);
            _setResourceBandwidthMenuItem.DropDownItems.Add(item);
        }
    }

    private void ResourcesGrid_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || !_resourcesGrid.Visible)
        {
            return;
        }

        var hit = _resourcesGrid.HitTest(e.X, e.Y);
        if (hit.RowIndex < 0 || hit.RowIndex >= _resourcesGrid.Rows.Count)
        {
            return;
        }

        var row = _resourcesGrid.Rows[hit.RowIndex];
        if (!row.Selected)
        {
            _resourcesGrid.ClearSelection();
            row.Selected = true;
        }

        _resourcesGrid.CurrentCell = row.Cells[0];
    }

    private void ResourcesContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        var selectedRows = GetSelectedOrCurrentResourceRows();
        if (selectedRows.Count == 0)
        {
            _downloadSelectedResourcesMenuItem.Enabled = false;
            _pauseSelectedResourcesMenuItem.Enabled = false;
            _resumeSelectedResourcesMenuItem.Enabled = false;
            _stopSelectedResourcesMenuItem.Enabled = false;
            _setResourceBandwidthMenuItem.Enabled = false;
            _retrySelectedResourcesMenuItem.Enabled = false;
            _syncMissingFromIdcMenuItem.Enabled = false;
            SetCheckedResourceBandwidthPreset(-1);
            return;
        }

        var selectedWithSourceCount = selectedRows.Count(row => row.HasSource);
        var selectedTasks = GetSelectedActiveResourceTasks(selectedRows);
        var hasRunning = selectedTasks.Any(item => !item.Control.IsPaused);
        var hasPaused = selectedTasks.Any(item => item.Control.IsPaused);
        var hasAnyTask = selectedTasks.Count > 0;
        var canRetry = selectedRows.Any(row =>
            row.HasSource &&
            FindLatestMonitorRowForResource(row) is { } monitorRow &&
            !IsResourceSyncRunning(monitorRow) &&
            IsRetryableMonitorStatus(monitorRow.Status));
        var canSyncMissing = selectedRows.Any(row =>
            row.HasSource &&
            row.IsDownloaded &&
            !string.IsNullOrWhiteSpace(row.InstallPath));

        _downloadSelectedResourcesMenuItem.Enabled = selectedWithSourceCount > 0;
        _pauseSelectedResourcesMenuItem.Enabled = hasRunning;
        _resumeSelectedResourcesMenuItem.Enabled = hasPaused;
        _stopSelectedResourcesMenuItem.Enabled = hasAnyTask;
        _setResourceBandwidthMenuItem.Enabled = hasAnyTask;
        _retrySelectedResourcesMenuItem.Enabled = canRetry;
        _syncMissingFromIdcMenuItem.Enabled = canSyncMissing;

        var selectedBandwidths = selectedTasks
            .Select(item => item.Control.BandwidthLimitMbps)
            .Distinct()
            .ToList();
        var unifiedBandwidth = selectedBandwidths.Count == 1 ? selectedBandwidths[0] : -1;
        SetCheckedResourceBandwidthPreset(unifiedBandwidth);
    }

    private async void DownloadSelectedResourcesMenuItem_Click(object? sender, EventArgs e)
    {
        var selectedRows = GetSelectedOrCurrentResourceRows()
            .Where(row => row.HasSource)
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedRows.Count == 0)
        {
            ShowInfo(I18n.Server.NoResourceWithSourceToDownload);
            return;
        }

        string? overrideTargetRoot = null;
        var targetRoots = GetConfiguredResourceTargetRoots();
        if (targetRoots.Count > 1 && selectedRows.Any(r => !r.IsDownloaded))
        {
            using var form = new TargetDriveSelectionForm(targetRoots);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                overrideTargetRoot = form.SelectedDrive;
            }
            else
            {
                return; // User cancelled
            }
        }

        await RunResourceSyncForRowsAsync(selectedRows, ResourceSyncMode.Incremental, overrideTargetRoot);
    }

    private void PauseSelectedResourcesMenuItem_Click(object? sender, EventArgs e)
    {
        var selectedTasks = GetSelectedActiveResourceTasks();
        var paused = 0;
        foreach (var (monitorRow, control) in selectedTasks)
        {
            if (control.IsPaused)
            {
                continue;
            }

            control.Pause();
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, I18n.Server.UpdatePausedStatus, I18n.Server.ResourcePauseFromList);
            paused++;
        }

        if (paused == 0)
        {
            ShowInfo(I18n.Server.NoRunningTaskToPause);
        }
    }

    private void ResumeSelectedResourcesMenuItem_Click(object? sender, EventArgs e)
    {
        var selectedTasks = GetSelectedActiveResourceTasks();
        var resumed = 0;
        foreach (var (monitorRow, control) in selectedTasks)
        {
            if (!control.IsPaused)
            {
                continue;
            }

            control.Resume();
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, I18n.Server.UpdateRunningStatus, I18n.Server.ResourceResumeFromList);
            resumed++;
        }

        if (resumed == 0)
        {
            ShowInfo(I18n.Server.NoPausedTaskToResume);
        }
    }

    private void StopSelectedResourcesMenuItem_Click(object? sender, EventArgs e)
    {
        var selectedTasks = GetSelectedActiveResourceTasks();
        var stopped = 0;
        foreach (var (monitorRow, control) in selectedTasks)
        {
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, I18n.Server.UpdateStoppingStatus, I18n.Server.ResourceRunningTaskInListStopMessage);
            control.Cancel();
            stopped++;
        }

        if (stopped == 0)
        {
            ShowInfo(I18n.Server.NoRunningTaskToStop);
        }
    }

    private async void RetrySelectedResourcesMenuItem_Click(object? sender, EventArgs e)
    {
        var selectedRows = GetSelectedOrCurrentResourceRows()
            .Where(row =>
                row.HasSource &&
                FindLatestMonitorRowForResource(row) is { } monitorRow &&
                !IsResourceSyncRunning(monitorRow) &&
                IsRetryableMonitorStatus(monitorRow.Status))
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedRows.Count == 0)
        {
            ShowInfo(I18n.Server.NoRetryableTask);
            return;
        }

        await RunResourceSyncForRowsAsync(selectedRows, ResourceSyncMode.Incremental);
    }

    private void ResourceBandwidthPresetMenuItem_Click(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem { Tag: int mbps })
        {
            return;
        }

        var selectedTasks = GetSelectedActiveResourceTasks();
        if (selectedTasks.Count == 0)
        {
            ShowInfo(I18n.Server.NoRunningTaskToSetBandwidth);
            return;
        }

        foreach (var (monitorRow, control) in selectedTasks)
        {
            control.SetBandwidthLimitMbps(mbps);
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, monitorRow.Status, I18n.Server.BandwidthLimitSet(mbps));
        }

        SetCheckedResourceBandwidthPreset(mbps);
    }

    private void SetCheckedResourceBandwidthPreset(int mbps)
    {
        foreach (var item in _resourceBandwidthPresetMenuItems)
        {
            item.Checked = item.Tag is int value && value == mbps;
        }
    }

    private async void SyncMissingFromIdcMenuItem_Click(object? sender, EventArgs e)
    {
        var selectedRows = GetSelectedOrCurrentResourceRows()
            .Where(row =>
                row.HasSource &&
                row.IsDownloaded &&
                !string.IsNullOrWhiteSpace(row.InstallPath))
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedRows.Count == 0)
        {
            ShowInfo(I18n.Server.NoEligibleGameToSyncMissing);
            return;
        }

        await RunResourceSyncForRowsAsync(selectedRows, ResourceSyncMode.MissingOnly);
    }

    private IReadOnlyList<ResourceGameRow> GetSelectedResourceRows()
    {
        return _resourcesGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .OrderBy(row => row.Index)
            .Select(row => row.DataBoundItem as ResourceGameRow)
            .Where(row => row is not null)
            .Cast<ResourceGameRow>()
            .ToList();
    }

    private IReadOnlyList<ResourceGameRow> GetSelectedOrCurrentResourceRows()
    {
        var selectedRows = GetSelectedResourceRows();
        if (selectedRows.Count > 0)
        {
            return selectedRows;
        }

        if (_resourcesGrid.CurrentRow?.DataBoundItem is ResourceGameRow currentRow)
        {
            return new[] { currentRow };
        }

        return Array.Empty<ResourceGameRow>();
    }

    private IReadOnlyList<(DownloadMonitorRow MonitorRow, ResourceSyncTaskControl Control)> GetSelectedActiveResourceTasks(
        IReadOnlyList<ResourceGameRow>? selectedRows = null)
    {
        var rows = selectedRows ?? GetSelectedOrCurrentResourceRows();
        var result = new List<(DownloadMonitorRow MonitorRow, ResourceSyncTaskControl Control)>();
        var seen = new HashSet<DownloadMonitorRow>();

        foreach (var resourceRow in rows)
        {
            var monitorRow = FindActiveMonitorRowForResource(resourceRow);
            if (monitorRow is null || !seen.Add(monitorRow))
            {
                continue;
            }

            if (TryGetResourceSyncToken(monitorRow, out var control))
            {
                result.Add((monitorRow, control));
            }
        }

        return result;
    }

    private DownloadMonitorRow? FindActiveMonitorRowForResource(ResourceGameRow resourceRow)
    {
        return _downloadMonitorRows
            .Where(row => IsResourceSyncRunning(row))
            .Where(row =>
                (!string.IsNullOrWhiteSpace(resourceRow.SourceKey) &&
                 string.Equals(row.ResourceKey, resourceRow.SourceKey, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(row.GameName, resourceRow.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(row => row.UpdatedAt)
            .FirstOrDefault();
    }

    private DownloadMonitorRow? FindLatestMonitorRowForResource(ResourceGameRow resourceRow)
    {
        return _downloadMonitorRows
            .Where(row =>
                (!string.IsNullOrWhiteSpace(resourceRow.SourceKey) &&
                 string.Equals(row.ResourceKey, resourceRow.SourceKey, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(row.GameName, resourceRow.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(row => row.UpdatedAt)
            .FirstOrDefault();
    }

    private static bool IsRetryableMonitorStatus(string status)
    {
        return string.Equals(status, I18n.Server.UpdateFailedStatus, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, I18n.Server.UpdateStoppedStatus, StringComparison.OrdinalIgnoreCase);
    }

    private void ConfigureDownloadMonitorGrid()
    {
        _downloadMonitorGrid.AutoGenerateColumns = false;
        _downloadMonitorGrid.AllowUserToAddRows = false;
        _downloadMonitorGrid.AllowUserToDeleteRows = false;
        _downloadMonitorGrid.MultiSelect = false;
        _downloadMonitorGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _downloadMonitorGrid.ReadOnly = true;
        _downloadMonitorGrid.RowHeadersVisible = false;
        _downloadMonitorGrid.DataSource = _downloadMonitorBinding;

        _downloadMonitorGrid.Columns.Add(CreateTextColumn(I18n.Server.MonitorGridHeaderIndex, nameof(DownloadMonitorRow.SerialNumber), 50));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn(I18n.Server.MonitorGridHeaderGameId, nameof(DownloadMonitorRow.GameIdDisplay), 80));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn(I18n.Server.MonitorGridHeaderGameName, nameof(DownloadMonitorRow.GameName), 190));
        var progressColumn = new DataGridViewTextBoxColumn
        {
            Name = DownloadProgressColumnName,
            HeaderText = I18n.Server.MonitorGridHeaderProgress,
            DataPropertyName = nameof(DownloadMonitorRow.ProgressPercent),
            Width = 100
        };
        _downloadMonitorGrid.Columns.Add(progressColumn);
        _downloadMonitorGrid.Columns.Add(CreateTextColumn(I18n.Server.MonitorGridHeaderStatus, nameof(DownloadMonitorRow.Status), 110));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn(I18n.Server.MonitorGridHeaderTotalGb, nameof(DownloadMonitorRow.TotalSizeGbDisplay), 115));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn(I18n.Server.MonitorGridHeaderRemainingMb, nameof(DownloadMonitorRow.RemainingMbDisplay), 115));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn(I18n.Server.MonitorGridHeaderRemainingTime, nameof(DownloadMonitorRow.RemainingTimeDisplay), 125));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn(I18n.Server.MonitorGridHeaderSpeed, nameof(DownloadMonitorRow.SpeedMbpsDisplay), 100));
        _downloadMonitorGrid.CellPainting -= DownloadMonitorGrid_CellPainting;
        _downloadMonitorGrid.CellPainting += DownloadMonitorGrid_CellPainting;
    }

    private async Task RebuildResourceRowsAsync(IReadOnlyList<GameRecord> games)
    {
        _allResourceRows.Clear();
        var sourceFolders = await GetSourceFolderEntriesAsync();
        var sourceFoldersByKey = sourceFolders
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var game in games.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            _allResourceRows.Add(CreateResourceRow(game, sourceFoldersByKey));
        }

        var existingSourceKeys = new HashSet<string>(
            _allResourceRows
                .Where(row => !string.IsNullOrWhiteSpace(row.SourceKey))
                .Select(row => row.SourceKey),
            StringComparer.OrdinalIgnoreCase);

        foreach (var sourceFolder in sourceFolders)
        {
            if (existingSourceKeys.Contains(sourceFolder.Key))
            {
                continue;
            }

            _allResourceRows.Add(CreateSourceOnlyResourceRow(sourceFolder));
        }

        await RefreshResourceCompletionStatesAsync(games);

        _allResourceRows.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));

        ApplyResourceFilter(_currentResourceFilter);
    }

    private ResourceGameRow CreateResourceRow(GameRecord game, IReadOnlyDictionary<string, SourceFolderEntry> sourceFoldersByKey)
    {
        var sourceKey = ResolveSourceKeyForGame(game);
        var sourcePath = ResolveSourcePathForGame(game);
        var sourceRoot = string.Empty;
        var sourceExists = sourceFoldersByKey.ContainsKey(sourceKey);

        if (!sourceExists &&
            !string.IsNullOrWhiteSpace(sourcePath) &&
            !sourcePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !sourcePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            sourceExists = Directory.Exists(sourcePath);
        }

        if (sourceFoldersByKey.TryGetValue(sourceKey, out var sourceFolder) &&
            !string.IsNullOrWhiteSpace(sourceFolder.FullPath))
        {
            sourcePath = sourceFolder.FullPath;
            sourceRoot = sourceFolder.SourceRoot;
        }

        var hasDownloadedData = HasAnyFileSystemEntry(game.InstallPath);
        var launchPath = ResolveLaunchPath(game);
        var runReady = !string.IsNullOrWhiteSpace(launchPath) && File.Exists(launchPath);
        var manifest = TryLoadManifest(game);

        long? totalBytes = null;
        int? fileCount = null;
        if (manifest is not null)
        {
            totalBytes = manifest.Files.Sum(file => file.Size);
            fileCount = manifest.Files.Count;
        }

        var requiredAdditionalBytes = EstimateRequiredAdditionalBytes(sourcePath, game.InstallPath);
        var healthStatus = BuildResourceHealthStatus(sourceExists, hasDownloadedData, runReady, requiredAdditionalBytes);

        return new ResourceGameRow
        {
            Id = game.Id,
            ManagedGameId = game.Id,
            Name = game.Name,
            Category = game.Category,
            SourceKey = sourceKey,
            SourceRoot = sourceRoot,
            SourcePath = sourcePath,
            SourceStatus = sourceExists ? I18n.Server.ResourceStatusHasSource : I18n.Server.ResourceStatusMissingSource,
            InstallPath = game.InstallPath,
            LastUpdatedAt = game.LastUpdatedAt,
            IsDownloaded = hasDownloadedData,
            IsManaged = true,
            HasSource = sourceExists,
            HealthStatus = healthStatus,
            DownloadStatus = hasDownloadedData ? I18n.Server.DownloadStatusDownloaded : I18n.Server.DownloadStatusMissing,
            DownloadSpeedDisplay = "-",
            RunStatus = runReady ? I18n.Server.RunStatusReady : I18n.Server.RunStatusMissingExe,
            FileCountDisplay = fileCount?.ToString("N0") ?? "-",
            SizeGbDisplay = totalBytes.HasValue ? (totalBytes.Value / 1024d / 1024d / 1024d).ToString("N2") : "-",
            RequiredAdditionalBytes = requiredAdditionalBytes
        };
    }

    private string ResolveSourcePathForGame(GameRecord game)
    {
        var sourceKey = ResolveSourceKeyForGame(game);
        var sourceRoots = GetConfiguredResourceSourceRoots();
        foreach (var sourceRoot in sourceRoots)
        {
            var candidate = ResolveSourcePathForKey(sourceKey, sourceRoot);
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return sourceRoots.Count > 0
            ? ResolveSourcePathForKey(sourceKey, sourceRoots[0])
            : string.Empty;
    }

    private string ResolveSourceKeyForGame(GameRecord game)
    {
        if (string.IsNullOrWhiteSpace(game.InstallPath))
        {
            return game.Name;
        }

        try
        {
            var normalizedTargetRoot = Path.GetFullPath(_resourceTargetRootPath);
            var normalizedInstallPath = Path.GetFullPath(game.InstallPath);

            var normalizedRoot = normalizedTargetRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var normalizedInstallWithSlash = normalizedInstallPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            string relativePath;
            if (normalizedInstallWithSlash.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = Path.GetRelativePath(normalizedTargetRoot, normalizedInstallPath);
            }
            else
            {
                relativePath = Path.GetFileName(normalizedInstallPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                relativePath = game.Name;
            }

            return relativePath;
        }
        catch
        {
            return game.Name;
        }
    }

    private string ResolveSourcePathForKey(string sourceKey)
    {
        var sourceRoots = GetConfiguredResourceSourceRoots();
        if (sourceRoots.Count == 0)
        {
            return string.Empty;
        }

        return ResolveSourcePathForKey(sourceKey, sourceRoots[0]);
    }

    private string ResolveSourcePathForKey(string sourceKey, string sourceRoot)
    {
        if (string.IsNullOrWhiteSpace(sourceKey) || string.IsNullOrWhiteSpace(sourceRoot))
        {
            return string.Empty;
        }

        if (IsHttpSourceRootConfigured(sourceRoot))
        {
            try
            {
                if (!Uri.TryCreate(sourceRoot.Trim(), UriKind.Absolute, out var sourceRootUri))
                {
                    return string.Empty;
                }

                var rootUri = sourceRootUri.AbsoluteUri.EndsWith('/')
                    ? sourceRootUri
                    : new Uri($"{sourceRootUri.AbsoluteUri}/");
                var encodedSegments = sourceKey
                    .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(Uri.EscapeDataString);
                var relativePath = string.Join("/", encodedSegments);
                var combined = new Uri(rootUri, relativePath);
                return combined.AbsoluteUri;
            }
            catch
            {
                return string.Empty;
            }
        }

        try
        {
            return Path.GetFullPath(Path.Combine(sourceRoot, sourceKey));
        }
        catch
        {
            return string.Empty;
        }
    }

    private IReadOnlyList<string> GetConfiguredResourceTargetRoots()
    {
        if (string.IsNullOrWhiteSpace(_resourceTargetRootPath))
            return Array.Empty<string>();

        return _resourceTargetRootPath.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
    }

    private string ResolveTargetPathForSourceKey(string sourceKey)
    {
        var targetRoots = GetConfiguredResourceTargetRoots();
        if (targetRoots.Count == 0)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            return targetRoots[0];
        }

        // 1. If the game ALREADY exists in one of the configured drives, we must return that path (so it updates in-place)
        foreach (var root in targetRoots)
        {
            try
            {
                var combined = Path.GetFullPath(Path.Combine(root, sourceKey));
                if (Directory.Exists(combined) && Directory.EnumerateFileSystemEntries(combined).Any())
                {
                    return combined;
                }
            }
            catch { }
        }

        // 2. If it does not exist anywhere, we should pick the drive with the MOST available free space.
        string bestRoot = targetRoots[0];
        long maxFreeSpace = -1;

        foreach (var root in targetRoots)
        {
            try
            {
                var driveRoot = Path.GetPathRoot(Path.GetFullPath(root));
                if (driveRoot != null)
                {
                    var driveInfo = new DriveInfo(driveRoot);
                    if (driveInfo.IsReady && driveInfo.AvailableFreeSpace > maxFreeSpace)
                    {
                        maxFreeSpace = driveInfo.AvailableFreeSpace;
                        bestRoot = root;
                    }
                }
            }
            catch { }
        }

        try
        {
            return Path.GetFullPath(Path.Combine(bestRoot, sourceKey));
        }
        catch
        {
            return Path.Combine(bestRoot, sourceKey);
        }
    }

    private bool IsHttpSourceRootConfigured(string sourceRoot)
    {
        return _resourceSyncService.IsHttpSourceRoot(sourceRoot);
    }

    private bool IsHttpSourceRootConfigured()
    {
        return GetConfiguredResourceSourceRoots().Any(IsHttpSourceRootConfigured);
    }

    private async Task<IReadOnlyList<SourceFolderEntry>> GetSourceFolderEntriesAsync()
    {
        var sourceRoots = GetConfiguredResourceSourceRoots();
        if (sourceRoots.Count == 0)
        {
            return Array.Empty<SourceFolderEntry>();
        }

        var result = new List<SourceFolderEntry>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceRoot in sourceRoots)
        {
            if (IsHttpSourceRootConfigured(sourceRoot))
            {
                IReadOnlyList<string> sourceKeys;
                try
                {
                    sourceKeys = await _resourceSyncService.GetHttpTopLevelDirectoryKeysAsync(sourceRoot);
                }
                catch
                {
                    continue;
                }

                foreach (var sourceKey in sourceKeys)
                {
                    if (!seenKeys.Add(sourceKey))
                    {
                        continue;
                    }

                    result.Add(new SourceFolderEntry
                    {
                        Key = sourceKey,
                        SourceRoot = sourceRoot,
                        Name = sourceKey,
                        FullPath = ResolveSourcePathForKey(sourceKey, sourceRoot)
                    });
                }

                continue;
            }

            string sourceRootPath;
            try
            {
                sourceRootPath = Path.GetFullPath(sourceRoot);
            }
            catch
            {
                continue;
            }

            if (!Directory.Exists(sourceRootPath))
            {
                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(sourceRootPath, "*", SearchOption.TopDirectoryOnly))
            {
                var folderName = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(folderName) || !seenKeys.Add(folderName))
                {
                    continue;
                }

                result.Add(new SourceFolderEntry
                {
                    Key = folderName,
                    SourceRoot = sourceRoot,
                    Name = folderName,
                    FullPath = directory
                });
            }
        }

        return result.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private ResourceGameRow CreateSourceOnlyResourceRow(SourceFolderEntry sourceFolder)
    {
        var targetPath = ResolveTargetPathForSourceKey(sourceFolder.Key);
        var hasDownloadedData = HasAnyFileSystemEntry(targetPath);
        var launchPath = FindPreferredExecutablePath(targetPath, sourceFolder.Name);
        var runReady = !string.IsNullOrWhiteSpace(launchPath) && File.Exists(launchPath);
        var requiredAdditionalBytes = EstimateRequiredAdditionalBytes(sourceFolder.FullPath, targetPath);
        var healthStatus = BuildResourceHealthStatus(true, hasDownloadedData, runReady, requiredAdditionalBytes);

        return new ResourceGameRow
        {
            Id = 0,
            ManagedGameId = null,
            Name = sourceFolder.Name,
            Category = I18n.Server.ResourceDefaultCategoryIdc,
            SourceKey = sourceFolder.Key,
            SourceRoot = sourceFolder.SourceRoot,
            SourceStatus = I18n.Server.ResourceStatusHasSource,
            SourcePath = sourceFolder.FullPath,
            DownloadStatus = hasDownloadedData ? I18n.Server.DownloadStatusDownloaded : I18n.Server.DownloadStatusMissing,
            DownloadSpeedDisplay = "-",
            RunStatus = runReady ? I18n.Server.RunStatusReady : I18n.Server.RunStatusNotConfiguredExe,
            FileCountDisplay = "-",
            SizeGbDisplay = "-",
            LastUpdatedAt = null,
            InstallPath = targetPath,
            IsDownloaded = hasDownloadedData,
            IsManaged = false,
            HasSource = true,
            HealthStatus = healthStatus,
            RequiredAdditionalBytes = requiredAdditionalBytes
        };
    }

    private async Task RefreshResourceCompletionStatesAsync(IReadOnlyList<GameRecord> games)
    {
        var gamesById = games
            .GroupBy(game => game.Id)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var row in _allResourceRows)
        {
            var monitorRow = FindActiveMonitorRowForResource(row);
            if (monitorRow is not null && IsResourceSyncRunning(monitorRow))
            {
                continue;
            }

            var isDownloaded = await DetermineResourceDownloadedStateAsync(row, gamesById);
            row.IsDownloaded = isDownloaded;
            row.DownloadStatus = isDownloaded ? I18n.Server.DownloadStatusDownloaded : I18n.Server.DownloadStatusMissing;
            row.DownloadSpeedDisplay = "-";
            row.RunStatus = isDownloaded
                ? GetRunStatusAfterSync(row)
                : (row.IsManaged ? I18n.Server.RunStatusMissingExe : I18n.Server.RunStatusNotConfiguredExe);
        }
    }

    private async Task<bool> DetermineResourceDownloadedStateAsync(
        ResourceGameRow row,
        IReadOnlyDictionary<int, GameRecord> gamesById)
    {
        if (!HasAnyFileSystemEntry(row.InstallPath))
        {
            return false;
        }

        if (row.ManagedGameId.HasValue &&
            gamesById.TryGetValue(row.ManagedGameId.Value, out var managedGame) &&
            TryCheckDownloadedByManifest(managedGame, row.InstallPath, out var isCompleteFromManifest))
        {
            return isCompleteFromManifest;
        }

        if (!row.HasSource)
        {
            return true;
        }

        var candidateSourcePaths = GetCandidateSourceRootsForRow(row)
            .Select(root => ResolveSourcePathForKey(row.SourceKey, root))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidateSourcePaths.Count == 0 && !string.IsNullOrWhiteSpace(row.SourcePath))
        {
            candidateSourcePaths.Add(row.SourcePath);
        }

        foreach (var sourcePath in candidateSourcePaths)
        {
            try
            {
                if (await _resourceSyncService.IsSourceMirroredToTargetAsync(sourcePath, row.InstallPath))
                {
                    return true;
                }
            }
            catch
            {
                // Continue trying mirror source roots.
            }
        }

        return false;
    }

    private static bool TryCheckDownloadedByManifest(GameRecord game, string installPath, out bool isComplete)
    {
        isComplete = false;
        var manifest = TryLoadManifest(game);
        if (manifest is null || manifest.Files.Count == 0)
        {
            return false;
        }

        string normalizedInstallRoot;
        try
        {
            normalizedInstallRoot = Path.GetFullPath(installPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
        }
        catch
        {
            return true;
        }

        foreach (var file in manifest.Files)
        {
            string targetPath;
            try
            {
                targetPath = Path.GetFullPath(Path.Combine(installPath, file.RelativePath));
            }
            catch
            {
                return true;
            }

            if (!targetPath.StartsWith(normalizedInstallRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!File.Exists(targetPath))
            {
                return true;
            }

            long actualSize;
            try
            {
                actualSize = new FileInfo(targetPath).Length;
            }
            catch
            {
                return true;
            }

            if (actualSize != file.Size)
            {
                return true;
            }
        }

        isComplete = true;
        return true;
    }

    private static string FindPreferredExecutablePath(string installPath, string preferredName)
    {
        if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
        {
            return string.Empty;
        }

        var preferredFileName = string.IsNullOrWhiteSpace(preferredName)
            ? string.Empty
            : preferredName.Trim() + ".exe";

        if (!string.IsNullOrWhiteSpace(preferredFileName))
        {
            var exactPath = Path.Combine(installPath, preferredFileName);
            if (File.Exists(exactPath))
            {
                return exactPath;
            }
        }

        var rootExecutables = Directory
            .EnumerateFiles(installPath, "*.exe", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rootPreferred = rootExecutables.FirstOrDefault(path =>
            string.Equals(Path.GetFileName(path), preferredFileName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(rootPreferred))
        {
            return rootPreferred;
        }

        var rootFirst = rootExecutables.FirstOrDefault(path =>
            !Path.GetFileName(path).Contains("unins", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(rootFirst))
        {
            return rootFirst;
        }

        return Directory
            .EnumerateFiles(installPath, "*.exe", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(path => !Path.GetFileName(path).Contains("unins", StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;
    }

    private static string FindPreferredLaunchRelativePath(string installPath, string preferredName)
    {
        var executablePath = FindPreferredExecutablePath(installPath, preferredName);
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return string.Empty;
        }

        return Path.GetRelativePath(installPath, executablePath);
    }

    private static string ResolveLaunchPath(GameRecord game)
    {
        if (string.IsNullOrWhiteSpace(game.LaunchRelativePath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(game.LaunchRelativePath))
        {
            return Path.GetFullPath(game.LaunchRelativePath);
        }

        if (string.IsNullOrWhiteSpace(game.InstallPath))
        {
            return string.Empty;
        }

        return Path.GetFullPath(Path.Combine(game.InstallPath, game.LaunchRelativePath));
    }

    private static bool HasAnyFileSystemEntry(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return false;
        }

        try
        {
            using var enumerator = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
            return enumerator.MoveNext();
        }
        catch
        {
            return false;
        }
    }

    private static string GetManifestPath(GameRecord game)
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "manifests",
            $"{game.Id:0000}-{ToSafeFileName(game.Name)}.manifest.json");
    }

    private static string ToSafeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(invalidChars.Contains(character) ? '_' : character);
        }

        return builder.ToString().Trim().ToLowerInvariant();
    }

    private static GameManifest? TryLoadManifest(GameRecord game)
    {
        try
        {
            var path = GetManifestPath(game);
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<GameManifest>(json, ManifestJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private void ApplyResourceFilter(ResourceFilterKind filterKind)
    {
        _currentResourceFilter = filterKind;

        if (filterKind == ResourceFilterKind.DownloadMonitor)
        {
            _resourcesGrid.Visible = false;
            _downloadMonitorGrid.Visible = true;
            _downloadMonitorGrid.BringToFront();
            UpdateDownloadSummary();
            return;
        }

        RefreshResourceRowsFromFileSystem();

        _downloadMonitorGrid.Visible = false;
        _resourcesGrid.Visible = true;
        _resourcesGrid.BringToFront();

        var filtered = filterKind switch
        {
            ResourceFilterKind.Missing => _allResourceRows.Where(row => row.HasSource && !row.IsDownloaded).ToList(),
            ResourceFilterKind.Downloaded => _allResourceRows.Where(row => row.IsDownloaded).ToList(),
            _ => _allResourceRows.ToList()
        };

        if (!string.IsNullOrWhiteSpace(_resourceSearchQuery))
        {
            var lowerQuery = _resourceSearchQuery.ToLowerInvariant();
            filtered = filtered.Where(row => 
                (row.Name?.ToLowerInvariant().Contains(lowerQuery) == true) || 
                (row.Category?.ToLowerInvariant().Contains(lowerQuery) == true)).ToList();
        }

        _resourcesBinding.DataSource = filtered;
        UpdateResourceSummary(filtered);
    }

    private void FilterResourceDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _filterResourceDebounceTimer.Stop();
        ApplyResourceFilter(_currentResourceFilter);
    }

    private void RefreshResourceRowsFromFileSystem()
    {
        foreach (var row in _allResourceRows)
        {
            var hasDownloadedData = HasAnyFileSystemEntry(row.InstallPath);
            if (!hasDownloadedData)
            {
                row.IsDownloaded = false;
            }

            var activeMonitor = FindActiveMonitorRowForResource(row);
            var isSyncRunning = activeMonitor is not null && IsResourceSyncRunning(activeMonitor);
            if (isSyncRunning)
            {
                continue;
            }

            row.DownloadStatus = row.IsDownloaded ? I18n.Server.DownloadStatusDownloaded : I18n.Server.DownloadStatusMissing;
            row.DownloadSpeedDisplay = "-";
            row.RunStatus = row.IsDownloaded
                ? GetRunStatusAfterSync(row)
                : (row.IsManaged ? I18n.Server.RunStatusMissingExe : I18n.Server.RunStatusNotConfiguredExe);
        }
    }

    private void UpdateResourceSummary(IReadOnlyList<ResourceGameRow> filteredRows)
    {
        var total = _allResourceRows.Count;
        var downloaded = _allResourceRows.Count(row => row.IsDownloaded);
        var missing = total - downloaded;
        var totalRequiredGb = _allResourceRows
            .Where(row => row.RequiredAdditionalBytes.HasValue)
            .Sum(row => row.RequiredAdditionalBytes!.Value) / 1024d / 1024d / 1024d;
            
        _resourceSummaryLabel.Text = I18n.Server.ResourceSummaryText(filteredRows.Count, total, downloaded, missing, totalRequiredGb, BuildResourceHealthSummary());

        // Update Stat Cards
        _statDisplayCountLabel.Text = $"Hiển thị {filteredRows.Count}/{total} trò chơi";
        _statDownloadedLabel.Text = $"Đã tải {downloaded} trò chơi";
        _statMissingLabel.Text = $"Chưa tải {missing} trò chơi";
        _statSizeLabel.Text = $"Cần thêm {totalRequiredGb:0.0} GB";
        
        var healthSummary = BuildResourceHealthSummary();
        _statSourceOkLabel.Text = string.IsNullOrWhiteSpace(healthSummary) ? "Đang kiểm tra" : healthSummary.Split(',')[0];
        
        // Disk size logic
        try
        {
            var targetRoots = GetConfiguredResourceTargetRoots();
            double totalGb = 0;
            double freeGb = 0;
            int readyDrives = 0;
            foreach (var rootStr in targetRoots.Where(r => Directory.Exists(r)))
            {
                try
                {
                    var driveInfo = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(rootStr)) ?? "C:\\");
                    if (driveInfo.IsReady)
                    {
                        totalGb += driveInfo.TotalSize / 1024d / 1024d / 1024d;
                        freeGb += driveInfo.AvailableFreeSpace / 1024d / 1024d / 1024d;
                        readyDrives++;
                    }
                }
                catch {}
            }
            
            if (readyDrives > 0)
            {
                double usedGb = totalGb - freeGb;
                double percent = totalGb > 0 ? (usedGb / totalGb) * 100d : 0;
                
                _statDiskProgressLabel.Text = $"{freeGb:0.0}/{totalGb:0.0} GB ({100 - percent:0}%)";
                _statDiskProgressBar.Value = Math.Max(0, Math.Min(100, (int)percent));
                _statTargetOkLabel.Text = "Ổ game sẵn sàng";
            }
            else
            {
                throw new Exception("No ready drives");
            }
        }
        catch
        {
            _statDiskProgressLabel.Text = "Không xác định";
            _statDiskProgressBar.Value = 0;
            _statTargetOkLabel.Text = "Lỗi ổ đĩa";
        }

        // Update tree badge counts
        if (_resourceDownloadedCount != downloaded || _resourceMissingCount != missing)
        {
            _resourceDownloadedCount = downloaded;
            _resourceMissingCount = missing;
            _resourceTree.Invalidate();
        }
    }

    private void UpdateDownloadSummary()
    {
        var total = _downloadMonitorRows.Count;
        var running = _downloadMonitorRows.Count(row => string.Equals(row.Status, I18n.Server.UpdateRunningStatus, StringComparison.OrdinalIgnoreCase));
        var paused = _downloadMonitorRows.Count(row => string.Equals(row.Status, I18n.Server.UpdatePausedStatus, StringComparison.OrdinalIgnoreCase));
        var stopping = _downloadMonitorRows.Count(row => string.Equals(row.Status, I18n.Server.UpdateStoppingStatus, StringComparison.OrdinalIgnoreCase));
        var failed = _downloadMonitorRows.Count(row => string.Equals(row.Status, I18n.Server.UpdateFailedStatus, StringComparison.OrdinalIgnoreCase));
        var totalSpeed = _downloadMonitorRows.Where(row => string.Equals(row.Status, I18n.Server.UpdateRunningStatus, StringComparison.OrdinalIgnoreCase)).Sum(row => row.SpeedMbps.GetValueOrDefault());
        var totalRemainingMb = _downloadMonitorRows
            .Where(row => row.TotalBytes.HasValue)
            .Sum(row => Math.Max(0L, row.TotalBytes!.Value - row.ProcessedBytes)) / 1024d / 1024d;
        _resourceSummaryLabel.Text = I18n.Server.ResourceDownloadMonitorSummary(total, running, paused, stopping, failed, totalSpeed, totalRemainingMb);
    }
}









