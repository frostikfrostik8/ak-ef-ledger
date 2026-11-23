using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using AefAccount.Services;

namespace AefAccount.Models;

public static class AppCulture
{
    public static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
    public static readonly TimeSpan ServerOffset = TimeSpan.FromHours(8);
}

public sealed class SnapshotSummary
{
    public int Id { get; init; }
    public string TakenAt { get; init; } = "";
    public string TakenAtServer { get; init; } = "";
    public string? GameVersion { get; init; }
    public string? PlayerName { get; init; }
    public string? RoleId { get; init; }
    public string? ServerId { get; init; }
    public string? ServerName { get; init; }
    public int? PlayerLevel { get; init; }
    public int? WorldLevel { get; init; }
    public int? Exp { get; init; }
    public int? CharNum { get; init; }
    public int? WeaponNum { get; init; }
    public int? DocNum { get; init; }
    public int? Stamina { get; init; }
    public int? StaminaMax { get; init; }
    public int? BpLevel { get; init; }
    public int? BpMax { get; init; }
    public int? Daily { get; init; }
    public int? DailyMax { get; init; }
    public int? Weekly { get; init; }
    public int? WeeklyMax { get; init; }
    public int? Credit { get; init; }
    public int? CreditMax { get; init; }
    public int? Seek { get; init; }
    public int? SeekMax { get; init; }
    public int? AchieveCount { get; init; }
    public string? DiffSummary { get; init; }
    public string? MainMission { get; init; }
    public string? CreatedTs { get; init; }
    public string? LastLoginTs { get; init; }
    public string? ReportPath { get; init; }
    public string? RawPath { get; init; }

    public DateTimeOffset? TakenAtTime => DateTimeOffset.TryParse(TakenAtServer, out var value) ? value : null;
    public string CreatedText => DateText.FromIso(CreatedTs);
    public string LastLoginText => DateText.FromIso(LastLoginTs);

    public string DisplayDate => TakenAtTime?.ToString("dd MMM yyyy, HH:mm", AppCulture.Ru) ?? "—";
    public string DisplayDateShort => TakenAtTime?.ToString("dd MMM yyyy", AppCulture.Ru) ?? "—";
    public string DisplayLabel => $"#{Id} · {DisplayDate} · v{GameVersion ?? "?"}";
    public string DisplayLabelShort => $"#{Id} · {TakenAtTime?.ToString("dd.MM", AppCulture.Ru) ?? "—"} · v{GameVersion ?? "?"}";
    public string StaminaText => Ratio(Stamina, StaminaMax);
    public string BpText => Ratio(BpLevel, BpMax);
    public string DailyText => Ratio(Daily, DailyMax);
    public string WeeklyText => Ratio(Weekly, WeeklyMax);
    public string CreditText => Ratio(Credit, CreditMax);
    public string SeekText => Ratio(Seek, SeekMax);
    public double StaminaPercent => Percent(Stamina, StaminaMax);
    public double BpPercent => Percent(BpLevel, BpMax);
    public double DailyPercent => Percent(Daily, DailyMax);
    public double WeeklyPercent => Percent(Weekly, WeeklyMax);
    public double CreditPercent => Percent(Credit, CreditMax);
    public double SeekPercent => Percent(Seek, SeekMax);

    public static string Ratio(int? value, int? max) => value == null && max == null ? "—" : $"{value ?? 0} / {max ?? 0}";

    private static double Percent(int? value, int? max) =>
        max is > 0 ? Math.Clamp((value ?? 0) * 100.0 / max.Value, 0, 100) : 0;
}

public sealed class SnapshotMeta
{
    public SnapshotSummary? Latest { get; init; }
    public int SnapshotsCount { get; init; }
    public string? CurrentVersion { get; init; }
    public double? AgeDays { get; init; }
    public DateTimeOffset Now { get; init; } = DateTimeOffset.Now;
    public List<VersionInfo> Versions { get; init; } = [];
}

public sealed class VersionInfo
{
    public string Version { get; init; } = "";
    public string? Title { get; init; }
    public string? StartDate { get; init; }
    public string? EndDate { get; init; }
    public bool Estimated { get; init; }
    public string? Notes { get; init; }
}

