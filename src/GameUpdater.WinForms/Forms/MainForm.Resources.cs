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
{
    private void BuildResourceTree()
    {
        _resourceTree.AfterSelect -= ResourceTree_AfterSelect;
        _resourceTree.Nodes.Clear();

        var resourceRoot = new TreeNode("Tải tài nguyên")
        {
            Tag = ResourceFilterKind.All
        };
        resourceRoot.Nodes.Add(new TreeNode("Trò chơi chưa tải")
        {
            Tag = ResourceFilterKind.Missing
        });
        resourceRoot.Nodes.Add(new TreeNode("Trò chơi đã tải")
        {
            Tag = ResourceFilterKind.Downloaded
        });

        var monitorRoot = new TreeNode("Trung tâm giám sát")
        {
            Tag = ResourceFilterKind.DownloadMonitor
        };
        monitorRoot.Nodes.Add(new TreeNode("Tải xuống máy chủ")
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
            Description = "Chọn thư mục nguồn tài nguyên (IDC).",
            UseDescriptionForTitle = true,
            SelectedPath = _resourceSourceRootTextBox.Text
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _resourceSourceRootTextBox.Text = dialog.SelectedPath;
        }
    }

    private void BrowseResourceTargetButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Chọn thư mục đích trên máy chủ.",
            UseDescriptionForTitle = true,
            SelectedPath = _resourceTargetRootTextBox.Text
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _resourceTargetRootTextBox.Text = dialog.SelectedPath;
        }
    }

    private async void SaveResourceSettingsButton_Click(object? sender, EventArgs e)
    {
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            UpdateResourceRootsFromInputs();
            await SaveUiSettingsAsync();
            await ReloadAllAsync(SelectedGame?.Id);
            ShowInfo("Đã lưu cấu hình nguồn/đích tài nguyên.");
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
            var missingRunFile = _allResourceRows.Count(row => row.IsDownloaded && !string.Equals(row.HealthStatus, "OK", StringComparison.OrdinalIgnoreCase));
            UpdateResourceSummary(_allResourceRows);
            ShowInfo($"Kiểm tra tài nguyên xong. Thiếu nguồn: {missingSource}. Cần đồng bộ: {needSync}. Cần kiểm tra file chạy: {missingRunFile}.\n{BuildResourceHealthSummary()}");
        });
    }

    private async void SyncSelectedResourceButton_Click(object? sender, EventArgs e)
{
    if (_resourcesGrid.Visible == false)
    {
        ShowInfo("Vui lòng chuyển sang danh sách tài nguyên để chọn trò chơi.");
        return;
    }

    var selectedRows = GetSelectedOrCurrentResourceRows()
        .Where(row => row.HasSource)
        .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (selectedRows.Count == 0)
    {
        ShowInfo("Vui lòng chọn trò chơi có nguồn IDC để tải.");
        return;
    }

    await RunResourceSyncForRowsAsync(selectedRows, ResourceSyncMode.Incremental);
}

    private async Task RunResourceSyncForRowsAsync(
        IReadOnlyList<ResourceGameRow> rows,
        ResourceSyncMode syncMode)
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
                    AppendUpdateMessage($"Bỏ qua {row.Name}: đang có tác vụ tải chạy.");
                    continue;
                }

                try
                {
                    var gameId = await SyncResourceRowAsync(row, syncMode);
                    if (gameId.HasValue)
                    {
                        selectedGameId = gameId.Value;
                    }
                }
                catch (OperationCanceledException)
                {
                    AppendUpdateMessage($"Đã dừng tác vụ tải tài nguyên của {row.Name} theo yêu cầu.");
                    break;
                }
            }

            await AutoExportCatalogAsync();
            await ReloadAllAsync(selectedGameId);
        }, () => ToggleResourceSyncControls(true));
    }

    private async Task SyncGameFromResourceLegacyAsync(GameRecord game)
    {
        var monitorRow = StartDownloadMonitor(game.Name, game.Id > 0 ? game.Id : null, resourceKey: ResolveSourceKeyForGame(game));
        var syncControl = new ResourceSyncTaskControl();
        var syncMode = ResourceSyncMode.Incremental;
        var actionName = "Đồng bộ tài nguyên";

        try
        {
            var progress = new Progress<UpdateProgressInfo>(info =>
            {
                if (syncControl.IsPaused)
                {
                    return;
                }
                if (syncControl.IsPaused)
                {
                    return;
                }

                UpdateDownloadMonitor(monitorRow, info.Percent, "Đang tải", info.Message, info);
            });

            var result = await _resourceSyncService.SyncGameAsync(
                game,
                _resourceSourceRootPath,
                _resourceTargetRootPath,
                progress);

            var successMessage = syncMode == ResourceSyncMode.MissingOnly
                ? $"Đồng bộ file thiếu {game.Name}: sao chép {result.CopiedFiles}/{result.TotalFiles} tệp."
                : $"Đã tải {game.Name}: sao chép {result.CopiedFiles}/{result.TotalFiles} tệp từ {result.SourcePath} về {result.TargetPath}.";

            UpdateDownloadMonitor(monitorRow, 100, "Hoàn tất", successMessage);
            AppendUpdateMessage(successMessage);

            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = game.Id > 0 ? game.Id : null,
                GameName = game.Name,
                Action = actionName,
                Status = "Thành công",
                Message = successMessage,
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception exception)
        {
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, "Thất bại", exception.Message);

            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = game.Id > 0 ? game.Id : null,
                GameName = game.Name,
                Action = "Đồng bộ tài nguyên",
                Status = "Thất bại",
                Message = exception.Message,
                CreatedAt = DateTime.UtcNow
            });

            throw;
        }
    }

    private async Task SyncGameFromResourceAsync(
        GameRecord game,
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
        var actionName = syncMode == ResourceSyncMode.MissingOnly ? "Đồng bộ file thiếu IDC" : "Đồng bộ tài nguyên";

        try
        {
            var progress = new Progress<UpdateProgressInfo>(info =>
            {
                if (syncControl.IsPaused)
                {
                    return;
                }

                UpdateDownloadMonitor(monitorRow, info.Percent, "Đang tải", info.Message, info);
            });

            var result = await SyncGameWithMirrorFallbackAsync(
                game,
                sourceRootCandidates,
                progress,
                syncMode,
                syncControl);

            var successMessage = syncMode == ResourceSyncMode.MissingOnly
                ? $"Đồng bộ file thiếu {game.Name}: sao chép {result.CopiedFiles}/{result.TotalFiles} tệp."
                : $"Đã tải {game.Name}: sao chép {result.CopiedFiles}/{result.TotalFiles} tệp từ {result.SourcePath} về {result.TargetPath}.";

            UpdateDownloadMonitor(monitorRow, 100, "Hoàn tất", successMessage);
            AppendUpdateMessage(successMessage);

            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = game.Id > 0 ? game.Id : null,
                GameName = game.Name,
                Action = actionName,
                Status = "Thành công",
                Message = successMessage,
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (OperationCanceledException)
        {
            var canceledMessage = $"Đã dừng tải {game.Name} theo yêu cầu.";
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, "Đã dừng", canceledMessage);
            AppendUpdateMessage(canceledMessage);

            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = game.Id > 0 ? game.Id : null,
                GameName = game.Name,
                Action = actionName,
                Status = "Đã dừng",
                Message = canceledMessage,
                CreatedAt = DateTime.UtcNow
            });

            throw;
        }
        catch (Exception exception)
        {
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, "Thất bại", exception.Message);

            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = game.Id > 0 ? game.Id : null,
                GameName = game.Name,
                Action = actionName,
                Status = "Thất bại",
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
        IProgress<UpdateProgressInfo> progress,
        ResourceSyncMode syncMode,
        ResourceSyncTaskControl syncControl)
    {
        if (sourceRoots.Count == 0)
        {
            throw new InvalidOperationException("Chưa cấu hình nguồn IDC hợp lệ.");
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
                $"Đang thử nguồn IDC {index + 1}/{sourceRoots.Count}: {sourceRoot}"));

            try
            {
                return await Task.Run(
                    () => _resourceSyncService.SyncGameAsync(
                        game,
                        sourceRoot,
                        _resourceTargetRootPath,
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
            catch (Exception exception)
            {
                lastException = exception;
                AppendUpdateMessage($"Nguồn IDC lỗi ({sourceRoot}): {exception.Message}");
            }
        }

        throw lastException ?? new InvalidOperationException("Không thể đồng bộ từ các nguồn IDC đã cấu hình.");
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
            Category = string.IsNullOrWhiteSpace(row.Category) ? "IDC" : row.Category,
            InstallPath = row.InstallPath,
            Version = "1.0.0",
            LaunchRelativePath = string.Empty,
            LaunchArguments = string.Empty,
            Notes = "Tạo tự động từ nguồn IDC"
        };
    }

    private async Task<int?> SyncResourceRowAsync(ResourceGameRow row, ResourceSyncMode syncMode = ResourceSyncMode.Incremental)
    {
        if (!await ConfirmDiskSpaceForResourceSyncAsync(row))
        {
            AppendUpdateMessage($"Bỏ qua tải {row.Name}: không đủ dung lượng trống.");
            return null;
        }

        var existingGame = row.ManagedGameId.HasValue
            ? FindGameById(row.ManagedGameId.Value)
            : FindGameByInstallPath(row.InstallPath);

        var game = existingGame ?? BuildTransientGameRecordFromResourceRow(row);
        var sourceRoots = GetCandidateSourceRootsForRow(row);
        await SyncGameFromResourceAsync(game, syncMode, sourceRoots, resourceKey: row.SourceKey);
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
            $"Dung lượng trống có thể không đủ để tải {row.Name}.{Environment.NewLine}" +
            $"Cần thêm khoảng: {requiredGb:N2} GB{Environment.NewLine}" +
            $"Đang trống: {availableGb:N2} GB (khuyến nghị dự phòng {reserveGb:N0} GB).{Environment.NewLine}{Environment.NewLine}" +
            "Bạn có muốn tiếp tục không?",
            "Cảnh báo dung lượng",
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
            return "Thiếu nguồn IDC";
        }

        if (!hasDownloadedData)
        {
            return "Chưa tải";
        }

        if (!runReady)
        {
            return "Thiếu file chạy";
        }

        if (requiredAdditionalBytes.GetValueOrDefault() > 0)
        {
            return "Cần đồng bộ";
        }

        return "OK";
    }

    private string BuildResourceHealthSummary()
    {
        var messages = new List<string>();
        var sourceRoots = GetConfiguredResourceSourceRoots();
        var sourceOk = sourceRoots.Any(root => IsHttpSourceRootConfigured(root) || Directory.Exists(root));
        if (sourceRoots.Count > 1)
        {
            messages.Add(sourceOk
                ? $"Nguồn IDC mirror: {sourceRoots.Count} nguồn"
                : $"Nguồn IDC mirror: {sourceRoots.Count} nguồn không truy cập");
        }
        else
        {
            messages.Add(sourceOk ? "Nguồn IDC: OK" : "Nguồn IDC: Không truy cập");
        }

        var targetOk = Directory.Exists(_resourceTargetRootPath);
        var targetWritable = targetOk && CanWriteToFolder(_resourceTargetRootPath);
        messages.Add(targetWritable ? "Đích game: OK" : "Đích game: Không ghi được");

        if (targetOk)
        {
            var root = Path.GetPathRoot(Path.GetFullPath(_resourceTargetRootPath));
            if (!string.IsNullOrWhiteSpace(root))
            {
                var drive = new DriveInfo(root);
                var freeGb = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
                var totalGb = drive.TotalSize / 1024d / 1024d / 1024d;
                var usedPercent = totalGb <= 0 ? 0 : (totalGb - freeGb) * 100d / totalGb;
                var warning = freeGb < 100 || usedPercent >= 90 ? " ⚠" : string.Empty;
                messages.Add($"Ổ game trống: {freeGb:N1}/{totalGb:N1} GB ({usedPercent:N0}% dùng){warning}");
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
            game.Version = "1.0.0";
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
        _resourceTargetRootPath = _resourceTargetRootTextBox.Text.Trim();
        _resourceBandwidthLimitMbps = Decimal.ToInt32(_resourceBandwidthLimitNumeric.Value);

        if (GetConfiguredResourceSourceRoots().Count == 0)
        {
            throw new InvalidOperationException("Vui lòng nhập ít nhất một nguồn IDC (hỗ trợ ngăn cách bằng dấu ; hoặc xuống dòng để mirror/fallback).");
        }

        if (string.IsNullOrWhiteSpace(_resourceTargetRootPath))
        {
            throw new InvalidOperationException("Vui lòng nhập thư mục đích máy chủ.");
        }
    }

    private void ConfigureResourcesGrid()
    {
        _resourcesGrid.AutoGenerateColumns = false;
        _resourcesGrid.AllowUserToAddRows = false;
        _resourcesGrid.AllowUserToDeleteRows = false;
        _resourcesGrid.MultiSelect = true;
        _resourcesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _resourcesGrid.ReadOnly = true;
        _resourcesGrid.RowHeadersVisible = false;
        _resourcesGrid.DataSource = _resourcesBinding;

        _resourcesGrid.Columns.Add(CreateTextColumn("ID", nameof(ResourceGameRow.Id), 70));
        _resourcesGrid.Columns.Add(CreateTextColumn("Tên trò chơi", nameof(ResourceGameRow.Name), 180));
        _resourcesGrid.Columns.Add(CreateTextColumn("Nhóm", nameof(ResourceGameRow.Category), 120));
        _resourcesGrid.Columns.Add(CreateTextColumn("Nguồn IDC", nameof(ResourceGameRow.SourceStatus), 110));
        _resourcesGrid.Columns.Add(CreateTextColumn("Tình trạng", nameof(ResourceGameRow.HealthStatus), 140));
        _resourcesGrid.Columns.Add(CreateTextColumn("Trạng thái tải", nameof(ResourceGameRow.DownloadStatus), 160));
        _resourcesGrid.Columns.Add(CreateTextColumn("Tốc độ", nameof(ResourceGameRow.DownloadSpeedDisplay), 100));
        _resourcesGrid.Columns.Add(CreateTextColumn("Trạng thái chạy", nameof(ResourceGameRow.RunStatus), 130));
        _resourcesGrid.Columns.Add(CreateTextColumn("Số tệp", nameof(ResourceGameRow.FileCountDisplay), 90));
        _resourcesGrid.Columns.Add(CreateTextColumn("Kích thước (GB)", nameof(ResourceGameRow.SizeGbDisplay), 110));
        _resourcesGrid.Columns.Add(CreateTextColumn("Cần thêm GB", nameof(ResourceGameRow.RequiredAdditionalGbDisplay), 110));
        _resourcesGrid.Columns.Add(CreateTextColumn("Cập nhật gần nhất", nameof(ResourceGameRow.LastUpdatedAt), 150, "yyyy-MM-dd HH:mm:ss"));
        _resourcesGrid.Columns.Add(CreateTextColumn("Đường dẫn nguồn", nameof(ResourceGameRow.SourcePath), 260));
        _resourcesGrid.Columns.Add(CreateTextColumn("Đường dẫn cài đặt", nameof(ResourceGameRow.InstallPath), 400, fill: true));
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
            ShowInfo("Không có trò chơi có nguồn IDC để tải.");
            return;
        }

        await RunResourceSyncForRowsAsync(selectedRows, ResourceSyncMode.Incremental);
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
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, "Tạm dừng", "Đã tạm dừng theo yêu cầu từ danh sách tài nguyên.");
            paused++;
        }

        if (paused == 0)
        {
            ShowInfo("Không có tác vụ đang chạy để tạm dừng.");
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
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, "Đang tải", "Đã tiếp tục theo yêu cầu từ danh sách tài nguyên.");
            resumed++;
        }

        if (resumed == 0)
        {
            ShowInfo("Không có tác vụ tạm dừng để tiếp tục.");
        }
    }

    private void StopSelectedResourcesMenuItem_Click(object? sender, EventArgs e)
    {
        var selectedTasks = GetSelectedActiveResourceTasks();
        var stopped = 0;
        foreach (var (monitorRow, control) in selectedTasks)
        {
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, "Đang dừng", "Đang gửi yêu cầu dừng từ danh sách tài nguyên...");
            control.Cancel();
            stopped++;
        }

        if (stopped == 0)
        {
            ShowInfo("Không có tác vụ đang chạy để dừng.");
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
            ShowInfo("Không có mục phù hợp để tải lại.");
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
            ShowInfo("Không có tác vụ đang chạy để đặt băng thông.");
            return;
        }

        foreach (var (monitorRow, control) in selectedTasks)
        {
            control.SetBandwidthLimitMbps(mbps);
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, monitorRow.Status, $"Đã đặt giới hạn băng thông: {mbps} MB/s.");
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
            ShowInfo("Không có trò chơi phù hợp để đồng bộ file thiếu.");
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
        return string.Equals(status, "Thất bại", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "Đã dừng", StringComparison.OrdinalIgnoreCase);
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

        _downloadMonitorGrid.Columns.Add(CreateTextColumn("STT", nameof(DownloadMonitorRow.SerialNumber), 50));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Game ID", nameof(DownloadMonitorRow.GameIdDisplay), 80));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Tên Game", nameof(DownloadMonitorRow.GameName), 190));
        var progressColumn = new DataGridViewTextBoxColumn
        {
            Name = DownloadProgressColumnName,
            HeaderText = "Tiến trình",
            DataPropertyName = nameof(DownloadMonitorRow.ProgressPercent),
            Width = 100
        };
        _downloadMonitorGrid.Columns.Add(progressColumn);
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Trạng thái", nameof(DownloadMonitorRow.Status), 110));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Dung lượng (GB)", nameof(DownloadMonitorRow.TotalSizeGbDisplay), 115));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Còn lại (MB)", nameof(DownloadMonitorRow.RemainingMbDisplay), 115));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Thời gian còn lại", nameof(DownloadMonitorRow.RemainingTimeDisplay), 125));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Tốc độ (MB/S)", nameof(DownloadMonitorRow.SpeedMbpsDisplay), 100));
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
            SourceStatus = sourceExists ? "Có nguồn" : "Thiếu nguồn",
            InstallPath = game.InstallPath,
            LastUpdatedAt = game.LastUpdatedAt,
            IsDownloaded = hasDownloadedData,
            IsManaged = true,
            HasSource = sourceExists,
            HealthStatus = healthStatus,
            DownloadStatus = hasDownloadedData ? "Đã tải" : "Chưa tải",
            DownloadSpeedDisplay = "-",
            RunStatus = runReady ? "Sẵn sàng chạy" : "Thiếu tệp chạy",
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

    private string ResolveTargetPathForSourceKey(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            return _resourceTargetRootPath;
        }

        try
        {
            return Path.GetFullPath(Path.Combine(_resourceTargetRootPath, sourceKey));
        }
        catch
        {
            return Path.Combine(_resourceTargetRootPath, sourceKey);
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
            Category = "IDC",
            SourceKey = sourceFolder.Key,
            SourceRoot = sourceFolder.SourceRoot,
            SourceStatus = "Có nguồn",
            SourcePath = sourceFolder.FullPath,
            DownloadStatus = hasDownloadedData ? "Đã tải" : "Chưa tải",
            DownloadSpeedDisplay = "-",
            RunStatus = runReady ? "Sẵn sàng chạy" : "Chưa cấu hình tệp chạy",
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
            row.DownloadStatus = isDownloaded ? "Đã tải" : "Chưa tải";
            row.DownloadSpeedDisplay = "-";
            row.RunStatus = isDownloaded
                ? GetRunStatusAfterSync(row)
                : (row.IsManaged ? "Thiếu tệp chạy" : "Chưa cấu hình tệp chạy");
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

        _resourcesBinding.DataSource = filtered;
        UpdateResourceSummary(filtered);
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

            row.DownloadStatus = row.IsDownloaded ? "Đã tải" : "Chưa tải";
            row.DownloadSpeedDisplay = "-";
            row.RunStatus = row.IsDownloaded
                ? GetRunStatusAfterSync(row)
                : (row.IsManaged ? "Thiếu tệp chạy" : "Chưa cấu hình tệp chạy");
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
        _resourceSummaryLabel.Text = $"Hiển thị {filteredRows.Count}/{total} trò chơi. Đã tải: {downloaded}. Chưa tải: {missing}. Cần thêm: {totalRequiredGb:N1} GB. {BuildResourceHealthSummary()}";
    }

    private void UpdateDownloadSummary()
    {
        var total = _downloadMonitorRows.Count;
        var running = _downloadMonitorRows.Count(row => string.Equals(row.Status, "Đang tải", StringComparison.OrdinalIgnoreCase));
        var paused = _downloadMonitorRows.Count(row => string.Equals(row.Status, "Tạm dừng", StringComparison.OrdinalIgnoreCase));
        var stopping = _downloadMonitorRows.Count(row => string.Equals(row.Status, "Đang dừng", StringComparison.OrdinalIgnoreCase));
        var failed = _downloadMonitorRows.Count(row => string.Equals(row.Status, "Thất bại", StringComparison.OrdinalIgnoreCase));
        var totalSpeed = _downloadMonitorRows.Where(row => string.Equals(row.Status, "Đang tải", StringComparison.OrdinalIgnoreCase)).Sum(row => row.SpeedMbps.GetValueOrDefault());
        var totalRemainingMb = _downloadMonitorRows
            .Where(row => row.TotalBytes.HasValue)
            .Sum(row => Math.Max(0L, row.TotalBytes!.Value - row.ProcessedBytes)) / 1024d / 1024d;
        _resourceSummaryLabel.Text = $"Giám sát tải xuống máy chủ: tổng {total} tác vụ, đang tải {running}, tạm dừng {paused}, đang dừng {stopping}, thất bại {failed}. Tổng tốc độ: {totalSpeed:N2} MB/s. Còn lại: {totalRemainingMb:N0} MB.";
    }
}





