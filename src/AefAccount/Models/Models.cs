using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using AefAccount.Localization;
using AefAccount.Services;

namespace AefAccount.Models;

public static class AppCulture
{
    public static CultureInfo Current => LocalizationService.Instance.Culture;
    public static string DateFormat => LocalizationService.Instance.DateFormat;
    public static string DateFormatShort => LocalizationService.Instance.DateFormatShort;
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

    public string DiffSummaryText
    {
        get
        {
            var raw = DiffSummary;
            if (string.IsNullOrWhiteSpace(raw)) return "";
            if (LocalizationService.Instance.CurrentLanguage.StartsWith("ru", StringComparison.OrdinalIgnoreCase)) return raw;

            var parts = raw.Split("; ", StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < parts.Length; index++)
            {
                var part = parts[index];
                var separator = part.IndexOf(" ×", StringComparison.Ordinal);
                if (separator < 0) separator = part.IndexOf(": ", StringComparison.Ordinal);
                var name = (separator >= 0 ? part[..separator] : part).Trim();
                if (!DiffSummaryGroupKeys.TryGetValue(name, out var key)) continue;
                parts[index] = L.T(key) + (separator >= 0 ? part[separator..] : "");
            }
            return string.Join("; ", parts);
        }
    }

    private static readonly Dictionary<string, string> DiffSummaryGroupKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["новые операторы"] = "diff_group_new_operators",
        ["пропали из ответа"] = "diff_group_removed_operators",
        ["потенциалы"] = "diff_group_potentials",
        ["уровни"] = "diff_group_levels",
        ["пробуждение"] = "diff_group_evolve",
        ["оружие"] = "diff_group_weapons",
        ["скиллы"] = "diff_group_skills",
        ["таланты"] = "diff_group_talents",
        ["гир"] = "diff_group_gear",
        ["достижения"] = "diff_group_achievements",
        ["ресурсы"] = "diff_group_resources",
        ["ресурсы и прогресс"] = "diff_group_resources",
        ["домены"] = "diff_group_domains",
        ["регионы"] = "diff_group_domains",
        ["поселения"] = "diff_group_settlements",
        ["аванпосты"] = "diff_group_settlements",
        ["коллекции"] = "diff_group_collections",
        ["первый снимок"] = "diff_first_snapshot",
        ["без изменений"] = "diff_no_changes_short",
    };
    public string? MainMission { get; init; }
    public string? CreatedTs { get; init; }
    public string? LastLoginTs { get; init; }
    public string? ReportPath { get; init; }
    public string? RawPath { get; init; }

    public DateTimeOffset? TakenAtTime => DateTimeOffset.TryParse(TakenAtServer, out var value) ? value : null;
    public string CreatedText => DateText.FromIso(CreatedTs);
    public string LastLoginText => DateText.FromIso(LastLoginTs);

    public string DisplayDate => TakenAtTime?.ToString(AppCulture.DateFormat, AppCulture.Current) ?? "—";
    public string DisplayDateShort => TakenAtTime?.ToString(AppCulture.DateFormatShort, AppCulture.Current) ?? "—";
    public string DisplayLabel => $"#{Id} · {DisplayDate} · v{GameVersion ?? "?"}";
    public string DisplayLabelShort => $"#{Id} · {TakenAtTime?.ToString("dd.MM", AppCulture.Current) ?? "—"} · v{GameVersion ?? "?"}";
    public string StaminaText => Ratio(Stamina, StaminaMax);
    public string BpText => Ratio(BpLevel, BpMax);
    public string DailyText => Ratio(Daily, DailyMax);
    public string WeeklyText => Ratio(Weekly, WeeklyMax);
    public string CreditText => Ratio(Credit, CreditMax);
    public string SeekText => Ratio(Seek, SeekMax);
    public string ExpText => Exp is null ? "—" : Exp.Value.ToString("N0", AppCulture.Current);
    public double StaminaPercent => Percent(Stamina, StaminaMax);
    public double BpPercent => Percent(BpLevel, BpMax);
    public double DailyPercent => Percent(Daily, DailyMax);
    public double WeeklyPercent => Percent(Weekly, WeeklyMax);
    public double CreditPercent => Percent(Credit, CreditMax);
    public double SeekPercent => Percent(Seek, SeekMax);

    public static string Ratio(int? value, int? max) => value == null && max == null
        ? "—"
        : string.Format(AppCulture.Current, "{0:N0} / {1:N0}", value ?? 0, max ?? 0);

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
    public string DiffSummary => Diff?.Summary
        ?? (string.IsNullOrWhiteSpace(Snapshot.DiffSummaryText) ? L.T("diff_first_snapshot") : Snapshot.DiffSummaryText);

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
    public string DisplayName => LocalizationService.Instance.CurrentLanguage.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
        ? OperatorArt.RuName(Name)
        : OperatorArt.EnglishName(Name);
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
    public string LevelChip => L.T("level_chip", Level?.ToString() ?? "—");
    public string WeaponText => WeaponName ?? L.T("gear_not_equipped");
    public string WeaponNameText => WeaponName ?? L.T("gear_not_equipped");
    public string WeaponRefineText => WeaponRefine is null ? "—" : $"R{WeaponRefine}";
    public string LevelText => Level?.ToString() ?? "—";
    public string PotentialText => $"P{Potential}";
    public string EvolveText => $"E{Evolve ?? 0}";
    public string GearCountText => L.T("gear_count", Gear.Count);
    public string AcquiredText => DateText.FromEpoch(OwnTs);
    public string WeaponSummary =>
        WeaponName == null
            ? L.T("gear_not_equipped")
            : L.T("weapon_summary", WeaponType ?? "—", WeaponRarity ?? 0, WeaponRefine ?? 0, WeaponLevel ?? 0, WeaponBreakthrough ?? 0);
    public string SkillLevelsText => string.Join("  ", Skills.Select(skill => $"{skill.Level ?? 0}"));
    public bool HasSkills => Skills.Count > 0;
    public bool HasWeaponSkills => WeaponSkills.Count > 0;
    public bool HasTags => Tags.Count > 0;
    public List<GearSlotModel> GearSlots =>
    [
        new() { Label = L.T("gear_slot_body"), Gear = Gear.GetValueOrDefault("body") },
        new() { Label = L.T("gear_slot_arm"), Gear = Gear.GetValueOrDefault("arm") },
        new() { Label = L.T("gear_slot_acc1"), Gear = Gear.GetValueOrDefault("acc1") },
        new() { Label = L.T("gear_slot_acc2"), Gear = Gear.GetValueOrDefault("acc2") },
        new() { Label = L.T("gear_slot_tactical"), Gear = Gear.GetValueOrDefault("tactical") },
    ];
}