public sealed class SnapshotData
{
    public required SnapshotSummary Snapshot { get; init; }
    public List<OperatorModel> Operators { get; init; } = [];
    public List<AchievementModel> Achievements { get; init; } = [];
    public List<DomainModel> Domains { get; init; } = [];
    public List<ShipRoomModel> Rooms { get; init; } = [];
    public Dictionary<string, CounterModel> Counters { get; init; } = [];
    public ExtrasModel Extras { get; init; } = new();
    public List<CrisisEventModel> CrisisEvents { get; init; } = [];
    public List<WarSeasonModel> WarSeasons { get; init; } = [];
    public List<IndieGroupModel> IndieGroups { get; init; } = [];
    public DiffModel? Diff { get; init; }
    public int? PreviousId { get; init; }
    public int? NextId { get; init; }
    public string DiffSummary => Diff?.Summary ?? Snapshot.DiffSummary ?? "первый снимок";

    public IEnumerable<CollectionModel> Collections => Domains.SelectMany(domain => domain.Collections);
    public int PlatedCount => Achievements.Count(item => item.IsPlated);
}

public sealed class OperatorModel
{
    public string Name { get; init; } = "";
    public string? CharId { get; init; }
    public int? Rarity { get; init; }
    public string? Element { get; init; }
    public string? Class { get; init; }
    public int Potential { get; init; }
    public int? Level { get; init; }
    public int? Evolve { get; init; }
    public string? OwnTs { get; init; }
    public string? AvatarUrl { get; init; }
    public string? IllustrationUrl { get; init; }
    public List<string> Tags { get; init; } = [];
    public List<SkillModel> Skills { get; init; } = [];
    public TalentModel Talents { get; init; } = new();
    public List<WeaponSkillModel> WeaponSkills { get; init; } = [];
    public Dictionary<string, GearModel> Gear { get; init; } = [];
    public string? WeaponName { get; init; }
    public string? WeaponType { get; init; }
    public int? WeaponRarity { get; init; }
    public int? WeaponLevel { get; init; }
    public int? WeaponRefine { get; init; }
    public int? WeaponBreakthrough { get; init; }
    public string? WeaponGem { get; init; }

    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : Name.TrimStart('"', '“', '”').FirstOrDefault().ToString().ToUpperInvariant();
    public string DisplayName => OperatorArt.RuName(Name);
    public string EnglishName => OperatorArt.EnglishName(Name);
    public string? ArtSlug => OperatorArt.Slug(Name);
    public bool HasLocalArt => OperatorArt.HasLocalArt(Name);
    public string RarityStars => new('★', Math.Clamp(Rarity ?? 0, 0, 6));
    public string ElementHex => ColorPalette.Element(Element);
    public string ElementTintHex => ColorPalette.ElementTint(Element);
    public string RarityHex => ColorPalette.Rarity(Rarity);
    public string ClassCode => Class switch
    {
        "Vanguard" => "VAN",
        "Guard" => "GRD",
        "Defender" => "DEF",
        "Caster" => "CAS",
        "Striker" => "STR",
        "Supporter" => "SUP",
        _ => "—",
    };
    public string ElementCode => Element switch
    {
        "Physical" => "PHY",
        "Heat" => "HEAT",
        "Electric" => "ELEC",
        "Cryo" => "CRYO",
        "Nature" => "NAT",
        _ => "—",
    };
    public string Subtitle => $"{Element ?? "—"} · {Class ?? "—"}";
    public string LevelChip => $"ур. {Level?.ToString() ?? "—"}";
    public string WeaponText => WeaponName ?? "Без оружия";
    public string WeaponRefineText => WeaponRefine is null ? "—" : $"R{WeaponRefine}";
    public string LevelText => Level?.ToString() ?? "—";
    public string PotentialText => $"P{Potential}";
    public string EvolveText => $"E{Evolve ?? 0}";
    public string GearCountText => $"{Gear.Count} гир";
    public string AcquiredText => DateText.FromEpoch(OwnTs);
    public string WeaponSummary =>
        WeaponName == null
            ? "Не надето"
            : $"{WeaponType ?? "—"} · {WeaponRarity ?? 0}★ · R{WeaponRefine ?? 0} · ур. {WeaponLevel ?? 0} · B{WeaponBreakthrough ?? 0}";
    public string SkillLevelsText => string.Join("  ", Skills.Select(skill => $"{skill.Level ?? 0}"));
    public bool HasSkills => Skills.Count > 0;
    public bool HasWeaponSkills => WeaponSkills.Count > 0;
    public bool HasTags => Tags.Count > 0;
    public List<GearSlotModel> GearSlots =>
    [
        new() { Label = "Тело", Gear = Gear.GetValueOrDefault("body") },
        new() { Label = "Руки", Gear = Gear.GetValueOrDefault("arm") },
        new() { Label = "Аксессуар 1", Gear = Gear.GetValueOrDefault("acc1") },
        new() { Label = "Аксессуар 2", Gear = Gear.GetValueOrDefault("acc2") },
        new() { Label = "Тактика", Gear = Gear.GetValueOrDefault("tactical") },
    ];
}

