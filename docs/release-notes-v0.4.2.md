# AK:EF Ledger v0.4.2 — local database fix

> **Security cleanup 2026-11-25:** the accidentally published `data/seed.sql`
> was removed from the repository, git history and this release asset.
> `init_db.cmd` works without it; the app does not use the seed table.

## English

Patch release with a local database lookup fix.

- The app uses **only its own folder** for `data/`, `scripts/` and `.secrets/`.
  It no longer climbs up and searches there.
- Fixed a case where a fresh clone or portable copy showed a database from a
  parent folder instead of its own (empty) one.
- `build.cmd` now copies `data/schema.sql` and `scripts/*.py` into the publish
  folder, so a source build is self-contained.
- The UI has not changed. The README screenshots from v0.4.1 are still valid.
- App version is **0.4.2**.

## Русский

Патч-релиз с исправлением поиска локальной базы.

- Приложение использует **только свою папку** для `data/`, `scripts/` и
  `.secrets/`. Оно больше не поднимается выше и там не ищет.
- Исправлен случай, когда свежий клон или portable-копия показывали базу из
  родительской папки вместо своей (пустой).
- `build.cmd` теперь копирует `data/schema.sql` и `scripts/*.py` в
  publish-папку, чтобы сборка из исходников была самодостаточной.
- Интерфейс не менялся. Скриншоты в README — с v0.4.1 актуальны.
- Версия приложения — **0.4.2**.
