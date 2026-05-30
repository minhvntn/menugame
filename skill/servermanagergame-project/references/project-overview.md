# Project Overview

## Mục tiêu dự án

Bộ công cụ quản lý game LAN/PC room gồm:
- `GameUpdater.WinForms`: chạy phía server để quản lý game, cập nhật bản vá, xuất catalog cho client.
- `GameLauncher.Client`: chạy phía client để hiển thị menu game dạng icon card và launch game.

## Cấu trúc mã nguồn

- `src/GameUpdater.WinForms`
- `src/GameLauncher.Client`
- `src/GameUpdater.Core`
- `src/GameUpdater.Data`
- `src/GameUpdater.Shared`

Solution: `GameUpdater.sln`

## Tính năng chính (theo README)

- Quản lý danh sách game: tên, nhóm, đường dẫn cài đặt, version, EXE, tham số.
- Quét thư mục game và tạo manifest SHA256.
- Cập nhật từ thư mục patch hoặc file ZIP.
- Backup file trước khi ghi đè khi update.
- Ghi lịch sử thao tác vào SQLite.
- Tự động xuất `games.catalog.json` sau thao tác quản lý/cập nhật.

## Lệnh thường dùng

Restore + chạy updater server:

```powershell
dotnet restore GameUpdater.sln
dotnet run --project .\src\GameUpdater.WinForms\GameUpdater.WinForms.csproj
```

Chạy launcher client:

```powershell
dotnet run --project .\src\GameLauncher.Client\GameLauncher.Client.csproj
```

Build release:

```powershell
dotnet build GameUpdater.sln -c Release
```

## Cấu hình client

Tạo `launcher.settings.json` cạnh file EXE client:

```json
{
  "CatalogPath": "\\\\SERVER\\GameShare\\games.catalog.json"
}
```

## Build outputs

- Server EXE: `src\GameUpdater.WinForms\bin\Release\net8.0-windows\GameUpdater.WinForms.exe`
- Client EXE: `src\GameLauncher.Client\bin\Release\net8.0-windows\GameLauncher.Client.exe`

## Gợi ý kiểm tra nhanh sau khi sửa

1. Build solution không lỗi.
2. Updater vẫn load danh sách game và xuất catalog bình thường.
3. Launcher đọc được catalog và bấm chạy game thành công.
