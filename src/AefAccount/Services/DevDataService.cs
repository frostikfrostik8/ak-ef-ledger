using System.Globalization;
using AefAccount.Localization;
using AefAccount.Models;

namespace AefAccount.Services;

public sealed class DevProfile
{
    public required List<SnapshotSummary> Snapshots { get; init; }
    public required Dictionary<int, SnapshotData> Data { get; init; }
}

public static class DevDataService
{
    public const int FirstId = 9001;
    public const int Count = 8;

    private static readonly string[] Elements = ["Physical", "Heat", "Electric", "Cryo", "Nature"];
    private static readonly string[] Classes = ["Vanguard", "Guard", "Defender", "Caster", "Striker", "Supporter"];

    public static DevProfile Create()
    {
        var snapshots = new List<SnapshotSummary>(Count);
        var data = new Dictionary<int, SnapshotData>(Count);
        SnapshotData? previous = null;

        for (var index = 0; index < Count; index++)
        {
            var id = FirstId + index;
            var date = new DateTimeOffset(2026, 11, 16 + index, 12, 0, 0, TimeSpan.FromHours(8));
            var level = 58 + (index + 1) / 3;
            var world = 6 + index / 4;
            var stamina = 92 + (index * 23) % 130;
            var bp = 45 + index * 2;
            var daily = (index * 17) % 101;
            var weekly = 2 + index / 2;
            var credit = 12 + index * 3;
            var seek = 150_000 + index * 7_000;
            var achieve = 140 + index * 2;
            var chars = 28 + index / 4;
            var weapons = 58 + index;
            var docs = 480 + index * 4;

            var summary = new SnapshotSummary
            {
                Id = id,
                TakenAt = date.ToString("o", CultureInfo.InvariantCulture),
                TakenAtServer = date.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
                GameVersion = "1.6",
                PlayerName = "dev / frostik",
                RoleId = "DEV-0001",
                ServerName = "Dev Server",
                PlayerLevel = level,
                WorldLevel = world,
                Exp = 300_000 + index * 9_500,
                CharNum = chars,
                WeaponNum = weapons,
                DocNum = docs,
                Stamina = stamina,
                StaminaMax = 360,
                BpLevel = bp,
                BpMax = 60,
                Daily = daily,
                DailyMax = 100,
                Weekly = weekly,
                WeeklyMax = 10,
                Credit = credit,
                CreditMax = 300,
                Seek = seek,
                SeekMax = 200_000,
                AchieveCount = achieve,
                DiffSummary = index == 0 ? L.T("dev_diff_first") : L.T("dev_diff_growth"),
                MainMission = "Test the timeline",
                CreatedTs = "2025-12-01T10:00:00+08:00",
                LastLoginTs = date.ToString("o", CultureInfo.InvariantCulture),
                ReportPath = null,
                RawPath = null,
            };
            snapshots.Add(summary);

            var current = BuildData(summary, index, level, chars, weapons, achieve, stamina, bp, daily, weekly, credit, seek);
            if (previous != null)
            {
                current = WithDiff(current, DiffService.Compute(previous, current), previous.Snapshot.Id);
            }
            data[id] = current;
            previous = current;
        }

        return new DevProfile { Snapshots = snapshots, Data = data };
    }

    private static SnapshotData BuildData(
        SnapshotSummary summary,
        int index,
        int level,
        int chars,
        int weapons,
        int achieve,
        int stamina,
        int bp,
        int daily,
        int weekly,
        int credit,
        int seek)
    {
        var vesperLevel = 80 + Math.Min(10, index);
        var operators = new List<OperatorModel>
        {
            BuildVesper(vesperLevel, 5),
            BuildOperator("Rossi", 6, "Physical", "Guard", 0, level, 4, "Wolf's Bane", 80, 0, 3, 7),
            BuildOperator("Si", 6, "Cryo", "Supporter", 0, 90, 4, "Jade Frost", 90, 0, 3, 7),
            BuildOperator("Test Subject 01", 5, "Electric", "Caster", 3, 40 + index, 2, "Test Frame", 40, 0, 1, 4),
        };

        var achievements = new List<AchievementModel>
        {
            new() { Id = "dev-ach-1", Name = "Timeline Walker", Category = "Dev", Cate = "Dev", Level = 1, IsPlated = true, ObtainedTs = "1793000000" },
            new() { Id = "dev-ach-2", Name = "Chart Reader", Category = "Dev", Cate = "Dev", Level = 2, IsPlated = false, ObtainedTs = "1793100000" },
            new() { Id = "dev-ach-3", Name = "Snapshot Tester", Category = "Dev", Cate = "Dev", Level = 3, IsPlated = false, ObtainedTs = "1793200000" },
        };

        var domain = new DomainModel
        {
            Id = "dev_domain",
            Name = "Dev Outpost",
            Level = 10 + index,
            MoneyTotal = 30_000_000,
            MoneyCount = 700_000 + index * 25_000,
            Settlements =
            [
                new SettlementModel { Id = "dev_settlement", Name = "Test Camp", Level = 3, Money = 250_000 },
            ],
            Collections =
            [
                new CollectionModel
                {
                    DomainId = "dev_domain",
                    LevelId = "dev_level",
                    Name = "Test Sector",
                    Metrics =
                    [
                        new MetricModel { Key = "chests", Label = "Сундуки", Count = 8 + index, Total = 20 },
                        new MetricModel { Key = "heads", Label = "Головоломки", Count = 3 + index / 2, Total = 10 },
                        new MetricModel { Key = "boxes", Label = "Чёрные ящики", Count = 1 + index / 3, Total = 5 },
                    ],
                },
            ],
        };

        var counters = new Dictionary<string, CounterModel>
        {
            ["stamina.current"] = new() { Key = "stamina.current", Label = "Рассудок", ValueNum = stamina },
            ["stamina.max"] = new() { Key = "stamina.max", Label = "Рассудок (макс.)", ValueNum = 360 },
            ["credit.current"] = new() { Key = "credit.current", Label = "Кредиты", ValueNum = credit },
            ["seek.current"] = new() { Key = "seek.current", Label = "Seek Suspicion", ValueNum = seek },
        };

        return new SnapshotData
        {
            Snapshot = summary,
            Operators = operators,
            Achievements = achievements,
            Domains = [domain],
            Rooms = [],
            Counters = counters,
            Extras = new ExtrasModel
            {
                Inventory = new InventoryModel
                {
                    Weapons = weapons,
                    Equips = 198 + index,
                    EquipCopies = 900 + index * 4,
                    TacticalItems = 1,
                },
                Profile = new ProfileModel { Nickname = "dev / frostik", Created = "2025-12-01T10:00:00+08:00" },
                Attendance = new AttendanceModel { Signed = 20 + index, Total = 30, HasToday = true },
            },
            CrisisEvents =
            [
                new CrisisEventModel
                {
                    Name = "Dev Crisis",
                    Highest = 12 + index / 2,
                    ChallengeCount = 2 + index / 3,
                    WeeklyCount = 1 + index / 2,
                    WeeklyTotal = 8,
                    IndicatorCount = 2 + index / 2,
                    IndicatorTotal = 8,
                    StageCount = 1 + index / 2,
                    StageTotal = 4,
                    EndTs = "1794000000",
                },
            ],
            WarSeasons =
            [
                new WarSeasonModel
                {
                    Name = "Dev War Echo",
                    Stars = 3 + index,
                    EndTs = "1794000000",
                    Weeks = [new WarWeekModel { Name = "Week 1", Stars = 3 + index }],
                },
            ],
            IndieGroups =
            [
                new IndieGroupModel { Name = "Dev Indie", Total = 6, NormalPassed = index, HardPassed = index / 2 },
            ],
            PreviousId = null,
            NextId = null,
        };
    }