public sealed class GearSlotModel
{
    public string Label { get; init; } = "";
    public GearModel? Gear { get; init; }
    public bool HasGear => Gear != null;
    public string Name => Gear?.Name ?? "Не надето";
    public string Meta => Gear == null ? "—" : $"{Gear.LevelText}" + (Gear.EnhanceText == "—" ? "" : $" · энч. {Gear.EnhanceText}");
    public string Suit => Gear?.Suit ?? "";
}

public sealed class GearModel
{
    public string Operator { get; init; } = "";
    public string Slot { get; init; } = "";
    public string? Name { get; init; }
    public int? Level { get; init; }
    public string? Enhance { get; init; }
    public string? Suit { get; init; }
    public string? Rarity { get; init; }
    public string? EquipId { get; init; }
    public string SlotLabel => Slot switch
    {
        "body" => "Тело",
        "arm" => "Руки",
        "acc1" => "Аксессуар 1",
        "acc2" => "Аксессуар 2",
        "tactical" => "Тактика",
        _ => Slot,
    };
    public string EnhanceText => FormatEnhance(Enhance);
    public string LevelText => Level is null ? "—" : $"ур. {Level}";
    public string Display => $"{Name ?? "—"} · ур. {Level?.ToString() ?? "—"}" + (EnhanceText == "—" ? "" : $" · энч. {EnhanceText}");

    public static string FormatEnhance(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "—";
        try
        {
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                var values = root.EnumerateArray().Select(item => item.ToString()).ToList();
                return values.Count == 0 ? "—" : string.Join("/", values);
            }
            if (root.ValueKind == JsonValueKind.Object)
            {
                var properties = root.EnumerateObject().ToList();
                if (properties.Count == 0) return "—";
                if (properties.All(item => int.TryParse(item.Name, out var index) && index is >= 1 and <= 3))
                {
                    return string.Join("/", Enumerable.Range(1, 3).Select(index =>
                        properties.FirstOrDefault(item => item.Name == index.ToString()).Value.ToString() ?? "0"));
                }
                return string.Join("/", properties.Select(item => item.Value.ToString()));
            }
        }
        catch (JsonException)
        {
            // Не JSON — показываем как есть.
        }
        return value;
    }
}

public sealed class SkillModel
{
    public string? Name { get; init; }
    public string? Type { get; init; }
    public int? Level { get; init; }
    public int? Max { get; init; }
    public double Percent => Max is > 0 ? Math.Clamp(Level ?? 0, 0, Max.Value) * 100.0 / Max.Value : 0;
    public string LevelText => $"{Level ?? 0}" + (Max is > 0 ? $" / {Max}" : "");
}

public sealed class WeaponSkillModel
{
    public string? Name { get; init; }
    public int? Level { get; init; }
    public int? Max { get; init; }
    public string Display => $"{Name ?? "—"} · {Level ?? 0}" + (Max is > 0 ? $" / {Max}" : "");
}

public sealed class TalentModel
{
    public string? BreakNode { get; init; }
    public int PassiveCount { get; init; }
    public int FactoryCount { get; init; }
    public int ShipCount { get; init; }
    public string BreakLabel => string.IsNullOrWhiteSpace(BreakNode) ? "—" : BreakNode.Replace("charBreak", "");
    public string PassiveText => $"{PassiveCount} узла";
    public string FactoryText => $"{FactoryCount} узла";
    public string ShipText => $"{ShipCount} узла";
}

public sealed class AchievementModel
{
    public string Id { get; init; } = "";
    public string? Name { get; init; }
    public string? Category { get; init; }
    public string? Cate { get; init; }
    public int? Level { get; init; }
    public int? InitLevel { get; init; }
    public bool IsPlated { get; init; }
    public string? ObtainedTs { get; init; }
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : Name.TrimStart('"', '“', '”').FirstOrDefault().ToString().ToUpperInvariant();
    public string ObtainedText => DateText.FromEpoch(ObtainedTs);
    public string LevelText => $"ур. {Level?.ToString() ?? "—"}";
    public string CategoryText => Category ?? "—";
}

public sealed class DomainModel
{
    public string Id { get; init; } = "";
    public string? Name { get; init; }
    public int? Level { get; init; }
    public long? MoneyTotal { get; init; }
    public long? MoneyCount { get; init; }
    public List<SettlementModel> Settlements { get; init; } = [];
    public List<CollectionModel> Collections { get; init; } = [];
    public string DisplayName => Name ?? Id;
    public string MoneyText => $"{MoneyCount ?? 0:N0} / {MoneyTotal ?? 0:N0}".Replace(",", " ");
    public double OverallPercent
    {
        get
        {
            var total = Collections.Sum(item => item.Metrics.Sum(metric => metric.Total));
            var count = Collections.Sum(item => item.Metrics.Sum(metric => metric.Count));
            return total > 0 ? Math.Clamp(count * 100.0 / total, 0, 100) : 0;
        }
    }
    public int LocationCount => Collections.Count;
}

