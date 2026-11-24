using AefAccount.Localization;
using AefAccount.Models;

namespace AefAccount.Services;

public static class DiffService
{
    public static DiffModel Compute(SnapshotData previous, SnapshotData current)
    {
        var groups = new List<DiffGroup>();
        Add(groups, L.T("diff_group_new_operators"), AddedOperators(previous, current));
        Add(groups, L.T("diff_group_removed_operators"), RemovedOperators(previous, current));
        Add(groups, L.T("diff_group_potentials"), PotentialChanges(previous, current));
        Add(groups, L.T("diff_group_levels"), LevelChanges(previous, current));
        Add(groups, L.T("diff_group_evolve"), EvolveChanges(previous, current));
        Add(groups, L.T("diff_group_weapons"), WeaponChanges(previous, current));
        Add(groups, L.T("diff_group_skills"), SkillChanges(previous, current));
        Add(groups, L.T("diff_group_talents"), TalentChanges(previous, current));
        Add(groups, L.T("diff_group_gear"), GearChanges(previous, current));
        Add(groups, L.T("diff_group_achievements"), AchievementChanges(previous, current));
        Add(groups, L.T("diff_group_resources"), CounterChanges(previous, current));
        Add(groups, L.T("diff_group_domains"), DomainChanges(previous, current));
        Add(groups, L.T("diff_group_settlements"), SettlementChanges(previous, current));
        Add(groups, L.T("diff_group_collections"), CollectionChanges(previous, current));
        return new DiffModel { Groups = groups };
    }

    private static void Add(List<DiffGroup> groups, string title, List<DiffLine> lines)
    {
        if (lines.Count > 0) groups.Add(new DiffGroup { Title = title, Lines = lines });
    }

    private static List<DiffLine> AddedOperators(SnapshotData previous, SnapshotData current)
    {
        var oldNames = previous.Operators.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return current.Operators
            .Where(item => !oldNames.Contains(item.Name))
            .OrderBy(item => item.Name)
            .Select(item => new DiffLine
            {
                Kind = DiffKind.Up,
                Text = $"+ {item.Name} — {OperatorDescription(item)}",
            }).ToList();
    }

    private static List<DiffLine> RemovedOperators(SnapshotData previous, SnapshotData current)
    {
        var newNames = current.Operators.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return previous.Operators
            .Where(item => !newNames.Contains(item.Name))
            .OrderBy(item => item.Name)
            .Select(item => new DiffLine
            {
                Kind = DiffKind.Down,
                Text = $"− {item.Name} — {OperatorDescription(item)}",
            }).ToList();
    }

    private static List<DiffLine> PotentialChanges(SnapshotData previous, SnapshotData current)
    {
        var result = new List<DiffLine>();
        foreach (var (oldOperator, newOperator) in CommonOperators(previous, current))
        {
            if (oldOperator.Potential != newOperator.Potential)
            {
                result.Add(new DiffLine
                {
                    Kind = DiffKind.Up,
                    Text = $"{newOperator.Name}: P{oldOperator.Potential} → P{newOperator.Potential}",
                });
            }
        }
        return result;
    }

    private static List<DiffLine> LevelChanges(SnapshotData previous, SnapshotData current)
    {
        var result = new List<DiffLine>();
        foreach (var (oldOperator, newOperator) in CommonOperators(previous, current))
        {
            if (oldOperator.Level != newOperator.Level)
            {
                result.Add(new DiffLine
                {
                    Kind = DiffKind.Up,
                    Text = $"{newOperator.Name}: {L.T("level_short")}{oldOperator.Level?.ToString() ?? "—"} → {newOperator.Level?.ToString() ?? "—"}",
                });
            }
        }
        return result;
    }

    private static List<DiffLine> EvolveChanges(SnapshotData previous, SnapshotData current)
    {
        var result = new List<DiffLine>();
        foreach (var (oldOperator, newOperator) in CommonOperators(previous, current))
        {
            if (oldOperator.Evolve != newOperator.Evolve)
            {
                result.Add(new DiffLine
                {
                    Kind = DiffKind.Up,
                    Text = $"{newOperator.Name}: E{oldOperator.Evolve ?? 0} → E{newOperator.Evolve ?? 0}",
                });
            }
        }
        return result;
    }

