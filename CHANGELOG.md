# Changelog

## v0.4.4 - art loading and snapshot switching (2026-11-27)

- Opening a snapshot no longer waits for operator art downloads: the
  snapshot is shown immediately. Missing art is fetched in the background
  only after explicit consent.
- Automatic art download for viewing is now **opt-in and off by default**
  (Settings → Operator art). The in-app snapshot action passes
  `--no-art-cache` until the option is enabled; `update_snapshot.cmd` runs the
  script directly.
- Switching snapshots no longer covers the content with the modal busy
  overlay; the header ComboBox stays usable.
- Rapid snapshot switching keeps the last selection (generation + cancellation,
  latest-wins); the header ComboBox and timeline buttons no longer lose clicks.
- Manual Refresh keeps the selected archive snapshot: the id is captured before
  the snapshot list is rebuilt.
- `IsBusy` is now ref-counted, so one operation cannot clear the busy overlay
  of another.
- Background art updates already rendered roster cards and an open operator
  window (targeted `INotifyPropertyChanged` + `RefreshArt`).
- Art cache checks each kind separately (`icon`/`full`), checks the PNG
  signature, uses unique temporary names and refreshes changed URLs when
  automatic download is enabled.
- Failed downloads keep the old file; `source.json` is updated for changed
  URLs when the cache is refreshed and written only for an existing valid PNG.
- Malformed `source.json` (`null` values, duplicate keys) no longer breaks the
  download queue.
- A corrupt PNG falls back to the next local variant or the letter instead of
  leaving a blank card.
- Art pack install/remove refreshes visible cards, not only the status line.
- No database schema changes; existing snapshots remain compatible.

## v0.4.3 - operator art cache (2026-11-25)

- Operator art is resolved by `char_id` first, then by manifest aliases and
  the local name/slug map. Unknown API operators can use a local cache without
  a code change.
- `snapshot.py` caches `avatar_url` (icon) and `illustration_url` (full) under
  `data/art_cache/<char_id>/`; failures never break a snapshot.
- Opening an older snapshot also backfills missing art on demand from the
  stored API URLs; the roster prefers a portrait, then the small icon, then
  full art, then the letter.
- Art lookup order: curated art pack → API cache → embedded Vesper → letter.
- `tools/make_art_pack.py --catalog <json>` writes `charId`, `name`, `ru` and
  `en` into `manifest.json`, so pack aliases work for new operators.

## Security cleanup (2026-11-25)

- Removed `data/seed.sql` — an accidentally published sample seed file — from
  the repository, git history and release zips.
- `init_db.cmd` works without it: `schema.sql` creates an empty database.
- Release assets `v0.2.0`–`v0.4.2` were rebuilt without `data/seed.sql`.
  The application binaries are unchanged.

## v0.4.2 - local database fix (2026-11-25)

### Fixed

- The app uses **only its own folder** for `data/`, `scripts/` and
  `.secrets/`. It no longer walks up into a parent workspace or falls back to
  `C:\AEF`, which could show a foreign database from a parent folder.
- `build.cmd` copies `data/schema.sql` and `scripts/*.py` into the publish
  folder, so a source build is self-contained.

### Changed

- Version bumped to **0.4.2**. UI unchanged; README screenshots are from 0.4.1.

## v0.4.1 - key onboarding (2026-11-25)

### Added

- **First-run access key screen** in the Endfield UI style: masked input
  (`*`), "never share your key" note, links to `skport.com` and the token page,
  and two pill buttons — **Exit** and **Save**.
- The prompt appears on startup without a key, when the token file disappears
  while the app is running, and before a snapshot if the key is missing.
- Saving writes `.secrets/endfield_token.txt` atomically (or the active token
  file used by `snapshot.py`) and closes the prompt.
- Settings → About: repository link, creator **frostikfrostik8**, GitHub
  profile button and Discord handle (click to copy).
- Sidebar: repository link below the official site.

### Changed

- Version bumped to **0.4.1**.

## v0.4.0 - external art pack (2026-11-24)

### Added

- Operator art is loaded from an external `operators/` folder instead of being
  embedded in the exe. Lookup: `--art-dir` → Settings folder →
  `%LOCALAPPDATA%\AkEfLedger\art\operators` → `assets\operators` next to the
  exe → embedded Vesper → letter.
