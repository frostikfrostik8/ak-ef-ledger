# AK:EF Ledger v0.4.3 — operator art cache

## English

Feature release focused on operator art.

- Operator art is resolved by the stable API `char_id` first, then by manifest
  aliases, then by the name/slug map.
- `snapshot.py` caches the official `avatar_url` (icon) and `illustration_url`
  (full art) under `data/art_cache/<char_id>/`.
- Opening an older snapshot backfills missing art from the URLs already stored
  in that snapshot. The roster uses: portrait → small icon → full → letter.
- `tools/make_art_pack.py --catalog <json>` writes `charId`, `name`, `ru` and
  `en` into `manifest.json`, so pack aliases work for new operators.
- The app is tested on a clean Windows 10 2004 x64 VM installed from ISO on a
  separate HDD, with .NET 10 Desktop Runtime installed normally.
- No database, token, seed or real operator art is included in the release
  zip.
- Existing local databases and snapshots remain compatible; no schema change.

## Русский

Релиз про арты операторов.

- Арт ищется по стабильному `char_id` из API, затем по aliases в manifest,
  затем по старой карте имя/slug.
- `snapshot.py` кэширует официальные `avatar_url` (иконка) и
  `illustration_url` (полный арт) в `data/art_cache/<char_id>/`.
- При открытии старого снимка отсутствующие арты догружаются по уже
  сохранённым в нём URL. Ростер использует: портрет → маленькая иконка →
  полный арт → буква.
- `tools/make_art_pack.py --catalog <json>` записывает `charId`, `name`, `ru`
  и `en` в `manifest.json`, поэтому aliases работают и для новых операторов.
- Приложение проверено на чистой Windows 10 2004 x64 в VM, установленной с
  ISO на отдельный HDD, с обычной установкой .NET 10 Desktop Runtime.
- В release-zip нет базы, токена, seed и реальных артов операторов.
- Существующие локальные базы и снимки совместимы; схема не менялась.
