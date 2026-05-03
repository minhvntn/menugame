namespace GameUpdater.Shared.Localization;

public static class I18n
{
    public static class Common
    {
        public const string ErrorTitle = "Lỗi";
        public const string InfoTitle = "Thông báo";
        public const string ConfirmTitle = "Xác nhận";
        public const string ValidationTitle = "Kiểm tra dữ liệu";
        public const string SelectButton = "Chọn";
        public const string SaveButton = "Lưu";
        public const string CancelButton = "Hủy";
        public const string DeleteButton = "Xóa";
        public const string RefreshButton = "Làm mới";
        public const string CsvButton = "Xuất CSV";
    }

    public static class Server
    {
        public const string MainWindowTitle = "Quản lý cập nhật trò chơi";
        public const string MenuHotBadge = "HOT";
        public const string DefaultClientCafeName = "Cyber Game";
        public const string DefaultThemeAccent = "#38BDF8";
        public const string DefaultThemeFontFamily = "Segoe UI";

        public const string GameEditorHotCheckbox = "Hiển thị trong Hot game (client)";
        public const string GameEditorAddTitle = "Thêm trò chơi";
        public const string GameEditorEditTitle = "Sửa trò chơi";
        public const string GameEditorName = "Tên trò chơi";
        public const string GameEditorCategory = "Nhóm";
        public const string GameEditorInstallPath = "Đường dẫn cài đặt";
        public const string GameEditorVersion = "Phiên bản";
        public const string GameEditorExe = "Tệp chạy (EXE)";
        public const string GameEditorLaunchArgs = "Tham số";
        public const string GameEditorClientVisible = "Hiển thị client";
        public const string GameEditorNotes = "Ghi chú";
        public const string GameEditorDefaultCategory = "Online";
        public const string GameEditorDefaultVersion = "1.0.0";
        public const string GameEditorFolderPickerDescription = "Chọn thư mục trò chơi dùng chung trên server.";
        public const string GameEditorExeDialogTitle = "Chọn tệp chạy trò chơi";
        public const string GameEditorExeDialogFilter = "Tệp EXE (*.exe)|*.exe|Tất cả tệp (*.*)|*.*";
        public const string ValidationNameRequired = "Vui lòng nhập tên trò chơi.";
        public const string ValidationInstallPathRequired = "Vui lòng nhập đường dẫn cài đặt.";
        public const string ValidationExeRequired = "Vui lòng nhập tệp chạy trò chơi (EXE).";
        public const string DefaultCategoryFallback = "Chung";

        public const string GamesTab = "Trò chơi";
        public const string ClientTab = "Client";
        public const string ServerTab = "Server";
        public const string ResourcesTab = "Tài nguyên";
        public const string LogsTab = "Lịch sử";
        public const string SettingsTab = "Thiết lập";
        public const string GamesViewTable = "Dạng bảng";
        public const string GamesViewGrid = "Dạng lưới";
        public const string ExportClientCatalogButton = "Xuất Danh Mục Client";
        public const string RefreshResourcesButton = "Làm mới tài nguyên";
        public const string OpenClientStatusFolderButton = "Mở thư mục trạng thái";

        public const string ClientDashboardNoData = "Chưa có dữ liệu máy trạm.";
        public const string ClientDashboardGameStatsPlaceholder = "Game hot: - • Chơi nhiều nhất: - • Vừa cập nhật: -";
        public const string ServerDashboardLoading = "Đang tải thông tin máy server...";
        public const string ServerMetricCpu = "CPU";
        public const string ServerMetricRam = "RAM";
        public const string ServerMetricSystemDrive = "Ổ hệ thống";
        public const string ServerCardNetwork = "Mạng";
        public const string ServerCardStorage = "Kho dữ liệu & catalog";
        public const string ServerCardServices = "Dịch vụ đang chạy";
        public const string ServerCardRecommendation = "Khuyến nghị";

