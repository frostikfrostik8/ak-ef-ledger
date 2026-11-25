# Security

## Token

`AK:EF Ledger` uses a personal Skport token to download account snapshots.
The token grants full access to the account.

- Store it only in `.secrets/` (the active file is one of
  `endfield_token`, `endfield_token.txt`, `token`, `token.txt`; the last
  candidate is the legacy root `token.txt`).
- Never paste it into an issue, pull request, screenshot, chat or third-party
  website.
- `.secrets/`, the root `token.txt` and `*.token` are gitignored in this
  repository and in the release package.
- The WPF UI never displays the token and never sends it over the network.
  `TokenService` reads the active file only to check that it is non-empty, and
  saving writes the masked input to that same file. `scripts/snapshot.py` is
  the only code that passes the token to the official Skport API.

## Data

`data/*.db`, `data/snapshots/`, `data/last_sync.json`, `context/account-*.md`
and `context/` contain private account data. They are gitignored. Do not
attach them to public issues.

## Network

The app's only network request is the optional **Download** button for the art
pack (`art-v1.zip` from GitHub Releases). It runs only after an explicit click.
`scripts/snapshot.py` contacts only the official Skport API, and only when the
user presses “Снять снимок” or runs `update_snapshot.cmd`. There is no
telemetry.

## Reporting

If you find a security issue, open a private security advisory in the GitHub
repository rather than a public issue. Do not include tokens, account dumps or
personal data in the report.
