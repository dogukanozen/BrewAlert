using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BrewAlert.UI.Tests;

public class ProfileListViewModelTests
{
    private readonly BrewProfileService _profileService;
    private readonly IProfileRepository _repository = Substitute.For<IProfileRepository>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IBrewTimerService _timerService = Substitute.For<IBrewTimerService>();
    private readonly ILocalizationService _loc;
    private readonly IBrewHistoryService _history = Substitute.For<IBrewHistoryService>();

    public ProfileListViewModelTests()
    {
        _profileService = new BrewProfileService(_repository);
        _loc = Substitute.For<ILocalizationService>();
        _loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        _loc.CurrentLanguage.Returns("English");
        _history.GetRecentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<BrewHistoryEntry>>(Array.Empty<BrewHistoryEntry>()));
    }

    private ProfileListViewModel CreateVm() => new(
        _profileService,
        _navigation,
        _loc,
        _history,
        _timerService,
        NullLogger<ProfileListViewModel>.Instance);

    [AvaloniaFact]
    public async Task LoadProfiles_PopulatesCollection()
    {
        var profiles = BrewProfileService.DefaultProfiles.ToList();
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(profiles);

        var tcs = new TaskCompletionSource();
        var vm = CreateVm();

        if (!vm.IsLoading)
            tcs.SetResult();
        else
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(vm.IsLoading) && !vm.IsLoading)
                    tcs.TrySetResult();
            };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await tcs.Task.WaitAsync(cts.Token);

        Assert.Equal(BrewProfileService.DefaultProfiles.Count, vm.Profiles.Count);
    }

    [AvaloniaFact]
    public void SelectProfile_StartsBrewAndNavigatesToActiveBrews()
    {
        var profile = new BrewProfile { Name = "Tea", BrewDuration = TimeSpan.FromMinutes(3) };
        _timerService.Start(Arg.Any<BrewProfile>()).Returns(new BrewSession { Profile = profile });

        var vm = CreateVm();
        vm.SelectProfileCommand.Execute(profile);

        _timerService.Received(1).Start(profile);
        _navigation.Received(1).NavigateTo<ActiveBrewsViewModel>();
    }

    [AvaloniaFact]
    public void SelectProfile_AlwaysStartsNewSession_EvenIfOthersActive()
    {
        // With multi-session support, selecting a profile always starts a new brew
        // even when other brews are running — the user explicitly asked for another.
        var existing = new BrewSession
        {
            Profile = new BrewProfile { Name = "Coffee", BrewDuration = TimeSpan.FromMinutes(4) },
            State = BrewSessionState.Running
        };
        _timerService.GetActiveSessions().Returns([existing]);

        var tea = new BrewProfile { Name = "Tea", BrewDuration = TimeSpan.FromMinutes(3) };
        _timerService.Start(tea).Returns(new BrewSession { Profile = tea });

        var vm = CreateVm();
        vm.SelectProfileCommand.Execute(tea);

        _timerService.Received(1).Start(tea);
        _navigation.Received(1).NavigateTo<ActiveBrewsViewModel>();
    }

    [AvaloniaFact]
    public async Task HistoryUpdated_RefreshesRecentBrewsWithoutNavigation()
    {
        var entries = new List<BrewHistoryEntry>();
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<BrewProfile>());
        _loc.Get("MinShort").Returns("min");
        _history.GetRecentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<BrewHistoryEntry>>(entries.ToList()));

        using var vm = CreateVm();

        entries.Add(new BrewHistoryEntry(
            Guid.NewGuid(), DateTime.UtcNow, "Coffee", BrewType.Coffee, "☕", 240));

        _history.HistoryUpdated += Raise.Event<EventHandler<BrewHistoryEntry>>(this, entries[0]);

        await WaitUntilAsync(() => vm.RecentBrews.Count == 1);
        Assert.True(vm.HasRecentBrews);
        Assert.Equal("Coffee", vm.RecentBrews[0].Name);
        Assert.Equal("4 min", vm.RecentBrews[0].DurationText);
    }

    [AvaloniaFact]
    public async Task Dispose_PreventsPendingRecentBrewLoadFromUpdatingCollection()
    {
        var loadCompletion = new TaskCompletionSource<IReadOnlyList<BrewHistoryEntry>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<BrewProfile>());
        _history.GetRecentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => loadCompletion.Task);

        var vm = CreateVm();

        vm.Dispose();
        loadCompletion.SetResult([
            new BrewHistoryEntry(Guid.NewGuid(), DateTime.UtcNow, "Coffee", BrewType.Coffee, "☕", 240)
        ]);

        await Dispatcher.UIThread.InvokeAsync(() => { });

        Assert.Empty(vm.RecentBrews);
        Assert.False(vm.HasRecentBrews);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Dispatcher.UIThread.InvokeAsync(() => { });
            await Task.Delay(10, cts.Token);
        }
    }
}
