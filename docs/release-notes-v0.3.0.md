# AK:EF Ledger v0.3.0 — .NET 10 LTS

## English

Maintenance release focused on runtime support.

- Migrated the WPF app from .NET 8 to **.NET 10 LTS**.
- `Microsoft.Data.Sqlite` updated to 10.0.15.
- CI and documentation now require .NET 10 SDK / Desktop Runtime.
- App version is 0.3.0.
- No UI or data-format breaking changes; existing `data/aef.db` and snapshots
  continue to work.

.NET 8 reached end of support on 2026-11-10. .NET 10 LTS is supported until
2028-11-14.

## Русский

Технический релиз, посвящённый поддерживаемой платформе.

- WPF-приложение переведено с .NET 8 на **.NET 10 LTS**.
- `Microsoft.Data.Sqlite` обновлён до 10.0.15.
- CI и документация теперь требуют .NET 10 SDK / Desktop Runtime.
- Версия приложения — 0.3.0.
- Совместимость с существующей `data/aef.db` и снимками сохранена.

Поддержка .NET 8 закончилась 10 ноября 2026. .NET 10 LTS поддерживается до
14 ноября 2028.
