PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS operators (
    name        TEXT PRIMARY KEY,
    rarity      INTEGER NOT NULL,
    element     TEXT,
    class       TEXT,
    potential   INTEGER NOT NULL DEFAULT 0,
    owned       INTEGER NOT NULL DEFAULT 0,
    notes       TEXT
);

CREATE TABLE IF NOT EXISTS banners (
    id          INTEGER PRIMARY KEY,
    version     TEXT,
    name        TEXT NOT NULL,
    type        TEXT,
    start_date  TEXT,
    end_date    TEXT,
    featured    TEXT,
    notes       TEXT
);

CREATE TABLE IF NOT EXISTS pulls (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    date        TEXT NOT NULL,
    banner      TEXT,
    count       INTEGER NOT NULL,
    currency    TEXT,
    result      TEXT,
    notes       TEXT
);

CREATE TABLE IF NOT EXISTS notes (
    id          INTEGER PRIMARY KEY,
    date        TEXT NOT NULL,
    topic       TEXT,
    body        TEXT
);

CREATE TABLE IF NOT EXISTS sources (
    id          INTEGER PRIMARY KEY,
    title       TEXT,
    url         TEXT,
    checked_at  TEXT
);

-- Данные из API (scripts/sync.py)

CREATE TABLE IF NOT EXISTS weapons (
    operator    TEXT NOT NULL,
    name        TEXT NOT NULL,
    rarity      INTEGER,
    level       INTEGER,
    refine      INTEGER,
    breakthrough INTEGER,
    PRIMARY KEY (operator, name)
);

CREATE TABLE IF NOT EXISTS gear (
    operator    TEXT NOT NULL,
    slot        TEXT NOT NULL,
    name        TEXT,
    level       INTEGER,
    enhance     TEXT,
    PRIMARY KEY (operator, slot)
);

CREATE TABLE IF NOT EXISTS sync_log (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    ts          TEXT NOT NULL,
    source      TEXT,
    chars       INTEGER,
    note        TEXT
);

