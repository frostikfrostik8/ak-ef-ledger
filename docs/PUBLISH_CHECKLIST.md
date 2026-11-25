# Public release checklist

The repository is **public** since v0.4.1; this list remains the release gate.

## Secrets and private data

- [ ] `git log --all -- .secrets data/aef.db data/snapshots context data/last_sync.json` returns nothing.
- [ ] No `*.token`, `endfield_token*`, account dumps or screenshots of private
      data in the repository.
- [ ] `data/seed.sql` is absent from HEAD and release zips; `git log --all -- data/seed.sql` returns nothing.
- [ ] Release zip contains no `data/aef.db`, no `.secrets` token, no
      `data/snapshots` and no `data/art_cache`.
- [ ] Real operator art files are not committed; only
      `src/AefAccount/Assets/Operators/vesper/` is present. Screenshots under
      `docs/screenshots/` are illustrative; art files themselves are not tracked.

## Build and release

- [ ] `build.cmd` passes on a clean Windows machine with .NET 10 SDK.
- [ ] GitHub Actions `build` workflow is green.
- [ ] `AK-EF-Ledger-v0.4.3-win-x64.zip` extracts, `init_db.cmd` creates the DB,
      `launch.cmd` opens the app, DEV profile works without a token.
- [ ] `lang/ru.json` and `lang/en.json` are present in the release package.
- [ ] Release package contains `assets\operators\vesper\` only; no real art.
- [ ] `AkEfLedger.exe` is ~5.8 MB and does not embed real operator art.
- [ ] Version in `SettingsView.xaml`, csproj and release tag match.
- [ ] `CHANGELOG.md` and release notes are up to date.

## Art pack

- [ ] `tools/make_art_pack.py --source <snapshot> --out <dir>` builds
      `art-v1.zip` and `art-v1.zip.sha256`.
- [ ] Release `art-v1` has the `art-v1.zip` asset; the Download button reaches
      it (or shows the manual-download fallback while private).
- [ ] Install from the downloaded `.zip` in Settings shows the pack version and
      operator count; Remove clears it.
- [ ] Roster banner disappears after install and returns after removal.
- [ ] Operator art is **not** attached to the public release (copyright).

## Docs

- [ ] `README.md` and `README.ru.md` render correctly on GitHub.
- [ ] Language links at the top work.
- [ ] `docs/art-pack.md` matches the actual manifest and folder layout.
- [ ] `LICENSE` and `THIRD-PARTY-NOTICES.md` are present.
- [ ] `SECURITY.md` explains token handling.
- [ ] Screenshots (if added) contain no account names, tokens or private data.

## Visibility

- [ ] Owner explicitly approves making the repository public.
- [ ] `gh repo edit frostikfrostik8/ak-ef-ledger --visibility public --accept-visibility-change-consequences`
- [ ] Release v0.4.3 is the latest public release.
- [ ] Watch issues/security advisories after publication.

## After publication

- [ ] Add issue/PR templates if outside contributions are welcome.
- [x] Add screenshots to README.
- [x] Art pack is published as the separate public `art-v1` release (owner
      approved). The main app release contains no art files.
- [ ] Consider GitHub Pages for the user guide.
