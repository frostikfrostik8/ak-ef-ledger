# AK:EF Ledger

[English](README.md) | [Русский](README.ru.md)

A local Windows desktop dashboard for **Arknights: Endfield** account data:
roster, resources, domains, achievements, snapshot history and dynamics.
Built with .NET 10 LTS / WPF. No web server, no browser, no telemetry.

> **Unofficial fan project.** Not affiliated with HYPERGRYPH, GRYPHLINE or
> Arknights: Endfield. Game names and trademarks belong to their owners.
>
> **Status: private preview.** The repository stays private while the release
> is being polished. The code, docs and release package are already prepared
> for a public release.

## What it is

`AK:EF Ledger` reads your own snapshots from a local SQLite database and shows
them in a dense “engineering terminal” UI:

- **Overview** — player, patch, Sanity/BP/dailies/weeklies, credits, Seek,
  collection counters and the diff from the previous snapshot.
- **Roster** — search by RU/EN name, filters by rarity/element/class, operator
  cards and a paper-document detail window with weapon, gear, skills, talents.
- **Dynamics** — chart over snapshots, timeline, arbitrary A/B snapshot diff.
- **Progress** — domains, settlements, collections, Crisis Contract, War
  Echoes, Indie groups, inventory.
- **Achievements** — search, categories, plated badges.
- **Snapshots** — dense archive table, Markdown report, raw JSON.
- **Settings** — RU/EN placeholder, test operators, in-memory DEV snapshot
  profile, test-data reset.

## Data flow

```text
Skport API
   │  scripts/snapshot.py (only when you press “Снять снимок”)
   ▼
data/snapshots/*.json.gz  +  data/aef.db
   │
   ▼
AK:EF Ledger (WPF, Microsoft.Data.Sqlite)
```

The app never talks to a server by itself. Python is used only for the snapshot
updater. Everything else is a local SQLite read.

## Features

- Dark terminal visual system: `#FFFA00` accent, 0–2 px radii, Inter,
  Bahnschrift, Cascadia Mono, PT Serif document layer.
- Operator cards with `icon` / `portrait` / `full` local art support.
- Letter fallback for operators without local art (new API characters).
- Snapshot diff, timeline and custom line chart — no chart library.
- Test modes: random operator without art, **Vesper** with AI-generated
  transparent art, and a DEV profile with 8 fake snapshots.
- No telemetry, no analytics, no background network calls.

## Quick start (release package)

1. Install **.NET 10 Desktop Runtime** and **Python 3.10+**.
2. Extract the release zip.
3. Create the local database:

   ```cmd
   init_db.cmd
   ```

4. Put your Skport token into `.secrets/endfield_token.txt`
   (one line, no quotes).
5. Take the first snapshot:

   ```cmd
   update_snapshot.cmd
   ```

6. Start:

   ```cmd
   launch.cmd
   ```

The token is used only by `scripts/snapshot.py` and stays on your machine.

## Build from source

```powershell
dotnet publish "src\AefAccount\AefAccount.csproj" -c Release -r win-x64 `
  --self-contained false -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o "src\AefAccount\publish"
```

or:

```cmd
build.cmd
```

Run:

```cmd
run.cmd
```

Requires .NET 10 SDK. Python is optional for development; the UI runs without it.

## Repository layout

```text
src/AefAccount/          WPF application
  Assets/Fonts/          Inter + PT Serif (OFL)
  Assets/Operators/      test operator Vesper only
  Services/              DataService, DiffService, OperatorArt, DevDataService
  Views/                 Overview, Roster, Dynamics, Progress, Achievements,
                         Snapshots, Settings, OperatorWindow
scripts/                 snapshot.py, sync.py, aef.py
data/                    schema.sql, seed.sql
docs/                    release notes
```

## Operator art

Real operator art is **not** included in this repository: it is copyrighted
and large. The app works without it and shows the first letter of the operator
name as a fallback.

To use your own local art, place:

```text
src/AefAccount/Assets/Operators/<slug>/icon.png
src/AefAccount/Assets/Operators/<slug>/portrait.png
src/AefAccount/Assets/Operators/<slug>/full.png
```

The name-to-slug map is in `src/AefAccount/Services/OperatorArt.cs`.

The test operator **Vesper** is included in
`src/AefAccount/Assets/Operators/vesper/` with AI-generated transparent PNGs
and is intended for UI testing only.

## Settings and test data

- **Language** — RU is active; EN is a placeholder until the community
  terminology glossary is parsed from the wiki.
- **Show test operators** — toggles test cards in Roster and “Костяк”.
- **DEV snapshot profile** — 8 in-memory snapshots (IDs 9001–9008) with
  growing level, Sanity, credits and Seek. The chart, timeline and diff work;
  nothing is written to SQLite.
- **Test operator without art** — random stats, letter fallback.
- **Vesper** — 6★ Cryo Striker with transparent test art.
- **Reset** — clears test data and returns real snapshots.

## Security and privacy

- The Skport token is read from `.secrets/endfield_token` or `.txt`.
- `.secrets/`, `data/aef.db`, `data/snapshots/` are gitignored.
- The app has no telemetry and no auto-update.
- Only the official Skport API is contacted, only when you request a snapshot.
- The token grants full access to the account: never share it, never send it to
  third-party sites.

## Roadmap

- Full RU/EN localization from a community glossary.
- Keyboard focus ring and 125/150/200 % DPI pass.
- Vector element/class icons instead of text codes.
- Optional light “paper” theme as a second complete token set.
- Export snapshot to CSV/JSON.
- More chart metrics (credits, Seek, domains).

## License

Code: MIT — see [LICENSE](LICENSE).
Fonts: SIL Open Font License — see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
Arknights: Endfield names and trademarks belong to HYPERGRYPH / GRYPHLINE.

