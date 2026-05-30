---
name: servermanagergame-project
description: Nắm nhanh kiến trúc và quy trình làm việc của dự án ServerManagerGame/GameUpdater (.NET WinForms + client launcher). Dùng khi cần sửa code, debug, build, hoặc vận hành updater server và launcher client trong repo này.
---

# ServerManagerGame Project Context

## Đọc nhanh trước khi làm

1. Đọc [references/project-overview.md](references/project-overview.md) để nắm kiến trúc và luồng dữ liệu.
2. Xác định phạm vi chỉnh sửa theo module:
- Server UI: `src/GameUpdater.WinForms`
- Client UI: `src/GameLauncher.Client`
- Domain/service: `src/GameUpdater.Core`
- Data/SQLite: `src/GameUpdater.Data`
- Shared models/helpers: `src/GameUpdater.Shared`
3. Sau khi sửa, ưu tiên build solution:

```powershell
dotnet build GameUpdater.sln -c Release
```

## Quy ước thao tác

- Giữ tương thích Windows và .NET `net8.0-windows`.
- Khi thay đổi model hoặc flow cập nhật game, kiểm tra ảnh hưởng tới xuất `games.catalog.json`.
- Khi thay đổi logic launcher, giữ tương thích `launcher.settings.json` với trường `CatalogPath`.
