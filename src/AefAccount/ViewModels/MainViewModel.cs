using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AefAccount.Localization;
using AefAccount.Models;
using AefAccount.Services;

namespace AefAccount.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private const string FilterAllKey = "all";

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
    private string _pageTitle = "";
    private string _pageEyebrow = "// OVERVIEW";
    private string _currentPageKey = "overview";
    private string _freshnessText = "";
    private string _freshnessState = "Bad";
    private string _rosterSearch = "";
    private LocalizedOption? _selectedRarity;
    private LocalizedOption? _selectedElement;
    private LocalizedOption? _selectedClass;
    private LocalizedOption? _selectedSort;
    private string _rosterCountText = "";
    private string _achievementSearch = "";
    private LocalizedOption? _selectedAchievementCategory;
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
    private LanguageOption? _selectedLanguage;
    private string _settingsStatus = "";
    private int _testOperatorSeq;
    private ArtPackStatus _artStatus = ArtPackStatus.None;
    private bool _artBusy;
    private double _artProgress;

    public MainViewModel()
    {
        _dataService = new DataService();
        _updateService = new UpdateService(_dataService.RepoRoot, _dataService.DbPath);

        var localization = LocalizationService.Instance;
        localization.LanguageChanged += OnLanguageChanged;
        localization.LanguagesChanged += OnLanguagesChanged;

        RarityOptions.Add(new LocalizedOption { Key = FilterAllKey, LabelKey = "filter_all" });
        RarityOptions.Add(new LocalizedOption { Key = "6", Fallback = "6★" });
        RarityOptions.Add(new LocalizedOption { Key = "5", Fallback = "5★" });
        RarityOptions.Add(new LocalizedOption { Key = "4", Fallback = "4★" });
        ElementOptions.Add(new LocalizedOption { Key = FilterAllKey, LabelKey = "filter_all" });
        ClassOptions.Add(new LocalizedOption { Key = FilterAllKey, LabelKey = "filter_all" });
        AchievementCategories.Add(new LocalizedOption { Key = FilterAllKey, LabelKey = "filter_all" });
        SortOptions.Add(new LocalizedOption { Key = "rarity", LabelKey = "filter_sort_rarity" });
        SortOptions.Add(new LocalizedOption { Key = "potential", LabelKey = "filter_sort_potential" });
        SortOptions.Add(new LocalizedOption { Key = "level", LabelKey = "filter_sort_level" });
        SortOptions.Add(new LocalizedOption { Key = "name", LabelKey = "filter_sort_name" });

        _selectedRarity = RarityOptions[0];
        _selectedElement = ElementOptions[0];
        _selectedClass = ClassOptions[0];
        _selectedSort = SortOptions[0];
        _selectedAchievementCategory = AchievementCategories[0];
        _selectedLanguage = localization.AvailableLanguages
            .FirstOrDefault(item => item.Code == localization.LanguagePreference) ?? localization.AvailableLanguages.FirstOrDefault();
        _settingsStatus = L.T("settings_status_initial");
        _freshnessText = L.T("freshness_none");

        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(false));
        TakeSnapshotCommand = new AsyncRelayCommand(TakeSnapshotAsync);
        SelectSnapshotCommand = new AsyncRelayCommand<SnapshotSummary>(snapshot => SelectSnapshotAsync(snapshot?.Id));
        OpenSnapshotCommand = new AsyncRelayCommand<SnapshotSummary>(snapshot => OpenSnapshotAsync(snapshot));
        ClearStatusCommand = new RelayCommand(() => StatusMessage = "");
        AddTestOperatorCommand = new RelayCommand(AddTestOperator);
        AddVesperCommand = new RelayCommand(AddVesper);
        LoadDevProfileCommand = new RelayCommand(() => LoadDevProfile());
        ResetTestDataCommand = new RelayCommand(ResetTestData);
        ReloadLanguagesCommand = new RelayCommand(ReloadLanguages);
        OpenSettingsCommand = new RelayCommand(() => RequestNavigate("settings"));
        _artStatus = ArtPack.Status;

        Metrics =
        [
            new MetricOption { Key = "player_level", LabelKey = "metric_player_level" },
            new MetricOption { Key = "world_level", LabelKey = "metric_world_level" },
            new MetricOption { Key = "stamina", LabelKey = "metric_stamina" },
            new MetricOption { Key = "bp_level", LabelKey = "metric_bp" },
            new MetricOption { Key = "achieve_count", LabelKey = "metric_achievements" },
            new MetricOption { Key = "char_num", LabelKey = "metric_operators" },
            new MetricOption { Key = "weapon_num", LabelKey = "metric_weapons" },
            new MetricOption { Key = "doc_num", LabelKey = "metric_documents" },
            new MetricOption { Key = "credit", LabelKey = "metric_credits" },
            new MetricOption { Key = "seek", LabelKey = "metric_seek" },
        ];
        _selectedMetric = Metrics[0];
        SetPage("overview");
    }

    public event EventHandler<string>? NavigateRequested;

    /// <summary>Ключ пропал или не задан: окно показывает ввод ключа.</summary>
    public event EventHandler? TokenRequired;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string RepoRoot => _dataService.RepoRoot;
    public string DbPath => _dataService.DbPath;
    public bool DatabaseExists => _dataService.DatabaseExists;

    public ObservableCollection<SnapshotSummary> Snapshots { get; } = [];
    public ObservableCollection<OperatorModel> FilteredOperators { get; } = [];
    public ObservableCollection<OperatorModel> TestOperators { get; } = [];
    public ObservableCollection<AchievementModel> FilteredAchievements { get; } = [];
    public ObservableCollection<LanguageOption> LanguageOptions => LocalizationService.Instance.AvailableLanguages;
    public ObservableCollection<LocalizedOption> RarityOptions { get; } = [];
    public ObservableCollection<LocalizedOption> ElementOptions { get; } = [];
    public ObservableCollection<LocalizedOption> ClassOptions { get; } = [];
    public ObservableCollection<LocalizedOption> SortOptions { get; } = [];
    public ObservableCollection<LocalizedOption> AchievementCategories { get; } = [];
    public ObservableCollection<MetricOption> Metrics { get; }
    public ObservableCollection<NavItem> NavItems { get; } =
    [
        new NavItem { Key = "overview", LabelKey = "nav_overview", Icon = "\uE80F", Eyebrow = "// OVERVIEW" },
        new NavItem { Key = "roster", LabelKey = "nav_roster", Icon = "\uE716", Eyebrow = "// ROSTER" },
        new NavItem { Key = "dynamics", LabelKey = "nav_dynamics", Icon = "\uE9D9", Eyebrow = "// TIMELINE" },
        new NavItem { Key = "progress", LabelKey = "nav_progress", Icon = "\uE909", Eyebrow = "// REGIONS / EVENTS" },
        new NavItem { Key = "achievements", LabelKey = "nav_achievements", Icon = "\uE7C1", Eyebrow = "// ACHIEVEMENTS" },
        new NavItem { Key = "snapshots", LabelKey = "nav_snapshots", Icon = "\uE81C", Eyebrow = "// ARCHIVE" },
        new NavItem { Key = "settings", LabelKey = "nav_settings", Icon = "\uE713", Eyebrow = "// SETTINGS" },
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
    public ICommand ReloadLanguagesCommand { get; }
    public ICommand OpenSettingsCommand { get; }

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
        : string.Format(AppCulture.Current, "{0:N0} / {1:N0}", Data.Extras.Inventory.Equips, Data.Extras.Inventory.EquipCopies);
    public int DomainsCount => Data?.Domains.Count ?? 0;
    public int LocationsCount => Data?.Domains.Sum(domain => domain.Collections.Count) ?? 0;
    public string LatestText => Meta?.Latest?.DisplayDate ?? "—";
    public string PlayerName => Data?.Snapshot.PlayerName ?? L.T("no_data");
    public string PlayerMeta => Data == null
        ? L.T("snapshot_not_loaded")
        : $"LV {Data.Snapshot.PlayerLevel?.ToString() ?? "—"} · WL {Data.Snapshot.WorldLevel?.ToString() ?? "—"} · v{Data.Snapshot.GameVersion ?? "?"}";
    public string PlayerInitial => string.IsNullOrWhiteSpace(PlayerName) || PlayerName == L.T("no_data") ? "?" : PlayerName.Trim()[..1].ToUpperInvariant();
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
        ? L.T("diff_empty_first")
        : L.T("diff_empty_none");

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
            SettingsStatus = L.T(value ? "settings_status_test_visible" : "settings_status_test_hidden");
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
                SettingsStatus = L.T("settings_status_dev_off");
                _ = RefreshAsync(true);
            }
        }
    }

    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (!Set(ref _selectedLanguage, value) || value == null) return;
            LocalizationService.Instance.SetPreference(value.Code);
        }
    }

    public string CurrentPageKey => _currentPageKey;

    public void SetPage(string key)
    {
        var item = NavItems.FirstOrDefault(nav => nav.Key == key) ?? NavItems[0];
        _currentPageKey = item.Key;
        PageTitle = item.Label;
        PageEyebrow = item.Eyebrow;
    }

    public string SettingsStatus
    {
        get => _settingsStatus;
        private set => Set(ref _settingsStatus, value);
    }

    /// <summary>Строка состояния арт-пака: «37 оперативников · 18,3 МБ · v1».</summary>
    public string ArtStatusText
    {
        get
        {
            var status = _artStatus;
            if (!status.IsInstalled) return L.T("settings_art_status_missing");
            var version = string.IsNullOrWhiteSpace(status.Version) ? "" : $" · v{status.Version}";
            return L.T("settings_art_status_installed",
                status.OperatorCount,
                status.Megabytes.ToString("0.#"),
                version);
        }
    }

    public string ArtPathText => _artStatus.Root ?? L.T("settings_art_path_missing");

    public bool HasArtPack => _artStatus.IsInstalled;

    /// <summary>Убирать имеет смысл только свою папку или скачанный пак, не assets рядом с exe.</summary>
    public bool CanRemoveArtPack => _artStatus.Root != null &&
        !string.Equals(
            Path.GetFullPath(_artStatus.Root),
            Path.GetFullPath(ArtPack.ExeRoot),
            StringComparison.OrdinalIgnoreCase);

    public bool CanRemoveArtNow => CanUseArt && CanRemoveArtPack;

    public bool ShowArtBanner => !HasArtPack && (Data?.Operators.Count ?? 0) > 0;

    public bool IsArtBusy
    {
        get => _artBusy;
        set
        {
            if (!Set(ref _artBusy, value)) return;
            OnPropertyChanged(nameof(CanUseArt));
            OnPropertyChanged(nameof(CanRemoveArtNow));
        }
    }

    public bool CanUseArt => !_artBusy;

    public double ArtProgress
    {
        get => _artProgress;
        set => Set(ref _artProgress, value);
    }

    /// <summary>Перечитать папку арта после установки, смены пути или удаления.</summary>
    public void RefreshArtStatus()
    {
        _artStatus = ArtPack.Refresh();
        OperatorArt.Initialize();
        OnPropertyChanged(nameof(ArtStatusText));
        OnPropertyChanged(nameof(ArtPathText));
        OnPropertyChanged(nameof(HasArtPack));
        OnPropertyChanged(nameof(CanRemoveArtPack));
        OnPropertyChanged(nameof(CanRemoveArtNow));
        OnPropertyChanged(nameof(ShowArtBanner));
    }

    public void SetSettingsStatus(string message, bool error = false)
    {
        SettingsStatus = message;
        StatusIsError = error;
    }

    public void SetStatus(string message, bool error = false)
    {
        StatusMessage = message;
        StatusIsError = error;
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

    public LocalizedOption? SelectedRarity
    {
        get => _selectedRarity;
        set
        {
            if (Set(ref _selectedRarity, value)) ApplyRosterFilters();
        }
    }

    public LocalizedOption? SelectedElement
    {
        get => _selectedElement;
        set
        {
            if (Set(ref _selectedElement, value)) ApplyRosterFilters();
        }
    }

    public LocalizedOption? SelectedClass
    {
        get => _selectedClass;
        set
        {
            if (Set(ref _selectedClass, value)) ApplyRosterFilters();
        }
    }

    public LocalizedOption? SelectedSort
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

    public LocalizedOption? SelectedAchievementCategory
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
            StatusMessage = L.T("status_no_database", DbPath);
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
        BusyText = L.T("status_reading_snapshots");
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
                StatusMessage = L.T("status_no_snapshots");
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
            BusyText = L.T("status_loading_snapshot");
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
        var selectedElementKey = SelectedElement?.Key ?? FilterAllKey;
        var selectedClassKey = SelectedClass?.Key ?? FilterAllKey;
        ReplaceOptions(ElementOptions, BuildValueOptions(elements!));
        ReplaceOptions(ClassOptions, BuildValueOptions(classes!));
        SelectedElement = ElementOptions.FirstOrDefault(item => item.Key == selectedElementKey) ?? ElementOptions[0];
        SelectedClass = ClassOptions.FirstOrDefault(item => item.Key == selectedClassKey) ?? ClassOptions[0];

        var categories = data.Achievements.Select(item => item.Category).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().OrderBy(item => item).ToList();
        var selectedCategoryKey = SelectedAchievementCategory?.Key ?? FilterAllKey;
        ReplaceOptions(AchievementCategories, BuildValueOptions(categories!));
        SelectedAchievementCategory = AchievementCategories.FirstOrDefault(item => item.Key == selectedCategoryKey) ?? AchievementCategories[0];

        ApplyRosterFilters();
        ApplyAchievementFilters();
        BuildChart();
        SetupCompare(data);
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(PlatedCount));
        OnPropertyChanged(nameof(InventoryText));
        OnPropertyChanged(nameof(DomainsCount));
        OnPropertyChanged(nameof(ShowArtBanner));
        UpdateFreshness();
    }

    private static List<LocalizedOption> BuildValueOptions(IEnumerable<string> values) =>
    [
        new LocalizedOption { Key = FilterAllKey, LabelKey = "filter_all" },
        .. values.Select(value => new LocalizedOption { Key = value, Fallback = value }),
    ];

    private static void ReplaceOptions<T>(ObservableCollection<T> target, IEnumerable<T> values)
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
        var rarityKey = SelectedRarity?.Key ?? FilterAllKey;
        var elementKey = SelectedElement?.Key ?? FilterAllKey;
        var classKey = SelectedClass?.Key ?? FilterAllKey;
        var filtered = source.Where(op =>
        {
            if (rarityKey != FilterAllKey && $"{(char)('0' + (op.Rarity ?? 0))}" != rarityKey) return false;
            if (elementKey != FilterAllKey && op.Element != elementKey) return false;
            if (classKey != FilterAllKey && op.Class != classKey) return false;
            return query.Length == 0 ||
                   $"{op.Name} {op.DisplayName} {op.WeaponName} {op.Class} {op.Element}".Contains(query, StringComparison.OrdinalIgnoreCase);
        }).ToList();

        filtered = (SelectedSort?.Key ?? "rarity") switch
        {
            "name" => filtered.OrderBy(op => op.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(),
            "potential" => filtered.OrderByDescending(op => op.Potential).ThenByDescending(op => op.Rarity).ThenBy(op => op.Name).ToList(),
            "level" => filtered.OrderByDescending(op => op.Level).ThenByDescending(op => op.Rarity).ThenBy(op => op.Name).ToList(),
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
        var categoryKey = SelectedAchievementCategory?.Key ?? FilterAllKey;
        var filtered = Data.Achievements.Where(item =>
            (categoryKey == FilterAllKey || item.Category == categoryKey) &&
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
            DeltaText = delta == null
                ? ""
                : delta == 0
                    ? L.T("chart_no_changes")
                    : L.T("chart_delta_period", $"{(delta > 0 ? "+" : "")}{FormatMetric(delta.Value)}"),
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

    private void LoadDevProfile(bool navigate = true)
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
        SettingsStatus = L.T("settings_status_dev_loaded");
        if (navigate) RequestNavigate("dynamics");
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
        SettingsStatus = L.T("settings_status_stub_added", name);
        RequestNavigate("roster");
    }

    private void AddVesper()
    {
        if (TestOperators.Any(item => item.Name == "Vesper"))
        {
            SettingsStatus = L.T("settings_status_vesper_exists");
            RequestNavigate("roster");
            return;
        }

        TestOperators.Add(DevDataService.CreateVesper());
        _showTestOperators = true;
        OnPropertyChanged(nameof(ShowTestOperators));
        ApplyRosterFilters();
        OnPropertyChanged(nameof(TopOperators));
        SettingsStatus = L.T("settings_status_vesper_added");
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

        SettingsStatus = L.T("settings_status_reset");
    }

    private void RequestNavigate(string key) => NavigateRequested?.Invoke(this, key);

    private void OnLanguagesChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(LanguageOptions));
        var target = LanguageOptions
            .FirstOrDefault(item => item.Code == LocalizationService.Instance.LanguagePreference)
            ?? LanguageOptions.FirstOrDefault();
        if (target == null) return;
        _selectedLanguage = target;
        OnPropertyChanged(nameof(SelectedLanguage));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        SetPage(_currentPageKey);
        StatusMessage = "";
        SettingsStatus = L.T("settings_status_language_changed");

        foreach (var item in NavItems) item.Refresh();
        foreach (var item in Metrics) item.Refresh();
        RefreshOptionLabels(RarityOptions);
        RefreshOptionLabels(ElementOptions);
        RefreshOptionLabels(ClassOptions);
        RefreshOptionLabels(SortOptions);
        RefreshOptionLabels(AchievementCategories);

        OnPropertyChanged(string.Empty);
        BuildChart();
        _ = ReloadForLanguageAsync();
    }

    private static void RefreshOptionLabels(IEnumerable<LocalizedOption> options)
    {
        foreach (var option in options) option.Refresh();
    }

    private void ReloadLanguages()
    {
        LocalizationService.Instance.Reload();
        SettingsStatus = L.T("settings_status_languages_reloaded", LocalizationService.Instance.CurrentLanguageName);
    }

    private async Task ReloadForLanguageAsync()
    {
        if (Data == null) return;

        if (_useDevProfile)
        {
            var selectedId = Data.Snapshot.Id;
            LoadDevProfile(navigate: false);
            var target = _devProfile?.Snapshots.FirstOrDefault(item => item.Id == selectedId);
            if (target != null)
            {
                SelectSnapshotSilently(target);
                LoadDevSnapshot(selectedId);
            }
            return;
        }

        await LoadSnapshotAsync(Data.Snapshot.Id, false);
    }

    private async Task TakeSnapshotAsync()
    {
        if (IsBusy) return;
        if (!TokenService.HasToken(RepoRoot))
        {
            TokenRequired?.Invoke(this, EventArgs.Empty);
            return;
        }
        IsBusy = true;
        BusyText = L.T("status_taking_snapshot");
        StatusIsError = false;
        StatusMessage = L.T("status_starting_snapshot");
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
                ? L.T("status_snapshot_ready", result.Duration.TotalSeconds)
                : L.T("status_snapshot_ready_id", latestId, result.Duration.TotalSeconds);
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
            FreshnessText = L.T("freshness_none");
            FreshnessState = "Bad";
            return;
        }

        var isArchive = Data != null && Data.Snapshot.Id != latest.Id;
        var versionMismatch = Meta?.CurrentVersion != null && latest.GameVersion != null && Meta.CurrentVersion != latest.GameVersion;
        var old = Meta?.AgeDays is >= 7;
        if (versionMismatch)
        {
            FreshnessText = L.T("freshness_version_changed");
            FreshnessState = "Bad";
        }
        else if (old)
        {
            FreshnessText = L.T("freshness_old");
            FreshnessState = "Warn";
        }
        else
        {
            FreshnessText = L.T("freshness_fresh", Ago(latest.TakenAtServer));
            FreshnessState = "Good";
        }
        if (isArchive)
        {
            FreshnessText = L.T("freshness_archive", Data?.Snapshot.Id ?? 0);
            FreshnessState = "Warn";
        }
    }

    private static string Ago(string? iso)
    {
        if (!DateTimeOffset.TryParse(iso, out var date)) return "—";
        var span = DateTimeOffset.Now - date;
        if (span.TotalMinutes < 1) return L.T("ago_just_now");
        if (span.TotalHours < 1) return L.T("ago_minutes", (int)span.TotalMinutes);
        if (span.TotalDays < 1) return L.T("ago_hours_minutes", (int)span.TotalHours, (int)span.Minutes);
        return L.T("ago_days_hours", (int)span.TotalDays, (int)span.Hours);
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
            ? value.ToString("N0", AppCulture.Current)
            : value.ToString("N1", AppCulture.Current);

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
