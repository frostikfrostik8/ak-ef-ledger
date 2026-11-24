#!/usr/bin/env python3
"""Снимки аккаунта Arknights: Endfield.

Один запуск = один снимок: тянет card/detail (и безопасные GET-эндпоинты),
кладёт сырой ответ в data/snapshots/*.json.gz, раскладывает данные в SQLite
и обновляет человекочитаемые отчёты.

Токен = полный доступ к аккаунту. Скрипт ходит только на as.gryphline.com
и zonai.skport.com, ничего не отправляет в POST, кроме OAuth-обмена.

Примеры:
  python scripts\\snapshot.py                 # снять снимок
  python scripts\\snapshot.py --dry-run       # показать, не писать
  python scripts\\snapshot.py history         # история снимков
  python scripts\\snapshot.py diff            # последние два снимка
  python scripts\\snapshot.py show latest --full
  python scripts\\snapshot.py versions
"""

from __future__ import annotations

import argparse
import gzip
import hashlib
import json
import re
import sqlite3
import sys
import time
import urllib.error
from datetime import date, datetime, timedelta, timezone
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
ROOT = SCRIPT_DIR.parent
sys.path.insert(0, str(SCRIPT_DIR))
import sync  # noqa: E402

DB_PATH = ROOT / "data" / "aef.db"
SCHEMA_PATH = ROOT / "data" / "schema.sql"
SNAP_DIR = ROOT / "data" / "snapshots"
LATEST_MD = ROOT / "context" / "account-sync.md"
HISTORY_MD = ROOT / "context" / "account-history.md"
VERSION_FILE = ROOT / "data" / "game_version.txt"
SERVER_TZ = timezone(timedelta(hours=8))

GEAR_SLOTS = (
    ("bodyEquip", "body"),
    ("armEquip", "arm"),
    ("firstAccessory", "acc1"),
    ("secondAccessory", "acc2"),
    ("tacticalItem", "tactical"),
)
SLOT_LABELS = {
    "body": "Тело",
    "arm": "Руки",
    "acc1": "Аксессуар 1",
    "acc2": "Аксессуар 2",
    "tactical": "Тактика",
}
TALENT_LABELS = {
    "latestBreakNode": "прорыв",
    "latestPassiveSkillNodes": "пассивные навыки",
    "latestFactorySkillNodes": "фабрика",
    "latestSpaceshipSkillNodes": "корабль",
}
MODELED_SECTIONS = {"base", "chars", "achieve", "domain", "spaceShip"}
EXTRA_ENDPOINTS = (
    ("enums", "/web/v1/game/endfield/enums"),
    ("user", "/web/v2/user"),
    ("attendance", "/web/v1/game/endfield/attendance"),
    ("char_pool", "/web/v1/wiki/char-pool"),
    ("weapon_pool", "/web/v1/wiki/weapon-pool"),
    ("user_game_data", "/web/v1/game/endfield/team/user-game-data"),
)
METRIC_LABELS = {
    "puzzle": "головоломки",
    "chest": "сундуки",
    "equip_chest": "снаряж. сундуки",
    "piece": "детали",
    "blackbox": "чёрные ящики",
    "trstar": "trstar",
}


# ---------------------------------------------------------------------------
# Утилиты
# ---------------------------------------------------------------------------

def reconfigure_stdout() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def now_utc() -> datetime:
    return datetime.now(timezone.utc).replace(microsecond=0)


def iso_utc(dt: datetime) -> str:
    return dt.astimezone(timezone.utc).replace(microsecond=0).isoformat()


def iso_server(dt: datetime) -> str:
    return dt.astimezone(SERVER_TZ).replace(microsecond=0).isoformat()


def parse_iso(value: str | None) -> datetime | None:
    if not value:
        return None
    try:
        dt = datetime.fromisoformat(str(value).replace("Z", "+00:00"))
    except ValueError:
        return None
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt


def parse_taken_at(value: str | None) -> datetime:
    if not value:
        return now_utc()
    dt = parse_iso(value)
    if dt is None:
        raise SystemExit(f"Не понял --taken-at: {value!r}. Нужен ISO, например 2026-11-23T19:30:00+08:00")
    return dt.astimezone(timezone.utc).replace(microsecond=0)


def epoch_to_dt(value) -> datetime | None:
    n = as_int(value)
    if not n or n <= 0:
        return None
    try:
        return datetime.fromtimestamp(n, timezone.utc)
    except (OverflowError, OSError, ValueError):
        return None


def epoch_to_iso(value) -> str | None:
    dt = epoch_to_dt(value)
    return iso_utc(dt) if dt else None


def as_int(value) -> int | None:
    if value is None or isinstance(value, bool):
        return None
    if isinstance(value, int):
        return value
    if isinstance(value, float):
        return int(value)
    s = str(value).strip()
    if not s:
        return None
    try:
        return int(float(s))
    except ValueError:
        return None


def as_num(value) -> int | float | None:
    if value is None or isinstance(value, bool):
        return None
    if isinstance(value, (int, float)):
        return value
    s = str(value).strip().replace(",", "")
    if not s:
        return None
    try:
        return int(s)
    except ValueError:
        try:
            return float(s)
        except ValueError:
            return None


def json_dumps(value) -> str | None:
    if value is None:
        return None
    return json.dumps(value, ensure_ascii=False, sort_keys=True)


def jloads(value, default=None):
    if value is None or value == "":
        return default
    if isinstance(value, (dict, list)):
        return value
    try:
        return json.loads(value)
    except (TypeError, ValueError, json.JSONDecodeError):
        return default


def fmt_num(value) -> str:
    if value is None:
        return "—"
    if isinstance(value, float) and value.is_integer():
        value = int(value)
    if isinstance(value, int):
        return f"{value:,}".replace(",", " ")
    return str(value)


def fmt_ratio(a, b) -> str:
    if a is None and b is None:
        return "—"
    return f"{fmt_num(a)}/{fmt_num(b)}"


def fmt_value(value) -> str:
    if value is None:
        return "—"
    return fmt_num(value) if isinstance(value, (int, float)) else str(value)


def fmt_iso_utc(value: str | None) -> str:
    dt = parse_iso(value)
    return dt.astimezone(timezone.utc).strftime("%Y-%m-%d %H:%M UTC") if dt else "—"


def fmt_iso_server(value: str | None) -> str:
    dt = parse_iso(value)
    return dt.astimezone(SERVER_TZ).strftime("%Y-%m-%d %H:%M UTC+8") if dt else "—"


def fmt_short_server(value: str | None) -> str:
    dt = parse_iso(value)
    return dt.astimezone(SERVER_TZ).strftime("%Y-%m-%d %H:%M") if dt else "—"


def fmt_epoch(value) -> str:
    dt = epoch_to_dt(value)
    return dt.strftime("%Y-%m-%d %H:%M UTC") if dt else "—"


def md_cell(value) -> str:
    if value is None:
        return "—"
    return str(value).replace("|", "\\|").replace("\n", " ").strip() or "—"


def rel_path(path: Path) -> str:
    try:
        return str(path.resolve().relative_to(ROOT.resolve())).replace("\\", "/")
    except ValueError:
        return str(path)


def safe_version(value: str) -> str:
    cleaned = re.sub(r"[^0-9A-Za-z._-]+", "-", value or "unknown").strip("-")
    return cleaned or "unknown"


def normalize_enhance(value):
    parsed = jloads(value, value)
    if parsed is None:
        return None
    if isinstance(parsed, (dict, list)) and not parsed:
        return None
    if isinstance(parsed, str) and not parsed.strip():
        return None
    return parsed


def fmt_enhance(value) -> str:
    parsed = normalize_enhance(value)
    if parsed is None:
        return "—"
    if isinstance(parsed, dict):
        if not parsed:
            return "—"
        keys = [str(key) for key in parsed]
        if all(key.isdigit() and 1 <= int(key) <= 3 for key in keys):
            return "/".join(str(parsed.get(str(index), parsed.get(index, 0))) for index in (1, 2, 3))
        return "/".join(str(parsed[key]) for key in sorted(parsed, key=lambda item: str(item)))
    if isinstance(parsed, list):
        return "/".join(str(x) for x in parsed) if parsed else "—"
    text = str(parsed).strip()
    return text or "—"


def fmt_gear_cell(gear: dict | None) -> str:
    if not gear:
        return "—"
    name = gear.get("name") or "?"
    parts = [name]
    if gear.get("level") is not None:
        parts.append(f"ур.{gear['level']}")
    enhance = fmt_enhance(gear.get("enhance"))
    if enhance != "—":
        parts.append(f"энч.{enhance}")
    return " · ".join(parts)


def version_source_label(source: str | None) -> str:
    if not source:
        return "не определена"
    if source == "cli":
        return "аргумент --version"
    if source.startswith("file:"):
        return f"файл {source[5:]}"
    if source == "calendar":
        return "календарь версий"
    if source == "banners":
        return "даты баннеров"
    if source == "meta":
        return "метаданные файла"
    return source


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


def read_version_file(path: Path) -> str:
    if not path.exists():
        return ""
    text = read_text_any(path)
    for line in text.splitlines():
        line = line.strip()
        if line and not line.startswith("#"):
            return line
    return ""


# ---------------------------------------------------------------------------
# Сеть
# ---------------------------------------------------------------------------

def with_retry(fn, attempts: int = 3, delay: float = 1.5):
    last = None
    for attempt in range(attempts):
        try:
            return fn()
        except urllib.error.HTTPError as exc:
            if exc.code in (401, 403):
                raise
            last = exc
        except (urllib.error.URLError, TimeoutError, OSError) as exc:
            last = exc
        if attempt < attempts - 1:
            time.sleep(delay * (attempt + 1))
    raise last  # type: ignore[misc]


def api_get(cred_v: str, sign_token: str, path: str, extra_headers: dict | None = None) -> dict:
    sign, ts = sync.make_sign(sign_token, path, "")
    headers = {
        "cred": cred_v,
        "timestamp": ts,
        "sign": sign,
        "vName": sync.VNAME,
        "platform": sync.PLATFORM,
        "sk-language": "en",
        "Origin": "https://game.skport.com",
        "Referer": "https://game.skport.com/",
    }
    if extra_headers:
        headers.update(extra_headers)
    return sync.http_json("https://zonai.skport.com" + path, None, headers)


def fetch_extras(cred_v: str, sign_token: str, role_id: str, server_id: str) -> tuple[dict, dict]:
    extras: dict = {}
    errors: dict = {}
    for name, path in EXTRA_ENDPOINTS:
        headers = {}
        if name in ("attendance", "user_game_data"):
            headers["sk-game-role"] = f"3_{role_id}_{server_id}"
        try:
            response = api_get(cred_v, sign_token, path, headers)
            if response.get("code") == 0:
                extras[name] = response.get("data") if "data" in response else response
            else:
                errors[name] = response.get("message") or response.get("msg") or f"code {response.get('code')}"
        except Exception as exc:  # noqa: BLE001 - один дополнительный эндпоинт не должен ронять снимок
            errors[name] = str(exc)
            print(f"! доп. эндпоинт {name}: {exc}")
    return extras, errors


def fetch_live(args) -> tuple[dict, dict, dict, dict, str]:
    token, source = sync.resolve_token(args)
    print(f"токен: {source} ({len(token)} симв.)")
    code = with_retry(lambda: sync.grant(token))
    cred_v, sign_token = with_retry(lambda: sync.cred(code))
    role = with_retry(lambda: sync.binding(cred_v, sign_token))
    role_id = str(role.get("roleId"))
    server_id = str(role.get("serverId"))
    user_id = sync.skport_user_id(cred_v, sign_token)
    print(f"роль: {role.get('nickname', '?')} ({role_id} / {server_id})")
    detail = with_retry(lambda: sync.card_detail(cred_v, sign_token, role_id, server_id, user_id))
    extras: dict = {}
    errors: dict = {}
    if not args.no_extras:
        extras, errors = fetch_extras(cred_v, sign_token, role_id, server_id)
    return detail, extras, errors, role, "skport"