-- История снимков аккаунта (scripts/snapshot.py).
-- Снимок = один ответ card/detail + дополнительные GET-эндпоинты.
-- Сырой ответ лежит в data/snapshots/*.json.gz, здесь — разложенные данные.

CREATE TABLE IF NOT EXISTS game_versions (
    version     TEXT PRIMARY KEY,
    title       TEXT,
    start_date  TEXT,                 -- YYYY-MM-DD, серверное время UTC+8
    end_date    TEXT,                 -- YYYY-MM-DD, не включая; NULL = открытая
    estimated   INTEGER NOT NULL DEFAULT 0,
    notes       TEXT
);

CREATE TABLE IF NOT EXISTS snapshots (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    taken_at          TEXT NOT NULL,          -- ISO UTC
    taken_at_server   TEXT NOT NULL,          -- ISO UTC+8
    game_version      TEXT,
    version_source    TEXT,                   -- cli / file / calendar / banners / none
    api_current_ts    TEXT,                   -- currentTs из ответа, ISO UTC
    source            TEXT NOT NULL DEFAULT 'skport',
    player_name       TEXT,
    role_id           TEXT,
    server_id         TEXT,
    server_name       TEXT,
    player_level      INTEGER,
    world_level       INTEGER,
    exp               INTEGER,
    gender            INTEGER,
    main_mission      TEXT,
    created_ts        TEXT,
    last_login_ts     TEXT,
    save_ts           TEXT,
    char_num          INTEGER,
    weapon_num        INTEGER,
    doc_num           INTEGER,
    stamina           INTEGER,
    stamina_max       INTEGER,
    stamina_full_at   TEXT,
    bp_level          INTEGER,
    bp_max            INTEGER,
    daily             INTEGER,
    daily_max         INTEGER,
    weekly            INTEGER,
    weekly_max        INTEGER,
    credit            INTEGER,
    credit_max        INTEGER,
    seek              INTEGER,
    seek_max          INTEGER,
    achieve_count     INTEGER,
    raw_path          TEXT,
    report_path       TEXT,
    raw_sha256        TEXT,
    raw_bytes         INTEGER,
    diff_summary      TEXT,
    notes             TEXT
);
CREATE INDEX IF NOT EXISTS idx_snapshots_taken_at ON snapshots(taken_at DESC);
CREATE INDEX IF NOT EXISTS idx_snapshots_version ON snapshots(game_version);

CREATE TABLE IF NOT EXISTS snapshot_operators (
    snapshot_id         INTEGER NOT NULL REFERENCES snapshots(id) ON DELETE CASCADE,
    name                TEXT NOT NULL,
    char_id             TEXT,
    rarity              INTEGER,
    element             TEXT,
    class               TEXT,
    potential           INTEGER,
    level               INTEGER,
    evolve              INTEGER,
    own_ts              TEXT,
    tags_json           TEXT,
    talent_json         TEXT,
    skills_json         TEXT,
    gear_json           TEXT,
    weapon_id           TEXT,
    weapon_name         TEXT,
    weapon_type         TEXT,
    weapon_rarity       INTEGER,
    weapon_level        INTEGER,
    weapon_refine       INTEGER,
    weapon_breakthrough INTEGER,
    weapon_gem          TEXT,
    weapon_skills_json  TEXT,
    avatar_url          TEXT,
    illustration_url    TEXT,
    PRIMARY KEY (snapshot_id, name)
);

CREATE TABLE IF NOT EXISTS snapshot_gear (
    snapshot_id INTEGER NOT NULL REFERENCES snapshots(id) ON DELETE CASCADE,
    operator    TEXT NOT NULL,
    slot        TEXT NOT NULL,          -- body / arm / acc1 / acc2 / tactical
    name        TEXT,
    level       INTEGER,
    enhance     TEXT,
    suit        TEXT,
    rarity      TEXT,
    equip_id    TEXT,
    PRIMARY KEY (snapshot_id, operator, slot)
);

CREATE TABLE IF NOT EXISTS snapshot_achievements (
    snapshot_id INTEGER NOT NULL REFERENCES snapshots(id) ON DELETE CASCADE,
    achieve_id  TEXT NOT NULL,
    name        TEXT,
    category    TEXT,
    cate        TEXT,
    level       INTEGER,
    init_level  INTEGER,
    is_plated   INTEGER NOT NULL DEFAULT 0,
    obtained_ts TEXT,
    PRIMARY KEY (snapshot_id, achieve_id)
);

CREATE TABLE IF NOT EXISTS snapshot_domains (
    snapshot_id INTEGER NOT NULL REFERENCES snapshots(id) ON DELETE CASCADE,
    domain_id   TEXT NOT NULL,
    name        TEXT,
    level       INTEGER,
    money_total INTEGER,
    money_count INTEGER,
    PRIMARY KEY (snapshot_id, domain_id)
);

CREATE TABLE IF NOT EXISTS snapshot_settlements (
    snapshot_id         INTEGER NOT NULL REFERENCES snapshots(id) ON DELETE CASCADE,
    domain_id           TEXT NOT NULL,
    settlement_id       TEXT NOT NULL,
    name                TEXT,
    level               INTEGER,
    exp                 INTEGER,
    exp_to_level        INTEGER,
    money               INTEGER,
    money_max           INTEGER,
    is_final_max_level  INTEGER,
    officer_id          TEXT,
    PRIMARY KEY (snapshot_id, domain_id, settlement_id)
);

CREATE TABLE IF NOT EXISTS snapshot_collections (
    snapshot_id         INTEGER NOT NULL REFERENCES snapshots(id) ON DELETE CASCADE,
    domain_id           TEXT NOT NULL,
    level_id            TEXT NOT NULL,
    name                TEXT,
    puzzle_count        INTEGER,
    puzzle_total        INTEGER,
    chest_count         INTEGER,
    chest_total         INTEGER,
    equip_chest_count   INTEGER,
    equip_chest_total   INTEGER,
    piece_count         INTEGER,
    piece_total         INTEGER,
    blackbox_count      INTEGER,
    blackbox_total      INTEGER,
    trstar_count        INTEGER,
    trstar_total        INTEGER,
    PRIMARY KEY (snapshot_id, domain_id, level_id)
);

CREATE TABLE IF NOT EXISTS snapshot_spaceship_rooms (
    snapshot_id INTEGER NOT NULL REFERENCES snapshots(id) ON DELETE CASCADE,
    room_index  INTEGER NOT NULL,
    room_type   INTEGER,
    level       INTEGER,
    chars_json  TEXT,
    PRIMARY KEY (snapshot_id, room_index)
);

CREATE TABLE IF NOT EXISTS snapshot_spaceship_chars (
    snapshot_id       INTEGER NOT NULL REFERENCES snapshots(id) ON DELETE CASCADE,
    char_id           TEXT NOT NULL,
    room_index        INTEGER NOT NULL,
    room_type         INTEGER,
    favorability      REAL,
    physical_strength REAL,
    PRIMARY KEY (snapshot_id, char_id, room_index)
);

CREATE TABLE IF NOT EXISTS snapshot_counters (
    snapshot_id INTEGER NOT NULL REFERENCES snapshots(id) ON DELETE CASCADE,
    key         TEXT NOT NULL,
    label       TEXT,
    value_num   REAL,
    value_text  TEXT,
    PRIMARY KEY (snapshot_id, key)
);

CREATE TABLE IF NOT EXISTS snapshot_sections (
    snapshot_id INTEGER NOT NULL REFERENCES snapshots(id) ON DELETE CASCADE,
    section     TEXT NOT NULL,
    json        TEXT,
    PRIMARY KEY (snapshot_id, section)
);

CREATE TABLE IF NOT EXISTS snapshot_extras (
    snapshot_id INTEGER NOT NULL REFERENCES snapshots(id) ON DELETE CASCADE,
    name        TEXT NOT NULL,
    json        TEXT,
    PRIMARY KEY (snapshot_id, name)
);

-- Календарь версий. Даты — серверные (UTC+8), из context/banners.md.
-- 1.6: точная дата старта не зафиксирована, поэтому start_date = NULL.
-- 2.0: дата оценочная, помечена estimated = 1.
INSERT OR IGNORE INTO game_versions
    (version, title, start_date, end_date, estimated, notes)
VALUES
    ('1.6', NULL, NULL, '2026-11-25', 0,
     'Версия на 2026-11-23; точная дата старта не зафиксирована.'),
    ('1.7', 'Meeting You in the World o''Shrooms', '2026-11-25', '2027-01-07', 0,
     'Старт 2026-11-25 (UTC+8); техработы 06:00–12:00.'),
    ('2.0', NULL, '2027-01-07', NULL, 1,
     'Дата оценочная (6-недельный цикл); подтвердить после анонса.');
