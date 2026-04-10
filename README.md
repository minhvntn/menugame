# GameUpdater + Menu Game Client

Bộ công cụ gồm 2 ứng dụng:

- `GameUpdater.WinForms` (chạy trên server): quản lý game, cập nhật game, xuất danh mục cho client.
- `GameLauncher.Client` (chạy trên máy client): menu game giao diện thẻ icon để tìm và bấm chơi.

## Tính năng chính

- Quản lý danh sách game (tên, nhóm, đường dẫn cài đặt, phiên bản, tệp chạy EXE, tham số).
- Quét thư mục game và tạo `manifest` SHA256.
- Cập nhật từ thư mục bản vá hoặc tệp ZIP.
- Sao lưu tệp bị ghi đè trước khi cập nhật.
- Ghi lịch sử thao tác vào SQLite.
- Tự động xuất `games.catalog.json` sau các thao tác quản lý game/cập nhật.

## Yêu cầu

- Windows
- .NET SDK (`dotnet --info`)

## Chạy server updater

```powershell
dotnet restore GameUpdater.sln
dotnet run --project .\src\GameUpdater.WinForms\GameUpdater.WinForms.csproj
```

## Chạy menu client

```powershell
dotnet run --project .\src\GameLauncher.Client\GameLauncher.Client.csproj
```

## Build bản Release

```powershell
dotnet build GameUpdater.sln -c Release
```

File EXE:

- Server: `src\GameUpdater.WinForms\bin\Release\net8.0-windows\GameUpdater.WinForms.exe`
- Client: `src\GameLauncher.Client\bin\Release\net8.0-windows\GameLauncher.Client.exe`

## Các bước sử dụng thực tế

1. Trên server updater, thêm/sửa game và nhập đầy đủ `Tệp chạy (EXE)`.
2. Chọn đường dẫn xuất danh mục client lần đầu bằng nút `Xuất danh mục client`.
3. Từ các lần sau, danh mục sẽ tự động xuất lại theo đường dẫn đã chọn.
4. Trên máy client, đặt file `launcher.settings.json` cạnh EXE client, ví dụ:

```json
{
  "CatalogPath": "\\\\SERVER\\GameShare\\games.catalog.json"
}
```

5. Mở `GameLauncher.Client.exe`, menu game sẽ tự tải và cho phép bấm chơi.