public sealed class GearSlotModel
{
    public string Label { get; init; } = "";
    public GearModel? Gear { get; init; }
    public bool HasGear => Gear != null;
    public string Name => Gear?.Name ?? L.T("gear_not_equipped");
    public string Meta => Gear == null
        ? "—"
        : Gear.LevelText + (Gear.EnhanceText == "—" ? "" : $" · {L.T("gear_enhance_short")} {Gear.EnhanceText}");
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
        "body" => L.T("gear_slot_body"),
        "arm" => L.T("gear_slot_arm"),
        "acc1" => L.T("gear_slot_acc1"),
        "acc2" => L.T("gear_slot_acc2"),
        "tactical" => L.T("gear_slot_tactical"),
        _ => Slot,
    };
    public string EnhanceText => FormatEnhance(Enhance);
    public string LevelText => Level is null ? "—" : L.T("level_chip", Level);
    public string Display => $"{Name ?? "—"} · {LevelText}" + (EnhanceText == "—" ? "" : $" · {L.T("gear_enhance_short")} {EnhanceText}");

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
    public string PassiveText => L.T("talent_nodes", PassiveCount);
    public string FactoryText => L.T("talent_nodes", FactoryCount);
    public string ShipText => L.T("talent_nodes", ShipCount);
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
    public string LevelText => L.T("level_chip", Level?.ToString() ?? "—");
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
    public string MoneyText => string.Format(AppCulture.Current, "{0:N0} / {1:N0}", MoneyCount ?? 0, MoneyTotal ?? 0);
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
    public string LevelText => L.T("level_chip", Level?.ToString() ?? "—");
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
    public string Display => string.Format(AppCulture.Current, "{0:N0}/{1:N0}", Count, Total);
    public string DisplayLabel => Key switch
    {
        "chest" or "chests" => L.T("metric_chests"),
        "puzzle" or "heads" => L.T("metric_puzzles"),
        "blackbox" or "boxes" => L.T("metric_blackboxes"),
        "piece" => L.T("metric_pieces"),
        "equip_chest" => L.T("metric_equip_chests"),
        _ => Label,
    };
}