def load_from_json(path_str: str) -> tuple[dict, dict, dict, dict, str, dict]:
    path = Path(path_str)
    if not path.exists():
        raise SystemExit(f"Нет файла: {path}")
    data = path.read_bytes()
    if path.suffix == ".gz":
        data = gzip.decompress(data)
    try:
        obj = json.loads(data.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise SystemExit(f"Не разобрал JSON {path}: {exc}") from exc

    meta: dict = {}
    extras: dict = {}
    if isinstance(obj, dict) and isinstance(obj.get("detail"), dict):
        detail = obj["detail"]
        extras = obj.get("extras") or {}
        meta = obj.get("meta") or {}
    elif isinstance(obj, dict) and isinstance(obj.get("data"), dict) and isinstance(obj["data"].get("detail"), dict):
        detail = obj["data"]["detail"]
    elif isinstance(obj, dict) and "chars" in obj:
        detail = obj
    else:
        raise SystemExit(f"В {path} нет detail/chars")

    if not isinstance(meta, dict):
        meta = {}
    if not isinstance(extras, dict):
        extras = {}
    errors = meta.get("extra_errors") or {}
    role = meta.get("role") or {}
    return detail, extras, errors, role, f"from-json:{path}", meta


# ---------------------------------------------------------------------------
# Разбор ответа
# ---------------------------------------------------------------------------

def parse_gear_item(item) -> dict | None:
    if not isinstance(item, dict):
        return None
    data = item.get("equipData") or item.get("tacticalItemData") or {}
    if not isinstance(data, dict):
        data = {}
    name = data.get("name") or item.get("name")
    if not name:
        return None
    level_raw = data.get("level")
    if isinstance(level_raw, dict):
        level = as_int(sync.val(level_raw))
    else:
        level = as_int(level_raw if level_raw is not None else item.get("level"))
    rarity = sync.val(data.get("rarity") or item.get("rarity"))
    suit = ((data.get("suit") or {}).get("name")) if isinstance(data.get("suit"), dict) else None
    enhance = normalize_enhance(item.get("enhance"))
    return {
        "name": name,
        "level": level,
        "enhance": json_dumps(enhance) if isinstance(enhance, (dict, list)) else (str(enhance) if enhance is not None else None),
        "enhance_raw": enhance,
        "suit": suit,
        "rarity": str(rarity) if rarity is not None else None,
        "equip_id": item.get("equipId") or data.get("id"),
    }


def parse_skills(ch: dict, char_data: dict) -> list[dict]:
    levels: dict[str, dict] = {}
    user_skills = ch.get("userSkills")
    if isinstance(user_skills, dict):
        for skill_id, item in user_skills.items():
            if isinstance(item, dict):
                levels[item.get("skillId") or skill_id] = {
                    "level": as_int(item.get("level")),
                    "max": as_int(item.get("maxLevel")),
                }
    elif isinstance(user_skills, list):
        for item in user_skills:
            if isinstance(item, dict):
                levels[item.get("skillId") or item.get("id")] = {
                    "level": as_int(item.get("level")),
                    "max": as_int(item.get("maxLevel")),
                }

    out: list[dict] = []
    known = set()
    for skill in char_data.get("skills") or []:
        if not isinstance(skill, dict):
            continue
        skill_id = skill.get("id")
        known.add(skill_id)
        level = levels.get(skill_id) or {}
        out.append(
            {
                "id": skill_id,
                "name": skill.get("name"),
                "type": sync.val(skill.get("type")),
                "level": level.get("level"),
                "max": level.get("max"),
            }
        )
    for skill_id, level in levels.items():
        if skill_id in known:
            continue
        out.append({"id": skill_id, "name": skill_id, "type": None, "level": level.get("level"), "max": level.get("max")})
    return out


def parse_operator(ch: dict) -> dict | None:
    char_data = ch.get("charData") or {}
    name = char_data.get("name") or ch.get("id")
    if not name:
        return None

    gear: dict[str, dict] = {}
    for key, slot in GEAR_SLOTS:
        parsed = parse_gear_item(ch.get(key))
        if parsed:
            gear[slot] = parsed

    weapon = ch.get("weapon") or {}
    if not isinstance(weapon, dict):
        weapon = {}
    weapon_data = weapon.get("weaponData") or {}
    if not isinstance(weapon_data, dict):
        weapon_data = {}
    gem_block = weapon.get("gem") or {}
    if not isinstance(gem_block, dict):
        gem_block = {}
    gem = gem_block.get("gemData") or {}
    if not isinstance(gem, dict):
        gem = {}
    weapon_skills = []
    for skill in weapon.get("skills") or []:
        if isinstance(skill, dict):
            weapon_skills.append(
                {
                    "name": skill.get("name") or skill.get("key"),
                    "level": as_int(skill.get("level")),
                    "max": as_int(skill.get("currentMaxLevel")),
                }
            )

    return {
        "name": name,
        "char_id": char_data.get("id") or ch.get("id"),
        "rarity": as_int(sync.val(char_data.get("rarity"))),
        "element": sync.val(char_data.get("property")),
        "class": sync.val(char_data.get("profession")),
        "potential": as_int(ch.get("potentialLevel")) or 0,
        "level": as_int(ch.get("level")),
        "evolve": as_int(ch.get("evolvePhase")),
        "own_ts": str(ch.get("ownTs")) if ch.get("ownTs") is not None else None,
        "tags_json": json_dumps(char_data.get("tags")),
        "talent_json": json_dumps(ch.get("talent")),
        "skills_json": json_dumps(parse_skills(ch, char_data)),
        "gear_json": json_dumps(
            {
                slot: {
                    "name": g["name"],
                    "level": g["level"],
                    "enhance": g["enhance_raw"],
                    "suit": g["suit"],
                }
                for slot, g in gear.items()
            }
        ),
        "gear": gear,
        "weapon_id": weapon_data.get("id"),
        "weapon_name": weapon_data.get("name"),
        "weapon_type": sync.val(weapon_data.get("type")),
        "weapon_rarity": as_int(sync.val(weapon_data.get("rarity"))),
        "weapon_level": as_int(weapon.get("level")),
        "weapon_refine": as_int(weapon.get("refineLevel")),
        "weapon_breakthrough": as_int(weapon.get("breakthroughLevel")),
        "weapon_gem": gem.get("name"),
        "weapon_skills_json": json_dumps(weapon_skills),
        "avatar_url": char_data.get("avatarSqUrl") or char_data.get("avatarRtUrl"),
        "illustration_url": char_data.get("illustrationUrl"),
    }


def parse_achievements(detail: dict) -> tuple[list[dict], int]:
    achieve = detail.get("achieve") or {}
    by_id: dict[str, dict] = {}
    for medal in achieve.get("achieveMedals") or []:
        if not isinstance(medal, dict):
            continue
        data = medal.get("achievementData") or {}
        achieve_id = data.get("id")
        if not achieve_id:
            continue
        by_id[achieve_id] = {
            "achieve_id": achieve_id,
            "name": data.get("name"),
            "category": data.get("cateName"),
            "cate": data.get("cate"),
            "level": as_int(medal.get("level")),
            "init_level": as_int(data.get("initLevel")),
            "is_plated": 1 if medal.get("isPlated") else 0,
            "obtained_ts": str(medal.get("obtainTs")) if medal.get("obtainTs") is not None else None,
        }
    count = as_int(achieve.get("count"))
    return list(by_id.values()), count if count is not None else len(by_id)


def parse_domains(detail: dict) -> list[dict]:
    domains: list[dict] = []
    for domain in detail.get("domain") or []:
        if not isinstance(domain, dict):
            continue
        domain_id = domain.get("domainId") or domain.get("name") or "domain"
        money = domain.get("moneyMgr") or {}
        if not isinstance(money, dict):
            money = {}
        collections: dict[str, dict] = {}

        for level in domain.get("levels") or []:
            if not isinstance(level, dict):
                continue
            level_id = level.get("levelId")
            if not level_id:
                continue

            def pair(key):
                value = level.get(key)
                if isinstance(value, dict):
                    return as_int(value.get("count")), as_int(value.get("total"))
                return as_int(value), None

            puzzle_count, puzzle_total = pair("puzzleCount")
            chest_count, chest_total = pair("trchestCount")
            equip_count, equip_total = pair("equipTrchestCount")
            piece_count, piece_total = pair("pieceCount")
            blackbox_count, blackbox_total = pair("blackboxCount")
            trstar_count, trstar_total = pair("trstarCount")
            collections[level_id] = {
                "domain_id": domain_id,
                "level_id": level_id,
                "name": level.get("name"),
                "puzzle_count": puzzle_count,
                "puzzle_total": puzzle_total,
                "chest_count": chest_count,
                "chest_total": chest_total,
                "equip_chest_count": equip_count,
                "equip_chest_total": equip_total,
                "piece_count": piece_count,
                "piece_total": piece_total,
                "blackbox_count": blackbox_count,
                "blackbox_total": blackbox_total,
                "trstar_count": trstar_count,
                "trstar_total": trstar_total,
            }

        for collection in domain.get("collections") or []:
            if not isinstance(collection, dict):
                continue
            level_id = collection.get("levelId")
            if not level_id:
                continue
            entry = collections.setdefault(
                level_id,
                {
                    "domain_id": domain_id,
                    "level_id": level_id,
                    "name": None,
                    "puzzle_count": None,
                    "puzzle_total": None,
                    "chest_count": None,
                    "chest_total": None,
                    "equip_chest_count": None,
                    "equip_chest_total": None,
                    "piece_count": None,
                    "piece_total": None,
                    "blackbox_count": None,
                    "blackbox_total": None,
                    "trstar_count": None,
                    "trstar_total": None,
                },
            )
            for source_key, target_key in (
                ("puzzleCount", "puzzle_count"),
                ("trchestCount", "chest_count"),
                ("equipTrchestCount", "equip_chest_count"),
                ("pieceCount", "piece_count"),
                ("blackboxCount", "blackbox_count"),
                ("trstarCount", "trstar_count"),
            ):
                if entry[target_key] is None:
                    entry[target_key] = as_int(collection.get(source_key))

        settlements = []
        for settlement in domain.get("settlements") or []:
            if not isinstance(settlement, dict):
                continue
            settlements.append(
                {
                    "domain_id": domain_id,
                    "settlement_id": settlement.get("id") or settlement.get("name") or "settlement",
                    "name": settlement.get("name"),
                    "level": as_int(settlement.get("level")),
                    "exp": as_int(settlement.get("exp")),
                    "exp_to_level": as_int(settlement.get("expToLevelUp")),
                    "money": as_int(settlement.get("remainMoney")),
                    "money_max": as_int(settlement.get("moneyMax")),
                    "is_final_max_level": 1 if settlement.get("isFinalMaxLevel") else 0,
                    "officer_id": settlement.get("officerCharIds"),
                }
            )

        domains.append(
            {
                "domain_id": domain_id,
                "name": domain.get("name"),
                "level": as_int(domain.get("level")),
                "money_total": as_int(money.get("total")),
                "money_count": as_int(money.get("count")),
                "settlements": settlements,
                "collections": list(collections.values()),
            }
        )
    return domains


def parse_spaceship(detail: dict) -> list[dict]:
    rooms: list[dict] = []
    ship = detail.get("spaceShip") or {}
    for index, room in enumerate(ship.get("rooms") or []):
        if not isinstance(room, dict):
            continue
        chars = []
        for ch in room.get("chars") or []:
            if not isinstance(ch, dict):
                continue
            chars.append(
                {
                    "char_id": ch.get("charId"),
                    "favorability": as_num(ch.get("favorability")),
                    "physical_strength": as_num(ch.get("physicalStrength")),
                    "avatar_url": ch.get("avatarUrl"),
                }
            )
        rooms.append(
            {
                "index": index,
                "type": as_int(room.get("type")),
                "level": as_int(room.get("level")),
                "chars": chars,
            }
        )
    return rooms


def build_counters(detail: dict, extras: dict, achieve_count: int | None) -> dict[str, dict]:
    base = detail.get("base") or {}
    dungeon = detail.get("dungeon") or {}
    bp = detail.get("bpSystem") or {}
    daily = detail.get("dailyMission") or {}
    weekly = detail.get("weeklyMission") or {}
    credit = detail.get("credit") or {}
    seek = detail.get("seekSuspicion") or {}

    counters: dict[str, dict] = {}

    def add(key: str, label: str, value) -> None:
        if value is None:
            return
        number = as_num(value)
        if number is None:
            counters[key] = {"key": key, "label": label, "value_num": None, "value_text": str(value)}
        else:
            counters[key] = {"key": key, "label": label, "value_num": float(number), "value_text": None}

    add("player.level", "Уровень игрока", base.get("level"))
    add("player.exp", "Опыт", base.get("exp"))
    add("player.world_level", "World level", base.get("worldLevel"))
    add("collection.char_num", "Операторов", base.get("charNum"))
    add("collection.weapon_num", "Оружия", base.get("weaponNum"))
    add("collection.doc_num", "Документов", base.get("docNum"))
    add("achieve.count", "Достижений", achieve_count)

    add("stamina.current", "Рассудок", dungeon.get("curStamina"))
    add("stamina.max", "Рассудок (макс.)", dungeon.get("maxStamina"))
    add("bp.level", "Battle Pass", bp.get("curLevel"))
    add("bp.max", "Battle Pass (макс.)", bp.get("maxLevel"))
    add("daily.current", "Ежедневные задания", daily.get("dailyActivation"))
    add("daily.max", "Ежедневные задания (макс.)", daily.get("maxDailyActivation"))
    add("weekly.current", "Недельные задания", weekly.get("score"))
    add("weekly.max", "Недельные задания (макс.)", weekly.get("total"))
    add("credit.current", "Кредиты", credit.get("count"))
    add("credit.max", "Кредиты (макс.)", credit.get("total"))
    add("credit.has_today", "Кредиты получены сегодня", 1 if credit.get("hasToday") else 0)
    add("seek.current", "Seek Suspicion", seek.get("count"))
    add("seek.max", "Seek Suspicion (макс.)", seek.get("total"))

    for domain in detail.get("domain") or []:
        if not isinstance(domain, dict):
            continue
        domain_id = domain.get("domainId") or domain.get("name") or "domain"
        domain_name = domain.get("name") or domain_id
        money = domain.get("moneyMgr") or {}
        if not isinstance(money, dict):
            money = {}
        add(f"domain.{domain_id}.money_total", f"Регион {domain_name} — казна (всего)", money.get("total"))
        add(f"domain.{domain_id}.money_count", f"Регион {domain_name} — казна (накоплено)", money.get("count"))

    for index, room in enumerate((detail.get("spaceShip") or {}).get("rooms") or []):
        if isinstance(room, dict):
            add(f"ship.room.{index}.level", f"Корабль: комната {index + 1} (тип {room.get('type')})", room.get("level"))
    add("ship.rooms", "Корабль: комнат", len((detail.get("spaceShip") or {}).get("rooms") or []))

    for contract in detail.get("crisisContract") or []:
        if not isinstance(contract, dict):
            continue
        name = contract.get("name") or "Crisis Contract"
        prefix = f"event.crisis.{contract.get('id') or safe_version(name)}"
        add(f"{prefix}.highest", f"{name}: highest", contract.get("highest"))
        add(f"{prefix}.challenge", f"{name}: challenge count", contract.get("challengeCount"))
        weekly = contract.get("weeklyMission") or {}
        add(f"{prefix}.weekly", f"{name}: недельные", weekly.get("count"))
        add(f"{prefix}.weekly_total", f"{name}: недельные (макс.)", weekly.get("total"))
        indicator = contract.get("indicatorMission") or {}
        add(f"{prefix}.indicator", f"{name}: индикаторы", indicator.get("count"))
        add(f"{prefix}.indicator_total", f"{name}: индикаторы (макс.)", indicator.get("total"))
        stage = contract.get("stageMission") or {}
        add(f"{prefix}.stage", f"{name}: этапы", stage.get("count"))
        add(f"{prefix}.stage_total", f"{name}: этапы (макс.)", stage.get("total"))

    war = detail.get("warEchoes") or {}
    for season in war.get("seasons") or []:
        if not isinstance(season, dict):
            continue
        sid = season.get("id") or safe_version(season.get("name") or "season")
        add(f"event.war_echoes.{sid}.stars", f"War Echoes {season.get('name') or sid}: звёзды", season.get("stars"))
        add(f"event.war_echoes.{sid}.weeks", f"War Echoes {season.get('name') or sid}: недель", len(season.get("weeks") or []))

    indie = detail.get("indieHard") or {}
    normal_passed = 0
    hard_passed = 0
    total_groups = 0
    for group in indie.get("indieHardGroups") or []:
        if not isinstance(group, dict):
            continue
        for dungeon in group.get("dungeonGroups") or []:
            if not isinstance(dungeon, dict):
                continue
            total_groups += 1
            normal = dungeon.get("normalDungeon") or {}
            hard = dungeon.get("hardDungeon") or {}
            if normal.get("isPass"):
                normal_passed += 1
            if hard.get("isPass"):
                hard_passed += 1
    if total_groups:
        add("event.indie_hard.normal_passed", "Indie Hard: обычные пройдены", normal_passed)
        add("event.indie_hard.hard_passed", "Indie Hard: сложные пройдены", hard_passed)
        add("event.indie_hard.total", "Indie Hard: всего групп", total_groups)

    attendance = extras.get("attendance") or {}
    if isinstance(attendance, dict):
        calendar = attendance.get("calendar") or []
        if isinstance(calendar, list):
            signed = sum(1 for day_info in calendar if isinstance(day_info, dict) and day_info.get("done"))
            add("attendance.signed_days", "Подписано дней в месяце", signed)
            add("attendance.has_today", "Сегодня подписано", 1 if attendance.get("hasToday") else 0)

    game_data_block = extras.get("user_game_data") or {}
    if not isinstance(game_data_block, dict):
        game_data_block = {}
    game_data = game_data_block.get("userGameData") or {}
    if isinstance(game_data, dict):
        weapons = game_data.get("userWeapons") or {}
        equips = game_data.get("userEquips") or {}
        tactical = game_data.get("userTacticalItems") or {}
        if isinstance(weapons, dict):
            add("inventory.weapons", "Инвентарь: оружия", len(weapons))
        if isinstance(equips, dict):
            add("inventory.equips", "Инвентарь: гира", len(equips))
            copies = sum(
                as_int((item or {}).get("ownedCount")) or 0
                for item in equips.values()
                if isinstance(item, dict)
            )
            add("inventory.equip_copies", "Инвентарь: копий гира", copies)
        if isinstance(tactical, dict):
            add("inventory.tactical_items", "Инвентарь: тактических предметов", len(tactical))

    return counters


def parse_detail(detail: dict, extras: dict) -> dict:
    base = detail.get("base") or {}
    operators: list[dict] = []
    for char in detail.get("chars") or []:
        if isinstance(char, dict):
            op = parse_operator(char)
            if op:
                operators.append(op)

    achievements, achieve_count = parse_achievements(detail)
    domains = parse_domains(detail)
    rooms = parse_spaceship(detail)
    counters = build_counters(detail, extras, achieve_count)
    sections = {
        key: json.dumps(value, ensure_ascii=False)
        for key, value in detail.items()
        if key not in MODELED_SECTIONS
    }
    return {
        "base": base,
        "operators": operators,
        "achievements": achievements,
        "achieve_count": achieve_count,
        "domains": domains,
        "spaceship": rooms,
        "counters": list(counters.values()),
        "sections": sections,
        "extras": extras,
        "detail": detail,
    }


# ---------------------------------------------------------------------------
# Файлы
# ---------------------------------------------------------------------------

def write_raw(envelope: dict, version: str, taken_at: datetime, plain: bool = False) -> dict:
    SNAP_DIR.mkdir(parents=True, exist_ok=True)
    stem = f"{taken_at.strftime('%Y-%m-%d_%H%M%S')}_{safe_version(version)}"
    ext = ".json" if plain else ".json.gz"
    path = SNAP_DIR / f"{stem}{ext}"
    counter = 2
    while path.exists():
        path = SNAP_DIR / f"{stem}-{counter}{ext}"
        counter += 1

    payload = json.dumps(envelope, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    if plain:
        path.write_bytes(payload)
    else:
        with gzip.open(path, "wb", compresslevel=6) as handle:
            handle.write(payload)

    actual_stem = path.name
    if actual_stem.endswith(".json.gz"):
        actual_stem = actual_stem[:-8]
    elif actual_stem.endswith(".json"):
        actual_stem = actual_stem[:-5]

    return {
        "path": path,
        "rel": rel_path(path),
        "sha256": hashlib.sha256(payload).hexdigest(),
        "bytes": path.stat().st_size,
        "stem": actual_stem,
    }


def read_raw(relative_path: str) -> str:
    path = Path(relative_path)
    if not path.is_absolute():
        path = ROOT / path
    data = path.read_bytes()
    if path.suffix == ".gz":
        data = gzip.decompress(data)
    return data.decode("utf-8")


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def report_path_for(snap: dict) -> Path:
    if snap.get("report_path"):
        return ROOT / snap["report_path"]
    raw_path = snap.get("raw_path")
    if raw_path:
        name = Path(raw_path).name
        if name.endswith(".json.gz"):
            name = name[:-8]
        elif name.endswith(".json"):
            name = name[:-5]
        return SNAP_DIR / f"{name}.md"
    dt = parse_iso(snap.get("taken_at_server")) or now_utc()
    return SNAP_DIR / f"{dt.strftime('%Y-%m-%d_%H%M%S')}_{safe_version(snap.get('game_version') or 'unknown')}.md"


# ---------------------------------------------------------------------------
# База
# ---------------------------------------------------------------------------

def open_db(path: Path) -> sqlite3.Connection:
    path.parent.mkdir(parents=True, exist_ok=True)
    con = sqlite3.connect(path, timeout=30)
    con.row_factory = sqlite3.Row
    con.execute("PRAGMA foreign_keys = ON")
    con.executescript(SCHEMA_PATH.read_text(encoding="utf-8"))
    con.commit()
    return con


def memory_db() -> sqlite3.Connection:
    con = sqlite3.connect(":memory:")
    con.row_factory = sqlite3.Row
    con.execute("PRAGMA foreign_keys = ON")
    con.executescript(SCHEMA_PATH.read_text(encoding="utf-8"))
    return con


def resolve_version(con: sqlite3.Connection, args, server_date: str) -> tuple[str, str]:
    if args.version:
        return args.version.strip(), "cli"
    if args.version_file:
        value = read_version_file(Path(args.version_file))
        if value:
            return value, f"file:{args.version_file}"
    default_file = read_version_file(VERSION_FILE)
    if default_file:
        return default_file, f"file:{rel_path(VERSION_FILE)}"

    row = con.execute(
        """SELECT version FROM game_versions
           WHERE (start_date IS NULL OR start_date <= ?)
             AND (end_date IS NULL OR end_date > ?)
           ORDER BY COALESCE(start_date, '0000-00-00') DESC
           LIMIT 1""",
        (server_date, server_date),
    ).fetchone()
    if row:
        return row["version"], "calendar"

    row = con.execute(
        """SELECT version FROM banners
           WHERE start_date IS NOT NULL AND start_date <= ?
             AND (end_date IS NULL OR end_date = '' OR end_date >= ?)
           ORDER BY start_date DESC LIMIT 1""",
        (server_date, server_date),
    ).fetchone()
    if row and row["version"]:
        return row["version"], "banners"
    return "unknown", "none"


def snapshot_row(
    parsed: dict,
    detail: dict,
    taken_at: datetime,
    version: str,
    version_source: str,
    source: str,
    role: dict,
    raw_info: dict,
    extra_errors: dict,
) -> dict:
    base = parsed["base"]
    dungeon = detail.get("dungeon") or {}
    bp = detail.get("bpSystem") or {}
    daily = detail.get("dailyMission") or {}
    weekly = detail.get("weeklyMission") or {}
    credit = detail.get("credit") or {}
    seek = detail.get("seekSuspicion") or {}
    main_mission = base.get("mainMission") or {}
    return {
        "taken_at": iso_utc(taken_at),
        "taken_at_server": iso_server(taken_at),
        "game_version": version,
        "version_source": version_source,
        "api_current_ts": epoch_to_iso(detail.get("currentTs")),
        "source": source,
        "player_name": base.get("name"),
        "role_id": str(role.get("roleId") or base.get("roleId") or "") or None,
        "server_id": str(role.get("serverId") or "") or None,
        "server_name": base.get("serverName") or role.get("serverName"),
        "player_level": as_int(base.get("level")),
        "world_level": as_int(base.get("worldLevel")),
        "exp": as_int(base.get("exp")),
        "gender": as_int(base.get("gender")),
        "main_mission": main_mission.get("description") if isinstance(main_mission, dict) else None,
        "created_ts": epoch_to_iso(base.get("createTime")),
        "last_login_ts": epoch_to_iso(base.get("lastLoginTime")),
        "save_ts": epoch_to_iso(base.get("saveTime")),
        "char_num": as_int(base.get("charNum")),
        "weapon_num": as_int(base.get("weaponNum")),
        "doc_num": as_int(base.get("docNum")),
        "stamina": as_int(dungeon.get("curStamina")),
        "stamina_max": as_int(dungeon.get("maxStamina")),
        "stamina_full_at": epoch_to_iso(dungeon.get("maxTs")),
        "bp_level": as_int(bp.get("curLevel")),
        "bp_max": as_int(bp.get("maxLevel")),
        "daily": as_int(daily.get("dailyActivation")),
        "daily_max": as_int(daily.get("maxDailyActivation")),
        "weekly": as_int(weekly.get("score")),
        "weekly_max": as_int(weekly.get("total")),
        "credit": as_int(credit.get("count")),
        "credit_max": as_int(credit.get("total")),
        "seek": as_int(seek.get("count")),
        "seek_max": as_int(seek.get("total")),
        "achieve_count": parsed["achieve_count"],
        "raw_path": raw_info["rel"],
        "report_path": None,
        "raw_sha256": raw_info["sha256"],
        "raw_bytes": raw_info["bytes"],
        "diff_summary": None,
        "notes": json.dumps(extra_errors, ensure_ascii=False) if extra_errors else None,
    }


def insert_snapshot_row(con: sqlite3.Connection, snap: dict) -> int:
    cursor = con.execute(
        """INSERT INTO snapshots (
            taken_at, taken_at_server, game_version, version_source, api_current_ts,
            source, player_name, role_id, server_id, server_name, player_level,
            world_level, exp, gender, main_mission, created_ts, last_login_ts,
            save_ts, char_num, weapon_num, doc_num, stamina, stamina_max,
            stamina_full_at, bp_level, bp_max, daily, daily_max, weekly, weekly_max,
            credit, credit_max, seek, seek_max, achieve_count, raw_path, report_path,
            raw_sha256, raw_bytes, diff_summary, notes
        ) VALUES (
            :taken_at, :taken_at_server, :game_version, :version_source, :api_current_ts,
            :source, :player_name, :role_id, :server_id, :server_name, :player_level,
            :world_level, :exp, :gender, :main_mission, :created_ts, :last_login_ts,
            :save_ts, :char_num, :weapon_num, :doc_num, :stamina, :stamina_max,
            :stamina_full_at, :bp_level, :bp_max, :daily, :daily_max, :weekly, :weekly_max,
            :credit, :credit_max, :seek, :seek_max, :achieve_count, :raw_path, :report_path,
            :raw_sha256, :raw_bytes, :diff_summary, :notes
        )""",
        snap,
    )
    return int(cursor.lastrowid)


def insert_snapshot_rows(con: sqlite3.Connection, snapshot_id: int, parsed: dict) -> None:
    operators = parsed["operators"]
    if operators:
        con.executemany(
            """INSERT INTO snapshot_operators (
                snapshot_id, name, char_id, rarity, element, class, potential, level,
                evolve, own_ts, tags_json, talent_json, skills_json, gear_json,
                weapon_id, weapon_name, weapon_type, weapon_rarity, weapon_level,
                weapon_refine, weapon_breakthrough, weapon_gem, weapon_skills_json,
                avatar_url, illustration_url
            ) VALUES (
                :snapshot_id, :name, :char_id, :rarity, :element, :class, :potential, :level,
                :evolve, :own_ts, :tags_json, :talent_json, :skills_json, :gear_json,
                :weapon_id, :weapon_name, :weapon_type, :weapon_rarity, :weapon_level,
                :weapon_refine, :weapon_breakthrough, :weapon_gem, :weapon_skills_json,
                :avatar_url, :illustration_url
            )""",
            [
                {
                    **op,
                    "snapshot_id": snapshot_id,
                    "rarity": op["rarity"] if op["rarity"] is not None else 0,
                }
                for op in operators
            ],
        )

    gear_rows = []
    for op in operators:
        for slot, gear in op["gear"].items():
            gear_rows.append(
                {
                    "snapshot_id": snapshot_id,
                    "operator": op["name"],
                    "slot": slot,
                    "name": gear["name"],
                    "level": gear["level"],
                    "enhance": gear["enhance"],
                    "suit": gear["suit"],
                    "rarity": gear["rarity"],
                    "equip_id": gear["equip_id"],
                }
            )
    if gear_rows:
        con.executemany(
            """INSERT INTO snapshot_gear (
                snapshot_id, operator, slot, name, level, enhance, suit, rarity, equip_id
            ) VALUES (
                :snapshot_id, :operator, :slot, :name, :level, :enhance, :suit, :rarity, :equip_id
            )""",
            gear_rows,
        )

    achievements = parsed["achievements"]
    if achievements:
        con.executemany(
            """INSERT INTO snapshot_achievements (
                snapshot_id, achieve_id, name, category, cate, level, init_level,
                is_plated, obtained_ts
            ) VALUES (
                :snapshot_id, :achieve_id, :name, :category, :cate, :level, :init_level,
                :is_plated, :obtained_ts
            )""",
            [{**item, "snapshot_id": snapshot_id} for item in achievements],
        )

    domain_rows = []
    settlement_rows = []
    collection_rows = []
    for domain in parsed["domains"]:
        domain_rows.append(
            {
                "snapshot_id": snapshot_id,
                "domain_id": domain["domain_id"],
                "name": domain["name"],
                "level": domain["level"],
                "money_total": domain["money_total"],
                "money_count": domain["money_count"],
            }
        )
        for settlement in domain["settlements"]:
            settlement_rows.append({**settlement, "snapshot_id": snapshot_id})
        for collection in domain["collections"]:
            collection_rows.append({**collection, "snapshot_id": snapshot_id})

    if domain_rows:
        con.executemany(
            """INSERT INTO snapshot_domains (
                snapshot_id, domain_id, name, level, money_total, money_count
            ) VALUES (
                :snapshot_id, :domain_id, :name, :level, :money_total, :money_count
            )""",
            domain_rows,
        )
    if settlement_rows:
        con.executemany(
            """INSERT INTO snapshot_settlements (
                snapshot_id, domain_id, settlement_id, name, level, exp, exp_to_level,
                money, money_max, is_final_max_level, officer_id
            ) VALUES (
                :snapshot_id, :domain_id, :settlement_id, :name, :level, :exp, :exp_to_level,
                :money, :money_max, :is_final_max_level, :officer_id
            )""",
            settlement_rows,
        )
    if collection_rows:
        con.executemany(
            """INSERT INTO snapshot_collections (
                snapshot_id, domain_id, level_id, name,
                puzzle_count, puzzle_total, chest_count, chest_total,
                equip_chest_count, equip_chest_total, piece_count, piece_total,
                blackbox_count, blackbox_total, trstar_count, trstar_total
            ) VALUES (
                :snapshot_id, :domain_id, :level_id, :name,
                :puzzle_count, :puzzle_total, :chest_count, :chest_total,
                :equip_chest_count, :equip_chest_total, :piece_count, :piece_total,
                :blackbox_count, :blackbox_total, :trstar_count, :trstar_total
            )""",
            collection_rows,
        )

    room_rows = []
    ship_char_rows = []
    for room in parsed["spaceship"]:
        room_rows.append(
            {
                "snapshot_id": snapshot_id,
                "room_index": room["index"],
                "room_type": room["type"],
                "level": room["level"],
                "chars_json": json_dumps(room["chars"]),
            }
        )
        for ch in room["chars"]:
            if not ch.get("char_id"):
                continue
            ship_char_rows.append(
                {
                    "snapshot_id": snapshot_id,
                    "char_id": ch["char_id"],
                    "room_index": room["index"],
                    "room_type": room["type"],
                    "favorability": ch.get("favorability"),
                    "physical_strength": ch.get("physical_strength"),
                }
            )
    if room_rows:
        con.executemany(
            """INSERT INTO snapshot_spaceship_rooms (
                snapshot_id, room_index, room_type, level, chars_json
            ) VALUES (
                :snapshot_id, :room_index, :room_type, :level, :chars_json
            )""",
            room_rows,
        )
    if ship_char_rows:
        con.executemany(
            """INSERT INTO snapshot_spaceship_chars (
                snapshot_id, char_id, room_index, room_type, favorability, physical_strength
            ) VALUES (
                :snapshot_id, :char_id, :room_index, :room_type, :favorability, :physical_strength
            )""",
            ship_char_rows,
        )

    counters = parsed["counters"]
    if counters:
        con.executemany(
            """INSERT INTO snapshot_counters (snapshot_id, "key", label, value_num, value_text)
               VALUES (:snapshot_id, :key, :label, :value_num, :value_text)""",
            [{**item, "snapshot_id": snapshot_id} for item in counters],
        )

    sections = parsed["sections"]
    if sections:
        con.executemany(
            """INSERT INTO snapshot_sections (snapshot_id, section, json)
               VALUES (:snapshot_id, :section, :json)""",
            [{"snapshot_id": snapshot_id, "section": key, "json": value} for key, value in sections.items()],
        )

    extras = parsed["extras"]
    if extras:
        con.executemany(
            """INSERT INTO snapshot_extras (snapshot_id, name, json)
               VALUES (:snapshot_id, :name, :json)""",
            [
                {"snapshot_id": snapshot_id, "name": key, "json": json.dumps(value, ensure_ascii=False)}
                for key, value in extras.items()
            ],
        )


def update_current_tables(con: sqlite3.Connection, parsed: dict, snap: dict, snapshot_id: int, prune: bool) -> None:
    base = parsed["base"]
    operators = parsed["operators"]
    for op in operators:
        con.execute(
            """INSERT INTO operators (name, rarity, element, class, potential, owned)
               VALUES (:name, :rarity, :element, :class, :potential, 1)
               ON CONFLICT(name) DO UPDATE SET
                 rarity=excluded.rarity,
                 element=excluded.element,
                 class=excluded.class,
                 potential=excluded.potential,
                 owned=1""",
            {**op, "rarity": op["rarity"] if op["rarity"] is not None else 0},
        )
        con.execute("DELETE FROM weapons WHERE operator=?", (op["name"],))
        if op["weapon_name"]:
            con.execute(
                """INSERT INTO weapons (operator, name, rarity, level, refine, breakthrough)
                   VALUES (?, ?, ?, ?, ?, ?)""",
                (
                    op["name"],
                    op["weapon_name"],
                    op["weapon_rarity"],
                    op["weapon_level"],
                    op["weapon_refine"],
                    op["weapon_breakthrough"],
                ),
            )
        con.execute("DELETE FROM gear WHERE operator=?", (op["name"],))
        for slot, gear in op["gear"].items():
            con.execute(
                """INSERT INTO gear (operator, slot, name, level, enhance)
                   VALUES (?, ?, ?, ?, ?)""",
                (op["name"], slot, gear["name"], gear["level"], gear["enhance"]),
            )

    if prune and operators:
        placeholders = ",".join("?" for _ in operators)
        con.execute(
            f"UPDATE operators SET owned=0 WHERE name NOT IN ({placeholders})",
            [op["name"] for op in operators],
        )

    note = f"{base.get('name', '?')} · world {base.get('worldLevel', '?')} · chars {len(operators)} · snapshot #{snapshot_id}"
    con.execute(
        "INSERT INTO sync_log (ts, source, chars, note) VALUES (?, ?, ?, ?)",
        (snap["taken_at"], snap["source"], len(operators), note),
    )


def previous_snapshot_id(con: sqlite3.Connection, snapshot_id: int) -> int | None:
    row = con.execute(
        "SELECT id FROM snapshots WHERE id < ? ORDER BY id DESC LIMIT 1",
        (snapshot_id,),
    ).fetchone()
    return int(row["id"]) if row else None


def latest_snapshot_id(con: sqlite3.Connection) -> int | None:
    row = con.execute("SELECT id FROM snapshots ORDER BY id DESC LIMIT 1").fetchone()
    return int(row["id"]) if row else None


def resolve_snapshot_id(con: sqlite3.Connection, target: str | None) -> int | None:
    if not target or target in ("latest", "last"):
        return latest_snapshot_id(con)
    if target in ("prev", "previous"):
        latest = latest_snapshot_id(con)
        return previous_snapshot_id(con, latest) if latest is not None else None
    if target == "first":
        row = con.execute("SELECT id FROM snapshots ORDER BY id LIMIT 1").fetchone()
        return int(row["id"]) if row else None
    try:
        snapshot_id = int(target)
    except ValueError:
        return None
    row = con.execute("SELECT id FROM snapshots WHERE id = ?", (snapshot_id,)).fetchone()
    return int(row["id"]) if row else None


def load_snapshot_data(con: sqlite3.Connection, snapshot_id: int) -> dict:
    row = con.execute(
        """SELECT s.*, gv.title AS version_title, gv.estimated AS version_estimated,
                  gv.notes AS version_notes
           FROM snapshots s
           LEFT JOIN game_versions gv ON gv.version = s.game_version
           WHERE s.id = ?""",
        (snapshot_id,),
    ).fetchone()
    if not row:
        raise SystemExit(f"Нет снимка #{snapshot_id}")
    snap = dict(row)

    ops_list = [dict(r) for r in con.execute(
        "SELECT * FROM snapshot_operators WHERE snapshot_id = ? ORDER BY rarity DESC, potential DESC, name",
        (snapshot_id,),
    )]
    ops = {r["name"]: r for r in ops_list}

    gear_list = [dict(r) for r in con.execute(
        "SELECT * FROM snapshot_gear WHERE snapshot_id = ?",
        (snapshot_id,),
    )]
    gear = {(r["operator"], r["slot"]): r for r in gear_list}

    achs_list = [dict(r) for r in con.execute(
        "SELECT * FROM snapshot_achievements WHERE snapshot_id = ?",
        (snapshot_id,),
    )]
    achs = {r["achieve_id"]: r for r in achs_list}

    counters = {r["key"]: dict(r) for r in con.execute(
        "SELECT * FROM snapshot_counters WHERE snapshot_id = ?",
        (snapshot_id,),
    )}

    domains = {r["domain_id"]: dict(r) for r in con.execute(
        "SELECT * FROM snapshot_domains WHERE snapshot_id = ?",
        (snapshot_id,),
    )}
    settlements = {
        (r["domain_id"], r["settlement_id"]): dict(r)
        for r in con.execute(
            "SELECT * FROM snapshot_settlements WHERE snapshot_id = ?",
            (snapshot_id,),
        )
    }
    collections = {
        (r["domain_id"], r["level_id"]): dict(r)
        for r in con.execute(
            "SELECT * FROM snapshot_collections WHERE snapshot_id = ?",
            (snapshot_id,),
        )
    }
    rooms = [dict(r) for r in con.execute(
        "SELECT * FROM snapshot_spaceship_rooms WHERE snapshot_id = ? ORDER BY room_index",
        (snapshot_id,),
    )]
    sections = {
        r["section"]: r["json"]
        for r in con.execute(
            "SELECT * FROM snapshot_sections WHERE snapshot_id = ?",
            (snapshot_id,),
        )
    }
    extras = {
        r["name"]: r["json"]
        for r in con.execute(
            "SELECT * FROM snapshot_extras WHERE snapshot_id = ?",
            (snapshot_id,),
        )
    }
    return {
        "snap": snap,
        "ops": ops,
        "ops_list": ops_list,
        "gear": gear,
        "gear_list": gear_list,
        "achs": achs,
        "achs_list": achs_list,
        "counters": counters,
        "domains": domains,
        "settlements": settlements,
        "collections": collections,
        "rooms": rooms,
        "sections": sections,
        "extras": extras,
    }


# ---------------------------------------------------------------------------
# Дифф
# ---------------------------------------------------------------------------

def op_desc(op: dict) -> str:
    rarity = f"{op['rarity']}★" if op.get("rarity") is not None else "?★"
    return (
        f"{rarity} {op.get('element') or '—'} {op.get('class') or '—'}, "
        f"P{op.get('potential') or 0}, ур.{op.get('level') or '—'}, E{op.get('evolve') or 0}"
    )


def weapon_desc(op: dict) -> str:
    if not op.get("weapon_name"):
        return "—"
    parts = [op["weapon_name"]]
    if op.get("weapon_refine") is not None:
        parts.append(f"R{op['weapon_refine']}")
    if op.get("weapon_level") is not None:
        parts.append(f"ур.{op['weapon_level']}")
    if op.get("weapon_breakthrough") is not None:
        parts.append(f"B{op['weapon_breakthrough']}")
    return " ".join(parts)


def skill_map(op: dict) -> dict:
    result = {}
    for skill in jloads(op.get("skills_json"), []) or []:
        if isinstance(skill, dict):
            result[skill.get("name") or skill.get("id")] = skill
    return result


def counter_value(counter: dict | None):
    if not counter:
        return None
    if counter.get("value_num") is not None:
        value = counter["value_num"]
        return int(value) if isinstance(value, float) and value.is_integer() else value
    return counter.get("value_text")


def compute_diff(previous: dict | None, current: dict) -> dict | None:
    if previous is None:
        return None

    diff = {
        "added_ops": [],
        "removed_ops": [],
        "potential": [],
        "level": [],
        "evolve": [],
        "weapons": [],
        "skills": [],
        "talents": [],
        "gear": [],
        "ach_added": [],
        "ach_removed": [],
        "ach_plated": [],
        "ach_level": [],
        "counters": [],
        "domains": [],
        "settlements": [],
        "collections": [],
        "has_changes": False,
    }

    prev_ops = previous["ops"]
    cur_ops = current["ops"]

    for name in sorted(set(cur_ops) - set(prev_ops)):
        diff["added_ops"].append(cur_ops[name])
    for name in sorted(set(prev_ops) - set(cur_ops)):
        diff["removed_ops"].append(prev_ops[name])

    for name in sorted(set(prev_ops) & set(cur_ops)):
        old = prev_ops[name]
        new = cur_ops[name]
        if old.get("potential") != new.get("potential"):
            diff["potential"].append((name, old.get("potential"), new.get("potential")))
        if old.get("level") != new.get("level"):
            diff["level"].append((name, old.get("level"), new.get("level")))
        if old.get("evolve") != new.get("evolve"):
            diff["evolve"].append((name, old.get("evolve"), new.get("evolve")))

        old_weapon = (
            old.get("weapon_name"), old.get("weapon_refine"), old.get("weapon_level"),
            old.get("weapon_breakthrough"),
        )
        new_weapon = (
            new.get("weapon_name"), new.get("weapon_refine"), new.get("weapon_level"),
            new.get("weapon_breakthrough"),
        )
        if old_weapon != new_weapon:
            diff["weapons"].append(f"{name}: {weapon_desc(old)} → {weapon_desc(new)}")
        elif old.get("weapon_gem") != new.get("weapon_gem"):
            diff["weapons"].append(
                f"{name}: сфера оружия {fmt_value(old.get('weapon_gem'))} → {fmt_value(new.get('weapon_gem'))}"
            )

        old_skills = skill_map(old)
        new_skills = skill_map(new)
        for skill_name in sorted(set(old_skills) | set(new_skills)):
            old_skill = old_skills.get(skill_name) or {}
            new_skill = new_skills.get(skill_name) or {}
            if old_skill.get("level") != new_skill.get("level"):
                diff["skills"].append(
                    f"{name}: {skill_name} ур.{old_skill.get('level', '—')} → {new_skill.get('level', '—')}"
                )

        old_talents = jloads(old.get("talent_json"), {}) or {}
        new_talents = jloads(new.get("talent_json"), {}) or {}
        for key, label in TALENT_LABELS.items():
            old_value = old_talents.get(key)
            new_value = new_talents.get(key)
            if old_value == new_value:
                continue
            if isinstance(old_value, list) and isinstance(new_value, list):
                if len(old_value) != len(new_value):
                    diff["talents"].append(f"{name}: {label} {len(old_value)} → {len(new_value)}")
                else:
                    diff["talents"].append(f"{name}: {label} обновились")
            else:
                diff["talents"].append(
                    f"{name}: {label} {fmt_value(old_value)} → {fmt_value(new_value)}"
                )

    for key in sorted(set(previous["gear"]) | set(current["gear"])):
        old = previous["gear"].get(key)
        new = current["gear"].get(key)
        operator, slot = key
        slot_label = SLOT_LABELS.get(slot, slot)
        if old is None and new is not None:
            diff["gear"].append(f"{operator}: + {slot_label} — {fmt_gear_cell(new)}")
        elif old is not None and new is None:
            diff["gear"].append(f"{operator}: − {slot_label} — было {fmt_gear_cell(old)}")
        elif old is not None and new is not None:
            if old.get("name") != new.get("name"):
                diff["gear"].append(f"{operator}: {slot_label} {old.get('name')} → {new.get('name')}")
            elif old.get("level") != new.get("level") or normalize_enhance(old.get("enhance")) != normalize_enhance(new.get("enhance")):
                diff["gear"].append(f"{operator}: {slot_label} {fmt_gear_cell(old)} → {fmt_gear_cell(new)}")

    prev_achs = previous["achs"]
    cur_achs = current["achs"]
    for achieve_id in set(cur_achs) - set(prev_achs):
        diff["ach_added"].append(cur_achs[achieve_id])
    for achieve_id in set(prev_achs) - set(cur_achs):
        diff["ach_removed"].append(prev_achs[achieve_id])
    diff["ach_added"].sort(key=lambda item: as_int(item.get("obtained_ts")) or 0, reverse=True)
    for achieve_id in set(prev_achs) & set(cur_achs):
        old = prev_achs[achieve_id]
        new = cur_achs[achieve_id]
        if old.get("is_plated") != new.get("is_plated") and new.get("is_plated"):
            diff["ach_plated"].append(new)
        if old.get("level") != new.get("level"):
            diff["ach_level"].append((new.get("name") or achieve_id, old.get("level"), new.get("level")))

    # Сравниваем только общие счётчики: если доп. эндпоинт не тянули
    # (--no-extras) или он упал, это не должно выглядеть как изменение.
    for key in sorted(set(previous["counters"]) & set(current["counters"])):
        old_counter = previous["counters"].get(key)
        new_counter = current["counters"].get(key)
        old_value = counter_value(old_counter)
        new_value = counter_value(new_counter)
        if old_value == new_value:
            continue
        delta = None
        if isinstance(old_value, (int, float)) and isinstance(new_value, (int, float)):
            delta = new_value - old_value
        diff["counters"].append(
            {
                "key": key,
                "label": (new_counter or old_counter or {}).get("label") or key,
                "old": old_value,
                "new": new_value,
                "delta": delta,
            }
        )

    for domain_id in sorted(set(previous["domains"]) | set(current["domains"])):
        old = previous["domains"].get(domain_id)
        new = current["domains"].get(domain_id)
        if old is None and new is not None:
            diff["domains"].append(f"+ {new.get('name') or domain_id} (ур.{new.get('level')})")
        elif old is not None and new is None:
            diff["domains"].append(f"− {old.get('name') or domain_id}")
        elif old.get("level") != new.get("level"):
            diff["domains"].append(
                f"{new.get('name') or domain_id}: ур.{old.get('level')} → {new.get('level')}"
            )

    for key in sorted(set(previous["settlements"]) | set(current["settlements"])):
        old = previous["settlements"].get(key)
        new = current["settlements"].get(key)
        name = (new or old or {}).get("name") or key[1]
        if old is None and new is not None:
            diff["settlements"].append(f"+ {name} (ур.{new.get('level')})")
        elif old is not None and new is None:
            diff["settlements"].append(f"− {name}")
        else:
            changes = []
            if old.get("level") != new.get("level"):
                changes.append(f"ур.{old.get('level')} → {new.get('level')}")
            if old.get("money") != new.get("money"):
                changes.append(f"деньги {fmt_num(old.get('money'))} → {fmt_num(new.get('money'))}")
            if changes:
                diff["settlements"].append(f"{name}: " + ", ".join(changes))

    for key in sorted(set(previous["collections"]) | set(current["collections"])):
        old = previous["collections"].get(key)
        new = current["collections"].get(key)
        name = (new or old or {}).get("name") or key[1]
        if old is None and new is not None:
            diff["collections"].append(f"+ {name}")
            continue
        if old is not None and new is None:
            diff["collections"].append(f"− {name}")
            continue
        changes = []
        for metric, label in METRIC_LABELS.items():
            old_value = old.get(f"{metric}_count")
            new_value = new.get(f"{metric}_count")
            if old_value != new_value and (old_value is not None or new_value is not None):
                changes.append(f"{label} {fmt_num(old_value)} → {fmt_num(new_value)}")
        if changes:
            diff["collections"].append(f"{name}: " + ", ".join(changes))

    diff["has_changes"] = any(
        diff[key]
        for key in (
            "added_ops", "removed_ops", "potential", "level", "evolve", "weapons",
            "skills", "talents", "gear", "ach_added", "ach_removed", "ach_plated",
            "ach_level", "counters", "domains", "settlements", "collections",
        )
    )
    return diff


def diff_summary(diff: dict | None) -> str:
    if diff is None:
        return "первый снимок"
    if not diff["has_changes"]:
        return "без изменений"
    parts = []
    if diff["added_ops"]:
        parts.append(f"+{len(diff['added_ops'])} оп.")
    if diff["removed_ops"]:
        parts.append(f"−{len(diff['removed_ops'])} оп.")
    if diff["potential"]:
        parts.append(f"пот ×{len(diff['potential'])}")
    if diff["level"]:
        parts.append(f"ур. ×{len(diff['level'])}")
    if diff["evolve"]:
        parts.append(f"E ×{len(diff['evolve'])}")
    if diff["weapons"]:
        parts.append(f"оружие ×{len(diff['weapons'])}")
    if diff["skills"]:
        parts.append(f"скиллы ×{len(diff['skills'])}")
    if diff["talents"]:
        parts.append(f"таланты ×{len(diff['talents'])}")
    if diff["gear"]:
        parts.append(f"гир ×{len(diff['gear'])}")
    if diff["ach_added"]:
        parts.append(f"достиж. +{len(diff['ach_added'])}")
    if diff["ach_plated"]:
        parts.append(f"плашки +{len(diff['ach_plated'])}")
    if diff["counters"]:
        parts.append(f"ресурсы ×{len(diff['counters'])}")
    if diff["domains"] or diff["settlements"] or diff["collections"]:
        parts.append("домены")
    return "; ".join(parts) if parts else "без изменений"


def render_diff_md(diff: dict | None) -> str:
    if diff is None:
        return "_Первый снимок — сравнивать не с чем._\n"
    if not diff["has_changes"]:
        return "_Изменений нет._\n"

    lines: list[str] = []
    if diff["added_ops"]:
        lines.append("**Новые операторы**")
        for op in diff["added_ops"]:
            suffix = f", получен {fmt_epoch(op.get('own_ts'))}" if op.get("own_ts") else ""
            lines.append(f"- **+ {op.get('name')}** — {op_desc(op)}{suffix}")
        lines.append("")
    if diff["removed_ops"]:
        lines.append("**Пропали из ответа**")
        for op in diff["removed_ops"]:
            lines.append(f"- − {op.get('name')} — {op_desc(op)}")
        lines.append("")
    if diff["potential"]:
        lines.append("**Потенциалы**")
        for name, old, new in diff["potential"]:
            lines.append(f"- {name}: P{old} → P{new}")
        lines.append("")
    if diff["level"]:
        lines.append("**Уровни**")
        for name, old, new in diff["level"]:
            lines.append(f"- {name}: ур.{old} → {new}")
        lines.append("")
    if diff["evolve"]:
        lines.append("**Пробуждение**")
        for name, old, new in diff["evolve"]:
            lines.append(f"- {name}: E{old} → E{new}")
        lines.append("")
    if diff["weapons"]:
        lines.append("**Оружие**")
        lines.extend(f"- {line}" for line in diff["weapons"])
        lines.append("")
    if diff["skills"]:
        lines.append("**Скиллы**")
        lines.extend(f"- {line}" for line in diff["skills"])
        lines.append("")
    if diff["talents"]:
        lines.append("**Таланты**")
        lines.extend(f"- {line}" for line in diff["talents"])
        lines.append("")
    if diff["gear"]:
        lines.append("**Гир**")
        lines.extend(f"- {line}" for line in diff["gear"])
        lines.append("")
    if diff["ach_added"] or diff["ach_removed"] or diff["ach_plated"] or diff["ach_level"]:
        lines.append("**Достижения**")
        for item in diff["ach_added"]:
            category = f" ({item['category']})" if item.get("category") else ""
            lines.append(f"- + {item.get('name')}{category}")
        for item in diff["ach_removed"]:
            lines.append(f"- − {item.get('name')}")
        for item in diff["ach_plated"]:
            lines.append(f"- плашка: {item.get('name')}")
        for name, old, new in diff["ach_level"]:
            lines.append(f"- {name}: ур.{old} → {new}")
        lines.append("")
    if diff["counters"]:
        lines.append("**Ресурсы и прогресс**")
        for item in diff["counters"]:
            old_value = item["old"]
            new_value = item["new"]
            if isinstance(old_value, (int, float)) and isinstance(new_value, (int, float)):
                delta = new_value - old_value
                delta_text = f" ({delta:+})" if delta else ""
                lines.append(f"- {item['label']}: {fmt_num(old_value)} → {fmt_num(new_value)}{delta_text}")
            else:
                lines.append(f"- {item['label']}: {fmt_value(old_value)} → {fmt_value(new_value)}")
        lines.append("")
    if diff["domains"]:
        lines.append("**Регионы**")
        lines.extend(f"- {line}" for line in diff["domains"])
        lines.append("")
    if diff["settlements"]:
        lines.append("**Аванпосты**")
        lines.extend(f"- {line}" for line in diff["settlements"])
        lines.append("")
    if diff["collections"]:
        lines.append("**Сбор коллекций**")
        lines.extend(f"- {line}" for line in diff["collections"])
        lines.append("")
    return "\n".join(lines)


# ---------------------------------------------------------------------------
# Отчёты
# ---------------------------------------------------------------------------

def render_header(snap: dict) -> list[str]:
    version = snap.get("game_version") or "unknown"
    if snap.get("version_title"):
        version += f" — {snap['version_title']}"
    if snap.get("version_estimated"):
        version += " (оценка)"
    lines = [
        f"- **Версия игры:** {version} · источник: {version_source_label(snap.get('version_source'))}",
        f"- **Снято:** {fmt_iso_utc(snap.get('taken_at'))} / {fmt_iso_server(snap.get('taken_at_server'))}",
    ]
    player = snap.get("player_name") or "?"
    role_id = snap.get("role_id") or "—"
    server = snap.get("server_name") or "—"
    server_id = snap.get("server_id") or "—"
    lines.append(f"- **Игрок:** {player} · роль {role_id} · сервер {server} ({server_id})")
    lines.append(
        f"- **Уровень:** {fmt_num(snap.get('player_level'))} · world level {fmt_num(snap.get('world_level'))}"
        f" · опыт {fmt_num(snap.get('exp'))}"
    )
    lines.append(
        f"- **Создан:** {fmt_iso_utc(snap.get('created_ts'))} · "
        f"**последний вход:** {fmt_iso_utc(snap.get('last_login_ts'))} · "
        f"**сохранение:** {fmt_iso_utc(snap.get('save_ts'))}"
    )
    lines.append(
        f"- **Коллекция:** {fmt_num(snap.get('char_num'))} операторов · "
        f"{fmt_num(snap.get('weapon_num'))} оружия · {fmt_num(snap.get('doc_num'))} документов · "
        f"{fmt_num(snap.get('achieve_count'))} достижений"
    )
    lines.append(
        f"- **Рассудок:** {fmt_ratio(snap.get('stamina'), snap.get('stamina_max'))} · "
        f"полная к {fmt_iso_utc(snap.get('stamina_full_at'))}"
    )
    lines.append(
        f"- **Battle Pass:** {fmt_ratio(snap.get('bp_level'), snap.get('bp_max'))} · "
        f"**ежедневки:** {fmt_ratio(snap.get('daily'), snap.get('daily_max'))} · "
        f"**недельные:** {fmt_ratio(snap.get('weekly'), snap.get('weekly_max'))}"
    )
    lines.append(
        f"- **Кредиты:** {fmt_ratio(snap.get('credit'), snap.get('credit_max'))} · "
        f"**Seek Suspicion:** {fmt_ratio(snap.get('seek'), snap.get('seek_max'))}"
    )
    if snap.get("main_mission"):
        lines.append(f"- **Текущая миссия:** {snap['main_mission']}")
    return lines


def render_ops_table(ops: list[dict]) -> str:
    lines = [
        "| Оператор | Ред. | Элемент | Класс | Пот | Ур. | E | Оружие | R | B | Ур. ор. |",
        "|---|---|---|---|---|---|---|---|---|---|---|",
    ]
    for op in sorted(ops, key=lambda x: (-(x.get("rarity") or 0), -(x.get("potential") or 0), x.get("name") or "")):
        lines.append(
            "| {name} | {rarity} | {element} | {klass} | P{potential} | {level} | E{evolve} | "
            "{weapon} | {refine} | {breakthrough} | {wlevel} |".format(
                name=md_cell(op.get("name")),
                rarity=f"{op.get('rarity')}★" if op.get("rarity") is not None else "—",
                element=md_cell(op.get("element")),
                klass=md_cell(op.get("class")),
                potential=op.get("potential") or 0,
                level=md_cell(op.get("level")),
                evolve=op.get("evolve") if op.get("evolve") is not None else 0,
                weapon=md_cell(op.get("weapon_name")),
                refine=md_cell(op.get("weapon_refine")),
                breakthrough=md_cell(op.get("weapon_breakthrough")),
                wlevel=md_cell(op.get("weapon_level")),
            )
        )
    return "\n".join(lines)


def render_gear_table(ops: list[dict], gear_list: list[dict]) -> str:
    by_operator: dict[str, dict[str, dict]] = {}
    for gear in gear_list:
        by_operator.setdefault(gear["operator"], {})[gear["slot"]] = gear

    lines = [
        "| Оператор | Тело | Руки | Аксессуар 1 | Аксессуар 2 | Тактика |",
        "|---|---|---|---|---|---|",
    ]
    for op in sorted(ops, key=lambda x: (-(x.get("rarity") or 0), -(x.get("potential") or 0), x.get("name") or "")):
        gear = by_operator.get(op["name"], {})
        lines.append(
            "| {name} | {body} | {arm} | {acc1} | {acc2} | {tactical} |".format(
                name=md_cell(op.get("name")),
                body=fmt_gear_cell(gear.get("body")),
                arm=fmt_gear_cell(gear.get("arm")),
                acc1=fmt_gear_cell(gear.get("acc1")),
                acc2=fmt_gear_cell(gear.get("acc2")),
                tactical=fmt_gear_cell(gear.get("tactical")),
            )
        )
    return "\n".join(lines)


def render_achievements(achs: list[dict], achieve_count: int | None) -> str:
    lines = [f"Всего: **{fmt_num(achieve_count)}**", ""]
    latest = sorted(achs, key=lambda item: as_int(item.get("obtained_ts")) or 0, reverse=True)[:10]
    if not latest:
        lines.append("_Достижения не пришли в ответе._")
        return "\n".join(lines)
    lines.extend(
        [
            "| Достижение | Категория | Ур. | Получено |",
            "|---|---|---|---|",
        ]
    )
    for item in latest:
        lines.append(
            f"| {md_cell(item.get('name'))} | {md_cell(item.get('category'))} | "
            f"{md_cell(item.get('level'))} | {fmt_epoch(item.get('obtained_ts'))} |"
        )
    return "\n".join(lines)


def render_domains(domains: list[dict], settlements: list[dict], collections: list[dict]) -> str:
    if not domains:
        return "_Регионы не пришли в ответе._"
    lines: list[str] = []
    for domain in sorted(domains, key=lambda item: item.get("name") or ""):
        lines.append(f"### {domain.get('name') or domain['domain_id']} — уровень {fmt_num(domain.get('level'))}")
        lines.append("")
        own_settlements = [s for s in settlements if s.get("domain_id") == domain["domain_id"]]
        if own_settlements:
            parts = [
                f"{s.get('name') or s['settlement_id']} ур.{fmt_num(s.get('level'))}"
                for s in sorted(own_settlements, key=lambda item: item.get("name") or "")
            ]
            lines.append("**Аванпосты:** " + " · ".join(parts))
        if domain.get("money_total") is not None or domain.get("money_count") is not None:
            lines.append(
                f"**Казна:** {fmt_num(domain.get('money_count'))} / {fmt_num(domain.get('money_total'))}"
            )
        own_collections = [c for c in collections if c.get("domain_id") == domain["domain_id"]]
        if own_collections:
            lines.extend(
                [
                    "",
                    "| Локация | Сундуки | Головоломки | Чёрные ящики | Детали | Снаряж. сундуки | trstar |",
                    "|---|---|---|---|---|---|---|",
                ]
            )
            for collection in sorted(own_collections, key=lambda item: item.get("level_id") or ""):
                lines.append(
                    "| {name} | {chest} | {puzzle} | {blackbox} | {piece} | {equip} | {trstar} |".format(
                        name=md_cell(collection.get("name") or collection.get("level_id")),
                        chest=fmt_ratio(collection.get("chest_count"), collection.get("chest_total")),
                        puzzle=fmt_ratio(collection.get("puzzle_count"), collection.get("puzzle_total")),
                        blackbox=fmt_ratio(collection.get("blackbox_count"), collection.get("blackbox_total")),
                        piece=fmt_ratio(collection.get("piece_count"), collection.get("piece_total")),
                        equip=fmt_ratio(collection.get("equip_chest_count"), collection.get("equip_chest_total")),
                        trstar=fmt_ratio(collection.get("trstar_count"), collection.get("trstar_total")),
                    )
                )
        lines.append("")
    return "\n".join(lines)


def render_events(sections: dict) -> str:
    lines: list[str] = []

    crisis = jloads(sections.get("crisisContract"), []) or []
    if crisis:
        lines.append("### Crisis Contract")
        for item in crisis:
            if not isinstance(item, dict):
                continue
            weekly = item.get("weeklyMission") or {}
            indicator = item.get("indicatorMission") or {}
            stage = item.get("stageMission") or {}
            lines.append(
                f"- **{item.get('name') or 'Crisis'}** — highest {fmt_num(item.get('highest'))}, "
                f"challenge {fmt_num(item.get('challengeCount'))}, "
                f"недельные {fmt_ratio(weekly.get('count'), weekly.get('total'))}, "
                f"индикаторы {fmt_ratio(indicator.get('count'), indicator.get('total'))}, "
                f"этапы {fmt_ratio(stage.get('count'), stage.get('total'))}, "
                f"до {fmt_epoch(item.get('endAtTs'))}"
            )
        lines.append("")

    war = jloads(sections.get("warEchoes"), {}) or {}
    seasons = war.get("seasons") or []
    if seasons:
        lines.append("### War Echoes")
        for season in seasons:
            if not isinstance(season, dict):
                continue
            weeks = season.get("weeks") or []
            current = weeks[0] if weeks else {}
            lines.append(
                f"- **{season.get('name') or 'War Echoes'}** — {fmt_num(season.get('stars'))} звёзд, "
                f"недель {len(weeks)}"
                + (
                    f"; цикл: {current.get('name')} ({fmt_num(current.get('stars'))} звёзд)"
                    if current else ""
                )
                + f", до {fmt_epoch(season.get('endTs'))}"
            )
        lines.append("")

    indie = jloads(sections.get("indieHard"), {}) or {}
    groups = indie.get("indieHardGroups") or []
    if groups:
        lines.append("### Indie Hard")
        for group in groups:
            if not isinstance(group, dict):
                continue
            dungeons = group.get("dungeonGroups") or []
            normal = sum(1 for item in dungeons if isinstance(item, dict) and (item.get("normalDungeon") or {}).get("isPass"))
            hard = sum(1 for item in dungeons if isinstance(item, dict) and (item.get("hardDungeon") or {}).get("isPass"))
            lines.append(
                f"- **{group.get('name') or 'Indie Hard'}** — обычные {normal}/{len(dungeons)}, "
                f"сложные {hard}/{len(dungeons)}"
            )
        lines.append("")

    return "\n".join(lines) if lines else "_Событий в ответе нет._"


def render_ship(rooms: list[dict]) -> str:
    if not rooms:
        return "_Корабль не пришёл в ответе._"
    lines = [
        "| # | Тип | Ур. | Операторов | На 1500 fav |",
        "|---|---|---|---|---|",
    ]
    for room in sorted(rooms, key=lambda item: item.get("room_index") or 0):
        chars = jloads(room.get("chars_json"), []) or []
        max_fav = sum(1 for ch in chars if isinstance(ch, dict) and (ch.get("favorability") or 0) >= 1500)
        lines.append(
            f"| {room.get('room_index', 0) + 1} | {md_cell(room.get('room_type'))} | "
            f"{md_cell(room.get('level'))} | {len(chars)} | {max_fav} |"
        )
    return "\n".join(lines)


def render_extras(extras: dict) -> str:
    lines: list[str] = []
    attendance = jloads(extras.get("attendance"), {}) or {}
    if attendance:
        calendar = attendance.get("calendar") or []
        signed = sum(1 for item in calendar if isinstance(item, dict) and item.get("done"))
        lines.append(
            f"- **Attendance:** подписано {signed}/{len(calendar)} дней, "
            f"сегодня — {'да' if attendance.get('hasToday') else 'нет'}"
        )

    user = jloads(extras.get("user"), {}) or {}
    basic = ((user.get("user") or {}).get("basicUser") or {}) if isinstance(user, dict) else {}
    if basic.get("nickname"):
        lines.append(f"- **Профиль Skport:** {basic['nickname']} · создан {fmt_epoch(basic.get('createdAt'))}")

    game_data_block = jloads(extras.get("user_game_data"), {}) or {}
    game_data = (game_data_block.get("userGameData") or {}) if isinstance(game_data_block, dict) else {}
    if isinstance(game_data, dict) and game_data:
        weapons = game_data.get("userWeapons") or {}
        equips = game_data.get("userEquips") or {}
        tactical = game_data.get("userTacticalItems") or {}
        copies = sum(
            as_int((item or {}).get("ownedCount")) or 0
            for item in equips.values()
            if isinstance(item, dict)
        ) if isinstance(equips, dict) else 0
        lines.append(
            f"- **Инвентарь:** {fmt_num(len(weapons) if isinstance(weapons, dict) else None)} оружия · "
            f"{fmt_num(len(equips) if isinstance(equips, dict) else None)} гира ({fmt_num(copies)} копий) · "
            f"тактических предметов: {fmt_num(len(tactical) if isinstance(tactical, dict) else None)}"
        )

    for key, title in (("char_pool", "Текущие пулы операторов"), ("weapon_pool", "Текущие пулы оружия")):
        pool_data = jloads(extras.get(key), {}) or {}
        pools = pool_data.get("list") or [] if isinstance(pool_data, dict) else []
        if pools:
            parts = []
            for pool in pools[:3]:
                if isinstance(pool, dict):
                    parts.append(
                        f"{pool.get('name') or '?'} (до {fmt_epoch(pool.get('endAtTs') or pool.get('poolEndAtTs'))})"
                    )
            if parts:
                lines.append(f"- **{title}:** " + " · ".join(parts))

    return "\n".join(lines) if lines else "_Дополнительные данные не пришли._"


def render_files(snap: dict) -> list[str]:
    return [
        f"- Сырой ответ: `{snap.get('raw_path') or '—'}`",
        f"- Полный отчёт: `{snap.get('report_path') or '—'}`",
        f"- История: `{rel_path(HISTORY_MD)}`",
        f"- Последний снимок: `{rel_path(LATEST_MD)}`",
    ]


def render_latest_md(data: dict, diff: dict | None) -> str:
    snap = data["snap"]
    lines = [
        "# Синхронизация аккаунта (API)",
        "",
        f"Обновлено: {fmt_iso_server(snap.get('taken_at_server'))} · снимок #{snap.get('id')}",
        "",
        *render_header(snap),
        "",
        "## Операторы",
        "",
        render_ops_table(data["ops_list"]),
        "",
        "## Изменения с прошлым снимком",
        "",
        render_diff_md(diff),
        "## Файлы",
        "",
        *render_files(snap),
        "",
    ]
    return "\n".join(lines)


def render_full_md(data: dict, diff: dict | None) -> str:
    snap = data["snap"]
    lines = [
        f"# Снимок аккаунта #{snap.get('id')}",
        "",
        *render_header(snap),
        "",
        "## Операторы",
        "",
        render_ops_table(data["ops_list"]),
        "",
        "## Гир",
        "",
        render_gear_table(data["ops_list"], data["gear_list"]),
        "",
        "## Достижения",
        "",
        render_achievements(data["achs_list"], snap.get("achieve_count")),
        "",
        "## Регионы",
        "",
        render_domains(
            list(data["domains"].values()),
            list(data["settlements"].values()),
            list(data["collections"].values()),
        ),
        "## События",
        "",
        render_events(data["sections"]),
        "",
        "## Корабль",
        "",
        render_ship(data["rooms"]),
        "",
        "## Дополнительные данные API",
        "",
        render_extras(data["extras"]),
        "",
        "## Изменения с прошлым снимком",
        "",
        render_diff_md(diff),
        "## Файлы",
        "",
        *render_files(snap),
        "",
    ]
    return "\n".join(lines)


def render_history_md(con: sqlite3.Connection) -> str:
    rows = [dict(r) for r in con.execute("SELECT * FROM snapshots ORDER BY id")]
    lines = [
        "# История снимков аккаунта",
        "",
        f"Обновлено: {fmt_iso_server(iso_server(now_utc()))} · снимков: {len(rows)}",
        "",
        "| # | Дата (UTC+8) | Версия | Ур. | World | Оп. | Оруж. | Рассудок | BP | Ежедн. | Достиж. | Изменения |",
        "|---|---|---|---|---|---|---|---|---|---|---|---|",
    ]
    for row in rows:
        lines.append(
            "| {id} | {date} | {version} | {level} | {world} | {chars} | {weapons} | {stamina} | "
            "{bp} | {daily} | {achieve} | {changes} |".format(
                id=row.get("id"),
                date=fmt_short_server(row.get("taken_at_server")),
                version=md_cell(row.get("game_version")),
                level=md_cell(row.get("player_level")),
                world=md_cell(row.get("world_level")),
                chars=md_cell(row.get("char_num")),
                weapons=md_cell(row.get("weapon_num")),
                stamina=fmt_ratio(row.get("stamina"), row.get("stamina_max")),
                bp=fmt_ratio(row.get("bp_level"), row.get("bp_max")),
                daily=fmt_ratio(row.get("daily"), row.get("daily_max")),
                achieve=md_cell(row.get("achieve_count")),
                changes=md_cell(row.get("diff_summary") or "—"),
            )
        )
    lines.extend(
        [
            "",
            "Сырые ответы: `data/snapshots/`. Полные отчёты — рядом с ними.",
            "Новый снимок: `python scripts\\snapshot.py`.",
            "",
        ]
    )
    return "\n".join(lines)


# ---------------------------------------------------------------------------
# Команды
# ---------------------------------------------------------------------------

def print_take_summary(parsed: dict, detail: dict, taken_at: datetime, version: str, version_source: str) -> None:
    base = parsed["base"]
    dungeon = detail.get("dungeon") or {}
    bp = detail.get("bpSystem") or {}
    daily = detail.get("dailyMission") or {}
    print(
        f"Снимок: {fmt_iso_server(iso_server(taken_at))} · версия {version} "
        f"({version_source_label(version_source)})"
    )
    print(
        f"Игрок: {base.get('name', '?')} · ур. {base.get('level', '?')} · "
        f"world {base.get('worldLevel', '?')} · {len(parsed['operators'])} операторов"
    )
    print(
        f"Рассудок: {fmt_ratio(dungeon.get('curStamina'), dungeon.get('maxStamina'))} · "
        f"BP {fmt_ratio(bp.get('curLevel'), bp.get('maxLevel'))} · "
        f"ежедневки {fmt_ratio(daily.get('dailyActivation'), daily.get('maxDailyActivation'))} · "
        f"достижения {fmt_num(parsed['achieve_count'])}"
    )


def cmd_take(con: sqlite3.Connection, args) -> None:
    meta: dict = {}
    if args.from_json:
        detail, extras, extra_errors, role, source, meta = load_from_json(args.from_json)
    else:
        detail, extras, extra_errors, role, source = fetch_live(args)

    taken_at = parse_taken_at(args.taken_at or meta.get("taken_at"))
    server_date = taken_at.astimezone(SERVER_TZ).date().isoformat()
    if args.from_json and meta.get("game_version") and not (args.version or args.version_file):
        version, version_source = str(meta["game_version"]), "meta"
    else:
        version, version_source = resolve_version(con, args, server_date)
    parsed = parse_detail(detail, extras)
    print_take_summary(parsed, detail, taken_at, version, version_source)

    if args.dry_run:
        print("dry-run: файлы и БД не изменены")
        return

    envelope = {
        "meta": {
            "taken_at": iso_utc(taken_at),
            "taken_at_server": iso_server(taken_at),
            "game_version": version,
            "version_source": version_source,
            "source": source,
            "api_current_ts": detail.get("currentTs"),
            "player": parsed["base"],
            "role": role,
            "extra_errors": extra_errors,
        },
        "detail": detail,
        "extras": extras,
    }
    raw_info = write_raw(envelope, version, taken_at, plain=args.plain_json)
    snap = snapshot_row(
        parsed, detail, taken_at, version, version_source, source, role, raw_info, extra_errors
    )

    con.execute("BEGIN")
    snapshot_id = insert_snapshot_row(con, snap)
    insert_snapshot_rows(con, snapshot_id, parsed)
    update_current_tables(con, parsed, snap, snapshot_id, prune=not args.no_prune)
    con.commit()

    data = load_snapshot_data(con, snapshot_id)
    previous_id = previous_snapshot_id(con, snapshot_id)
    previous = load_snapshot_data(con, previous_id) if previous_id else None
    diff = compute_diff(previous, data)
    summary = diff_summary(diff)

    report_path = SNAP_DIR / f"{raw_info['stem']}.md"
    data["snap"]["diff_summary"] = summary
    data["snap"]["report_path"] = rel_path(report_path) if not args.no_report else None
    con.execute(
        "UPDATE snapshots SET diff_summary = ?, report_path = ? WHERE id = ?",
        (summary, data["snap"]["report_path"], snapshot_id),
    )
    con.commit()

    if not args.no_report:
        write_text(report_path, render_full_md(data, diff))
        write_text(LATEST_MD, render_latest_md(data, diff))
        write_text(HISTORY_MD, render_history_md(con))

    print(f"ok: снимок #{snapshot_id}")
    print(f"  {raw_info['rel']}")
    if not args.no_report:
        print(f"  {rel_path(report_path)}")
        print(f"  {rel_path(LATEST_MD)}")
        print(f"  {rel_path(HISTORY_MD)}")
    if previous_id is None:
        print("Первый снимок — сравнивать не с чем.")
    elif diff and diff["has_changes"]:
        print(f"Изменения с #{previous_id}: {summary}")
        print(render_diff_md(diff))
    else:
        print(f"Изменений с #{previous_id} нет.")


def cmd_status(con: sqlite3.Connection, _args) -> None:
    now = now_utc()
    server_date = now.astimezone(SERVER_TZ).date().isoformat()
    current_version = None
    row = con.execute(
        """SELECT version FROM game_versions
           WHERE (start_date IS NULL OR start_date <= ?)
             AND (end_date IS NULL OR end_date > ?)
           ORDER BY COALESCE(start_date, '0000-00-00') DESC
           LIMIT 1""",
        (server_date, server_date),
    ).fetchone()
    if row:
        current_version = row["version"]

    print(
        f"Сейчас: {fmt_iso_utc(iso_utc(now))} / {fmt_iso_server(iso_server(now))} "
        f"· серверная дата: {server_date}"
    )
    if current_version:
        print(f"Версия по календарю: {current_version}")

    snap = con.execute(
        """SELECT id, taken_at_server, game_version, player_name, char_num,
                  weapon_num, achieve_count, diff_summary
           FROM snapshots ORDER BY id DESC LIMIT 1"""
    ).fetchone()
    if not snap:
        print("Последний снимок: нет")
        print("Снять: python scripts\\snapshot.py")
        return

    taken = parse_iso(snap["taken_at_server"])
    age_days = None
    age_text = "—"
    if taken:
        seconds = max(0, int((now - taken).total_seconds()))
        age_days = seconds / 86400
        if seconds < 3600:
            age_text = f"{seconds // 60} мин"
        elif seconds < 86400:
            age_text = f"{seconds // 3600} ч {seconds % 3600 // 60} мин"
        else:
            age_text = f"{seconds // 86400} д {seconds % 86400 // 3600} ч"

    print(
        f"Последний снимок: #{snap['id']} · {fmt_short_server(snap['taken_at_server'])} UTC+8 "
        f"({age_text} назад)"
    )
    print(
        f"Версия снимка: {snap['game_version'] or 'unknown'}"
        + (f" · по календарю: {current_version}" if current_version else "")
    )
    print(
        f"Игрок: {snap['player_name'] or '?'} · {fmt_num(snap['char_num'])} операторов · "
        f"{fmt_num(snap['weapon_num'])} оружия · {fmt_num(snap['achieve_count'])} достижений"
    )
    print(f"Изменения: {snap['diff_summary'] or '—'}")

    if current_version and snap["game_version"] and current_version != snap["game_version"]:
        print("Рекомендация: версия сменилась — снять новый снимок.")
    elif age_days is None:
        print("Рекомендация: не удалось определить возраст снимка — проверь вручную.")
    elif age_days >= 7:
        print("Рекомендация: снимок старше 7 дней — снять новый для ростера/гира.")
        print("Для точных ресурсов (рассудок/BP/ежедневки) новый снимок нужен всегда.")
    else:
        print("Рекомендация: для ростера/гира снимок свежий.")
        print("Для точных ресурсов (рассудок/BP/ежедневки) снять новый снимок.")


def cmd_history(con: sqlite3.Connection, args) -> None:
    rows = [dict(r) for r in con.execute(
        "SELECT * FROM snapshots ORDER BY id DESC LIMIT ?",
        (args.limit,),
    )]
    rows.reverse()
    if args.json:
        print(json.dumps(rows, ensure_ascii=False, indent=2))
        return
    if not rows:
        print("Снимков пока нет. Запусти: python scripts\\snapshot.py")
        return
    print(
        f"{'#':>3}  {'Дата (UTC+8)':<16}  {'Вер.':<6}  {'Ур.':>3}  {'World':>5}  "
        f"{'Оп.':>3}  {'Оруж.':>5}  {'Рассудок':>8}  {'BP':>6}  {'Достиж.':>7}  Изменения"
    )
    for row in rows:
        print(
            f"{row.get('id'):>3}  {fmt_short_server(row.get('taken_at_server')):<16}  "
            f"{str(row.get('game_version') or '?'):<6}  {str(row.get('player_level') or '?'):>3}  "
            f"{str(row.get('world_level') or '?'):>5}  {str(row.get('char_num') or '?'):>3}  "
            f"{str(row.get('weapon_num') or '?'):>5}  "
            f"{fmt_ratio(row.get('stamina'), row.get('stamina_max')):>8}  "
            f"{fmt_ratio(row.get('bp_level'), row.get('bp_max')):>6}  "
            f"{str(row.get('achieve_count') or '?'):>7}  {row.get('diff_summary') or '—'}"
        )


def cmd_show(con: sqlite3.Connection, args) -> None:
    snapshot_id = resolve_snapshot_id(con, args.target)
    if snapshot_id is None:
        print("Снимок не найден.")
        return
    data = load_snapshot_data(con, snapshot_id)
    snap = data["snap"]

    if args.json:
        if not snap.get("raw_path"):
            print("У снимка нет raw_path.")
            return
        print(read_raw(snap["raw_path"]))
        return

    if args.full and snap.get("report_path"):
        path = ROOT / snap["report_path"]
        if path.exists():
            print(path.read_text(encoding="utf-8"))
            return

    print(f"Снимок #{snapshot_id} · {fmt_iso_server(snap.get('taken_at_server'))}")
    print(f"Версия: {snap.get('game_version') or 'unknown'} ({version_source_label(snap.get('version_source'))})")
    print(f"Игрок: {snap.get('player_name') or '?'} · ур. {snap.get('player_level')} · world {snap.get('world_level')}")
    print(
        f"Операторов: {fmt_num(snap.get('char_num'))} · оружия: {fmt_num(snap.get('weapon_num'))} · "
        f"достижений: {fmt_num(snap.get('achieve_count'))}"
    )
    print(f"Изменения: {snap.get('diff_summary') or '—'}")
    print(f"Сырой ответ: {snap.get('raw_path') or '—'}")
    print(f"Полный отчёт: {snap.get('report_path') or '—'}")
    if not args.full:
        print("(полный отчёт: --full; сырой JSON: --json)")


def cmd_diff(con: sqlite3.Connection, args) -> None:
    if args.target and args.target2:
        left_id = resolve_snapshot_id(con, args.target)
        right_id = resolve_snapshot_id(con, args.target2)
    elif args.target:
        right_id = resolve_snapshot_id(con, args.target)
        left_id = previous_snapshot_id(con, right_id) if right_id is not None else None
    else:
        right_id = latest_snapshot_id(con)
        left_id = previous_snapshot_id(con, right_id) if right_id is not None else None

    if left_id is None or right_id is None:
        print("Нужно минимум два снимка (или укажи существующие id).")
        return

    previous = load_snapshot_data(con, left_id)
    current = load_snapshot_data(con, right_id)
    diff = compute_diff(previous, current)
    if args.json:
        print(json.dumps(diff, ensure_ascii=False, indent=2))
        return

    left = previous["snap"]
    right = current["snap"]
    print(
        f"# Diff #{left_id} → #{right_id}\n\n"
        f"Слева: {fmt_iso_server(left.get('taken_at_server'))} · версия {left.get('game_version') or '?'}  \n"
        f"Справа: {fmt_iso_server(right.get('taken_at_server'))} · версия {right.get('game_version') or '?'}"
    )
    print()
    print(render_diff_md(diff))


def cmd_report(con: sqlite3.Connection, args) -> None:
    rows = [dict(r) for r in con.execute("SELECT * FROM snapshots ORDER BY id")]
    if not rows:
        print("Снимков пока нет. Запусти: python scripts\\snapshot.py")
        return

    selected = rows
    if args.target and args.target != "all":
        snapshot_id = resolve_snapshot_id(con, args.target)
        if snapshot_id is None:
            print("Снимок не найден.")
            return
        selected = [row for row in rows if row["id"] == snapshot_id]

    for row in selected:
        snapshot_id = int(row["id"])
        data = load_snapshot_data(con, snapshot_id)
        previous_id = previous_snapshot_id(con, snapshot_id)
        previous = load_snapshot_data(con, previous_id) if previous_id else None
        diff = compute_diff(previous, data)
        summary = diff_summary(diff)
        report_path = report_path_for(data["snap"])
        data["snap"]["diff_summary"] = summary
        data["snap"]["report_path"] = rel_path(report_path)
        write_text(report_path, render_full_md(data, diff))
        con.execute(
            "UPDATE snapshots SET diff_summary = ?, report_path = ? WHERE id = ?",
            (summary, data["snap"]["report_path"], snapshot_id),
        )
    con.commit()

    latest_id = latest_snapshot_id(con)
    if latest_id is not None:
        data = load_snapshot_data(con, latest_id)
        previous_id = previous_snapshot_id(con, latest_id)
        previous = load_snapshot_data(con, previous_id) if previous_id else None
        diff = compute_diff(previous, data)
        write_text(LATEST_MD, render_latest_md(data, diff))

    write_text(HISTORY_MD, render_history_md(con))
    print(f"ok: отчёты обновлены ({len(selected)})")


def cmd_versions(con: sqlite3.Connection, _args) -> None:
    rows = [dict(r) for r in con.execute(
        "SELECT * FROM game_versions ORDER BY COALESCE(start_date, '0000-00-00'), version"
    )]
    if not rows:
        print("Календарь версий пуст.")
        return
    print("| Версия | Название | Начало | Конец | Оценка | Заметки |")
    print("|---|---|---|---|---|---|")
    for row in rows:
        print(
            f"| {row.get('version')} | {md_cell(row.get('title'))} | {row.get('start_date') or '—'} | "
            f"{row.get('end_date') or '—'} | {'да' if row.get('estimated') else 'нет'} | "
            f"{md_cell(row.get('notes'))} |"
        )


def cmd_version_set(con: sqlite3.Connection, args) -> None:
    version = (args.target or "").strip()
    if not version:
        raise SystemExit("Укажи версию: snapshot.py version-set 1.7 --start 2026-11-25")
    for value in (args.start, args.end):
        if value:
            try:
                date.fromisoformat(value)
            except ValueError as exc:
                raise SystemExit(f"Дата должна быть YYYY-MM-DD: {value}") from exc

    con.execute(
        """INSERT INTO game_versions (version, title, start_date, end_date, estimated, notes)
           VALUES (?, ?, ?, ?, ?, ?)
           ON CONFLICT(version) DO UPDATE SET
             title = COALESCE(excluded.title, game_versions.title),
             start_date = COALESCE(excluded.start_date, game_versions.start_date),
             end_date = COALESCE(excluded.end_date, game_versions.end_date),
             estimated = excluded.estimated,
             notes = COALESCE(excluded.notes, game_versions.notes)""",
        (
            version,
            args.title,
            args.start,
            args.end,
            1 if args.estimated else 0,
            args.notes,
        ),
    )
    con.commit()
    print(f"ok: версия {version} обновлена")


def main() -> None:
    reconfigure_stdout()
    parser = argparse.ArgumentParser(
        prog="snapshot",
        description="Снимки аккаунта Arknights: Endfield: API → файлы + SQLite + дифф.",
    )
    parser.add_argument(
        "command",
        nargs="?",
        default="take",
        choices=("take", "status", "history", "show", "diff", "report", "versions", "version-set"),
        help="take (по умолчанию), status, history, show, diff, report, versions, version-set",
    )
    parser.add_argument("target", nargs="?", help="id снимка / latest / prev / first; для version-set — версия")
    parser.add_argument("target2", nargs="?", help="второй id для diff")
    parser.add_argument("--db", default=str(DB_PATH), help="путь к SQLite")
    parser.add_argument("--token", help="ACCOUNT_TOKEN (не рекомендуется: попадёт в историю)")
    parser.add_argument("--token-file", help="файл с токеном")
    parser.add_argument("--from-json", help="разобрать сохранённый detail/envelope без сети")
    parser.add_argument("--version", help="версия игры для снимка (переопределяет календарь)")
    parser.add_argument("--version-file", help="файл с версией игры")
    parser.add_argument("--taken-at", help="время снимка, ISO (по умолчанию сейчас)")
    parser.add_argument("--dry-run", action="store_true", help="только показать, не писать")
    parser.add_argument("--no-extras", action="store_true", help="не тянуть доп. GET-эндпоинты")
    parser.add_argument("--no-report", action="store_true", help="не писать Markdown-отчёты")
    parser.add_argument("--no-prune", action="store_true", help="не отмечать отсутствующих операторов как owned=0")
    parser.add_argument("--plain-json", action="store_true", help="писать сырой JSON без gzip")
    parser.add_argument("--limit", type=int, default=30, help="сколько снимков в history")
    parser.add_argument("--json", action="store_true", help="JSON-вывод для history/show/diff")
    parser.add_argument("--full", action="store_true", help="показать полный отчёт в show")
    parser.add_argument("--start", help="version-set: дата начала YYYY-MM-DD")
    parser.add_argument("--end", help="version-set: дата конца YYYY-MM-DD (не включая)")
    parser.add_argument("--title", help="version-set: название версии")
    parser.add_argument("--estimated", action="store_true", help="version-set: дата оценочная")
    parser.add_argument("--notes", help="version-set: заметка")
    args = parser.parse_args()

    if args.command == "take" and args.dry_run:
        con = memory_db()
    else:
        con = open_db(Path(args.db))

    try:
        if args.command == "take":
            cmd_take(con, args)
        elif args.command == "status":
            cmd_status(con, args)
        elif args.command == "history":
            cmd_history(con, args)
        elif args.command == "show":
            cmd_show(con, args)
        elif args.command == "diff":
            cmd_diff(con, args)
        elif args.command == "report":
            cmd_report(con, args)
        elif args.command == "versions":
            cmd_versions(con, args)
        elif args.command == "version-set":
            cmd_version_set(con, args)
    except (RuntimeError, urllib.error.HTTPError, urllib.error.URLError) as exc:
        raise SystemExit(f"Ошибка API: {exc}") from exc
    finally:
        con.close()


if __name__ == "__main__":
    main()

