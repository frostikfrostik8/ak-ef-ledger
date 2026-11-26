# Art pack

Operator art lives **outside the exe**. The application reads PNG files from an
`operators/` folder, so the exe stays ~5.8 MB and the art can be updated,
replaced or removed without rebuilding.

## Folder layout

```text
operators/
  manifest.json            optional
  akekuri/
    icon.png               120x120, card chip
    portrait.png           303x386, roster card
    full.png               up to 1200 px, detail window
  alesh/
    ...
```

File names and sizes are fixed; the game slug is the folder name. A folder
without `manifest.json` is valid — the loader probes `icon.png`, `portrait.png`
and `full.png`. The fallback order depends on the requested kind: for a roster
card `portrait → icon → full`, for the detail window `full → portrait → icon`.

## Lookup order

1. `--art-dir=<path>` command-line override (one run, not saved);
2. folder selected in **Settings → Operator art** (`%LOCALAPPDATA%\AkEfLedger\settings.json`, key `artDir`);
3. `%LOCALAPPDATA%\AkEfLedger\art\operators` — downloaded pack;
4. `assets\operators` next to `AkEfLedger.exe` — portable build;
5. `data\art_cache\<char_id>\{icon,full}.png` — automatic cache from Skport
   artwork URLs, written by `snapshot.py`;
6. embedded Vesper and the letter fallback.

If a folder is empty or has no `icon/portrait/full.png`, it is skipped.

## API art cache

`snapshot.py` also caches the official `avatar_url` and `illustration_url` from
the API response:

```text
data/art_cache/<char_id>/
  icon.png      # avatar_url, 144x144
  full.png      # illustration_url, up to 1200x1440
  source.json   # source URLs, used to skip unchanged files
```

This is a fallback for operators that are not in the curated art pack. The
curated pack is preferred: if an operator already has any of `icon`, `portrait`
or `full` in the pack, the app does not backfill the other kinds from the API.
The cache is local-only, never committed or published. Use `--no-art-cache` to
disable it or `--art-cache-dir <path>` to choose another cache directory.

The WPF app can also backfill this cache when an older snapshot is opened. This
is **opt-in and off by default**: enable **Settings → Operator art → Download
art automatically** first. The app checks the local pack/cache, then downloads
the missing images from the API URLs stored in that snapshot. On the roster
card, `portrait` falls back to `icon`, then `full`, then the letter.

Taking a snapshot with `snapshot.py` is a separate explicit action. From the
app, the script receives `--no-art-cache` unless **Download art automatically**
is enabled, so the opt-in covers both viewing and the in-app snapshot action.
`update_snapshot.cmd` and direct `snapshot.py` runs bypass this and may cache
art; pass `--no-art-cache` there explicitly.

## manifest.json

The manifest is optional metadata. It lets the app show the pack version and
add name aliases for operators that are not hardcoded in `OperatorArt.cs`.

```json
{
  "schema": 1,
  "version": "1",
  "generatedAt": "2026-11-24",
  "source": "endfield.gryphline.com snapshot",
  "operators": [
    {
      "slug": "akekuri",
      "charId": "a0591d65311b190e7d5e09faa0ed1cdd",
      "name": "Akekuri",
      "ru": "Акэкури",
      "en": "Akekuri",
      "files": {
        "icon":     { "w": 120, "h": 120, "bytes": 18734, "sha256": "..." },
        "portrait": { "w": 303, "h": 386, "bytes": 95421, "sha256": "..." },
        "full":     { "w": 1200, "h": 1293, "bytes": 411223, "sha256": "..." }
      }
    }
  ]
}
```

`charId`, `name`, `ru`, `en` and `files` are optional. Lookup tries the
display name and manifest aliases first, then `charId` — the stable API
character id. When `files[].sha256` is present, the installer verifies every
hash before replacing the active pack.

## Installing a pack

**Settings → Operator art** offers five actions:

| Action | What it does |
|---|---|
| Choose folder | Uses an existing folder in place (e.g. a site snapshot). Saves the path in `settings.json`. |
| Install from .zip | Unpacks `art-v1.zip` into `%LOCALAPPDATA%\AkEfLedger\art\operators`, verifies hashes, clears the custom folder. |
| Download | Downloads `art-v1.zip` from GitHub Releases, then installs it. |
| Open folder | Opens the active art folder in Explorer. |
| Remove | Clears the custom folder and deletes the downloaded pack. Never deletes the folder next to the exe. |

Installation is atomic: the archive is extracted into `operators.tmp`, checked,
and only then swapped with the active folder. A failed install keeps the old
pack.

## Building the pack

Source of truth is a local snapshot of the official site (not committed):

```text
design-references/Сайт/operators/<slug>/{icon,portrait,full}.png
```

```powershell
python tools/make_art_pack.py `
  --source "design-references/Сайт/operators" `
  --out "release/art-v1" `
  --version 1
```

Add `--catalog <path-to-catalog.json>` if you have a catalog of extra
operators: optional JSON (a list or `{"operators": [...]}`) with `slug`,
`charId`, `name`, `ru`, `en` fields. It makes aliases in the manifest work for
operators that are not in the built-in dictionary.

Output:

```text
release/art-v1/
  manifest.json
  operators/<slug>/{icon,portrait,full}.png
  art-v1.zip
  art-v1.zip.sha256
```

`full` is resized to 1200 px (enough for the 330×430 DIP detail box at 200%
DPI) and quantized to 256 colors. Measured on the 2026-11-23 snapshot:
34.07 MB → 12.45 MB; the whole pack is ~17.9 MB instead of 40 MB.

Use `--raw` to keep the original `full.png` files, and `--max-size` to change
the limit.

## Publishing the pack

1. Build `art-v1.zip` and check it locally.
2. Create a GitHub release with tag `art-v1` and upload `art-v1.zip`.
3. The **Download** button uses:

   ```text
   https://github.com/frostikfrostik8/ak-ef-ledger/releases/download/art-v1/art-v1.zip
   ```

The `art-v1` release is public: the **Download** button downloads the pack
directly from GitHub Releases.

GitHub Releases is transport, not a trust root: the pack format is versioned
and per-file hashes are verified. A future signature can be added on top
without changing the folder layout.

## Copyright

Operator art belongs to HYPERGRYPH / GRYPHLINE. The repository contains only
the AI-generated test operator **Vesper**; real art files are not committed.
The pack is published as the separate public `art-v1` release (owner's
decision).