public sealed class ShipRoomModel
{
    public int Index { get; init; }
    public int? Type { get; init; }
    public int? Level { get; init; }
    public List<ShipCharacterModel> Characters { get; init; } = [];
    public string Title => L.T("ship_room", Index + 1);
    public string TypeText => L.T("ship_room_type", Type?.ToString() ?? "—");
    public string CharactersText => L.T("ship_room_characters", Characters.Count);
    public string FavorabilityText => L.T("ship_favorability", Characters.Count(item => item.Favorability >= 1500));
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
        ? (ValueNum.Value == Math.Truncate(ValueNum.Value)
            ? ValueNum.Value.ToString("N0", AppCulture.Current)
            : ValueNum.Value.ToString("N1", AppCulture.Current))
        : ValueText ?? "—";
    public string DisplayLabel => Key switch
    {
        "stamina.current" => L.T("resource_sanity"),
        "stamina.max" => L.T("counter_sanity_max"),
        "credit.current" => L.T("resource_credits"),
        "seek.current" => L.T("resource_seek"),
        "bp.current" => L.T("counter_bp"),
        "daily.current" => L.T("counter_daily"),
        "weekly.current" => L.T("counter_weekly"),
        _ => Label ?? Key,
    };
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
    public string Display => L.T("attendance_days", Signed, Total) + (HasToday ? L.T("attendance_today") : "");
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
    public string CycleText => Weeks.FirstOrDefault()?.Name ?? L.T("season");
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
            if (Groups.Count == 0) return L.T("diff_no_changes_short");
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
        return lines.Count == 0 ? L.T("diff_no_changes") : string.Join("\n", lines.Take(limit).Select(line => line.Text));
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

public sealed class MetricOption : INotifyPropertyChanged
{
    public string Key { get; init; } = "";
    public string LabelKey { get; init; } = "";
    public string Label => L.T(LabelKey);
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
}

public sealed class NavItem : INotifyPropertyChanged
{
    public string Key { get; init; } = "";
    public string LabelKey { get; init; } = "";
    public string Label => L.T(LabelKey);
    public string Icon { get; init; } = "";
    public string Eyebrow { get; init; } = "";
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
}

/// <summary>Значение фильтра/сортировки: ключ остаётся стабильным, подпись переводится.</summary>
public sealed class LocalizedOption : INotifyPropertyChanged
{
    public string Key { get; init; } = "";
    public string? LabelKey { get; init; }
    public string? Fallback { get; init; }
    public string Label => LabelKey is null ? Fallback ?? Key : L.T(LabelKey);
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
}

public static class DateText
{
    public static string FromEpoch(string? value)
    {
        if (!long.TryParse(value, out var seconds) || seconds <= 0) return "—";
        return DateTimeOffset.FromUnixTimeSeconds(seconds)
            .ToOffset(AppCulture.ServerOffset)
            .ToString(AppCulture.DateFormat, AppCulture.Current);
    }

    public static string FromIso(string? value)
    {
        if (!DateTimeOffset.TryParse(value, out var date)) return "—";
        return date.ToOffset(AppCulture.ServerOffset).ToString(AppCulture.DateFormat, AppCulture.Current);
    }

    public static string FromIsoShort(string? value)
    {
        if (!DateTimeOffset.TryParse(value, out var date)) return "—";
        return date.ToOffset(AppCulture.ServerOffset).ToString(AppCulture.DateFormatShort, AppCulture.Current);
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