public sealed class SettlementModel
{
    public string Id { get; init; } = "";
    public string? Name { get; init; }
    public int? Level { get; init; }
    public long? Money { get; init; }
    public string DisplayName => Name ?? Id;
    public string LevelText => $"ур. {Level?.ToString() ?? "—"}";
}

public sealed class CollectionModel
{
    public string DomainId { get; init; } = "";
    public string LevelId { get; init; } = "";
    public string? Name { get; init; }
    public List<MetricModel> Metrics { get; init; } = [];
    public string DisplayName => Name ?? LevelId;
    public double OverallPercent
    {
        get
        {
            var total = Metrics.Sum(metric => metric.Total);
            var count = Metrics.Sum(metric => metric.Count);
            return total > 0 ? Math.Clamp(count * 100.0 / total, 0, 100) : 0;
        }
    }

    public List<MetricModel> KeyMetrics
    {
        get
        {
            var visible = Metrics.Where(metric => metric.Total > 0).Take(3).ToList();
            return visible.Count > 0 ? visible : Metrics.Take(3).ToList();
        }
    }
}

public sealed class MetricModel
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public int Count { get; init; }
    public int Total { get; init; }
    public double Percent => Total > 0 ? Math.Clamp(Count * 100.0 / Total, 0, 100) : 0;
    public string Display => $"{Count:N0}/{Total:N0}".Replace(",", " ");
}

public sealed class ShipRoomModel
{
    public int Index { get; init; }
    public int? Type { get; init; }
    public int? Level { get; init; }
    public List<ShipCharacterModel> Characters { get; init; } = [];
    public string Title => $"Комната {Index + 1}";
    public string TypeText => $"тип {Type?.ToString() ?? "—"}";
    public string CharactersText => $"{Characters.Count} операторов";
    public string FavorabilityText => $"{Characters.Count(item => item.Favorability >= 1500)} на 1500";
}

public sealed class ShipCharacterModel
{
    public string? CharId { get; init; }
    public double? Favorability { get; init; }
    public double? PhysicalStrength { get; init; }
}

public sealed class CounterModel
{
    public string Key { get; init; } = "";
    public string? Label { get; init; }
    public double? ValueNum { get; init; }
    public string? ValueText { get; init; }
    public string Display => ValueNum is not null
        ? (ValueNum.Value == Math.Truncate(ValueNum.Value) ? ValueNum.Value.ToString("N0", AppCulture.Ru) : ValueNum.Value.ToString("N1", AppCulture.Ru))
        : ValueText ?? "—";
}

public sealed class ExtrasModel
{
    public AttendanceModel Attendance { get; init; } = new();
    public InventoryModel Inventory { get; init; } = new();
    public ProfileModel Profile { get; init; } = new();
    public List<PoolModel> CharacterPools { get; init; } = [];
    public List<PoolModel> WeaponPools { get; init; } = [];
}

public sealed class AttendanceModel
{
    public int Signed { get; init; }
    public int Total { get; init; }
    public bool HasToday { get; init; }
    public string Display => $"{Signed}/{Total} дней" + (HasToday ? " · сегодня есть" : "");
    public double Percent => Total > 0 ? Math.Clamp(Signed * 100.0 / Total, 0, 100) : 0;
}

public sealed class InventoryModel
{
    public int Weapons { get; init; }
    public int Equips { get; init; }
    public int EquipCopies { get; init; }
    public int TacticalItems { get; init; }
}

public sealed class ProfileModel
{
    public string? Nickname { get; init; }
    public string? Created { get; init; }
}

public sealed class PoolModel
{
    public string? Name { get; init; }
    public string? Start { get; init; }
    public string? End { get; init; }
    public string EndText => DateText.FromEpoch(End);
}

public sealed class CrisisEventModel
{
    public string Name { get; init; } = "Crisis Contract";
    public int? Highest { get; init; }
    public int? ChallengeCount { get; init; }
    public int? WeeklyCount { get; init; }
    public int? WeeklyTotal { get; init; }
    public int? IndicatorCount { get; init; }
    public int? IndicatorTotal { get; init; }
    public int? StageCount { get; init; }
    public int? StageTotal { get; init; }
    public string? EndTs { get; init; }
    public string EndText => DateText.FromEpoch(EndTs);
    public double WeeklyPercent => WeeklyTotal is > 0 ? Math.Clamp((WeeklyCount ?? 0) * 100.0 / WeeklyTotal.Value, 0, 100) : 0;
}

