# AK:EF Ledger v0.4.1 — key onboarding

## English

Minor UI release focused on the first launch and the account key.

- **First-run access key screen** in the Endfield UI style: masked input (`*`),
  a "never share your key" note, links to `skport.com` and the token page.
- The prompt appears when the token is missing at startup, when the token file
  is deleted while the app runs, and before a snapshot without a key.
- **Save** writes `.secrets/endfield_token.txt` atomically (or the active token
  file used by `snapshot.py`); **Exit** closes the app.
- Settings → About: repository link below the official site, creator
  **frostikfrostik8**, GitHub profile and Discord handle (click to copy).
- Sidebar: repository link.
- App version is **0.4.1**.

No database or snapshot format changes. The app itself still sends nothing to
the internet; only `snapshot.py` talks to the Skport API.

## Русский

Минорный UI-релиз про первый запуск и ключ аккаунта.

- **Экран ввода ключа при первом запуске** в стиле Endfield: поле с маской
  (`*`), пометка «никому не сообщайте ключ», ссылки на `skport.com` и страницу
  токена.
- Экран появляется, если ключа нет при старте, если файл ключа исчез во время
  работы приложения, и перед снятием снимка без ключа.
- **Сохранить** атомарно пишет `.secrets/endfield_token.txt` (или активный
  файл, который использует `snapshot.py`); **Выйти** закрывает приложение.
- Настройки → «О приложении»: ссылка на репозиторий под официальным сайтом,
  создатель **frostikfrostik8**, кнопка GitHub и ник Discord (клик копирует).
- Сайдбар: ссылка на репозиторий.
- Версия приложения — **0.4.1**.

Формат базы и снимков не менялся. Само приложение по-прежнему ничего не
отправляет в интернет; со Skport API общается только `snapshot.py`.