        public const string ResourceTreeRoot = "Tải tài nguyên";
        public const string ResourceTreeMissing = "Trò chơi chưa tải";
        public const string ResourceTreeDownloaded = "Trò chơi đã tải";
        public const string ResourceTreeMonitorRoot = "Trung tâm giám sát";
        public const string ResourceTreeMonitor = "Tải xuống máy chủ";
        public const string ResourceSourceLabel = "Nguồn IDC";
        public const string ResourceTargetLabel = "Đích máy chủ";
        public const string ResourceBandwidthLabel = "Giới hạn MB/s";
        public const string ResourceBandwidthHint = "0 = không giới hạn";
        public const string ResourceSaveConfig = "Lưu cấu hình";
        public const string ResourceHealthCheck = "Kiểm tra tài nguyên";
        public const string ResourceSyncSelected = "Tải trò chơi đã chọn";
        public const string ResourceLoading = "Đang tải dữ liệu tài nguyên...";
        public const string ResourceListTab = "Danh sách trò chơi";
        public const string ResourceConfigTab = "Cấu hình nguồn";
        public const string ResourceConfigHint = "Cấu hình nguồn/đích và giới hạn băng thông tải tài nguyên.";
        public const string ResourceSourcePickerDescription = "Chọn thư mục nguồn tài nguyên (IDC).";
        public const string ResourceTargetPickerDescription = "Chọn thư mục đích trên máy chủ.";
        public const string ResourceContextDownloadSelected = "Tải mục đã chọn";
        public const string ResourceContextPauseSelected = "Tạm dừng tải";
        public const string ResourceContextResumeSelected = "Tiếp tục tải";
        public const string ResourceContextStopSelected = "Dừng tải";
        public const string ResourceContextSetBandwidth = "Giới hạn băng thông";
        public const string ResourceContextRetryFromIdc = "Tải lại từ IDC";
        public const string ResourceContextSyncMissingFromIdc = "Đồng bộ file thiếu từ IDC";
        public const string ResourceHealthOk = "OK";
        public const string ResourceHealthMissingSource = "Thiếu nguồn IDC";
        public const string ResourceHealthNotDownloaded = "Chưa tải";
        public const string ResourceHealthMissingRunFile = "Thiếu file chạy";
        public const string ResourceHealthNeedSync = "Cần đồng bộ";
        public const string ResourceSourceStatusOk = "Nguồn IDC: OK";
        public const string ResourceSourceStatusUnavailable = "Nguồn IDC: Không truy cập";
        public const string ResourceTargetStatusOk = "Đích game: OK";
        public const string ResourceTargetStatusNotWritable = "Đích game: Không ghi được";
        public const string ResourceStatusHasSource = "Có nguồn";
        public const string ResourceStatusMissingSource = "Thiếu nguồn";
        public const string ResourceGridHeaderStatus = "Tình trạng";
        public const string ResourceGridHeaderGameName = "Tên trò chơi";
        public const string ResourceGridHeaderCategory = "Nhóm";
        public const string ResourceGridHeaderSource = "Nguồn IDC";
        public const string ResourceGridHeaderDownloadStatus = "Trạng thái tải";
        public const string ResourceGridHeaderSpeed = "Tốc độ";
        public const string ResourceGridHeaderRunStatus = "Trạng thái chạy";
        public const string ResourceGridHeaderFileCount = "Số tệp";
        public const string ResourceGridHeaderSizeGb = "Kích thước (GB)";
        public const string ResourceGridHeaderRequiredGb = "Cần thêm GB";
        public const string ResourceGridHeaderLastUpdated = "Cập nhật gần nhất";
        public const string ResourceGridHeaderSourcePath = "Đường dẫn nguồn";
        public const string ResourceGridHeaderInstallPath = "Đường dẫn cài đặt";
        public const string MonitorGridHeaderIndex = "STT";
        public const string MonitorGridHeaderGameId = "Game ID";
        public const string MonitorGridHeaderGameName = "Tên Game";
        public const string MonitorGridHeaderProgress = "Tiến trình";
        public const string MonitorGridHeaderStatus = "Trạng thái";
        public const string MonitorGridHeaderTotalGb = "Dung lượng (GB)";
        public const string MonitorGridHeaderRemainingMb = "Còn lại (MB)";
        public const string MonitorGridHeaderRemainingTime = "Thời gian còn lại";
        public const string MonitorGridHeaderSpeed = "Tốc độ (MB/S)";
        public const string ResourceSyncAction = "Đồng bộ tài nguyên";
        public const string ResourceSyncMissingAction = "Đồng bộ file thiếu IDC";
        public const string ResourceSourceConfigInvalid = "Chưa cấu hình nguồn IDC hợp lệ.";
        public const string ResourceSyncAllSourcesFailed = "Không thể đồng bộ từ các nguồn IDC đã cấu hình.";
        public const string ResourceAutoCreatedFromSourceNote = "Tạo tự động từ nguồn IDC";
        public const string ResourceDefaultCategoryIdc = "IDC";
        public const string ResourceRunningTaskInListStopMessage = "Đang gửi yêu cầu dừng từ danh sách tài nguyên...";