public sealed class WarSeasonModel
{
    public string? Name { get; init; }
    public int? Stars { get; init; }
    public string? EndTs { get; init; }
    public List<WarWeekModel> Weeks { get; init; } = [];
    public string EndText => DateText.FromEpoch(EndTs);
    public string CycleText => Weeks.FirstOrDefault()?.Name ?? "сезон";
    public double StarsPercent => Math.Clamp((Stars ?? 0) * 100.0 / 9, 0, 100);
}

public sealed class WarWeekModel
{
    public string? Name { get; init; }
    public int? Stars { get; init; }
}

public sealed class IndieGroupModel
{
    public string? Name { get; init; }
    public int Total { get; init; }
    public int NormalPassed { get; init; }
    public int HardPassed { get; init; }
    public double Percent => Total > 0 ? Math.Clamp((NormalPassed + HardPassed) * 100.0 / (Total * 2), 0, 100) : 0;
}

public enum DiffKind { Neutral, Up, Down }

public sealed class DiffLine
{
    public string Text { get; init; } = "";
    public DiffKind Kind { get; init; }
}

public sealed class DiffGroup
{
    public string Title { get; init; } = "";
    public List<DiffLine> Lines { get; init; } = [];
}

public sealed class DiffModel
{
    public List<DiffGroup> Groups { get; init; } = [];
    public bool HasChanges => Groups.Any(group => group.Lines.Count > 0);
    public List<DiffLine> AllLines => Groups.SelectMany(group => group.Lines).ToList();
    public string Summary
    {
        get
        {
            if (Groups.Count == 0) return "без изменений";
            var parts = new List<string>();
            foreach (var group in Groups.Where(group => group.Lines.Count > 0).Take(5))
            {
                parts.Add($"{group.Title}: {group.Lines.Count}");
            }
            return string.Join("; ", parts);
        }
    }
    public string Compact(int limit = 7)
    {
        var lines = AllLines;
        return lines.Count == 0 ? "Изменений нет." : string.Join("\n", lines.Take(limit).Select(line => line.Text));
    }
}

public sealed class ChartPoint
{
    public int SnapshotId { get; init; }
    public double Value { get; init; }
    public string Label { get; init; } = "";
    public string DateText { get; init; } = "";
}

public sealed class ChartModel
{
    public string Label { get; init; } = "";
    public List<ChartPoint> Points { get; init; } = [];
    public string ValueText { get; init; } = "";
    public string DeltaText { get; init; } = "";
}

public sealed class MetricOption
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
}

public sealed class NavItem
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string Icon { get; init; } = "";
    public string Eyebrow { get; init; } = "";
}

public static class DateText
{
    public static string FromEpoch(string? value)
    {
        if (!long.TryParse(value, out var seconds) || seconds <= 0) return "—";
        return DateTimeOffset.FromUnixTimeSeconds(seconds)
            .ToOffset(AppCulture.ServerOffset)
            .ToString("dd MMM yyyy, HH:mm", AppCulture.Ru);
    }

    public static string FromIso(string? value)
    {
        if (!DateTimeOffset.TryParse(value, out var date)) return "—";
        return date.ToOffset(AppCulture.ServerOffset).ToString("dd MMM yyyy, HH:mm", AppCulture.Ru);
    }

    public static string FromIsoShort(string? value)
    {
        if (!DateTimeOffset.TryParse(value, out var date)) return "—";
        return date.ToOffset(AppCulture.ServerOffset).ToString("dd MMM yyyy", AppCulture.Ru);
    }
}

public static class ColorPalette
{
    public static string Element(string? element) => element switch
    {
        "Physical" => "#444444",
        "Heat" => "#FF633D",
        "Electric" => "#FFC001",
        "Cryo" => "#22C6D0",
        "Nature" => "#C3E354",
        _ => "#6A6A6A",
    };

    public static string ElementTint(string? element) => element switch
    {
        "Physical" => "#1C1C1C",
        "Heat" => "#2A1C17",
        "Electric" => "#2A2413",
        "Cryo" => "#15292B",
        "Nature" => "#222A1A",
        _ => "#191919",
    };

    public static string Rarity(int? rarity) => rarity switch
    {
        6 => "#F7D481",
        5 => "#B9C7D9",
        4 => "#8A8A8A",
        _ => "#6A6A6A",
    };
}
