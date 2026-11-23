# AK:EF Ledger

Private source repository for the **AK:EF Ledger** — a local WPF account
terminal for *Arknights: Endfield*.

## Stack

- .NET 8 / WPF
- `Microsoft.Data.Sqlite`
- Python only for the snapshot updater (`scripts/snapshot.py`)

## Build

```cmd
build.cmd
```

or:

```powershell
dotnet publish "src\AefAccount\AefAccount.csproj" -c Release -r win-x64 `
  --self-contained false -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o "src\AefAccount\publish"
```

## Run

```cmd
run.cmd
```

Or start `src\AefAccount\publish\AkEfLedger.exe`.

## Data and scripts

The app looks for `data/aef.db` next to the executable (or above it), and for
`scripts/snapshot.py` in the application root.

For a self-contained working folder use:

```text
AK-EF-Ledger/
├─ AkEfLedger.exe
├─ data/aef.db
├─ data/snapshots/
├─ scripts/snapshot.py
├─ scripts/sync.py
└─ .secrets/endfield_token.txt
```

`.secrets/` and `data/aef.db` are gitignored. Never commit the token.

## Operator art

Real operator `icon` / `portrait` / `full` PNGs are **not** committed here
(copyright + size). Put them locally into:

```text
src/AefAccount/Assets/Operators/<slug>/{icon,portrait,full}.png
```

The map of names/slugs is in `src/AefAccount/Services/OperatorArt.cs`.

The test operator **Vesper** is included in
`src/AefAccount/Assets/Operators/vesper/` as AI-generated transparent test
art.

If a file is missing, the card shows the first letter of the name — this is
the intended fallback for newly added API operators.

## Settings / test data

The **Настройки** tab has:

- RU/EN language switch (EN is a placeholder until the term glossary is ready);
- test operator visibility;
- DEV snapshot profile (8 in-memory snapshots for chart/diff testing);
- buttons for a random operator without art, Vesper with test art, loading the
  DEV profile and resetting test data.

DEV snapshots never touch SQLite.

## Visual system

Dark engineering terminal, `#FFFA00` accent, Inter / Bahnschrift /
Cascadia Mono, paper-document operator detail. Fonts are under OFL; see
`src/AefAccount/Assets/Fonts/`.