        public const string UpdateTab = "Cập nhật";
        public const string UpdateSourceFolder = "Thư mục";
        public const string UpdateSourceZip = "Tệp ZIP";
        public const string BackupBeforeUpdate = "Sao lưu trước khi cập nhật";
        public const string StartUpdateButton = "Bắt đầu cập nhật";

        public const string FieldGame = "Trò chơi";
        public const string FieldSourceType = "Loại nguồn";
        public const string FieldUpdateSource = "Nguồn cập nhật";
        public const string FieldVersion = "Phiên bản";
        public const string FieldOptions = "Tùy chọn";
        public const string FieldProgress = "Tiến trình";
        public const string FieldActions = "Thao tác";
        public const string FieldLogs = "Nhật ký";

        public const string LogsRefresh = "Làm mới lịch sử";
        public const string LogsDelete = "Xóa lịch sử";
        public const string LogsDeleteConfirm = "Bạn có chắc chắn muốn xóa toàn bộ lịch sử không?";
        public const string LogsDeleteSuccess = "Đã xóa lịch sử thành công.";

        public const string SettingWallpaper = "Hình nền Windows máy trạm";
        public const string SettingCafeName = "Tên quán trên client";
        public const string SettingBanner = "Banner/thông báo";
        public const string SettingThemeColor = "Màu theme client";
        public const string SettingThemeFont = "Font chữ giao diện";
        public const string SettingStatusFolder = "Thư mục trạng thái client";
        public const string SettingHeartbeat = "Heartbeat client (giây)";
        public const string SettingDashboardRefresh = "Dashboard refresh (giây)";
        public const string SettingAllowCloseHotkey = "Cho phép máy trạm đóng ứng dụng bằng Ctrl + Alt + K";
        public const string SettingEnableKiosk = "Bật fullscreen/kiosk mode cho client";
        public const string SettingHint = "Lưu ý: client ghi trạng thái vào thư mục client-status cạnh games.catalog.json. Có thể nhập thư mục trạng thái riêng nếu dùng shared path.";
        public const string SaveSettingsButton = "Lưu thiết lập";

        public const string UpdateSourceFolderPickerDescription = "Chọn thư mục bản vá để chép vào thư mục trò chơi.";
        public const string UpdateSourceZipFilter = "Tệp ZIP (*.zip)|*.zip";
        public const string JsonFileFilter = "Tệp JSON (*.json)|*.json";
        public const string CsvFileFilter = "Tệp CSV (*.csv)|*.csv";
        public const string ImageFileFilter = "Ảnh (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp|Tất cả tệp (*.*)|*.*";
        public const string LogsCsvHeader = "Thời gian,Trò chơi,Hành động,Trạng thái,Nội dung";
        public const string UpdateStartedPrefix = "Bắt đầu cập nhật";
        public const string UpdateCompleted = "Cập nhật hoàn tất.";
        public const string UpdateRunningStatus = "Đang tải";
        public const string UpdatePausedStatus = "Tạm dừng";
        public const string UpdateStoppingStatus = "Đang dừng";
        public const string UpdateStoppedStatus = "Đã dừng";
        public const string UpdateFailedStatus = "Thất bại";
        public const string UpdateSuccessStatus = "Hoàn tất";
        public const string DownloadTaskInitMessage = "Khởi tạo tác vụ cập nhật.";
        public const string TaskNoLongerRunning = "Tác vụ này không còn chạy.";
        public const string TaskPaused = "Tác vụ đã tạm dừng.";
        public const string TaskResumed = "Tác vụ đã tiếp tục.";
        public const string BatchPaused = "Đã tạm dừng theo yêu cầu hàng loạt.";
        public const string BatchResumed = "Đã tiếp tục theo yêu cầu hàng loạt.";
        public const string TaskStopping = "Đang gửi yêu cầu dừng...";
        public const string RetryWhileRunningNotAllowed = "Tác vụ đang chạy, không thể tải lại.";
        public const string RetrySourceNotFound = "Không tìm thấy nguồn IDC để tải lại tác vụ này.";
        public const string NeedStopTaskBeforeRemoveRow = "Vui lòng dừng tác vụ trước khi xóa dòng.";
        public const string LogActionUpdateKeyword = "Cập nhật";
        public const string LogActionSyncKeyword = "Đồng bộ";
        public const string DownloadStatusDownloaded = "Đã tải";
        public const string DownloadStatusMissing = "Chưa tải";
        public const string DownloadStatusError = "Lỗi tải";
        public const string RunStatusReady = "Sẵn sàng chạy";
        public const string RunStatusMissingExe = "Thiếu tệp chạy";
        public const string RunStatusNotConfiguredExe = "Chưa cấu hình tệp chạy";

