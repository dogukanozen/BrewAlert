using System.ComponentModel;
using BrewAlert.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BrewAlert.UI.ViewModels;

public partial class AppearanceSettingsViewModel : ObservableObject, IDisposable
{
    private readonly IAppearanceService _appearance;
    private readonly ILocalizationService _loc;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string _error = "";
    public IReadOnlyList<DayThemeViewModel> Days { get; }
    public bool IsWeekly => _appearance.Options.Mode == AppearanceOptions.Weekly;
    public bool IsObsidian => _appearance.Options.Mode == nameof(BrewTheme.Obsidian);
    public bool IsPorcelain => _appearance.Options.Mode == nameof(BrewTheme.Porcelain);
    public bool IsSumi => _appearance.Options.Mode == nameof(BrewTheme.Sumi);
    public bool IsIbre => _appearance.Options.Mode == nameof(BrewTheme.Ibre);
    public bool IsAizome => _appearance.Options.Mode == nameof(BrewTheme.Aizome);
    public string Title => _loc.Get("AppearanceTitle");
    public string Hint => _loc.Get("AppearanceHint");
    public string WeeklyText => _loc.Get("WeeklyTheme");
    public string ObsidianText => _loc.Get("ThemeObsidian");
    public string PorcelainText => _loc.Get("ThemePorcelain");
    public string SumiText => _loc.Get("ThemeSumi");
    public string IbreText => _loc.Get("ThemeIbre");
    public string AizomeText => _loc.Get("ThemeAizome");
    public string ActiveText => string.Format(_loc.Get("CurrentAppearance"),
        _loc.Get("Theme" + _appearance.CurrentTheme));

    public AppearanceSettingsViewModel(IAppearanceService appearance, ILocalizationService loc)
    {
        _appearance = appearance;
        _loc = loc;
        Days = Enumerable.Range(0, 7).Select(index =>
            new DayThemeViewModel((DayOfWeek)((index + 1) % 7), SaveDayAsync)).ToArray();
        _appearance.PropertyChanged += OnAppearanceChanged;
        _loc.LanguageChanged += OnLanguageChanged;
        Refresh();
    }
    [RelayCommand]
    private async Task SetMode(string mode)
    {
        if (IsSaving) return;
        var options = _appearance.Options;
        options.Mode = mode;
        await SaveAsync(options);
    }
    private async Task SaveDayAsync(DayOfWeek day, BrewTheme theme)
    {
        if (IsSaving) return;
        var options = _appearance.Options;
        options.SetDay(day, theme);
        await SaveAsync(options);
    }
    private async Task SaveAsync(AppearanceOptions options)
    {
        IsSaving = true;
        Error = "";
        try { await _appearance.SaveAsync(options); }
        catch (Exception ex) { Error = string.Format(_loc.Get("SaveFailed"), ex.Message); }
        finally { Refresh(); IsSaving = false; }
    }
    private void OnAppearanceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IAppearanceService.Options) or nameof(IAppearanceService.CurrentTheme)) Refresh();
    }
    private void OnLanguageChanged(string _) => Refresh();
    private void Refresh()
    {
        foreach (var property in new[] { nameof(Title), nameof(Hint), nameof(WeeklyText), nameof(ObsidianText),
            nameof(PorcelainText), nameof(SumiText), nameof(IbreText), nameof(AizomeText),
            nameof(ActiveText), nameof(IsWeekly), nameof(IsObsidian), nameof(IsPorcelain),
            nameof(IsSumi), nameof(IsIbre), nameof(IsAizome) })
            OnPropertyChanged(property);
        var options = _appearance.Options;
        foreach (var day in Days) day.Refresh(options.ForDay(day.Day), _loc);
    }
    public void Dispose()
    {
        _appearance.PropertyChanged -= OnAppearanceChanged;
        _loc.LanguageChanged -= OnLanguageChanged;
    }
}

public sealed record ThemeOption(BrewTheme Theme, string Name);

public partial class DayThemeViewModel(DayOfWeek day, Func<DayOfWeek, BrewTheme, Task> save) : ObservableObject
{
    private bool _refreshing;
    public DayOfWeek Day { get; } = day;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private ThemeOption? _selectedTheme;
    public IReadOnlyList<ThemeOption> Themes { get; private set; } = [];

    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
        if (!_refreshing && value is not null) _ = save(Day, value.Theme);
    }

    [RelayCommand] private Task SetTheme(string theme) => save(Day, Enum.Parse<BrewTheme>(theme));

    public void Refresh(BrewTheme theme, ILocalizationService loc)
    {
        _refreshing = true;
        try
        {
            Name = loc.CurrentCulture.DateTimeFormat.GetDayName(Day);
            Themes = Enum.GetValues<BrewTheme>().Select(value => new ThemeOption(value, loc.Get("Theme" + value))).ToArray();
            OnPropertyChanged(nameof(Themes));
            SelectedTheme = Themes.Single(value => value.Theme == theme);
        }
        finally { _refreshing = false; }
    }
}