    public static OperatorModel CreateVesper(int level = 90, int potential = 5) => BuildVesper(level, potential);

    private static OperatorModel BuildVesper(int level, int potential) => new()
    {
        Name = "Vesper",
        CharId = "dev-vesper",
        Rarity = 6,
        Element = "Cryo",
        Class = "Striker",
        Potential = potential,
        Level = level,
        Evolve = 4,
        OwnTs = "1793000000",
        Tags = ["FROST", "BURST"],
        Skills =
        [
            new SkillModel { Name = "Frostline", Type = "Basic", Level = 7, Max = 10 },
            new SkillModel { Name = "Whiteout", Type = "Battle", Level = 6, Max = 10 },
            new SkillModel { Name = "Absolute Zero", Type = "Ultimate", Level = 5, Max = 10 },
        ],
        Talents = new TalentModel { BreakNode = "charBreak4", PassiveCount = 3, FactoryCount = 2, ShipCount = 2 },
        WeaponSkills = [new WeaponSkillModel { Name = "Cold Pursuit", Level = 6, Max = 8 }],
        Gear = new Dictionary<string, GearModel>
        {
            ["body"] = new() { Operator = "Vesper", Slot = "body", Name = "Rimeweave Coat", Level = 70, Enhance = "[3]", Suit = "Rime" },
            ["arm"] = new() { Operator = "Vesper", Slot = "arm", Name = "Frostguard Gloves", Level = 70, Enhance = "[3]", Suit = "Rime" },
            ["acc1"] = new() { Operator = "Vesper", Slot = "acc1", Name = "Cryo Relay", Level = 70, Enhance = "[3]", Suit = "Rime" },
            ["acc2"] = new() { Operator = "Vesper", Slot = "acc2", Name = "Cryo Relay", Level = 70, Enhance = "[3]", Suit = "Rime" },
            ["tactical"] = new() { Operator = "Vesper", Slot = "tactical", Name = "Test Beacon", Level = 70, Enhance = "[3]", Suit = "Rime" },
        },
        WeaponName = "Frostline",
        WeaponType = "Polearm",
        WeaponRarity = 6,
        WeaponLevel = 90,
        WeaponRefine = 1,
        WeaponBreakthrough = 3,
        WeaponGem = "Cryo",
    };

    private static OperatorModel BuildOperator(
        string name,
        int rarity,
        string element,
        string cls,
        int potential,
        int level,
        int evolve,
        string weapon,
        int weaponLevel,
        int refine,
        int breakthrough,
        int skillLevel) => new()
    {
        Name = name,
        CharId = $"dev-{name.ToLowerInvariant().Replace(' ', '-')}",
        Rarity = rarity,
        Element = element,
        Class = cls,
        Potential = potential,
        Level = level,
        Evolve = evolve,
        OwnTs = "1793000000",
        Skills = [new SkillModel { Name = "Test Skill", Type = "Basic", Level = skillLevel, Max = 10 }],
        Talents = new TalentModel { BreakNode = "charBreak4", PassiveCount = 2, FactoryCount = 2, ShipCount = 1 },
        WeaponName = weapon,
        WeaponType = "Test",
        WeaponRarity = rarity,
        WeaponLevel = weaponLevel,
        WeaponRefine = refine,
        WeaponBreakthrough = breakthrough,
    };

    private static SnapshotData WithDiff(SnapshotData data, DiffModel diff, int previousId) => new()
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
        PreviousId = previousId,
        NextId = data.NextId,
    };
}