        public const string NeedSelectGameFirst = "Vui lòng chọn trò chơi trước.";
        public const string NeedSwitchToResourceList = "Vui lòng chuyển sang danh sách tài nguyên để chọn trò chơi.";
        public const string NeedSelectResourceWithSource = "Vui lòng chọn trò chơi có nguồn IDC để tải.";
        public const string NeedConfigStatusFolder = "Chưa cấu hình thư mục trạng thái client.";
        public const string SettingsSavedAndCatalogSynced = "Đã lưu thiết lập và đồng bộ catalog cho client.";
        public const string DiskSpaceWarningTitle = "Cảnh báo dung lượng";
        public const string NeedAtLeastOneIdcSource = "Vui lòng nhập ít nhất một nguồn IDC (hỗ trợ ngăn cách bằng dấu ; hoặc xuống dòng để mirror/fallback).";
        public const string NeedResourceTargetFolder = "Vui lòng nhập thư mục đích máy chủ.";
        public const string ResourceConfigSaved = "Đã lưu cấu hình nguồn/đích tài nguyên.";
        public const string NoResourceWithSourceToDownload = "Không có trò chơi có nguồn IDC để tải.";
        public const string NoRunningTaskToPause = "Không có tác vụ đang chạy để tạm dừng.";
        public const string NoPausedTaskToResume = "Không có tác vụ tạm dừng để tiếp tục.";
        public const string NoRunningTaskToStop = "Không có tác vụ đang chạy để dừng.";
        public const string NoRetryableTask = "Không có mục phù hợp để tải lại.";
        public const string NoRunningTaskToSetBandwidth = "Không có tác vụ đang chạy để đặt băng thông.";
        public const string NoEligibleGameToSyncMissing = "Không có trò chơi phù hợp để đồng bộ file thiếu.";
        public const string ResourceResumeFromList = "Đã tiếp tục theo yêu cầu từ danh sách tài nguyên.";
        public const string ResourcePauseFromList = "Đã tạm dừng theo yêu cầu từ danh sách tài nguyên.";

        public const string GamesContextAdd = "Thêm";
        public const string GamesContextEdit = "Sửa";
        public const string GamesContextDelete = "Xóa";
        public const string GamesContextViewManifest = "Xem manifest";
        public const string GamesContextScanManifest = "Quét manifest";
        public const string GamesContextMoveTop = "Lên đầu";
        public const string GamesContextMoveUp = "Lên trên";
        public const string GamesContextMoveDown = "Xuống dưới";
        public const string GamesContextMarkHot = "Đánh dấu Hot game";
        public const string GamesContextUnmarkHot = "Bỏ Hot game";

        public const string DownloadContextPause = "Tạm dừng";
        public const string DownloadContextResume = "Tiếp tục";
        public const string DownloadContextPauseAll = "Tạm dừng tất cả";
        public const string DownloadContextResumeAll = "Tiếp tục tất cả";
        public const string DownloadContextStop = "Dừng tải";
        public const string DownloadContextSetBandwidth = "Giới hạn băng thông";
        public const string DownloadContextRetryFromIdc = "Tải lại từ IDC";
        public const string DownloadContextRemoveRow = "Xóa dòng";
        public const string DownloadContextRemoveFinished = "Xóa tác vụ đã xong";

