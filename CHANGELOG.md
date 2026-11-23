# Changelog

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
