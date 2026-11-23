#!/usr/bin/env python3
"""Синхронизация аккаунта Arknights: Endfield (Skport/Gryphline) в data/aef.db.

ВАЖНО: ACCOUNT_TOKEN — это полный доступ к аккаунту. Не вставляй его в чаты,
не давай сайтам и не коммить в git. Скрипт ходит только на as.gryphline.com
и zonai.skport.com.

Использование:
  python scripts\\sync.py --dry-run
  python scripts\\sync.py
  python scripts\\sync.py --from-json data\\last_sync.json --db data\\test.db
"""

from __future__ import annotations

import argparse
import hashlib
import hmac
import json
import os
import sqlite3
import sys
import time
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DB_PATH = ROOT / "data" / "aef.db"
SCHEMA_PATH = ROOT / "data" / "schema.sql"
SECRETS_PATH = ROOT / ".secrets" / "endfield_token"
TOKEN_CANDIDATES = [
    SECRETS_PATH,
    ROOT / ".secrets" / "endfield_token.txt",
    ROOT / ".secrets" / "token",
    ROOT / ".secrets" / "token.txt",
    ROOT / "token.txt",
]
LAST_JSON = ROOT / "data" / "last_sync.json"
# Старый простой синк больше не трогает основной отчёт snapshot.py.
MD_PATH = ROOT / "context" / "account-sync-legacy.md"

APP_CODE = "6eb76d4e13aa36e6"
PLATFORM = "3"
VNAME = "1.0.0"
UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36"
)


def http_json(url: str, payload: dict | None = None, headers: dict | None = None) -> dict:
    data = None if payload is None else json.dumps(payload).encode("utf-8")
    h = {"User-Agent": UA, "Accept": "*/*"}
    if payload is not None:
        h["Content-Type"] = "application/json"
    if headers:
        h.update(headers)
    req = urllib.request.Request(url, data=data, headers=h, method="POST" if data else "GET")
    with urllib.request.urlopen(req, timeout=30) as resp:
        raw = resp.read().decode("utf-8", "replace")
    return json.loads(raw)


def make_sign(sign_token: str, path: str, body: str = "") -> tuple[str, str]:
    ts = str(int(time.time()))
    header = {"platform": PLATFORM, "timestamp": ts, "dId": "", "vName": VNAME}
    header_str = json.dumps(header, separators=(",", ":"))
    digest = hmac.new(
        sign_token.encode("utf-8"),
        (path + body + ts + header_str).encode("utf-8"),
        hashlib.sha256,
    ).hexdigest()
    return hashlib.md5(digest.encode("utf-8")).hexdigest(), ts


def grant(token: str) -> str:
    d = http_json(
        "https://as.gryphline.com/user/oauth2/v2/grant",
        {"appCode": APP_CODE, "token": token, "type": 0},
        {"x-language": "en"},
    )
    if d.get("status") != 0:
        raise RuntimeError(f"grant: {d.get('msg') or d.get('message')}")
    return d["data"]["code"]


def cred(code: str) -> tuple[str, str]:
    d = http_json(
        "https://zonai.skport.com/web/v1/user/auth/generate_cred_by_code",
        {"code": code, "kind": 1},
    )
    if d.get("code") != 0:
        raise RuntimeError(f"cred: {d.get('message')}")
    return d["data"]["cred"], d["data"]["token"]


def binding(cred_v: str, sign_token: str) -> dict:
    path = "/api/v1/game/player/binding"
    sign, ts = make_sign(sign_token, path, "")
    d = http_json(
        "https://zonai.skport.com" + path,
        None,
        {
            "cred": cred_v,
            "platform": PLATFORM,
            "vName": VNAME,
            "timestamp": ts,
            "sign": sign,
            "Content-Type": "application/json",
        },
    )
    if d.get("code") != 0:
        raise RuntimeError(f"binding: {d.get('message')}")
    for app in d["data"]["list"]:
        if app.get("appCode") == "endfield" and app.get("bindingList"):
            b = app["bindingList"][0]
            return b.get("defaultRole") or b["roles"][0]
    raise RuntimeError("нет привязанной роли Endfield")