        public static string ServerDashboardReadError(string message) => $"Không đọc được thông tin server: {message}";
        public static string ClientDashboardReadError(string message) => $"Không đọc được trạng thái máy trạm: {message}";
        public static string InfoSelectGameFirst(string name) => $"Vui lòng chọn {name} trước.";
        public static string ExportCatalogDone(string path) => $"Đã xuất danh mục:{Environment.NewLine}{path}";
        public static string CsvExportDone(string path) => $"Đã xuất CSV: {path}";
        public static string BackupSaved(string path) => $"Đã lưu bản sao lưu: {path}";
        public static string UpdateStarted(string gameName) => $"{UpdateStartedPrefix} {gameName}.";
        public static string BandwidthLimitSet(int mbps) => $"Đã đặt giới hạn băng thông: {mbps} MB/s.";
        public static string DeleteGameConfirm(string gameName) =>
            $"Bạn có chắc muốn xóa {gameName} khỏi danh sách quản lý? Dữ liệu trò chơi trên ổ đĩa sẽ không bị xóa.";
        public static string DiskSpaceWarning(string gameName, double requiredGb, double availableGb, double reserveGb) =>
            $"Dung lượng trống có thể không đủ để tải {gameName}.{Environment.NewLine}" +
            $"Cần thêm khoảng: {requiredGb:N2} GB{Environment.NewLine}" +
            $"Đang trống: {availableGb:N2} GB (khuyến nghị dự phòng {reserveGb:N0} GB).{Environment.NewLine}{Environment.NewLine}" +
            "Bạn có muốn tiếp tục không?";
        public static string ResourceHealthCheckDone(int missingSource, int needSync, int missingRunFile, string summary) =>
            $"Kiểm tra tài nguyên xong. Thiếu nguồn: {missingSource}. Cần đồng bộ: {needSync}. Cần kiểm tra file chạy: {missingRunFile}.{Environment.NewLine}{summary}";
        public static string ResourceSkipBecauseTaskRunning(string gameName) =>
            $"Bỏ qua {gameName}: đang có tác vụ tải chạy.";
        public static string ResourceTaskStoppedByRequest(string gameName) =>
            $"Đã dừng tác vụ tải tài nguyên của {gameName} theo yêu cầu.";
        public static string ResourceSyncMissingSuccess(string gameName, int copiedFiles, int totalFiles) =>
            $"Đồng bộ file thiếu {gameName}: sao chép {copiedFiles}/{totalFiles} tệp.";
        public static string ResourceSyncDownloadedSuccess(string gameName, int copiedFiles, int totalFiles, string sourcePath, string targetPath) =>
            $"Đã tải {gameName}: sao chép {copiedFiles}/{totalFiles} tệp từ {sourcePath} về {targetPath}.";
        public static string ResourceDownloadStopped(string gameName) =>
            $"Đã dừng tải {gameName} theo yêu cầu.";
        public static string ResourceTrySourceProgress(int index, int total, string sourceRoot) =>
            $"Đang thử nguồn IDC {index}/{total}: {sourceRoot}";
        public static string ResourceSourceError(string sourceRoot, string message) =>
            $"Nguồn IDC lỗi ({sourceRoot}): {message}";
        public static string ResourceSkipDueLowDisk(string gameName) =>
            $"Bỏ qua tải {gameName}: không đủ dung lượng trống.";
        public static string ResourceMirrorSummary(int count, bool accessible) =>
            accessible
                ? $"Nguồn IDC mirror: {count} nguồn"
                : $"Nguồn IDC mirror: {count} nguồn không truy cập";
        public static string ResourceDiskFreeSummary(double freeGb, double totalGb, double usedPercent, string warning) =>
            $"Ổ game trống: {freeGb:N1}/{totalGb:N1} GB ({usedPercent:N0}% dùng){warning}";
        public static string ResourceSummaryText(int filtered, int total, int downloaded, int missing, double requiredGb, string healthSummary) =>
            $"Hiển thị {filtered}/{total} trò chơi. Đã tải: {downloaded}. Chưa tải: {missing}. Cần thêm: {requiredGb:N1} GB. {healthSummary}";
        public static string ResourceDownloadMonitorSummary(int total, int running, int paused, int stopping, int failed, double totalSpeed, double remainingMb) =>
            $"Giám sát tải xuống máy chủ: tổng {total} tác vụ, đang tải {running}, tạm dừng {paused}, đang dừng {stopping}, thất bại {failed}. Tổng tốc độ: {totalSpeed:N2} MB/s. Còn lại: {remainingMb:N0} MB.";
    }

    public static class Launcher
    {
        public const string WindowTitle = "Menu trò chơi";
        public const string DefaultCafeName = "Cyber Game";
        public const string DefaultFontFamily = "Segoe UI";
        public const string HeaderSectionTitle = "Menu Trò Chơi";
        public const string DefaultBannerMessage = "Chào mừng quý khách";
        public const string CleanRamButton = "Dọn RAM";
        public const string MouseButton = "Chuột";
        public const string CleanRamDone = "Đã dọn dẹp bộ nhớ RAM thành công!";
        public const string OpenMouseControlErrorPrefix = "Không thể mở bảng điều khiển chuột: ";

        public const string QuickLinkYoutubeText = "YT";
        public const string QuickLinkYoutubeTooltip = "YouTube";
        public const string QuickLinkYoutubeUrl = "https://www.youtube.com";
        public const string QuickLinkFacebookText = "FB";
        public const string QuickLinkFacebookTooltip = "Facebook";
        public const string QuickLinkFacebookUrl = "https://www.facebook.com";
        public const string QuickLinkWebText = "Web";
        public const string QuickLinkWebTooltip = "Website";
        public const string QuickLinkWebUrl = "https://www.google.com";

        public const string DefaultCategory = "Tất cả";
        public const string MissingCatalogPath = "Chưa cấu hình đường dẫn danh mục trò chơi.";
    }
}
