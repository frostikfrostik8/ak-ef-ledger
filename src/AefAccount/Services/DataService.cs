using System.Globalization;
using System.IO;
using System.Text.Json;
using AefAccount.Localization;
using AefAccount.Models;
using Microsoft.Data.Sqlite;

namespace AefAccount.Services;

public sealed class DataService
{
    public string RepoRoot { get; }
    public string DbPath { get; }

    public DataService()
    {
        RepoRoot = FindRepoRoot();
        DbPath = Path.Combine(RepoRoot, "data", "aef.db");
    }

    public static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "data", "aef.db")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        return Directory.Exists(@"C:\AEF") ? @"C:\AEF" : Directory.GetCurrentDirectory();
    }

    public bool DatabaseExists => File.Exists(DbPath);

    private SqliteConnection Open()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DbPath,
            Mode = SqliteOpenMode.ReadOnly,
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    public SnapshotMeta LoadMeta()
    {
        using var connection = Open();
        var latest = ReadSnapshots(connection, "SELECT * FROM snapshots ORDER BY id DESC LIMIT 1").FirstOrDefault();
        var count = Convert.ToInt32(Scalar(connection, "SELECT COUNT(*) FROM snapshots") ?? 0);
        var now = DateTimeOffset.Now;
        double? ageDays = latest?.TakenAtTime is { } taken ? Math.Max(0, (now - taken).TotalDays) : null;
        return new SnapshotMeta
        {
            Latest = latest,
            SnapshotsCount = count,
            CurrentVersion = ReadCurrentVersion(connection, now),
            AgeDays = ageDays,
            Now = now,
            Versions = ReadVersions(connection),
        };
    }

    public List<SnapshotSummary> LoadSnapshots()
    {
        using var connection = Open();
        return ReadSnapshots(connection, "SELECT * FROM snapshots ORDER BY id");
    }

    public SnapshotData LoadSnapshot(int snapshotId)
    {
        var current = LoadCore(snapshotId);
        if (current.PreviousId is not int previousId) return current;

        var previous = LoadCore(previousId);
        return CopyWithDiff(current, DiffService.Compute(previous, current));
    }

    public DiffModel LoadDiff(int previousId, int currentId)
    {
        var previous = LoadCore(previousId);
        var current = LoadCore(currentId);
        return DiffService.Compute(previous, current);
    }

    private static SnapshotData CopyWithDiff(SnapshotData data, DiffModel diff) => new()
    {
        Snapshot = data.Snapshot,
        Operators = data.Operators,
        Achievements = data.Achievements,
        Domains = data.Domains,
        Rooms = data.Rooms,
        Counters = data.Counters,
        Extras = data.Extras,
        CrisisEvents = data.CrisisEvents,
        WarSeasons = data.WarSeasons,
        IndieGroups = data.IndieGroups,
        Diff = diff,
        PreviousId = data.PreviousId,
        NextId = data.NextId,
    };

    private SnapshotData LoadCore(int snapshotId)
    {
        using var connection = Open();
        var snapshot = ReadSnapshots(connection, "SELECT * FROM snapshots WHERE id = @id", ("@id", snapshotId)).FirstOrDefault()
            ?? throw new InvalidOperationException(L.T("data_snapshot_missing", snapshotId));

        var operators = LoadOperators(connection, snapshotId);
        var gear = LoadGear(connection, snapshotId);
        foreach (var item in gear)
        {
            if (operators.TryGetValue(item.Operator, out var op))
            {
                op.Gear[item.Slot] = item;
            }
        }

        var achievements = LoadAchievements(connection, snapshotId);
        var domains = LoadDomains(connection, snapshotId);
        var rooms = LoadRooms(connection, snapshotId);
        var counters = LoadCounters(connection, snapshotId);
        var sections = LoadJsonTable(connection, "snapshot_sections", "section", snapshotId);
        var extrasRaw = LoadJsonTable(connection, "snapshot_extras", "name", snapshotId);

        var previousId = ReadInt(connection,
            "SELECT id FROM snapshots WHERE id < @id ORDER BY id DESC LIMIT 1", ("@id", snapshotId));
        var nextId = ReadInt(connection,
            "SELECT id FROM snapshots WHERE id > @id ORDER BY id LIMIT 1", ("@id", snapshotId));

        return new SnapshotData
        {
            Snapshot = snapshot,
            Operators = operators.Values.OrderByDescending(op => op.Rarity).ThenByDescending(op => op.Potential).ThenBy(op => op.Name).ToList(),
            Achievements = achievements,
            Domains = domains,
            Rooms = rooms,
            Counters = counters,
            Extras = ParseExtras(extrasRaw),
            CrisisEvents = ParseCrisis(sections.GetValueOrDefault("crisisContract")),
            WarSeasons = ParseWarEchoes(sections.GetValueOrDefault("warEchoes")),
            IndieGroups = ParseIndie(sections.GetValueOrDefault("indieHard")),
            PreviousId = previousId,
            NextId = nextId,
        };
    }

    private Dictionary<string, OperatorModel> LoadOperators(SqliteConnection connection, int snapshotId)
    {
        var result = new Dictionary<string, OperatorModel>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM snapshot_operators WHERE snapshot_id = @id";
        command.Parameters.AddWithValue("@id", snapshotId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var op = new OperatorModel
            {
                Name = Str(reader, "name") ?? "?",
                CharId = Str(reader, "char_id"),
                Rarity = Int(reader, "rarity"),
                Element = Str(reader, "element"),
                Class = Str(reader, "class"),
                Potential = Int(reader, "potential") ?? 0,
                Level = Int(reader, "level"),
                Evolve = Int(reader, "evolve"),
                OwnTs = Str(reader, "own_ts"),
                AvatarUrl = Str(reader, "avatar_url"),
                IllustrationUrl = Str(reader, "illustration_url"),
                WeaponName = Str(reader, "weapon_name"),
                WeaponType = Str(reader, "weapon_type"),
                WeaponRarity = Int(reader, "weapon_rarity"),
                WeaponLevel = Int(reader, "weapon_level"),
                WeaponRefine = Int(reader, "weapon_refine"),
                WeaponBreakthrough = Int(reader, "weapon_breakthrough"),
                WeaponGem = Str(reader, "weapon_gem"),
                Tags = ParseStringArray(Str(reader, "tags_json")),
                Skills = ParseSkills(Str(reader, "skills_json")),
                Talents = ParseTalents(Str(reader, "talent_json")),
                WeaponSkills = ParseWeaponSkills(Str(reader, "weapon_skills_json")),
            };
            result[op.Name] = op;
        }
        return result;
    }

    private static List<GearModel> LoadGear(SqliteConnection connection, int snapshotId)
    {
        var result = new List<GearModel>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM snapshot_gear WHERE snapshot_id = @id";
        command.Parameters.AddWithValue("@id", snapshotId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new GearModel
            {
                Operator = Str(reader, "operator") ?? "",
                Slot = Str(reader, "slot") ?? "",
                Name = Str(reader, "name"),
                Level = Int(reader, "level"),
                Enhance = Str(reader, "enhance"),
                Suit = Str(reader, "suit"),
                Rarity = Str(reader, "rarity"),
                EquipId = Str(reader, "equip_id"),
            });
        }
        return result;
    }

    private static List<AchievementModel> LoadAchievements(SqliteConnection connection, int snapshotId)
    {
        var result = new List<AchievementModel>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM snapshot_achievements WHERE snapshot_id = @id";
        command.Parameters.AddWithValue("@id", snapshotId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new AchievementModel
            {
                Id = Str(reader, "achieve_id") ?? "",
                Name = Str(reader, "name"),
                Category = Str(reader, "category"),
                Cate = Str(reader, "cate"),
                Level = Int(reader, "level"),
                InitLevel = Int(reader, "init_level"),
                IsPlated = (Int(reader, "is_plated") ?? 0) != 0,
                ObtainedTs = Str(reader, "obtained_ts"),
            });
        }
        return result
            .OrderByDescending(item => long.TryParse(item.ObtainedTs, out var seconds) ? seconds : 0)
            .ToList();
    }

    private static List<DomainModel> LoadDomains(SqliteConnection connection, int snapshotId)
    {
        var domains = new Dictionary<string, DomainBuilder>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM snapshot_domains WHERE snapshot_id = @id ORDER BY name";
            command.Parameters.AddWithValue("@id", snapshotId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = Str(reader, "domain_id") ?? "";
                domains[id] = new DomainBuilder
                {
                    Id = id,
                    Name = Str(reader, "name"),
                    Level = Int(reader, "level"),
                    MoneyTotal = Long(reader, "money_total"),
                    MoneyCount = Long(reader, "money_count"),
                };
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM snapshot_settlements WHERE snapshot_id = @id";
            command.Parameters.AddWithValue("@id", snapshotId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var domainId = Str(reader, "domain_id") ?? "";
                if (!domains.TryGetValue(domainId, out var domain)) continue;
                domain.Settlements.Add(new SettlementModel
                {
                    Id = Str(reader, "settlement_id") ?? "",
                    Name = Str(reader, "name"),
                    Level = Int(reader, "level"),
                    Money = Long(reader, "money"),
                });
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM snapshot_collections WHERE snapshot_id = @id";
            command.Parameters.AddWithValue("@id", snapshotId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var domainId = Str(reader, "domain_id") ?? "";
                if (!domains.TryGetValue(domainId, out var domain)) continue;
                var metrics = new List<MetricModel>
                {
                    Metric("chest", "Сундуки", reader),
                    Metric("puzzle", "Головоломки", reader),
                    Metric("blackbox", "Чёрные ящики", reader),
                    Metric("piece", "Детали", reader),
                    Metric("equip_chest", "Снаряж. сундуки", reader),
                    Metric("trstar", "trstar", reader),
                };
                domain.Collections.Add(new CollectionModel
                {
                    DomainId = domainId,
                    LevelId = Str(reader, "level_id") ?? "",
                    Name = Str(reader, "name"),
                    Metrics = metrics,
                });
            }
        }

        return domains.Values
            .OrderBy(domain => domain.Name)
            .Select(domain => new DomainModel
            {
                Id = domain.Id,
                Name = domain.Name,
                Level = domain.Level,
                MoneyTotal = domain.MoneyTotal,
                MoneyCount = domain.MoneyCount,
                Settlements = domain.Settlements.OrderBy(item => item.Name).ToList(),
                Collections = domain.Collections.OrderBy(item => item.LevelId).ToList(),
            })
            .ToList();
    }

    private static MetricModel Metric(string key, string label, SqliteDataReader reader) => new()
    {
        Key = key,
        Label = label,
        Count = Int(reader, $"{key}_count") ?? 0,
        Total = Int(reader, $"{key}_total") ?? 0,
    };

    private static List<ShipRoomModel> LoadRooms(SqliteConnection connection, int snapshotId)
    {
        var result = new List<ShipRoomModel>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM snapshot_spaceship_rooms WHERE snapshot_id = @id ORDER BY room_index";
        command.Parameters.AddWithValue("@id", snapshotId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var chars = new List<ShipCharacterModel>();
            var json = Str(reader, "chars_json");
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    using var document = JsonDocument.Parse(json);
                    foreach (var item in document.RootElement.EnumerateArray())
                    {
                        chars.Add(new ShipCharacterModel
                        {
                            CharId = Text(item, "char_id"),
                            Favorability = Number(item, "favorability"),
                            PhysicalStrength = Number(item, "physical_strength"),
                        });
                    }
                }
                catch (JsonException)
                {
                    // Пустой/invalid JSON — просто без операторов.
                }
            }
            result.Add(new ShipRoomModel
            {
                Index = Int(reader, "room_index") ?? 0,
                Type = Int(reader, "room_type"),
                Level = Int(reader, "level"),
                Characters = chars,
            });
        }
        return result;
    }

    private static Dictionary<string, CounterModel> LoadCounters(SqliteConnection connection, int snapshotId)
    {
        var result = new Dictionary<string, CounterModel>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"key\", label, value_num, value_text FROM snapshot_counters WHERE snapshot_id = @id";
        command.Parameters.AddWithValue("@id", snapshotId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var key = Str(reader, "key") ?? "";
            result[key] = new CounterModel
            {
                Key = key,
                Label = Str(reader, "label"),
                ValueNum = Number(reader, "value_num"),
                ValueText = Str(reader, "value_text"),
            };
        }
        return result;
    }

    private static Dictionary<string, string> LoadJsonTable(SqliteConnection connection, string table, string nameColumn, int snapshotId)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {nameColumn}, json FROM {table} WHERE snapshot_id = @id";
        command.Parameters.AddWithValue("@id", snapshotId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result[Str(reader, nameColumn) ?? ""] = Str(reader, "json") ?? "";
        }
        return result;
    }

    private static List<VersionInfo> ReadVersions(SqliteConnection connection)
    {
        var result = new List<VersionInfo>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT version, title, start_date, end_date, estimated, notes FROM game_versions ORDER BY COALESCE(start_date, '0000-00-00')";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new VersionInfo
            {
                Version = Str(reader, "version") ?? "?",
                Title = Str(reader, "title"),
                StartDate = Str(reader, "start_date"),
                EndDate = Str(reader, "end_date"),
                Estimated = (Int(reader, "estimated") ?? 0) != 0,
                Notes = Str(reader, "notes"),
            });
        }
        return result;
    }

    private string? ReadCurrentVersion(SqliteConnection connection, DateTimeOffset now)
    {
        var versionFile = Path.Combine(RepoRoot, "data", "game_version.txt");
        if (File.Exists(versionFile))
        {
            var line = File.ReadLines(versionFile)
                .Select(value => value.Trim())
                .FirstOrDefault(value => value.Length > 0 && !value.StartsWith('#'));
            if (!string.IsNullOrWhiteSpace(line)) return line;
        }

        var serverDate = now.ToOffset(AppCulture.ServerOffset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT version FROM game_versions
            WHERE (start_date IS NULL OR start_date <= @date)
              AND (end_date IS NULL OR end_date > @date)
            ORDER BY COALESCE(start_date, '0000-00-00') DESC
            LIMIT 1
            """;
        command.Parameters.AddWithValue("@date", serverDate);
        return command.ExecuteScalar()?.ToString();
    }

    private static List<SnapshotSummary> ReadSnapshots(SqliteConnection connection, string sql, params (string Name, object Value)[] args)
    {
        var result = new List<SnapshotSummary>();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in args)
        {
            command.Parameters.AddWithValue(name, value);
        }
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new SnapshotSummary
            {
                Id = Int(reader, "id") ?? 0,
                TakenAt = Str(reader, "taken_at") ?? "",
                TakenAtServer = Str(reader, "taken_at_server") ?? "",
                GameVersion = Str(reader, "game_version"),
                PlayerName = Str(reader, "player_name"),
                RoleId = Str(reader, "role_id"),
                ServerId = Str(reader, "server_id"),
                ServerName = Str(reader, "server_name"),
                PlayerLevel = Int(reader, "player_level"),
                WorldLevel = Int(reader, "world_level"),
                Exp = Int(reader, "exp"),
                CharNum = Int(reader, "char_num"),
                WeaponNum = Int(reader, "weapon_num"),
                DocNum = Int(reader, "doc_num"),
                Stamina = Int(reader, "stamina"),
                StaminaMax = Int(reader, "stamina_max"),
                BpLevel = Int(reader, "bp_level"),
                BpMax = Int(reader, "bp_max"),
                Daily = Int(reader, "daily"),
                DailyMax = Int(reader, "daily_max"),
                Weekly = Int(reader, "weekly"),
                WeeklyMax = Int(reader, "weekly_max"),
                Credit = Int(reader, "credit"),
                CreditMax = Int(reader, "credit_max"),
                Seek = Int(reader, "seek"),
                SeekMax = Int(reader, "seek_max"),
                AchieveCount = Int(reader, "achieve_count"),
                DiffSummary = Str(reader, "diff_summary"),
                MainMission = Str(reader, "main_mission"),
                CreatedTs = Str(reader, "created_ts"),
                LastLoginTs = Str(reader, "last_login_ts"),
                ReportPath = Str(reader, "report_path"),
                RawPath = Str(reader, "raw_path"),
            });
        }
        return result;
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static int? ReadInt(SqliteConnection connection, string sql, params (string Name, object Value)[] args)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in args) command.Parameters.AddWithValue(name, value);
        var result = command.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static List<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.EnumerateArray().Select(item => item.GetString() ?? "").Where(item => item.Length > 0).ToList()
                : [];
        }
        catch (JsonException) { return []; }
    }

    private static List<SkillModel> ParseSkills(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
            return document.RootElement.EnumerateArray().Select(item => new SkillModel
            {
                Name = Text(item, "name"),
                Type = Text(item, "type"),
                Level = Integer(item, "level"),
                Max = Integer(item, "max"),
            }).ToList();
        }
        catch (JsonException) { return []; }
    }

    private static TalentModel ParseTalents(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new TalentModel();
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return new TalentModel
            {
                BreakNode = Text(root, "latestBreakNode"),
                PassiveCount = ArrayLength(root, "latestPassiveSkillNodes"),
                FactoryCount = ArrayLength(root, "latestFactorySkillNodes"),
                ShipCount = ArrayLength(root, "latestSpaceshipSkillNodes"),
            };
        }
        catch (JsonException) { return new TalentModel(); }
    }

    private static List<WeaponSkillModel> ParseWeaponSkills(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
            return document.RootElement.EnumerateArray().Select(item => new WeaponSkillModel
            {
                Name = Text(item, "name"),
                Level = Integer(item, "level"),
                Max = Integer(item, "max"),
            }).ToList();
        }
        catch (JsonException) { return []; }
    }

    private static ExtrasModel ParseExtras(Dictionary<string, string> raw)
    {
        var attendance = new AttendanceModel();
        if (TryRoot(raw.GetValueOrDefault("attendance"), out var attendanceRoot))
        {
            var calendar = GetArray(attendanceRoot, "calendar");
            attendance = new AttendanceModel
            {
                Signed = calendar.Count(item => GetBool(item, "done")),
                Total = calendar.Count,
                HasToday = GetBool(attendanceRoot, "hasToday"),
            };
        }

        var inventory = new InventoryModel();
        if (TryRoot(raw.GetValueOrDefault("user_game_data"), out var gameRoot) &&
            gameRoot.TryGetProperty("userGameData", out var gameData))
        {
            var weapons = GetObject(gameData, "userWeapons");
            var equips = GetObject(gameData, "userEquips");
            var tactical = GetObject(gameData, "userTacticalItems");
            inventory = new InventoryModel
            {
                Weapons = weapons.Count,
                Equips = equips.Count,
                EquipCopies = equips.Values.Sum(item => Integer(item, "ownedCount") ?? 0),
                TacticalItems = tactical.Count,
            };
        }

        var profile = new ProfileModel();
        if (TryRoot(raw.GetValueOrDefault("user"), out var userRoot) &&
            userRoot.TryGetProperty("user", out var user) &&
            user.TryGetProperty("basicUser", out var basic))
        {
            profile = new ProfileModel
            {
                Nickname = Text(basic, "nickname"),
                Created = Text(basic, "createdAt"),
            };
        }

        return new ExtrasModel
        {
            Attendance = attendance,
            Inventory = inventory,
            Profile = profile,
            CharacterPools = ParsePools(raw.GetValueOrDefault("char_pool")),
            WeaponPools = ParsePools(raw.GetValueOrDefault("weapon_pool")),
        };
    }

    private static List<PoolModel> ParsePools(string? json)
    {
        if (!TryRoot(json, out var root)) return [];
        var list = GetArray(root, "list");
        return list.Select(item => new PoolModel
        {
            Name = Text(item, "name"),
            Start = Text(item, "startAtTs") ?? Text(item, "poolStartAtTs"),
            End = Text(item, "endAtTs") ?? Text(item, "poolEndAtTs"),
        }).ToList();
    }

    private static List<CrisisEventModel> ParseCrisis(string? json)
    {
        if (!TryRoot(json, out var root) || root.ValueKind != JsonValueKind.Array) return [];
        return root.EnumerateArray().Select(item =>
        {
            var weekly = GetObjectElement(item, "weeklyMission");
            var indicator = GetObjectElement(item, "indicatorMission");
            var stage = GetObjectElement(item, "stageMission");
            return new CrisisEventModel
            {
                Name = Text(item, "name") ?? "Crisis Contract",
                Highest = Integer(item, "highest"),
                ChallengeCount = Integer(item, "challengeCount"),
                WeeklyCount = weekly.ValueKind == JsonValueKind.Object ? Integer(weekly, "count") : null,
                WeeklyTotal = weekly.ValueKind == JsonValueKind.Object ? Integer(weekly, "total") : null,
                IndicatorCount = indicator.ValueKind == JsonValueKind.Object ? Integer(indicator, "count") : null,
                IndicatorTotal = indicator.ValueKind == JsonValueKind.Object ? Integer(indicator, "total") : null,
                StageCount = stage.ValueKind == JsonValueKind.Object ? Integer(stage, "count") : null,
                StageTotal = stage.ValueKind == JsonValueKind.Object ? Integer(stage, "total") : null,
                EndTs = Text(item, "endAtTs"),
            };
        }).ToList();
    }

    private static List<WarSeasonModel> ParseWarEchoes(string? json)
    {
        if (!TryRoot(json, out var root)) return [];
        var seasons = GetArray(root, "seasons");
        return seasons.Select(item => new WarSeasonModel
        {
            Name = Text(item, "name"),
            Stars = Integer(item, "stars"),
            EndTs = Text(item, "endTs"),
            Weeks = GetArray(item, "weeks").Select(week => new WarWeekModel
            {
                Name = Text(week, "name"),
                Stars = Integer(week, "stars"),
            }).ToList(),
        }).ToList();
    }

    private static List<IndieGroupModel> ParseIndie(string? json)
    {
        if (!TryRoot(json, out var root)) return [];
        var groups = GetArray(root, "indieHardGroups");
        return groups.Select(group =>
        {
            var dungeons = GetArray(group, "dungeonGroups");
            var normal = dungeons.Count(item => GetBool(GetObjectElement(item, "normalDungeon"), "isPass"));
            var hard = dungeons.Count(item => GetBool(GetObjectElement(item, "hardDungeon"), "isPass"));
            return new IndieGroupModel
            {
                Name = Text(group, "name"),
                Total = dungeons.Count,
                NormalPassed = normal,
                HardPassed = hard,
            };
        }).ToList();
    }

    private static bool TryRoot(string? json, out JsonElement root)
    {
        root = default;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            root = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            ? value.ValueKind == JsonValueKind.Null ? null : value.ToString()
            : null;

    private static int? Integer(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out var number) ? number : null,
            JsonValueKind.String => int.TryParse(value.GetString(), out var number) ? number : null,
            _ => null,
        };
    }

    private static double? Number(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String => double.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var number) ? number : null,
            _ => null,
        };
    }

    private static bool GetBool(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) &&
        (value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed) && parsed));

    private static List<JsonElement> GetArray(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(item => item.Clone()).ToList()
            : [];

    private static Dictionary<string, JsonElement> GetObject(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, JsonElement>();
        return value.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.Clone());
    }

    private static JsonElement GetObjectElement(JsonElement element, string property)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.Object)
        {
            return value.Clone();
        }
        return default;
    }

    private static int ArrayLength(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength()
            : 0;

    private static string? Str(SqliteDataReader reader, string name)
    {
        var index = reader.GetOrdinal(name);
        return reader.IsDBNull(index) ? null : reader.GetValue(index)?.ToString();
    }

    private static int? Int(SqliteDataReader reader, string name)
    {
        var index = reader.GetOrdinal(name);
        return reader.IsDBNull(index) ? null : Convert.ToInt32(reader.GetValue(index), CultureInfo.InvariantCulture);
    }

    private static long? Long(SqliteDataReader reader, string name)
    {
        var index = reader.GetOrdinal(name);
        return reader.IsDBNull(index) ? null : Convert.ToInt64(reader.GetValue(index), CultureInfo.InvariantCulture);
    }

    private static double? Number(SqliteDataReader reader, string name)
    {
        var index = reader.GetOrdinal(name);
        return reader.IsDBNull(index) ? null : Convert.ToDouble(reader.GetValue(index), CultureInfo.InvariantCulture);
    }

    private sealed class DomainBuilder
    {
        public string Id { get; init; } = "";
        public string? Name { get; init; }
        public int? Level { get; init; }
        public long? MoneyTotal { get; init; }
        public long? MoneyCount { get; init; }
        public List<SettlementModel> Settlements { get; } = [];
        public List<CollectionModel> Collections { get; } = [];
    }
}

