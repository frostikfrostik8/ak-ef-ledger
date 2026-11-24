# AK:EF Ledger v0.4.0 — external art pack

## English

Art loading release. Operator images are no longer baked into the exe.

- Art lives in an external `operators/` folder:
  `<slug>/{icon,portrait,full}.png` plus an optional `manifest.json`.
- Lookup order: `--art-dir` → Settings folder →
  `%LOCALAPPDATA%\AkEfLedger\art\operators` → `assets\operators` next to the
  exe → embedded Vesper → first letter.
- **Settings → Operator art**: status, active path, choose folder, install from
  `.zip`, download from GitHub Releases, open folder, remove.
- `manifest.json`: pack version, name aliases, per-file SHA-256. Installation
  is atomic: staging → hash check → swap; a failed install keeps the old pack.
- Roster banner with a Settings shortcut when art is not installed.
- `tools/make_art_pack.py` builds `art-v1.zip` from a local site snapshot.
  `full` is resized to 1200 px / 256 colors: 34.1 MB → 12.5 MB; the pack is
  ~17.9 MB instead of 40 MB.
- Exe size: **46.8 MB → 5.8 MB**. Portable build keeps art in
  `assets\operators`; the public-safe build still contains only Vesper.
- `OperatorArt.Has` now checks the real file, so the letter fallback appears
  only when art is missing.
- `settings.json` stores independent keys; saving the language no longer drops
  other settings.
- Guide: `docs/art-pack.md`.
- App version is **0.4.0**.

No database or snapshot format changes; existing `data/aef.db`, snapshots and
`lang/*.json` files continue to work.

## Русский

Релиз с внешними артами. Картинки оперативников больше не вшиваются в exe.

- Арты лежат во внешней папке `operators/`:
  `<slug>/{icon,portrait,full}.png` и опциональный `manifest.json`.
- Порядок поиска: `--art-dir` → папка в настройках →
  `%LOCALAPPDATA%\AkEfLedger\art\operators` → `assets\operators` рядом с exe →
  встроенная Веспер → первая буква.
- **Настройки → Арт оперативников**: статус, активный путь, выбор папки,
  установка из `.zip`, скачивание с GitHub Releases, открытие папки, удаление.
- `manifest.json`: версия пака, aliases имён, SHA-256 каждого файла. Установка
  атомарная: staging → проверка хешей → замена; при ошибке старый пак остаётся.
- Баннер на Ростере с переходом в настройки, если арт не установлен.
- `tools/make_art_pack.py` собирает `art-v1.zip` из локального снапшота сайта.
  `full` уменьшается до 1200 px / 256 цветов: 34,1 МБ → 12,5 МБ; пак — ~17,9 МБ
  вместо 40.
- Размер exe: **46,8 МБ → 5,8 МБ**. Portable-сборка держит арты в
  `assets\operators`; public-safe сборка по-прежнему содержит только Веспер.
- `OperatorArt.Has` проверяет реальный файл, поэтому буква-заглушка появляется
  только когда арта действительно нет.
- `settings.json` хранит независимые ключи: сохранение языка больше не стирает
  остальные настройки.
- Руководство: `docs/art-pack.md`.
- Версия приложения — **0.4.0**.

Формат базы и снимков не менялся; существующие `data/aef.db`, снимки и файлы
`lang/*.json` продолжают работать.
