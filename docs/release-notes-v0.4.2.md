# AK:EF Ledger v0.4.2 — local database fix

## English

Patch release for a local database resolution bug.

- The app now uses **only its own folder** for `data/`, `scripts/` and
  `.secrets/`. It no longer walks up into a parent workspace or falls back to
  `C:\AEF`.
- This fixes a case where a fresh clone or a portable copy could show a
  database from a parent folder instead of its own empty database.
- `build.cmd` now copies `data/schema.sql`, `data/seed.sql` and
  `scripts/*.py` into the publish folder, so a source build is self-contained.
- No UI changes. README screenshots are from v0.4.1.
- App version is **0.4.2**.

## Русский

Патч-релиз с исправлением поиска локальной базы.

- Приложение использует **только свою папку** для `data/`, `scripts/` и
  `.secrets/`. Оно больше не поднимается в родительский workspace и не
  откатывается к `C:\AEF`.
- Исправлен случай, когда свежий клон или portable-копия показывали базу из
  родительской папки вместо своей (пустой).
- `build.cmd` теперь копирует `data/schema.sql`, `data/seed.sql` и
  `scripts/*.py` в publish-папку, чтобы сборка из исходников была
  самодостаточной.
- Интерфейс не менялся. Скриншоты в README — с v0.4.1.
- Версия приложения — **0.4.2**.