def skport_user_id(cred_v: str, sign_token: str) -> str | None:
    path = "/web/v1/wiki/me"
    sign, ts = make_sign(sign_token, path, "")
    try:
        d = http_json(
            "https://zonai.skport.com" + path,
            None,
            {
                "cred": cred_v,
                "timestamp": ts,
                "vname": VNAME,
                "sign": sign,
                "sk-language": "en",
                "platform": PLATFORM,
            },
        )
    except Exception:
        return None
    if d.get("code") == 0:
        return ((d.get("data") or {}).get("user") or {}).get("userId")
    return None


def card_detail(cred_v: str, sign_token: str, role_id: str, server_id: str, user_id: str | None) -> dict:
    path = "/api/v1/game/endfield/card/detail"
    params = {"roleId": str(role_id), "serverId": str(server_id)}
    if user_id:
        params["userId"] = str(user_id)
    query = urllib.parse.urlencode(params)
    sign, ts = make_sign(sign_token, path, query)
    d = http_json(
        f"https://zonai.skport.com{path}?{query}",
        None,
        {
            "cred": cred_v,
            "sk-language": "en",
            "timestamp": ts,
            "sign": sign,
            "vname": VNAME,
            "sk-game-role": f"3_{role_id}_{server_id}",
            "platform": PLATFORM,
            "Origin": "https://game.skport.com",
            "Referer": "https://game.skport.com/",
        },
    )
    if d.get("code") != 0:
        raise RuntimeError(f"card/detail: {d.get('message')}")
    return d["data"]["detail"]


def val(x):
    if isinstance(x, dict):
        return x.get("value", x.get("name", x.get("key")))
    return x


def gear_data(g) -> dict:
    if isinstance(g, dict):
        return g.get("equipData") or g.get("tacticalItemData") or {}
    return {}


def gear_name(g):
    if isinstance(g, dict):
        return gear_data(g).get("name") or g.get("name") or g.get("id")
    return g if isinstance(g, str) else None


def parse_chars(detail: dict) -> list[dict]:
    out = []
    for ch in detail.get("chars") or []:
        cd = ch.get("charData") or {}
        name = cd.get("name") or ch.get("id")
        if not name:
            continue
        rarity = val(cd.get("rarity"))
        if isinstance(rarity, str):
            s = rarity.strip()
            if s.startswith("rarity_"):
                s = s.split("_")[-1]
            rarity = int(s) if s.isdigit() else None
        w = ch.get("weapon") or {}
        wd = w.get("weaponData") or {}
        gear = {}
        for key, slot in (
            ("bodyEquip", "body"),
            ("armEquip", "arm"),
            ("firstAccessory", "acc1"),
            ("secondAccessory", "acc2"),
            ("tacticalItem", "tactical"),
        ):
            g = ch.get(key)
            gname = gear_name(g)
            if gname:
                gd = gear_data(g)
                lv = gd.get("level")
                level = None
                if isinstance(lv, dict):
                    v = lv.get("value")
                    level = int(v) if isinstance(v, str) and v.isdigit() else v
                elif isinstance(lv, (int, str)):
                    level = lv
                gear[slot] = {
                    "name": gname,
                    "level": level,
                    "enhance": (
                        json.dumps(g.get("enhance"), ensure_ascii=False)
                        if isinstance(g, dict) and g.get("enhance")
                        else None
                    ),
                }
        out.append(
            {
                "name": name,
                "rarity": rarity,
                "element": val(cd.get("property")),
                "class": val(cd.get("profession")),
                "potential": int(ch.get("potentialLevel") or 0),
                "level": int(ch.get("level") or 0),
                "evolve": int(ch.get("evolvePhase") or 0),
                "weapon": wd.get("name"),
                "weapon_rarity": val(wd.get("rarity")),
                "weapon_refine": w.get("refineLevel"),
                "weapon_level": w.get("level"),
                "weapon_breakthrough": w.get("breakthroughLevel"),
                "gear": gear,
            }
        )
    return out


