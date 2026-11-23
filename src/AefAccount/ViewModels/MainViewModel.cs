using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AefAccount.Models;
using AefAccount.Services;

namespace AefAccount.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly DataService _dataService;
    private readonly UpdateService _updateService;
    private SnapshotMeta? _meta;
    private SnapshotSummary? _selectedSnapshot;
    private SnapshotData? _data;
    private bool _suppressSelection;
    private bool _suppressCompare;
    private bool _loading;
    private bool _refreshing;
    private bool _compareLoading;
    private bool _isBusy;
    private string _busyText = "";
    private string _statusMessage = "";
    private bool _statusIsError;
    private string _pageTitle = "Обзор";
    private string _pageEyebrow = "// OVERVIEW";
    private string _freshnessText = "нет снимков";
    private string _freshnessState = "Bad";
    private string _rosterSearch = "";
    private string _selectedRarity = "Все";
    private string _selectedElement = "Все";
    private string _selectedClass = "Все";
    private string _selectedSort = "Редкость";
    private string _rosterCountText = "";
    private string _achievementSearch = "";
    private string _selectedAchievementCategory = "Все";
    private MetricOption? _selectedMetric;
    private ChartModel? _chart;
    private SnapshotSummary? _compareA;
    private SnapshotSummary? _compareB;
    private DiffModel? _compareDiff;
    private string _compareHint = "";
    private int _compareGeneration;
    private readonly Dictionary<int, SnapshotData> _devData = [];
    private DevProfile? _devProfile;
    private bool _showTestOperators;
    private bool _useDevProfile;
    private string _selectedLanguage = "RU";
    private string _settingsStatus = "Тестовые данные выключены.";
    private int _testOperatorSeq;

    public MainViewModel()
    {
        _dataService = new DataService();
        _updateService = new UpdateService(_dataService.RepoRoot, _dataService.DbPath);
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(false));
        TakeSnapshotCommand = new AsyncRelayCommand(TakeSnapshotAsync);
        SelectSnapshotCommand = new AsyncRelayCommand<SnapshotSummary>(snapshot => SelectSnapshotAsync(snapshot?.Id));
        OpenSnapshotCommand = new AsyncRelayCommand<SnapshotSummary>(snapshot => OpenSnapshotAsync(snapshot));
        ClearStatusCommand = new RelayCommand(() => StatusMessage = "");
        Metrics =
        [
            new MetricOption { Key = "player_level", Label = "Уровень игрока" },
            new MetricOption { Key = "world_level", Label = "World level" },
            new MetricOption { Key = "stamina", Label = "Рассудок" },
            new MetricOption { Key = "bp_level", Label = "Battle Pass" },
            new MetricOption { Key = "achieve_count", Label = "Достижения" },
            new MetricOption { Key = "char_num", Label = "Операторы" },
            new MetricOption { Key = "weapon_num", Label = "Оружие" },
            new MetricOption { Key = "doc_num", Label = "Документы" },
            new MetricOption { Key = "credit", Label = "Кредиты" },
            new MetricOption { Key = "seek", Label = "Seek Suspicion" },
        ];
        _selectedMetric = Metrics[0];
        AddTestOperatorCommand = new RelayCommand(AddTestOperator);
        AddVesperCommand = new RelayCommand(AddVesper);
        LoadDevProfileCommand = new RelayCommand(LoadDevProfile);
        ResetTestDataCommand = new RelayCommand(ResetTestData);
    }

    public event EventHandler<string>? NavigateRequested;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string RepoRoot => _dataService.RepoRoot;
    public string DbPath => _dataService.DbPath;
    public bool DatabaseExists => _dataService.DatabaseExists;

    public ObservableCollection<SnapshotSummary> Snapshots { get; } = [];
    public ObservableCollection<OperatorModel> FilteredOperators { get; } = [];
    public ObservableCollection<OperatorModel> TestOperators { get; } = [];
    public ObservableCollection<AchievementModel> FilteredAchievements { get; } = [];
    public ObservableCollection<string> LanguageOptions { get; } = ["RU", "EN"];
    public ObservableCollection<string> RarityOptions { get; } = ["Все", "6★", "5★", "4★"];
    public ObservableCollection<string> ElementOptions { get; } = ["Все"];
    public ObservableCollection<string> ClassOptions { get; } = ["Все"];
    public ObservableCollection<string> SortOptions { get; } = ["Редкость", "Пот", "Уровень", "Имя"];
    public ObservableCollection<string> AchievementCategories { get; } = ["Все"];
    public ObservableCollection<MetricOption> Metrics { get; }
    public ObservableCollection<NavItem> NavItems { get; } =
    [
        new NavItem { Key = "overview", Label = "Обзор", Icon = "\uE80F", Eyebrow = "// OVERVIEW" },
        new NavItem { Key = "roster", Label = "Ростер", Icon = "\uE716", Eyebrow = "// ROSTER" },
        new NavItem { Key = "dynamics", Label = "Динамика", Icon = "\uE9D9", Eyebrow = "// TIMELINE" },
        new NavItem { Key = "progress", Label = "Прогресс", Icon = "\uE909", Eyebrow = "// DOMAINS / EVENTS" },
        new NavItem { Key = "achievements", Label = "Достижения", Icon = "\uE7C1", Eyebrow = "// ACHIEVEMENTS" },
        new NavItem { Key = "snapshots", Label = "Снимки", Icon = "\uE81C", Eyebrow = "// ARCHIVE" },
        new NavItem { Key = "settings", Label = "Настройки", Icon = "\uE713", Eyebrow = "// SETTINGS" },
    ];

    public ICommand RefreshCommand { get; }
    public ICommand TakeSnapshotCommand { get; }
    public ICommand SelectSnapshotCommand { get; }
    public ICommand OpenSnapshotCommand { get; }
    public ICommand ClearStatusCommand { get; }
    public ICommand AddTestOperatorCommand { get; }
    public ICommand AddVesperCommand { get; }
    public ICommand LoadDevProfileCommand { get; }
    public ICommand ResetTestDataCommand { get; }

    public SnapshotMeta? Meta
    {
        get => _meta;
        private set
        {
            if (Set(ref _meta, value))
            {
                UpdateFreshness();
                OnPropertyChanged(nameof(LatestText));
            }
        }
    }

    public SnapshotData? Data
    {
        get => _data;
        private set
        {
            if (Set(ref _data, value))
            {
                OnPropertyChanged(nameof(HasData));
                OnPropertyChanged(nameof(PlatedCount));
                OnPropertyChanged(nameof(InventoryText));
                OnPropertyChanged(nameof(DomainsCount));
                OnPropertyChanged(nameof(LocationsCount));
                OnPropertyChanged(nameof(PlayerName));
                OnPropertyChanged(nameof(PlayerMeta));
                OnPropertyChanged(nameof(PlayerInitial));
                OnPropertyChanged(nameof(TopOperators));
                OnPropertyChanged(nameof(HasDiffChanges));
                OnPropertyChanged(nameof(OverviewDiffLines));
                OnPropertyChanged(nameof(DiffEmptyText));
            }
        }
    }

    public bool HasData => Data != null;
    public int PlatedCount => Data?.PlatedCount ?? 0;
    public string InventoryText => Data == null
        ? "—"
        : $"{Data.Extras.Inventory.Equips:N0} / {Data.Extras.Inventory.EquipCopies:N0}".Replace(",", " ");
    public int DomainsCount => Data?.Domains.Count ?? 0;
    public int LocationsCount => Data?.Domains.Sum(domain => domain.Collections.Count) ?? 0;
    public string LatestText => Meta?.Latest?.DisplayDate ?? "—";
    public string PlayerName => Data?.Snapshot.PlayerName ?? "нет данных";
    public string PlayerMeta => Data == null
        ? "снимок не загружен"
        : $"LV {Data.Snapshot.PlayerLevel?.ToString() ?? "—"} · WL {Data.Snapshot.WorldLevel?.ToString() ?? "—"} · v{Data.Snapshot.GameVersion ?? "?"}";
    public string PlayerInitial => string.IsNullOrWhiteSpace(PlayerName) || PlayerName == "нет данных" ? "?" : PlayerName.Trim()[..1].ToUpperInvariant();
    public IEnumerable<OperatorModel> TopOperators => ActiveOperators.Take(8);

    private IEnumerable<OperatorModel> ActiveOperators
    {
        get
        {
            var operators = Data?.Operators.AsEnumerable() ?? Enumerable.Empty<OperatorModel>();
            if (ShowTestOperators) operators = operators.Concat(TestOperators);
            return operators;
        }
    }
    public bool HasDiffChanges => Data?.Diff?.HasChanges == true;
    public IEnumerable<DiffLine> OverviewDiffLines => Data?.Diff?.AllLines.Take(8) ?? [];
    public string DiffEmptyText => Data?.Diff == null
        ? "Первый снимок — база для будущей динамики."
        : "Изменений с прошлым снимком нет.";

    public SnapshotSummary? FirstSnapshot => Snapshots.LastOrDefault();

    public SnapshotSummary? SelectedSnapshot
    {
        get => _selectedSnapshot;
        set
        {
            if (!Set(ref _selectedSnapshot, value)) return;
            OnPropertyChanged(nameof(SelectedSnapshotId));
            if (!_suppressSelection && value != null)
            {
                _ = LoadSnapshotAsync(value.Id);
            }
        }
    }

    public int? SelectedSnapshotId => SelectedSnapshot?.Id;

    public bool ShowTestOperators
    {
        get => _showTestOperators;
        set
        {
            if (!Set(ref _showTestOperators, value)) return;
            ApplyRosterFilters();
            OnPropertyChanged(nameof(TopOperators));
            SettingsStatus = value ? "Тестовые операторы видны." : "Тестовые операторы скрыты.";
        }
    }

    public bool UseDevProfile
    {
        get => _useDevProfile;
        set
        {
            if (!Set(ref _useDevProfile, value)) return;
            if (value) LoadDevProfile();
            else
            {
                SettingsStatus = "DEV-профиль выключен: возвращаю реальные снимки.";
                _ = RefreshAsync(true);
            }
        }
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (!Set(ref _selectedLanguage, value)) return;
            SettingsStatus = value == "EN"
                ? "EN-перевод пока не реализован: сначала соберём термины с вики."
                : "Язык интерфейса: RU.";
        }
    }

    public string SettingsStatus
    {
        get => _settingsStatus;
        private set => Set(ref _settingsStatus, value);
    }

    public string PageTitle
    {
        get => _pageTitle;
        set => Set(ref _pageTitle, value);
    }

    public string PageEyebrow
    {
        get => _pageEyebrow;
        set => Set(ref _pageEyebrow, value);
    }

    public string FreshnessText
    {
        get => _freshnessText;
        private set => Set(ref _freshnessText, value);
    }

    public string FreshnessState
    {
        get => _freshnessState;
        private set => Set(ref _freshnessState, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => Set(ref _isBusy, value);
    }

    public string BusyText
    {
        get => _busyText;
        private set => Set(ref _busyText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (Set(ref _statusMessage, value)) OnPropertyChanged(nameof(HasStatus));
        }
    }

    public bool StatusIsError
    {
        get => _statusIsError;
        private set => Set(ref _statusIsError, value);
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public string RosterSearch
    {
        get => _rosterSearch;
        set
        {
            if (Set(ref _rosterSearch, value)) ApplyRosterFilters();
        }
    }

    public string SelectedRarity
    {
        get => _selectedRarity;
        set
        {
            if (Set(ref _selectedRarity, value)) ApplyRosterFilters();
        }
    }

    public string SelectedElement
    {
        get => _selectedElement;
        set
        {
            if (Set(ref _selectedElement, value)) ApplyRosterFilters();
        }
    }

    public string SelectedClass
    {
        get => _selectedClass;
        set
        {
            if (Set(ref _selectedClass, value)) ApplyRosterFilters();
        }
    }

    public string SelectedSort
    {
        get => _selectedSort;
        set
        {
            if (Set(ref _selectedSort, value)) ApplyRosterFilters();
        }
    }

    public string RosterCountText
    {
        get => _rosterCountText;
        private set => Set(ref _rosterCountText, value);
    }

    public bool HasRosterResults => FilteredOperators.Count > 0;
    public bool HasAchievementResults => FilteredAchievements.Count > 0;
    public string AchievementCountText => $"{FilteredAchievements.Count} / {Data?.Achievements.Count ?? 0}";
    public int AchievementCategoryCount => Math.Max(0, AchievementCategories.Count - 1);

    public string AchievementSearch
    {
        get => _achievementSearch;
        set
        {
            if (Set(ref _achievementSearch, value)) ApplyAchievementFilters();
        }
    }

    public string SelectedAchievementCategory
    {
        get => _selectedAchievementCategory;
        set
        {
            if (Set(ref _selectedAchievementCategory, value)) ApplyAchievementFilters();
        }
    }

    public MetricOption? SelectedMetric
    {
        get => _selectedMetric;
        set
        {
            if (Set(ref _selectedMetric, value)) BuildChart();
        }
    }

    public ChartModel? Chart
    {
        get => _chart;
        private set => Set(ref _chart, value);
    }

    public SnapshotSummary? CompareA
    {
        get => _compareA;
        set
        {
            if (!Set(ref _compareA, value)) return;
            if (!_suppressCompare) _ = LoadCompareAsync();
        }
    }

    public SnapshotSummary? CompareB
    {
        get => _compareB;
        set
        {
            if (!Set(ref _compareB, value)) return;
            if (!_suppressCompare) _ = LoadCompareAsync();
        }
    }

    public DiffModel? CompareDiff
    {
        get => _compareDiff;
        private set
        {
            if (Set(ref _compareDiff, value)) OnPropertyChanged(nameof(HasCompareDiff));
        }
    }

    public bool HasCompareDiff => CompareDiff is { HasChanges: true };
    public string CompareHint
    {
        get => _compareHint;
        private set => Set(ref _compareHint, value);
    }

    public bool IsCompareLoading
    {
        get => _compareLoading;
        private set => Set(ref _compareLoading, value);
    }

    public async Task InitializeAsync()
    {
        if (!DatabaseExists)
        {
            StatusIsError = true;
            StatusMessage = $"Нет базы: {DbPath}. Сначала запусти scripts\\snapshot.py";
            return;
        }
        await RefreshAsync(true);
    }

    public async Task RefreshAsync(bool selectLatest)
    {
        if (_refreshing) return;
        if (_useDevProfile)
        {
            _useDevProfile = false;
            OnPropertyChanged(nameof(UseDevProfile));
        }
        _devData.Clear();
        _devProfile = null;
        _refreshing = true;
        IsBusy = true;
        BusyText = "Читаю снимки…";
        try
        {
            var meta = await Task.Run(_dataService.LoadMeta);
            var snapshots = await Task.Run(_dataService.LoadSnapshots);
            Meta = meta;
            Snapshots.Clear();
            foreach (var item in snapshots.OrderByDescending(item => item.Id)) Snapshots.Add(item);
            OnPropertyChanged(nameof(Snapshots));
            OnPropertyChanged(nameof(FirstSnapshot));
            var preferredId = selectLatest ? meta.Latest?.Id : SelectedSnapshot?.Id ?? meta.Latest?.Id;
            var target = Snapshots.FirstOrDefault(item => item.Id == preferredId) ?? Snapshots.FirstOrDefault();
            if (target == null)
            {
                SelectSnapshotSilently(null);
                Data = null;
                StatusIsError = true;
                StatusMessage = "Снимков пока нет. Нажми «Снять снимок».";
                return;
            }
            SelectSnapshotSilently(target);
            await LoadSnapshotAsync(target.Id, false);
        }
        catch (Exception exception)
        {
            StatusIsError = true;
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
            _refreshing = false;
            UpdateFreshness();
        }
    }

    public async Task SelectSnapshotAsync(int? snapshotId)
    {
        if (snapshotId is null) return;
        var target = Snapshots.FirstOrDefault(item => item.Id == snapshotId);
        if (target == null) return;
        if (SelectedSnapshot?.Id == target.Id && Data?.Snapshot.Id == target.Id) return;
        SelectSnapshotSilently(target);
        await LoadSnapshotAsync(target.Id);
    }

    public async Task OpenSnapshotAsync(SnapshotSummary? snapshot)
    {
        if (snapshot == null) return;
        await SelectSnapshotAsync(snapshot.Id);
    }

    private void SelectSnapshotSilently(SnapshotSummary? snapshot)
    {
        _suppressSelection = true;
        SelectedSnapshot = snapshot;
        _suppressSelection = false;
    }

    private async Task LoadSnapshotAsync(int snapshotId, bool manageBusy = true)
    {
        if (_devData.TryGetValue(snapshotId, out _))
        {
            LoadDevSnapshot(snapshotId);
            return;
        }

        if (_loading) return;
        _loading = true;
        if (manageBusy)
        {
            IsBusy = true;
            BusyText = "Загружаю снимок…";
        }
        try
        {
            var data = await Task.Run(() => _dataService.LoadSnapshot(snapshotId));
            Data = data;
            var summary = Snapshots.FirstOrDefault(item => item.Id == snapshotId);
            if (summary != null && SelectedSnapshot?.Id != snapshotId) SelectSnapshotSilently(summary);
            ApplyData(data);
            StatusIsError = false;
            StatusMessage = "";
        }
        catch (Exception exception)
        {
            StatusIsError = true;
            StatusMessage = exception.Message;
        }
        finally
        {
            _loading = false;
            if (manageBusy) IsBusy = false;
            UpdateFreshness();
        }
    }

    private void ApplyData(SnapshotData data)
    {
        var elements = data.Operators.Select(item => item.Element).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().OrderBy(item => item).ToList();
        var classes = data.Operators.Select(item => item.Class).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().OrderBy(item => item).ToList();
        ReplaceOptions(ElementOptions, ["Все", .. elements!]);
        ReplaceOptions(ClassOptions, ["Все", .. classes!]);
        if (!ElementOptions.Contains(SelectedElement)) SelectedElement = "Все";
        if (!ClassOptions.Contains(SelectedClass)) SelectedClass = "Все";

        var categories = data.Achievements.Select(item => item.Category).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().OrderBy(item => item).ToList();
        ReplaceOptions(AchievementCategories, ["Все", .. categories!]);
        if (!AchievementCategories.Contains(SelectedAchievementCategory)) SelectedAchievementCategory = "Все";

        ApplyRosterFilters();
        ApplyAchievementFilters();
        BuildChart();
        SetupCompare(data);
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(PlatedCount));
        OnPropertyChanged(nameof(InventoryText));
        OnPropertyChanged(nameof(DomainsCount));
        UpdateFreshness();
    }

    private static void ReplaceOptions(ObservableCollection<string> target, IEnumerable<string> values)
    {
        var list = values.ToList();
        if (target.SequenceEqual(list)) return;
        target.Clear();
        foreach (var value in list) target.Add(value);
    }

    private void ApplyRosterFilters()
    {
        var source = Data == null ? new List<OperatorModel>() : Data.Operators.ToList();
        if (ShowTestOperators) source.AddRange(TestOperators);

        if (Data == null)
        {
            FilteredOperators.Clear();
            RosterCountText = "";
            return;
        }

        var query = RosterSearch.Trim();
        var filtered = source.Where(op =>
        {
            if (SelectedRarity != "Все" && $"{(char)('0' + (op.Rarity ?? 0))}★" != SelectedRarity) return false;
            if (SelectedElement != "Все" && op.Element != SelectedElement) return false;
            if (SelectedClass != "Все" && op.Class != SelectedClass) return false;
            return query.Length == 0 ||
                   $"{op.Name} {op.DisplayName} {op.WeaponName} {op.Class} {op.Element}".Contains(query, StringComparison.OrdinalIgnoreCase);
        }).ToList();

        filtered = SelectedSort switch
        {
            "Имя" => filtered.OrderBy(op => op.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(),
            "Пот" => filtered.OrderByDescending(op => op.Potential).ThenByDescending(op => op.Rarity).ThenBy(op => op.Name).ToList(),
            "Уровень" => filtered.OrderByDescending(op => op.Level).ThenByDescending(op => op.Rarity).ThenBy(op => op.Name).ToList(),
            _ => filtered.OrderByDescending(op => op.Rarity).ThenByDescending(op => op.Potential).ThenBy(op => op.Name).ToList(),
        };

        FilteredOperators.Clear();
        foreach (var op in filtered) FilteredOperators.Add(op);
        RosterCountText = $"{FilteredOperators.Count} / {source.Count}";
        OnPropertyChanged(nameof(HasRosterResults));
    }

    private void ApplyAchievementFilters()
    {
        if (Data == null)
        {
            FilteredAchievements.Clear();
            return;
        }

        var query = AchievementSearch.Trim();
        var filtered = Data.Achievements.Where(item =>
            (SelectedAchievementCategory == "Все" || item.Category == SelectedAchievementCategory) &&
            (query.Length == 0 || $"{item.Name} {item.Category}".Contains(query, StringComparison.OrdinalIgnoreCase)));
        FilteredAchievements.Clear();
        foreach (var item in filtered) FilteredAchievements.Add(item);
        OnPropertyChanged(nameof(HasAchievementResults));
        OnPropertyChanged(nameof(AchievementCountText));
        OnPropertyChanged(nameof(AchievementCategoryCount));
    }

    private void BuildChart()
    {
        if (Data == null || SelectedMetric == null) return;
        var points = new List<ChartPoint>();
        foreach (var snapshot in Snapshots.OrderBy(item => item.Id))
        {
            var value = MetricValue(snapshot, SelectedMetric.Key);
            if (value == null) continue;
            points.Add(new ChartPoint
            {
                SnapshotId = snapshot.Id,
                Value = value.Value,
                Label = $"#{snapshot.Id}",
                DateText = snapshot.DisplayDateShort,
            });
        }

        var first = points.FirstOrDefault()?.Value;
        var last = points.LastOrDefault()?.Value;
        var delta = points.Count > 1 && first != null && last != null ? last - first : null;
        Chart = new ChartModel
        {
            Label = SelectedMetric.Label,
            Points = points,
            ValueText = last == null ? "—" : FormatMetric(last.Value),
            DeltaText = delta == null ? "" : delta == 0 ? " · без изменений" : $" · {(delta > 0 ? "+" : "")}{FormatMetric(delta.Value)} за период",
        };
    }

    private void SetupCompare(SnapshotData data)
    {
        _suppressCompare = true;
        CompareB = Snapshots.FirstOrDefault(item => item.Id == data.Snapshot.Id);
        CompareA = data.PreviousId is int previousId
            ? Snapshots.FirstOrDefault(item => item.Id == previousId)
            : CompareB;
        _suppressCompare = false;
        CompareDiff = null;
        _ = LoadCompareAsync();
    }

    private async Task LoadCompareAsync()
    {
        var generation = ++_compareGeneration;
        var a = CompareA;
        var b = CompareB;
        if (a == null || b == null || a.Id == b.Id)
        {
            CompareDiff = null;
            CompareHint = "";
            IsCompareLoading = false;
            return;
        }

        if (_devData.TryGetValue(a.Id, out var aData) && _devData.TryGetValue(b.Id, out var bData))
        {
            CompareDiff = DiffService.Compute(aData, bData);
            CompareHint = $"#{a.Id} → #{b.Id} · DEV";
            IsCompareLoading = false;
            return;
        }

        IsCompareLoading = true;
        CompareHint = $"#{a.Id} → #{b.Id}";
        try
        {
            var diff = await Task.Run(() => _dataService.LoadDiff(a.Id, b.Id));
            if (generation != _compareGeneration) return;
            CompareDiff = diff;
        }
        catch (Exception exception)
        {
            if (generation != _compareGeneration) return;
            CompareDiff = null;
            CompareHint = exception.Message;
        }
        finally
        {
            if (generation == _compareGeneration) IsCompareLoading = false;
        }
    }

    private void LoadDevProfile()
    {
        if (!_useDevProfile)
        {
            _useDevProfile = true;
            OnPropertyChanged(nameof(UseDevProfile));
        }

        _devProfile = DevDataService.Create();
        _devData.Clear();
        foreach (var entry in _devProfile.Data) _devData[entry.Key] = entry.Value;

        Snapshots.Clear();
        foreach (var snapshot in _devProfile.Snapshots.OrderByDescending(item => item.Id)) Snapshots.Add(snapshot);
        OnPropertyChanged(nameof(Snapshots));
        OnPropertyChanged(nameof(FirstSnapshot));

        var latest = _devProfile.Snapshots[^1];
        Meta = new SnapshotMeta
        {
            Latest = latest,
            SnapshotsCount = _devProfile.Snapshots.Count,
            CurrentVersion = "1.6",
            AgeDays = 0,
            Now = DateTimeOffset.Now,
        };
        SelectSnapshotSilently(latest);
        LoadDevSnapshot(latest.Id);
        SettingsStatus = "DEV-профиль: 8 снимков, график и diff на фейковых данных.";
        RequestNavigate("dynamics");
    }

    private void LoadDevSnapshot(int snapshotId)
    {
        if (!_devData.TryGetValue(snapshotId, out var data)) return;
        Data = data;
        var summary = Snapshots.FirstOrDefault(item => item.Id == snapshotId);
        if (summary != null) SelectSnapshotSilently(summary);
        ApplyData(data);
        StatusIsError = false;
        StatusMessage = "";
        UpdateFreshness();
    }

    private void AddTestOperator()
    {
        _testOperatorSeq++;
        var random = Random.Shared;
        var elements = new[] { "Physical", "Heat", "Electric", "Cryo", "Nature" };
        var classes = new[] { "Vanguard", "Guard", "Defender", "Caster", "Striker", "Supporter" };
        var weapons = new[] { "Test Frame", "Null Edge", "Dummy Lance", "Sample Blade" };
        var element = elements[random.Next(elements.Length)];
        var cls = classes[random.Next(classes.Length)];
        var rarity = random.Next(4, 7);
        var name = $"Test Subject {_testOperatorSeq:00}";

        var op = new OperatorModel
        {
            Name = name,
            CharId = $"dev-{_testOperatorSeq:00}",
            Rarity = rarity,
            Element = element,
            Class = cls,
            Potential = random.Next(0, 6),
            Level = random.Next(1, 91),
            Evolve = random.Next(0, 5),
            OwnTs = "1793000000",
            WeaponName = weapons[random.Next(weapons.Length)],
            WeaponType = "Test",
            WeaponRarity = rarity,
            WeaponLevel = random.Next(1, 91),
            WeaponRefine = random.Next(0, 6),
            WeaponBreakthrough = random.Next(0, 4),
            Skills = [new SkillModel { Name = "Random Skill", Type = "Basic", Level = random.Next(1, 11), Max = 10 }],
            Talents = new TalentModel { BreakNode = "charBreak2", PassiveCount = 2, FactoryCount = 1, ShipCount = 1 },
        };

        TestOperators.Add(op);
        _showTestOperators = true;
        OnPropertyChanged(nameof(ShowTestOperators));
        ApplyRosterFilters();
        OnPropertyChanged(nameof(TopOperators));
        SettingsStatus = $"Добавлен {name} без арта: должна быть буква T.";
        RequestNavigate("roster");
    }

    private void AddVesper()
    {
        if (TestOperators.Any(item => item.Name == "Vesper"))
        {
            SettingsStatus = "Веспер уже в тестовом ростере.";
            RequestNavigate("roster");
            return;
        }

        TestOperators.Add(DevDataService.CreateVesper());
        _showTestOperators = true;
        OnPropertyChanged(nameof(ShowTestOperators));
        ApplyRosterFilters();
        OnPropertyChanged(nameof(TopOperators));
        SettingsStatus = "Добавлен Веспер: прозрачные icon/portrait/full из тестовых ассетов.";
        RequestNavigate("roster");
    }

    private void ResetTestData()
    {
        TestOperators.Clear();
        _testOperatorSeq = 0;
        _devData.Clear();
        _devProfile = null;
        _showTestOperators = true;
        OnPropertyChanged(nameof(ShowTestOperators));

        if (_useDevProfile)
        {
            _useDevProfile = false;
            OnPropertyChanged(nameof(UseDevProfile));
            _ = RefreshAsync(true);
        }
        else
        {
            ApplyRosterFilters();
            OnPropertyChanged(nameof(TopOperators));
        }

        SettingsStatus = "Тестовые операторы и DEV-профиль очищены.";
    }

    private void RequestNavigate(string key) => NavigateRequested?.Invoke(this, key);

    private async Task TakeSnapshotAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        BusyText = "Снимаю снимок через API…";
        StatusIsError = false;
        StatusMessage = "Запускаю snapshot.py…";
        try
        {
            var result = await _updateService.RunAsync();
            if (!result.Ok)
            {
                StatusIsError = true;
                StatusMessage = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
                return;
            }
            await RefreshAsync(true);
            var latestId = Meta?.Latest?.Id;
            StatusIsError = false;
            StatusMessage = latestId is null
                ? $"Снимок готов за {result.Duration.TotalSeconds:0.0} с"
                : $"Снимок #{latestId} готов за {result.Duration.TotalSeconds:0.0} с";
        }
        catch (Exception exception)
        {
            StatusIsError = true;
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateFreshness()
    {
        var latest = Meta?.Latest;
        if (latest == null)
        {
            FreshnessText = "НЕТ СНИМКОВ";
            FreshnessState = "Bad";
            return;
        }

        var isArchive = Data != null && Data.Snapshot.Id != latest.Id;
        var versionMismatch = Meta?.CurrentVersion != null && latest.GameVersion != null && Meta.CurrentVersion != latest.GameVersion;
        var old = Meta?.AgeDays is >= 7;
        if (versionMismatch)
        {
            FreshnessText = "ВЕРСИЯ СМЕНИЛАСЬ";
            FreshnessState = "Bad";
        }
        else if (old)
        {
            FreshnessText = "СТАРШЕ 7 ДНЕЙ";
            FreshnessState = "Warn";
        }
        else
        {
            FreshnessText = $"СВЕЖИЙ · {Ago(latest.TakenAtServer)}";
            FreshnessState = "Good";
        }
        if (isArchive)
        {
            FreshnessText = $"АРХИВ #{Data?.Snapshot.Id}";
            FreshnessState = "Warn";
        }
    }

    private static string Ago(string? iso)
    {
        if (!DateTimeOffset.TryParse(iso, out var date)) return "—";
        var span = DateTimeOffset.Now - date;
        if (span.TotalMinutes < 1) return "только что";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} мин";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours} ч {(int)span.Minutes} мин";
        return $"{(int)span.TotalDays} д {(int)span.Hours} ч";
    }

    private static double? MetricValue(SnapshotSummary snapshot, string key) => key switch
    {
        "player_level" => snapshot.PlayerLevel,
        "world_level" => snapshot.WorldLevel,
        "stamina" => snapshot.Stamina,
        "bp_level" => snapshot.BpLevel,
        "achieve_count" => snapshot.AchieveCount,
        "char_num" => snapshot.CharNum,
        "weapon_num" => snapshot.WeaponNum,
        "doc_num" => snapshot.DocNum,
        "credit" => snapshot.Credit,
        "seek" => snapshot.Seek,
        _ => null,
    };

    private static string FormatMetric(double value) =>
        value == Math.Truncate(value)
            ? value.ToString("N0", AppCulture.Ru)
            : value.ToString("N1", AppCulture.Ru);

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