- **Settings → Operator art**: status, active path, choose folder, install from
  `.zip`, download from GitHub Releases, open folder, remove.
- `manifest.json` support: pack version, name aliases, per-file SHA-256.
  Installation is atomic (staging → verify → swap) and hash-checked.
- `tools/make_art_pack.py` builds a normalized `art-v1.zip` from a local site
  snapshot.
- Roster banner with a Settings shortcut when art is not installed.

### Changed

- Exe dropped from **46.8 MB to 5.8 MB**; art is 40 MB next to the exe or
  ~17.9 MB as a normalized pack (`full` 1200 px / 256 colors).
- `OperatorArt.Has` checks the actual file, so the letter fallback appears only
  when art is really missing (previously it trusted the name map).
- `settings.json` stores independent keys: saving the language no longer drops
  other settings.
- Version bumped to **0.4.0**.

## v0.3.1 - localization (2026-11-24)

### Added

- **RU/EN interface localization** with automatic detection from the Windows UI
  language.
- Language switching **without restarting** the app: all views, nav labels,
  filters, dates, numbers and diff strings update in place.
- External translation files `lang/*.json` next to the exe, with built-in
  RU/EN fallback inside the assembly.
- New languages can be added by dropping another `lang/xx.json` file; the
  language list can be reloaded from Settings without restart.
- Draft English translation for the full interface, including the operator
  detail window, charts, progress and snapshots.
- `--lang=xx` command-line override for screenshots and testing.
- Settings: “Language folder” button and a language-list reload button.
- `docs/localization.md` — file format, auto-detection, fallback rules and a
  step-by-step guide for adding a language.

### Changed

- Operator display names follow the interface language: Russian names in RU,
  English names in EN.
- Dates and numbers follow the selected culture (`ru-RU` / `en-US`).
- Snapshot diff summaries from SQLite are translated for display in EN.
- Version bumped to **0.3.1**.

## v0.3.0 — .NET 10 migration (2026-11-24)

### Changed

- Migrated from .NET 8 to **.NET 10 LTS** (`net10.0-windows`).
- `Microsoft.Data.Sqlite` updated to 10.0.15.
- CI and docs now require .NET 10 SDK / Desktop Runtime.
- Version bumped to 0.3.0; release asset is built with .NET 10.

### Why

.NET 8 reached end of support on 2026-11-10. .NET 10 LTS is supported until
2028-11-14 and keeps the WPF app on a patched runtime.

## v0.2.0 — private preview (2026-11-24)

First prepared release of **AK:EF Ledger**.

### Added

- Settings tab: RU/EN language placeholder, test operator visibility,
  in-memory DEV snapshot profile, reset.
- Test operator without art: random rarity/element/class/potential/level/weapon,
  letter fallback on the card.
- **Vesper**: test 6★ Cryo Striker with AI-generated transparent
  `icon` / `portrait` / `full` assets.
- DEV profile: 8 fake snapshots (IDs 9001–9008) with chart, timeline and diff;
  never writes to SQLite.
- Public-ready repository docs: English and Russian README, MIT license,
  third-party notices, build workflow.
- Standalone distribution layout: exe + `data/` + `scripts/` + `.secrets/` +
  `context/` + licenses.

### Changed

- Application renamed to **AK:EF Ledger** (`AkEfLedger.exe`).
- New `akef.ico` application icon and AK:EF branding.
- `Sanity` is translated as **«Рассудок»** everywhere (UI, snapshot reports,
  glossary), matching the first Arknights localization.
- Operator cards hide the placeholder letter when local art exists; the letter
  remains only for new API operators without art.
- README and journal updated; release package includes only safe test assets.

### Fixed

- Chart line was invisible for a flat two-point series; now drawn with
  `DrawLine`.
- Stale async snapshot diff could overwrite the DEV diff; added a generation
  guard.
- Achievement and resource card wrapping at 1120–1480 px.
- Absurd `60/60/60` Y-axis labels replaced with integer/decimal formatting.

## v0.1.0 — internal baseline

- WPF / .NET 8 account dashboard: overview, roster, dynamics, progress,
  achievements, snapshots.
- SQLite data layer, Python snapshot updater, local operator art, dark
  engineering-terminal visual system.