def sync(rows: list[dict], detail: dict, db_path: Path, prune: bool = False) -> None:
    con = sqlite3.connect(db_path)
    con.executescript(SCHEMA_PATH.read_text(encoding="utf-8"))
    cols = [row[1] for row in con.execute("PRAGMA table_info(gear)")]
    if "enhance" not in cols:
        con.execute("ALTER TABLE gear ADD COLUMN enhance TEXT")
    for r in rows:
        con.execute(
            """INSERT INTO operators (name, rarity, element, class, potential, owned)
               VALUES (?, ?, ?, ?, ?, 1)
               ON CONFLICT(name) DO UPDATE SET
                 rarity=excluded.rarity,
                 element=excluded.element,
                 class=excluded.class,
                 potential=excluded.potential,
                 owned=1""",
            (r["name"], r["rarity"], r["element"], r["class"], r["potential"]),
        )
        if r["weapon"]:
            con.execute(
                """INSERT INTO weapons (operator, name, rarity, level, refine, breakthrough)
                   VALUES (?, ?, ?, ?, ?, ?)
                   ON CONFLICT(operator, name) DO UPDATE SET
                     rarity=excluded.rarity, level=excluded.level,
                     refine=excluded.refine, breakthrough=excluded.breakthrough""",
                (
                    r["name"],
                    r["weapon"],
                    r["weapon_rarity"],
                    r["weapon_level"],
                    r["weapon_refine"],
                    r["weapon_breakthrough"],
                ),
            )
        for slot, g in r["gear"].items():
            con.execute(
                """INSERT INTO gear (operator, slot, name, level, enhance)
                   VALUES (?, ?, ?, ?, ?)
                   ON CONFLICT(operator, slot) DO UPDATE SET
                     name=excluded.name, level=excluded.level, enhance=excluded.enhance""",
                (r["name"], slot, g["name"], g["level"], g.get("enhance")),
            )
    if prune and rows:
        placeholders = ",".join("?" for _ in rows)
        con.execute(
            f"UPDATE operators SET owned=0 WHERE name NOT IN ({placeholders})",
            [r["name"] for r in rows],
        )
    base = detail.get("base") or {}
    note = f"{base.get('name', '?')} · world {base.get('worldLevel', '?')} · chars {len(rows)}"
    con.execute(
        "INSERT INTO sync_log (ts, source, chars, note) VALUES (?, ?, ?, ?)",
        (datetime.now(timezone.utc).isoformat(timespec="seconds"), "skport", len(rows), note),
    )
    con.commit()
    con.close()


def write_md(rows: list[dict], detail: dict) -> None:
    base = detail.get("base") or {}
    lines = [
        "# Синхронизация аккаунта (API)",
        "",
        f"Обновлено: {datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M UTC')}",
        f"Игрок: {base.get('name', '?')} · world level {base.get('worldLevel', '?')} · "
        f"операторов {base.get('charNum', '?')} · оружия {base.get('weaponNum', '?')}",
        "",
        "| Оператор | Редкость | Элемент | Класс | Пот | Ур. | Оружие | Рефайн |",
        "|---|---|---|---|---|---|---|---|",
    ]
    for r in sorted(rows, key=lambda x: (-(x["rarity"] or 0), -x["potential"], x["name"])):
        lines.append(
            f"| {r['name']} | {r['rarity'] or '?'}★ | {r['element'] or '-'} | "
            f"{r['class'] or '-'} | P{r['potential']} | {r['level']} | "
            f"{r['weapon'] or '-'} | {r['weapon_refine'] if r['weapon_refine'] is not None else '-'} |"
        )
    MD_PATH.write_text("\n".join(lines) + "\n", encoding="utf-8")


def read_text_any(path: Path) -> str:
    data = path.read_bytes()
    if data.startswith(b"\xff\xfe") or data.startswith(b"\xfe\xff"):
        return data.decode("utf-16")
    if data.startswith(b"\xef\xbb\xbf"):
        return data.decode("utf-8-sig")
    try:
        return data.decode("utf-8")
    except UnicodeDecodeError:
        return data.decode("cp1251", errors="replace")


