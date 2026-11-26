# AK:EF Ledger v0.4.4 — art loading and snapshot switching

## English

Regression patch for v0.4.3.

- The first snapshot opens immediately. Missing operator art is downloaded in
  the background instead of blocking the roster, the overview and the busy
  overlay.
- Automatic art download for viewing is **opt-in and off by default** in
  Settings. Taking a snapshot is a separate explicit action; `snapshot.py` may
  cache art from the API response unless `--no-art-cache` is used.
- Switching snapshots no longer covers the content with the modal busy
  overlay; the header ComboBox stays usable while data is loading.
- Rapid snapshot switching keeps the last selection. Generation checks,
  cancellation and latest-wins replace the old `_loading` flag, so a slower
  previous load can no longer snap the ComboBox back.
- The busy overlay is ref-counted; a completed operation cannot hide the
  overlay of another one.
- Background art updates already visible roster cards and an open operator
  window; no page reload is required.
- The API art cache checks `icon` and `full` separately, checks the PNG
  signature, uses unique temporary files and refreshes changed URLs when
  automatic download is enabled. Failed downloads keep the old file.
- A corrupt PNG no longer throws during render; the cache entry is skipped
  until it can be refreshed.
- Installing or removing an art pack refreshes visible cards.
- No database or snapshot format changes; existing data remains compatible.

## Русский

Патч-регрессия для v0.4.3.

- Первый снимок открывается сразу: отсутствующий арт операторов докачивается
  в фоне и не блокирует ростер, обзор и оверлей занятости.
- Автоматическая догрузка арта для просмотра теперь **опциональна и по
  умолчанию выключена** в настройках. Снимок — отдельное явное действие;
  `snapshot.py` может кэшировать арт из ответа API, если не передан
  `--no-art-cache`.
- Переключение снимков больше не накрывает контент модальным оверлеем;
  ComboBox в шапке остаётся доступным, пока читаются данные.
- Быстрое переключение снимков сохраняет последний выбор. Generation-проверки,
  отмена и latest-wins заменяют старый флаг `_loading`, поэтому медленная
  предыдущая загрузка больше не возвращает ComboBox назад.
- Оверлей занятости теперь со счётчиком: завершённая операция не скрывает
  оверлей другой.
- Фоновая загрузка обновляет уже нарисованные карточки ростера и открытое
  окно оператора без перезагрузки страницы.
- Кэш API проверяет `icon` и `full` отдельно, проверяет PNG-сигнатуру,
  использует уникальные временные файлы и обновляет изменённые URL при
  включённой автодогрузке. При ошибке старый файл сохраняется.
- Битый PNG больше не падает при отрисовке; запись кэша пропускается до
  обновления.
- Установка/удаление арт-пака обновляет видимые карточки.
- Схема БД и формат снимков не меняются; существующие данные совместимы.
