# AK:EF Ledger v0.4.4 — art loading and snapshot switching

## English

Regression patch for v0.4.3.

- The first snapshot opens immediately. Missing operator art is downloaded in
  the background instead of blocking the roster, the overview and the busy
  overlay.
- Automatic art download for viewing is **opt-in and off by default** in
  Settings. The in-app snapshot action passes `--no-art-cache` unless the
  option is enabled; `update_snapshot.cmd` runs the script directly.
- Switching snapshots no longer covers the content with the modal busy
  overlay; the header ComboBox stays usable while data is loading.
- Rapid snapshot switching keeps the last selection. Generation checks,
  cancellation and latest-wins replace the old `_loading` flag, so a slower
  previous load can no longer snap the ComboBox back.
- Manual Refresh keeps the selected archive snapshot; the selection id is
  captured before the snapshot list is rebuilt.
- The busy overlay is ref-counted; a completed operation cannot hide the
  overlay of another one.
- Background art updates already visible roster cards and an open operator
  window; no page reload is required.
- The API art cache checks `icon` and `full` separately, validates the PNG
  signature/IHDR/IEND, uses unique temporary files and refreshes changed URLs
  when automatic download is enabled. Failed downloads keep the old file. A
  curated pack is preferred: if an operator has any art in the pack, missing
  API kinds are not backfilled.
- `source.json` is written only for an existing valid PNG. Malformed files
  (`null` values, duplicate keys) no longer break the download queue.
- A corrupt PNG falls back to the next local variant or the letter instead of
  leaving a blank card.
- Installing or removing an art pack refreshes visible cards.
- No database or snapshot format changes; existing data remains compatible.

## Русский

Патч-регрессия для v0.4.3.

- Первый снимок открывается сразу: отсутствующий арт операторов докачивается
  в фоне и не блокирует ростер, обзор и оверлей занятости.
- Автоматическая догрузка арта для просмотра теперь **опциональна и по
  умолчанию выключена** в настройках. Кнопка «Снять снимок» в приложении
  передаёт `--no-art-cache`, пока опция не включена; `update_snapshot.cmd`
  запускает скрипт напрямую.
- Переключение снимков больше не накрывает контент модальным оверлеем;
  ComboBox в шапке остаётся доступным, пока читаются данные.
- Быстрое переключение снимков сохраняет последний выбор. Generation-проверки,
  отмена и latest-wins заменяют старый флаг `_loading`, поэтому медленная
  предыдущая загрузка больше не возвращает ComboBox назад.
- Ручной Refresh сохраняет выбранный архивный снимок: id выбора запоминается
  до перестройки списка снимков.
- Оверлей занятости теперь со счётчиком: завершённая операция не скрывает
  оверлей другой.
- Фоновая загрузка обновляет уже нарисованные карточки ростера и открытое
  окно оператора без перезагрузки страницы.
- Кэш API проверяет `icon` и `full` отдельно, проверяет PNG-сигнатуру,
  IHDR и IEND, использует уникальные временные файлы и обновляет изменённые
  URL при включённой автодогрузке. При ошибке старый файл сохраняется.
  Курируемый пак в приоритете: если у оператора в паке есть любой арт,
  недостающие виды из API не догружаются.
- `source.json` пишется только для существующего валидного PNG. Битый файл
  (`null`-значения, дубли ключей) больше не роняет очередь загрузки.
- Битый PNG переключается на следующий локальный вариант или букву, а не
  оставляет пустую карточку.
- Установка/удаление арт-пака обновляет видимые карточки.
- Схема БД и формат снимков не меняются; существующие данные совместимы.