def normalize_token(raw: str) -> str:
    s = raw.strip().lstrip("\ufeff").strip()
    if len(s) >= 2 and s[0] == s[-1] and s[0] in "\"'":
        s = s[1:-1].strip()
    try:
        j = json.loads(s)
    except Exception:
        return s
    if isinstance(j, dict):
        for path in (("data", "content"), ("data", "token"), ("token",), ("content",)):
            cur = j
            for key in path:
                cur = cur.get(key) if isinstance(cur, dict) else None
            if isinstance(cur, str) and cur.strip():
                return cur.strip()
    if isinstance(j, str) and j.strip():
        return j.strip()
    return s


def resolve_token(args) -> tuple[str, str]:
    if args.token:
        return normalize_token(args.token), "--token"
    if args.token_file:
        p = Path(args.token_file)
        return normalize_token(read_text_any(p)), str(p)
    env = os.environ.get("ENDFIELD_TOKEN")
    if env and env.strip():
        return normalize_token(env), "ENDFIELD_TOKEN"
    for p in TOKEN_CANDIDATES:
        if p.exists():
            token = normalize_token(read_text_any(p))
            if token:
                return token, str(p)
    raise SystemExit(
        "Нет токена. Положи его в один из файлов:\n  "
        + "\n  ".join(str(p) for p in TOKEN_CANDIDATES)
        + "\nили задай --token-file / ENDFIELD_TOKEN."
    )


def main() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    print("note: sync.py — старый разовый синк; история и динамика — scripts\\snapshot.py")

    ap = argparse.ArgumentParser(prog="sync", description="Синхронизация аккаунта Endfield")
    ap.add_argument("--token", help="ACCOUNT_TOKEN (не рекомендуется: попадёт в историю)")
    ap.add_argument("--token-file", help="файл с токеном")
    ap.add_argument("--from-json", help="разобрать сохранённый detail JSON без сети")
    ap.add_argument("--db", default=str(DB_PATH), help="путь к SQLite")
    ap.add_argument("--dry-run", action="store_true", help="только показать, не писать")
    ap.add_argument("--prune", action="store_true", help="отметить отсутствующих как не полученных")
    ap.add_argument("--save-json", action="store_true", help="сохранить сырой detail в data/last_sync.json")
    ap.add_argument("--no-md", action="store_true", help="не писать context/account-sync-legacy.md")
    args = ap.parse_args()

    if args.from_json:
        detail = json.loads(Path(args.from_json).read_text(encoding="utf-8"))
        if isinstance(detail, dict) and "detail" in detail:
            detail = detail["detail"]
    else:
        token, src = resolve_token(args)
        print(f"токен: {src} ({len(token)} симв.)")
        code = grant(token)
        cred_v, sign_token = cred(code)
        role = binding(cred_v, sign_token)
        role_id = str(role.get("roleId"))
        server_id = str(role.get("serverId"))
        user_id = skport_user_id(cred_v, sign_token)
        print(f"роль: {role.get('nickname', '?')} ({role_id} / {server_id})")
        detail = card_detail(cred_v, sign_token, role_id, server_id, user_id)
        if args.save_json:
            LAST_JSON.write_text(json.dumps(detail, ensure_ascii=False, indent=2), encoding="utf-8")
            print(f"сырой ответ: {LAST_JSON}")

    rows = parse_chars(detail)
    base = detail.get("base") or {}
    print(f"игрок: {base.get('name', '?')} · операторов в ответе: {len(rows)}")

    for r in sorted(rows, key=lambda x: (-(x["rarity"] or 0), -x["potential"], x["name"])):
        print(
            f"  {r['name']:<18} {r['rarity'] or '?'}★  P{r['potential']}  "
            f"ур.{r['level']}  {r['weapon'] or '-'}"
            + (f" R{r['weapon_refine']}" if r["weapon_refine"] is not None else "")
        )

    if args.dry_run:
        print("dry-run: база не изменена")
        return

    sync(rows, detail, Path(args.db), prune=args.prune)
    if not args.no_md:
        write_md(rows, detail)
        print(f"ok: {MD_PATH}")
    print(f"ok: {args.db}")


if __name__ == "__main__":
    main()
