#!/usr/bin/env python3
"""AEF — небольшая CLI-обёртка над data/aef.db."""

import argparse
import sqlite3
import sys
from datetime import date
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DB_PATH = ROOT / "data" / "aef.db"
SCHEMA_PATH = ROOT / "data" / "schema.sql"
SEED_PATH = ROOT / "data" / "seed.sql"


def connect() -> sqlite3.Connection:
    DB_PATH.parent.mkdir(parents=True, exist_ok=True)
    con = sqlite3.connect(DB_PATH)
    con.row_factory = sqlite3.Row
    return con


def cmd_init(_args) -> None:
    con = connect()
    con.executescript(SCHEMA_PATH.read_text(encoding="utf-8"))
    if SEED_PATH.exists():
        con.executescript(SEED_PATH.read_text(encoding="utf-8"))
    con.commit()
    print(f"ok: {DB_PATH}")


def cmd_roster(args) -> None:
    con = connect()
    where = "owned = 0" if args.missing else "owned = 1"
    rows = con.execute(
        "SELECT name, rarity, element, class, potential FROM operators "
        f"WHERE {where} ORDER BY rarity DESC, potential DESC, name"
    )
    for r in rows:
        print(
            f"{r['name']:<18} {r['rarity']}★  "
            f"{(r['element'] or '-'):<9} {(r['class'] or '-'):<10} P{r['potential']}"
        )


def cmd_banners(_args) -> None:
    con = connect()
    rows = con.execute(
        "SELECT version, name, type, start_date, end_date, featured FROM banners "
        "ORDER BY start_date"
    )
    for r in rows:
        print(
            f"{r['start_date']} → {(r['end_date'] or '?'):<10} "
            f"[{r['version']}] {r['name']} — {r['featured']} ({r['type']})"
        )


def cmd_sql(args) -> None:
    con = connect()
    rows = con.execute(args.query).fetchall()
    if not rows:
        print("(пусто)")
        return
    print(" | ".join(rows[0].keys()))
    for r in rows:
        print(" | ".join("" if v is None else str(v) for v in r))


def cmd_pull(args) -> None:
    con = connect()
    con.execute(
        "INSERT INTO pulls (date, banner, count, currency, result, notes) "
        "VALUES (?, ?, ?, ?, ?, ?)",
        (args.date, args.banner, args.count, args.currency, args.result, args.notes),
    )
    con.commit()
    print("ok: запись добавлена")


def cmd_note(args) -> None:
    con = connect()
    con.execute(
        "INSERT INTO notes (date, topic, body) VALUES (?, ?, ?)",
        (args.date, args.topic, args.body),
    )
    con.commit()
    print("ok: заметка добавлена")


def main() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    p = argparse.ArgumentParser(prog="aef", description="AEF helper")
    sub = p.add_subparsers(dest="cmd", required=True)

    sub.add_parser("init", help="создать/обновить базу").set_defaults(func=cmd_init)

    r = sub.add_parser("roster", help="список операторов")
    r.add_argument("--missing", action="store_true", help="только отсутствующие")
    r.set_defaults(func=cmd_roster)

    sub.add_parser("banners", help="баннеры").set_defaults(func=cmd_banners)

    s = sub.add_parser("sql", help="произвольный SQL")
    s.add_argument("query")
    s.set_defaults(func=cmd_sql)

    pl = sub.add_parser("pull", help="добавить запись о крутках")
    pl.add_argument("--date", required=True)
    pl.add_argument("--banner", required=True)
    pl.add_argument("--count", type=int, required=True)
    pl.add_argument("--currency", default="oroberyl")
    pl.add_argument("--result", default="")
    pl.add_argument("--notes", default="")
    pl.set_defaults(func=cmd_pull)

    n = sub.add_parser("note", help="добавить заметку")
    n.add_argument("--date", default=None)
    n.add_argument("--topic", required=True)
    n.add_argument("--body", required=True)
    n.set_defaults(func=cmd_note)

    args = p.parse_args()
    if args.cmd == "note" and not args.date:
        args.date = date.today().isoformat()
    args.func(args)


if __name__ == "__main__":
    main()
