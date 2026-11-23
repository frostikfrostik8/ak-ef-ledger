# Security

## Token

`AK:EF Ledger` uses a personal Skport token to download account snapshots.
The token grants full access to the account.

- Store it only in `.secrets/endfield_token` or `.secrets/endfield_token.txt`.
- Never paste it into an issue, pull request, screenshot, chat or third-party
  website.
- `.secrets/` is gitignored in this repository and in the release package.
- The app reads the token only through `scripts/snapshot.py`; the WPF UI never
  loads or displays it.

## Data

`data/aef.db`, `data/snapshots/` and `context/account-*.md` contain private
account data. They are gitignored. Do not attach them to public issues.

## Network

The app itself makes no network requests. `scripts/snapshot.py` contacts only
the official Skport API, and only when the user presses “Снять снимок” or runs
`update_snapshot.cmd`.

## Reporting

If you find a security issue, open a private security advisory in the GitHub
repository rather than a public issue. Do not include tokens, account dumps or
personal data in the report.
