using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.Infrastructure.Configuration;
using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
    private readonly IRfidReaderService _rfidReader = Substitute.For<IRfidReaderService>();
    private readonly IRfidUserRepository _rfidUserRepo = Substitute.For<IRfidUserRepository>();
    private readonly RfidUserService _rfidUserService;
    private readonly IOptionsMonitor<RfidOptions> _rfidOptions = Substitute.For<IOptionsMonitor<RfidOptions>>();

    public ProfileListViewModelTests()
    {
        _profileService = new BrewProfileService(_repository);
        _rfidUserService = new RfidUserService(_rfidUserRepo);
        _rfidOptions.CurrentValue.Returns(new RfidOptions { RequireCardToBrew = false });
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
        _rfidReader,
        _rfidUserService,
        _rfidOptions,
        NullLogger<ProfileListViewModel>.Instance);

    [AvaloniaFact]
    public void LoadProfiles_WhenRepositoryFails_ShowsTheError()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<BrewProfile>>(new IOException("disk unavailable")));

        var vm = CreateVm();

        Assert.False(vm.IsLoading);
        Assert.Empty(vm.Profiles);
        Assert.Equal("ProfilesLoadError", vm.ProfilesErrorText);
    }

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
    public void SelectProfile_WhenCardNotRequired_StartsBrewAndNavigatesToActiveBrews()
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
    public void SelectProfile_WhenCardRequired_PromptsForCardInsteadOfStartingImmediately()
    {
        _rfidOptions.CurrentValue.Returns(new RfidOptions { RequireCardToBrew = true });
        var tea = new BrewProfile { Name = "Çay", BrewDuration = TimeSpan.FromMinutes(10) };

        using var vm = CreateVm();
        vm.SelectProfileCommand.Execute(tea);

        Assert.True(vm.IsAwaitingCardScan);
        Assert.Same(tea, vm.PendingProfile);
        _timerService.DidNotReceiveWithAnyArgs().Start(default!);
        _navigation.DidNotReceive().NavigateTo<ActiveBrewsViewModel>();
    }

    [AvaloniaFact]
    public async Task CardScanned_WithConfiguredUserAndPendingProfile_StartsPersonalizedTimerAndNavigates()
    {
        _rfidOptions.CurrentValue.Returns(new RfidOptions { RequireCardToBrew = true });
        var tea = new BrewProfile { Id = Guid.NewGuid(), Name = "Çay", BrewDuration = TimeSpan.FromMinutes(15) };
        _rfidUserRepo.GetByCardIdAsync("001234", Arg.Any<CancellationToken>())
            .Returns(new RfidUser
            {
                CardId = "001234",
                Name = "Ahmet",
                HasConfiguredProfiles = true,
                ProfileDurationMinutes = new Dictionary<Guid, int> { [tea.Id] = 20 },
            });
        _timerService.Start(Arg.Any<BrewProfile>(), "Ahmet", "001234")
            .Returns(new BrewSession { Profile = tea, BrewerName = "Ahmet", BrewerCardId = "001234" });

        using var vm = CreateVm();
        vm.SelectProfileCommand.Execute(tea);

        _rfidReader.CardScanned += Raise.Event<EventHandler<RfidCardScannedEvent>>(
            this, new RfidCardScannedEvent("001234", DateTime.UtcNow));

        await WaitUntilAsync(() => !vm.IsAwaitingCardScan);

        _timerService.Received(1).Start(
            Arg.Is<BrewProfile>(p => p.Id == tea.Id && p.BrewDuration == TimeSpan.FromMinutes(20)),
            "Ahmet",
            "001234");
        _navigation.Received(1).NavigateTo<ActiveBrewsViewModel>();
    }

    [AvaloniaFact]
    public async Task CardScanned_WithUnconfiguredUserAndPendingProfile_NavigatesToOperatorBrewView()
    {
        _rfidOptions.CurrentValue.Returns(new RfidOptions { RequireCardToBrew = true });
        var tea = new BrewProfile { Id = Guid.NewGuid(), Name = "Çay", BrewDuration = TimeSpan.FromMinutes(15) };
        var user = new RfidUser { CardId = "001234", Name = "Ahmet", HasConfiguredProfiles = false };
        _rfidUserRepo.GetByCardIdAsync("001234", Arg.Any<CancellationToken>()).Returns(user);

        using var vm = CreateVm();
        vm.SelectProfileCommand.Execute(tea);

        _rfidReader.CardScanned += Raise.Event<EventHandler<RfidCardScannedEvent>>(
            this, new RfidCardScannedEvent("001234", DateTime.UtcNow));

        await WaitUntilAsync(() => !vm.IsAwaitingCardScan);

        _timerService.DidNotReceiveWithAnyArgs().Start(default!, default, default);
        _navigation.Received(1).NavigateTo<OperatorBrewViewModel>(Arg.Any<Action<OperatorBrewViewModel>>());
    }

    [AvaloniaFact]
    public async Task DirectCardScanOnHome_WhenCardRequired_NavigatesToOperatorBrewView()
    {
        _rfidOptions.CurrentValue.Returns(new RfidOptions { RequireCardToBrew = true });
        var user = new RfidUser { CardId = "ABC123", Name = "Deniz", HasConfiguredProfiles = true };
        _rfidUserRepo.GetByCardIdAsync("ABC123", Arg.Any<CancellationToken>()).Returns(user);

        using var vm = CreateVm();
        Assert.False(vm.IsAwaitingCardScan);
        Assert.True(((IRfidCardScanAwaiter)vm).CanAcceptKeyboardCardScan);

        _rfidReader.CardScanned += Raise.Event<EventHandler<RfidCardScannedEvent>>(
            this, new RfidCardScannedEvent("abc123", DateTime.UtcNow));

        await WaitUntilAsync(() => _navigation.ReceivedCalls().Any());

        _navigation.Received(1).NavigateTo<OperatorBrewViewModel>(Arg.Any<Action<OperatorBrewViewModel>>());
    }

    [AvaloniaFact]
    public async Task DirectCardScanOnHome_WhenUnknownCard_OpensRegisterPrompt()
    {
        _rfidOptions.CurrentValue.Returns(new RfidOptions { RequireCardToBrew = true });
        _loc.Get("RfidUnknownCard").Returns("Unregistered card ({0})");
        _rfidUserRepo.GetByCardIdAsync("UNK99", Arg.Any<CancellationToken>()).Returns((RfidUser?)null);

        using var vm = CreateVm();
        _rfidReader.CardScanned += Raise.Event<EventHandler<RfidCardScannedEvent>>(
            this, new RfidCardScannedEvent("unk99", DateTime.UtcNow));

        await WaitUntilAsync(() => vm.HasUnregisteredCard);

        Assert.True(vm.IsAwaitingCardScan);
        Assert.True(vm.IsCardScanStep);
        Assert.Equal("UNK99", vm.UnregisteredCardId);
    }

    [AvaloniaFact]
    public async Task DirectCardScanOnHome_WhenCardNotRequired_IsIgnored()
    {
        _rfidOptions.CurrentValue.Returns(new RfidOptions { RequireCardToBrew = false });
        using var vm = CreateVm();
        Assert.False(((IRfidCardScanAwaiter)vm).CanAcceptKeyboardCardScan);

        _rfidReader.CardScanned += Raise.Event<EventHandler<RfidCardScannedEvent>>(
            this, new RfidCardScannedEvent("ABC123", DateTime.UtcNow));

        await Dispatcher.UIThread.InvokeAsync(() => { });
        _navigation.DidNotReceive().NavigateTo<OperatorBrewViewModel>(Arg.Any<Action<OperatorBrewViewModel>>());
    }

    [AvaloniaFact]
    public async Task OpenOperatorSignIn_LoadsOperatorsAndNavigatesOnSelection()
    {
        _rfidOptions.CurrentValue.Returns(new RfidOptions { RequireCardToBrew = true });
        var operatorUser = new RfidUser { CardId = null, Name = "Deniz", HasConfiguredProfiles = false };
        _rfidUserRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([operatorUser]);

        using var vm = CreateVm();
        await vm.OpenOperatorSignInCommand.ExecuteAsync(null);

        Assert.True(vm.IsAwaitingCardScan);
        Assert.True(vm.IsChoosingBrewer);
        Assert.Null(vm.PendingProfile);

        vm.SelectBrewerCommand.Execute(operatorUser);

        Assert.False(vm.IsAwaitingCardScan);
        _navigation.Received(1).NavigateTo<OperatorBrewViewModel>(Arg.Any<Action<OperatorBrewViewModel>>());
    }

    [AvaloniaFact]
    public async Task CardScanned_WithUnknownCard_ShowsErrorAndAllowsQuickRegistration()
    {
        _rfidOptions.CurrentValue.Returns(new RfidOptions { RequireCardToBrew = true });
        _loc.Get("RfidUnknownCard").Returns("Unregistered card ({0})");
        var coffee = new BrewProfile { Name = "Coffee", BrewDuration = TimeSpan.FromMinutes(4) };
        _rfidUserRepo.GetByCardIdAsync("NEW999", Arg.Any<CancellationToken>()).Returns((RfidUser?)null);

        using var vm = CreateVm();
        vm.SelectProfileCommand.Execute(coffee);

        _rfidReader.CardScanned += Raise.Event<EventHandler<RfidCardScannedEvent>>(
            this, new RfidCardScannedEvent("new999", DateTime.UtcNow));

        await WaitUntilAsync(() => vm.HasUnregisteredCard);

        Assert.True(vm.IsAwaitingCardScan);
        Assert.Equal("NEW999", vm.UnregisteredCardId);
        Assert.Contains("NEW999", vm.ScanErrorMessage);

        vm.RegisterUnregisteredCardCommand.Execute(null);

        Assert.False(vm.IsAwaitingCardScan);
        _navigation.Received(1).NavigateTo<RfidUsersViewModel>(Arg.Any<Action<RfidUsersViewModel>>());
    }

    [AvaloniaFact]
    public void CancelCardScan_DismissesPrompt()
    {
        _rfidOptions.CurrentValue.Returns(new RfidOptions { RequireCardToBrew = true });
        var tea = new BrewProfile { Name = "Tea", BrewDuration = TimeSpan.FromMinutes(5) };

        using var vm = CreateVm();
        vm.SelectProfileCommand.Execute(tea);
        Assert.True(vm.IsAwaitingCardScan);

        vm.CancelCardScanCommand.Execute(null);

        Assert.False(vm.IsAwaitingCardScan);
        Assert.Null(vm.PendingProfile);
    }

    [AvaloniaFact]
    public async Task ChooseBrewerWithoutCard_LoadsOperatorsAndStartsBrewForSelectedBrewer()
    {
        _rfidOptions.CurrentValue.Returns(new RfidOptions { RequireCardToBrew = true });
        var tea = new BrewProfile { Id = Guid.NewGuid(), Name = "Çay", BrewDuration = TimeSpan.FromMinutes(10) };
        var cardless = new RfidUser
        {
            CardId = null,
            Name = "Deniz",
            HasConfiguredProfiles = true,
            ProfileDurationMinutes = new Dictionary<Guid, int> { [tea.Id] = 18 },
        };
        var carded = new RfidUser { CardId = "112233", Name = "Ahmet", HasConfiguredProfiles = true };
        _rfidUserRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([carded, cardless]);
        _timerService.Start(Arg.Any<BrewProfile>(), "Deniz", null).Returns(new BrewSession { Profile = tea, BrewerName = "Deniz" });

        using var vm = CreateVm();
        vm.SelectProfileCommand.Execute(tea);
        Assert.True(vm.IsCardScanStep);

        await vm.ChooseBrewerWithoutCardCommand.ExecuteAsync(null);

        Assert.True(vm.IsChoosingBrewer);
        Assert.False(vm.IsCardScanStep);
        Assert.True(vm.HasAvailableBrewers);
        Assert.Equal(2, vm.AvailableBrewers.Count);

        vm.SelectBrewerCommand.Execute(cardless);

        _timerService.Received(1).Start(
            Arg.Is<BrewProfile>(p => p.Id == tea.Id && p.BrewDuration == TimeSpan.FromMinutes(18)),
            "Deniz",
            null);
        _navigation.Received(1).NavigateTo<ActiveBrewsViewModel>();
        Assert.False(vm.IsAwaitingCardScan);
    }

    [AvaloniaFact]
    public async Task WhoIsBrewing_BackToCardScanAndOpenAddOperator_WorkAsExpected()
    {
        _rfidOptions.CurrentValue.Returns(new RfidOptions { RequireCardToBrew = true });
        _rfidUserRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<RfidUser>());
        var coffee = new BrewProfile { Name = "Kahve", BrewDuration = TimeSpan.FromMinutes(4) };

        using var vm = CreateVm();
        vm.SelectProfileCommand.Execute(coffee);
        await vm.ChooseBrewerWithoutCardCommand.ExecuteAsync(null);

        Assert.True(vm.IsChoosingBrewer);
        Assert.False(vm.HasAvailableBrewers);

        vm.BackToCardScanCommand.Execute(null);
        Assert.False(vm.IsChoosingBrewer);
        Assert.True(vm.IsCardScanStep);

        await vm.ChooseBrewerWithoutCardCommand.ExecuteAsync(null);
        vm.OpenAddOperatorCommand.Execute(null);

        Assert.False(vm.IsAwaitingCardScan);
        _navigation.Received(1).NavigateTo<RfidUsersViewModel>(Arg.Any<Action<RfidUsersViewModel>>());
    }

    [AvaloniaFact]
    public void Dispose_UnsubscribesFromRfidReaderCardScanned()
    {
        var vm = CreateVm();
        vm.Dispose();

        _rfidReader.Received(1).CardScanned -= Arg.Any<EventHandler<RfidCardScannedEvent>>();
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
            Guid.NewGuid(), DateTime.UtcNow, "Coffee", BrewType.Coffee, "☕", 240, "Zeynep"));

        _history.HistoryUpdated += Raise.Event<EventHandler<BrewHistoryEntry>>(this, entries[0]);

        await WaitUntilAsync(() => vm.RecentBrews.Count == 1);
        Assert.True(vm.HasRecentBrews);
        Assert.Equal("Coffee", vm.RecentBrews[0].Name);
        Assert.Equal("4 min", vm.RecentBrews[0].DurationText);
        Assert.Equal("Zeynep", vm.RecentBrews[0].BrewerName);
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

    [AvaloniaFact]
    public async Task CancelledCardLookup_DiscardsLateResult_WhileSubsequentDirectScanWorks()
    {
        _rfidOptions.CurrentValue.Returns(new RfidOptions { RequireCardToBrew = true });
        var tea = new BrewProfile { Id = Guid.NewGuid(), Name = "Çay", BrewDuration = TimeSpan.FromMinutes(15) };
        var alice = new RfidUser { CardId = "ALICE1", Name = "Ayşe", HasConfiguredProfiles = true };
        var bob = new RfidUser { CardId = "BOB2", Name = "Burak", HasConfiguredProfiles = true };

        var firstLookup = new TaskCompletionSource<RfidUser?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _rfidUserRepo.GetByCardIdAsync("ALICE1", Arg.Any<CancellationToken>()).Returns(_ => firstLookup.Task);
        _rfidUserRepo.GetByCardIdAsync("BOB2", Arg.Any<CancellationToken>()).Returns(bob);

        using var vm = CreateVm();
        vm.SelectProfileCommand.Execute(tea);
        Assert.True(vm.IsAwaitingCardScan);

        _rfidReader.CardScanned += Raise.Event<EventHandler<RfidCardScannedEvent>>(
            this, new RfidCardScannedEvent("ALICE1", DateTime.UtcNow));
        await WaitUntilAsync(() => _rfidUserRepo.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IRfidUserRepository.GetByCardIdAsync)));

        // User cancels while lookup is in-flight.
        vm.CancelCardScanCommand.Execute(null);
        Assert.False(vm.IsAwaitingCardScan);

        firstLookup.SetResult(alice);
        await Dispatcher.UIThread.InvokeAsync(() => { });

        _timerService.DidNotReceiveWithAnyArgs().Start(default!, default, default);
        _navigation.DidNotReceive().NavigateTo<OperatorBrewViewModel>(Arg.Any<Action<OperatorBrewViewModel>>());

        // Subsequent direct card scan on Home still works.
        _rfidReader.CardScanned += Raise.Event<EventHandler<RfidCardScannedEvent>>(
            this, new RfidCardScannedEvent("BOB2", DateTime.UtcNow));
        await WaitUntilAsync(() => _navigation.ReceivedCalls().Any());

        _navigation.Received(1).NavigateTo<OperatorBrewViewModel>(Arg.Any<Action<OperatorBrewViewModel>>());
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