    private static List<DiffLine> WeaponChanges(SnapshotData previous, SnapshotData current)
    {
        var result = new List<DiffLine>();
        foreach (var (oldOperator, newOperator) in CommonOperators(previous, current))
        {
            var oldWeapon = WeaponKey(oldOperator);
            var newWeapon = WeaponKey(newOperator);
            if (oldWeapon != newWeapon)
            {
                result.Add(new DiffLine
                {
                    Kind = DiffKind.Up,
                    Text = $"{newOperator.Name}: {WeaponDescription(oldOperator)} → {WeaponDescription(newOperator)}",
                });
            }
            else if (oldOperator.WeaponGem != newOperator.WeaponGem)
            {
                result.Add(new DiffLine
                {
                    Kind = DiffKind.Up,
                    Text = $"{newOperator.Name}: {L.T("diff_weapon_gem", oldOperator.WeaponGem ?? "—", newOperator.WeaponGem ?? "—")}",
                });
            }
        }
        return result;
    }

    private static List<DiffLine> SkillChanges(SnapshotData previous, SnapshotData current)
    {
        var result = new List<DiffLine>();
        foreach (var (oldOperator, newOperator) in CommonOperators(previous, current))
        {
            var oldSkills = oldOperator.Skills.ToDictionary(item => item.Name ?? item.Type ?? "?", StringComparer.OrdinalIgnoreCase);
            var newSkills = newOperator.Skills.ToDictionary(item => item.Name ?? item.Type ?? "?", StringComparer.OrdinalIgnoreCase);
            foreach (var name in oldSkills.Keys.Union(newSkills.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(item => item))
            {
                oldSkills.TryGetValue(name, out var oldSkill);
                newSkills.TryGetValue(name, out var newSkill);
                if (oldSkill?.Level == newSkill?.Level) continue;

                result.Add(new DiffLine
                {
                    Kind = DiffKind.Up,
                    Text = $"{newOperator.Name}: {name} {L.T("level_short")}{oldSkill?.Level?.ToString() ?? "—"} → {newSkill?.Level?.ToString() ?? "—"}",
                });
            }
        }
        return result;
    }

    private static List<DiffLine> TalentChanges(SnapshotData previous, SnapshotData current)
    {
        var result = new List<DiffLine>();
        foreach (var (oldOperator, newOperator) in CommonOperators(previous, current))
        {
            if (oldOperator.Talents.BreakNode != newOperator.Talents.BreakNode)
            {
                result.Add(new DiffLine
                {
                    Kind = DiffKind.Up,
                    Text = $"{newOperator.Name}: {L.T("diff_breakthrough", oldOperator.Talents.BreakLabel, newOperator.Talents.BreakLabel)}",
                });
            }
            if (oldOperator.Talents.PassiveCount != newOperator.Talents.PassiveCount)
            {
                result.Add(new DiffLine
                {
                    Kind = DiffKind.Up,
                    Text = $"{newOperator.Name}: {L.T("diff_passive", oldOperator.Talents.PassiveCount, newOperator.Talents.PassiveCount)}",
                });
            }
            if (oldOperator.Talents.FactoryCount != newOperator.Talents.FactoryCount)
            {
                result.Add(new DiffLine
                {
                    Kind = DiffKind.Up,
                    Text = $"{newOperator.Name}: {L.T("diff_factory", oldOperator.Talents.FactoryCount, newOperator.Talents.FactoryCount)}",
                });
            }
            if (oldOperator.Talents.ShipCount != newOperator.Talents.ShipCount)
            {
                result.Add(new DiffLine
                {
                    Kind = DiffKind.Up,
                    Text = $"{newOperator.Name}: {L.T("diff_ship", oldOperator.Talents.ShipCount, newOperator.Talents.ShipCount)}",
                });
            }
        }
        return result;
    }

    private static List<DiffLine> GearChanges(SnapshotData previous, SnapshotData current)
    {
        var oldGear = GearMap(previous);
        var newGear = GearMap(current);
        var keys = oldGear.Keys.Union(newGear.Keys).OrderBy(item => item).ToList();
        var result = new List<DiffLine>();
        foreach (var key in keys)
        {
            oldGear.TryGetValue(key, out var oldItem);
            newGear.TryGetValue(key, out var newItem);
            var label = $"{key.Operator}: {oldItem?.SlotLabel ?? newItem?.SlotLabel ?? key.Slot}";
            if (oldItem == null && newItem != null)
            {
                result.Add(new DiffLine { Kind = DiffKind.Up, Text = $"+ {label} — {newItem.Display}" });
            }
            else if (oldItem != null && newItem == null)
            {
                result.Add(new DiffLine { Kind = DiffKind.Down, Text = $"{label} — {L.T("diff_was", oldItem.Display)}" });
            }
            else if (oldItem != null && newItem != null && oldItem.Display != newItem.Display)
            {
                result.Add(new DiffLine { Kind = DiffKind.Up, Text = $"{label}: {oldItem.Display} → {newItem.Display}" });
            }
        }
        return result;
    }

    private static List<DiffLine> AchievementChanges(SnapshotData previous, SnapshotData current)
    {
        var oldAchievements = previous.Achievements.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var newAchievements = current.Achievements.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var result = new List<DiffLine>();
        foreach (var item in current.Achievements.Where(item => !oldAchievements.ContainsKey(item.Id)))
        {
            result.Add(new DiffLine
            {
                Kind = DiffKind.Up,
                Text = $"+ {item.Name} ({item.CategoryText})",
            });
        }
        foreach (var item in previous.Achievements.Where(item => !newAchievements.ContainsKey(item.Id)))
        {
            result.Add(new DiffLine { Kind = DiffKind.Down, Text = $"− {item.Name}" });
        }
        foreach (var (id, oldItem) in oldAchievements)
        {
            if (!newAchievements.TryGetValue(id, out var newItem)) continue;
            if (!oldItem.IsPlated && newItem.IsPlated)
            {
                result.Add(new DiffLine { Kind = DiffKind.Up, Text = L.T("diff_plated", newItem.Name) });
            }
            if (oldItem.Level != newItem.Level)
            {
                result.Add(new DiffLine
                {
                    Kind = DiffKind.Up,
                    Text = $"{newItem.Name}: {L.T("level_short")}{oldItem.Level?.ToString() ?? "—"} → {newItem.Level?.ToString() ?? "—"}",
                });
            }
        }
        return result;
    }

    private static List<DiffLine> CounterChanges(SnapshotData previous, SnapshotData current)
    {
        var result = new List<DiffLine>();
        foreach (var key in previous.Counters.Keys.Intersect(current.Counters.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(item => item))
        {
            var oldItem = previous.Counters[key];
            var newItem = current.Counters[key];
            var oldValue = oldItem.ValueNum ?? (double.TryParse(oldItem.ValueText, out var parsedOld) ? parsedOld : null);
            var newValue = newItem.ValueNum ?? (double.TryParse(newItem.ValueText, out var parsedNew) ? parsedNew : null);
            if (oldValue == newValue && oldItem.ValueText == newItem.ValueText) continue;

            var delta = oldValue is not null && newValue is not null ? newValue - oldValue : null;
            var deltaText = delta is null or 0 ? "" : $" ({(delta > 0 ? "+" : "")}{Format(delta.Value)})";
            result.Add(new DiffLine
            {
                Kind = delta is null or 0 ? DiffKind.Neutral : delta > 0 ? DiffKind.Up : DiffKind.Down,
                Text = $"{newItem.DisplayLabel}: {oldItem.Display} → {newItem.Display}{deltaText}",
            });
        }
        return result;
    }

    private static List<DiffLine> DomainChanges(SnapshotData previous, SnapshotData current)
    {
        var oldDomains = previous.Domains.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var newDomains = current.Domains.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var result = new List<DiffLine>();
        foreach (var id in oldDomains.Keys.Union(newDomains.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(item => item))
        {
            oldDomains.TryGetValue(id, out var oldDomain);
            newDomains.TryGetValue(id, out var newDomain);
            if (oldDomain == null && newDomain != null)
            {
                result.Add(new DiffLine { Kind = DiffKind.Up, Text = $"+ {newDomain.DisplayName} ({L.T("level_short")}{newDomain.Level?.ToString() ?? "—"})" });
            }
            else if (oldDomain != null && newDomain == null)
            {
                result.Add(new DiffLine { Kind = DiffKind.Down, Text = $"− {oldDomain.DisplayName}" });
            }
            else if (oldDomain != null && newDomain != null && oldDomain.Level != newDomain.Level)
            {
                result.Add(new DiffLine
                {
                    Kind = DiffKind.Up,
                    Text = $"{newDomain.DisplayName}: {L.T("level_short")}{oldDomain.Level?.ToString() ?? "—"} → {newDomain.Level?.ToString() ?? "—"}",
                });
            }
        }
        return result;
    }

    private static List<DiffLine> SettlementChanges(SnapshotData previous, SnapshotData current)
    {
        var oldItems = previous.Domains.SelectMany(domain => domain.Settlements.Select(item => (domain.Id, Item: item)))
            .ToDictionary(pair => $"{pair.Id}/{pair.Item.Id}", pair => pair.Item, StringComparer.OrdinalIgnoreCase);
        var newItems = current.Domains.SelectMany(domain => domain.Settlements.Select(item => (domain.Id, Item: item)))
            .ToDictionary(pair => $"{pair.Id}/{pair.Item.Id}", pair => pair.Item, StringComparer.OrdinalIgnoreCase);
        var result = new List<DiffLine>();
        foreach (var key in oldItems.Keys.Union(newItems.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(item => item))
        {
            oldItems.TryGetValue(key, out var oldItem);
            newItems.TryGetValue(key, out var newItem);
            var name = newItem?.DisplayName ?? oldItem?.DisplayName ?? key;
            if (oldItem == null && newItem != null)
            {
                result.Add(new DiffLine { Kind = DiffKind.Up, Text = $"+ {name} ({L.T("level_short")}{newItem.Level?.ToString() ?? "—"})" });
            }
            else if (oldItem != null && newItem == null)
            {
                result.Add(new DiffLine { Kind = DiffKind.Down, Text = $"− {name}" });
            }
            else if (oldItem != null && newItem != null)
            {
                var changes = new List<string>();
                if (oldItem.Level != newItem.Level) changes.Add($"{L.T("level_short")}{oldItem.Level?.ToString() ?? "—"} → {newItem.Level?.ToString() ?? "—"}");
                if (oldItem.Money != newItem.Money) changes.Add(L.T("diff_money", Format(oldItem.Money), Format(newItem.Money)));
                if (changes.Count > 0) result.Add(new DiffLine { Kind = DiffKind.Up, Text = $"{name}: {string.Join(", ", changes)}" });
            }
        }
        return result;
    }

    private static List<DiffLine> CollectionChanges(SnapshotData previous, SnapshotData current)
    {
        var oldItems = previous.Collections.ToDictionary(item => $"{item.DomainId}/{item.LevelId}", StringComparer.OrdinalIgnoreCase);
        var newItems = current.Collections.ToDictionary(item => $"{item.DomainId}/{item.LevelId}", StringComparer.OrdinalIgnoreCase);
        var result = new List<DiffLine>();
        foreach (var key in oldItems.Keys.Union(newItems.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(item => item))
        {
            oldItems.TryGetValue(key, out var oldItem);
            newItems.TryGetValue(key, out var newItem);
            if (oldItem == null && newItem != null)
            {
                result.Add(new DiffLine { Kind = DiffKind.Up, Text = $"+ {newItem.DisplayName}" });
                continue;
            }
            if (oldItem != null && newItem == null)
            {
                result.Add(new DiffLine { Kind = DiffKind.Down, Text = $"− {oldItem.DisplayName}" });
                continue;
            }
            if (oldItem == null || newItem == null) continue;

            var changes = new List<string>();
            foreach (var oldMetric in oldItem.Metrics)
            {
                var newMetric = newItem.Metrics.FirstOrDefault(item => item.Key == oldMetric.Key);
                if (newMetric is null || oldMetric.Count == newMetric.Count) continue;
                changes.Add($"{oldMetric.DisplayLabel} {oldMetric.Count} → {newMetric.Count}");
            }
            if (changes.Count > 0) result.Add(new DiffLine { Kind = DiffKind.Up, Text = $"{newItem.DisplayName}: {string.Join(", ", changes)}" });
        }
        return result;
    }

    private static IEnumerable<(OperatorModel Old, OperatorModel New)> CommonOperators(SnapshotData previous, SnapshotData current)
    {
        var oldItems = previous.Operators.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var newItem in current.Operators)
        {
            if (oldItems.TryGetValue(newItem.Name, out var oldItem)) yield return (oldItem, newItem);
        }
    }

    private static Dictionary<(string Operator, string Slot), GearModel> GearMap(SnapshotData data) =>
        data.Operators.SelectMany(op => op.Gear.Values.Select(item => (Key: (item.Operator, item.Slot), Item: item)))
            .ToDictionary(pair => pair.Key, pair => pair.Item);

    private static (string? Name, int? Refine, int? Level, int? Breakthrough) WeaponKey(OperatorModel op) =>
        (op.WeaponName, op.WeaponRefine, op.WeaponLevel, op.WeaponBreakthrough);

    private static string WeaponDescription(OperatorModel op) =>
        op.WeaponName == null
            ? "—"
            : $"{op.WeaponName} R{op.WeaponRefine ?? 0} {L.T("level_short")}{op.WeaponLevel ?? 0} B{op.WeaponBreakthrough ?? 0}";

    private static string OperatorDescription(OperatorModel op) =>
        $"{op.Rarity ?? 0}★ {op.Element ?? "—"} {op.Class ?? "—"}, P{op.Potential}, {L.T("level_short")}{op.Level?.ToString() ?? "—"}, E{op.Evolve ?? 0}";

    private static string Format(double? value)
    {
        if (value is null) return "—";
        return value.Value == Math.Truncate(value.Value)
            ? value.Value.ToString("N0", AppCulture.Current)
            : value.Value.ToString("N1", AppCulture.Current);
    }

    private static string Format(long? value) =>
        value?.ToString("N0", AppCulture.Current) ?? "—";
}
