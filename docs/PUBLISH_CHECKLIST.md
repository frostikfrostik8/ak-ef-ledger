# Public release checklist

The repository is intentionally **private** until this list is done and the
owner approves the visibility switch.

## Secrets and private data

- [ ] `git log --all -- .secrets data/aef.db data/snapshots` returns nothing.
- [ ] No `*.token`, `endfield_token*`, account dumps or screenshots of private
      data in the repository.
- [ ] Release zip contains no `data/aef.db`, no `.secrets` token and no
      `data/snapshots`.
- [ ] Real operator art is not committed; only
      `src/AefAccount/Assets/Operators/vesper/` is present.

## Build and release

- [ ] `build.cmd` passes on a clean Windows machine with .NET 10 SDK.
- [ ] GitHub Actions `build` workflow is green.
- [ ] `AK-EF-Ledger-v0.3.1-win-x64.zip` extracts, `init_db.cmd` creates the DB,
      `launch.cmd` opens the app, DEV profile works without a token.
- [ ] `lang/ru.json` and `lang/en.json` are present in the release package.
- [ ] Version in `SettingsView.xaml`, csproj and release tag match.
- [ ] `CHANGELOG.md` and release notes are up to date.

## Docs

- [ ] `README.md` and `README.ru.md` render correctly on GitHub.
- [ ] Language links at the top work.
- [ ] `LICENSE` and `THIRD-PARTY-NOTICES.md` are present.
- [ ] `SECURITY.md` explains token handling.
- [ ] Screenshots (if added) contain no account names, tokens or private data.

## Visibility

- [ ] Owner explicitly approves making the repository public.
- [ ] `gh repo edit frostikfrostik8/ak-ef-ledger --visibility public --accept-visibility-change-consequences`
- [ ] Release v0.3.1 is changed from prerelease to latest, if appropriate.
- [ ] Watch issues/security advisories after publication.

## After publication

- [ ] Add issue/PR templates if outside contributions are welcome.
- [ ] Add screenshots to README.
- [ ] Decide whether to publish a public release with real operator art
      (not recommended: copyright) or keep the art local-only.
- [ ] Consider GitHub Pages for the user guide.

