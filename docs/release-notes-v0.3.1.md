# AK:EF Ledger v0.3.1 — localization

## English

Interface localization release.

- **RU/EN interface** with automatic detection from the Windows UI language.
- Switch languages **without restarting**: navigation, filters, tables, charts,
  dates, numbers, diff strings and the operator window update in place.
- Translations live in external `lang/*.json` files next to the exe; built-in
  RU/EN files remain as a fallback.
- Add a new language by dropping `lang/xx.json` and pressing reload in
  **Settings → Interface language**. Missing keys fall back to English, then
  Russian.
- Draft English translation of the complete interface.
- Operator names follow the selected language (Russian / English).
- Dates and numbers use the selected culture; snapshot diff summaries from
  SQLite are translated for display.
- `--lang=xx` command-line override for testing and screenshots.
- New guide: `docs/localization.md`.
- App version is **0.3.1**.

No database or snapshot format changes; existing `data/aef.db` and snapshots
continue to work.

## Русский

Релиз с локализацией интерфейса.

- **Интерфейс RU/EN** с автоопределением по языку Windows.
- Переключение языка **без перезапуска**: навигация, фильтры, таблицы, график,
  даты, числа, строки diff и карточка оператора обновляются на месте.
- Переводы лежат во внешних файлах `lang/*.json` рядом с exe; встроенные RU/EN
  остаются резервом.
- Новый язык добавляется файлом `lang/xx.json` и кнопкой перечитывания в
  **Настройки → Язык интерфейса**. Недостающие ключи берутся из английского,
  затем из русского.
- Черновой английский перевод всего интерфейса.
- Имена операторов следуют выбранному языку (русские / английские).
- Даты и числа используют выбранную культуру; сводки diff из SQLite
  переводятся для показа на английском.
- `--lang=xx` — разовый override для тестов и скриншотов.
- Новое руководство: `docs/localization.md`.
- Версия приложения — **0.3.1**.

Формат базы и снимков не менялся; существующие `data/aef.db` и снимки
продолжают работать.
