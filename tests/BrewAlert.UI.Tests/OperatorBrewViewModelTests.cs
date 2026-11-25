using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BrewAlert.UI.Tests;

public class OperatorBrewViewModelTests
{
    private readonly IProfileRepository _profileRepo = Substitute.For<IProfileRepository>();
    private readonly IRfidUserRepository _userRepo = Substitute.For<IRfidUserRepository>();
    private readonly IBrewTimerService _timerService = Substitute.For<IBrewTimerService>();
    private readonly IRfidReaderService _rfidReader = Substitute.For<IRfidReaderService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly ILocalizationService _loc = Substitute.For<ILocalizationService>();
    private readonly BrewProfileService _profileService;
    private readonly RfidUserService _userService;
    private readonly List<BrewProfile> _profiles;

    public OperatorBrewViewModelTests()
    {
        _profiles = BrewProfileService.DefaultProfiles.ToList();
        _profileRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<BrewProfile>>(_profiles.ToList()));
        _profileService = new BrewProfileService(_profileRepo);
        _userService = new RfidUserService(_userRepo);

        _loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        _loc.Get("MinShort").Returns("dk");
        _loc.Get("OperatorGreetingTitle").Returns("Selam, {0}");
        _loc.Get("OperatorDefaultDurationHint").Returns("Varsayılan: {0} dk");
        _loc.CurrentLanguage.Returns("Turkish");
        _loc.CurrentCulture.Returns(CultureInfo.GetCultureInfo("tr-TR"));
    }

    private OperatorBrewViewModel CreateVm() => new(
        _profileService,
        _userService,
        _timerService,
        _rfidReader,
        _navigation,
        _loc,
        NullLogger<OperatorBrewViewModel>.Instance);

    [AvaloniaFact]
    public void Initialize_UnconfiguredUser_OpensSetupPromptWithDefaultPreviewPills()
    {
        var user = new RfidUser { CardId = "CARD01", Name = "Doğukan", HasConfiguredProfiles = false };

        using var vm = CreateVm();
        vm.Initialize(user);

        Assert.True(vm.IsSetupPrompt);
        Assert.False(vm.IsBrewSelection);
        Assert.False(vm.IsEditingDurations);
        Assert.Equal("Selam, Doğukan", vm.GreetingTitle);
        Assert.Equal(2, vm.DefaultPreviewPills.Count);
        Assert.Contains("Çay · 15 dk", vm.DefaultPreviewPills);
        Assert.Contains("Filtre Kahve · 4 dk", vm.DefaultPreviewPills);
    }

    [AvaloniaFact]
    public async Task UseDefaultsCommand_PersistsAcceptanceAndShowsBrewSelection_OrStartsPendingBrew()
    {
        var user = new RfidUser { CardId = "CARD01", Name = "Doğukan", HasConfiguredProfiles = false };
        _userRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([user]);

        using var vm = CreateVm();
        vm.Initialize(user);

        await vm.UseDefaultsCommand.ExecuteAsync(null);

        Assert.True(user.HasConfiguredProfiles);
        Assert.Empty(user.ProfileDurationMinutes);
        Assert.True(vm.IsBrewSelection);
        Assert.Equal(2, vm.ProfileChoices.Count);
        Assert.All(vm.ProfileChoices, c => Assert.False(c.IsCustomDuration));

        // With pending profile: starts brew immediately after accepting defaults
        var unconfigured2 = new RfidUser { CardId = "CARD02", Name = "Elif", HasConfiguredProfiles = false };
        _userRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([unconfigured2]);
        var tea = _profiles[0];
        _timerService.Start(Arg.Any<BrewProfile>(), "Elif", "CARD02")
            .Returns(new BrewSession { Profile = tea, BrewerName = "Elif", BrewerCardId = "CARD02" });

        using var vmPending = CreateVm();
        vmPending.Initialize(unconfigured2, tea);
        await vmPending.UseDefaultsCommand.ExecuteAsync(null);

        _timerService.Received(1).Start(
            Arg.Is<BrewProfile>(p => p.Id == tea.Id && p.BrewDuration == TimeSpan.FromMinutes(15)),
            "Elif",
            "CARD02");
        _navigation.Received(1).NavigateTo<ActiveBrewsViewModel>();
    }

    [AvaloniaFact]
    public async Task EditAndSaveDurations_PersistsOnlyCustomOverrides_AndUpdatesCards()
    {
        var tea = _profiles[0];
        var coffee = _profiles[1];
        var user = new RfidUser { CardId = "CARD10", Name = "Ahmet", HasConfiguredProfiles = false };
        _userRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([user]);

        using var vm = CreateVm();
        vm.Initialize(user);

        vm.OpenDurationEditorCommand.Execute(null);
        Assert.True(vm.IsEditingDurations);
        Assert.Equal(2, vm.EditableDurations.Count);

        var teaItem = vm.EditableDurations.Single(i => i.ProfileId == tea.Id);
        var coffeeItem = vm.EditableDurations.Single(i => i.ProfileId == coffee.Id);
        for (var i = 0; i < 5; i++) teaItem.IncreaseCommand.Execute(null);
        for (var i = 0; i < 6; i++) coffeeItem.IncreaseCommand.Execute(null);

        await vm.SaveDurationsCommand.ExecuteAsync(null);

        Assert.True(vm.IsBrewSelection);
        Assert.True(user.HasConfiguredProfiles);
        Assert.Equal(20, user.ProfileDurationMinutes[tea.Id]);
        Assert.Equal(10, user.ProfileDurationMinutes[coffee.Id]);

        var teaChoice = vm.ProfileChoices.Single(c => c.SourceProfile.Id == tea.Id);
        var coffeeChoice = vm.ProfileChoices.Single(c => c.SourceProfile.Id == coffee.Id);
        Assert.True(teaChoice.IsCustomDuration);
        Assert.Equal("20 dk", teaChoice.DurationDisplay);
        Assert.Equal("Varsayılan: 15 dk", teaChoice.DefaultHintText);
        Assert.True(coffeeChoice.IsCustomDuration);
        Assert.Equal("10 dk", coffeeChoice.DurationDisplay);
    }

    [AvaloniaFact]
    public void SelectProfile_StartsPersonalizedBrewWithoutMutatingGlobalProfile()
    {
        var tea = _profiles[0];
        var user = new RfidUser
        {
            CardId = "CARD20",
            Name = "Deniz",
            HasConfiguredProfiles = true,
            ProfileDurationMinutes = new Dictionary<Guid, int> { [tea.Id] = 20 },
        };
        _timerService.Start(Arg.Any<BrewProfile>(), "Deniz", "CARD20")
            .Returns(new BrewSession { Profile = tea, BrewerName = "Deniz", BrewerCardId = "CARD20" });

        using var vm = CreateVm();
        vm.Initialize(user);

        Assert.True(vm.IsBrewSelection);
        var teaChoice = vm.ProfileChoices.Single(c => c.SourceProfile.Id == tea.Id);
        vm.SelectProfileCommand.Execute(teaChoice);

        _timerService.Received(1).Start(
            Arg.Is<BrewProfile>(p => p.Id == tea.Id && p.BrewDuration == TimeSpan.FromMinutes(20)),
            "Deniz",
            "CARD20");
        Assert.Equal(TimeSpan.FromMinutes(15), tea.BrewDuration);
        _navigation.Received(1).NavigateTo<ActiveBrewsViewModel>();
    }

    [AvaloniaFact]
    public async Task CardScanned_SwitchesOperatorOnGreetingScreen_AndIgnoresDuringEditing()
    {
        var alice = new RfidUser { CardId = "ALICE1", Name = "Ayşe", HasConfiguredProfiles = true };
        var bob = new RfidUser { CardId = "BOB2", Name = "Burak", HasConfiguredProfiles = false };
        _userRepo.GetByCardIdAsync("BOB2", Arg.Any<CancellationToken>()).Returns(bob);

        using var vm = CreateVm();
        vm.Initialize(alice);
        Assert.True(vm.IsBrewSelection);
        Assert.Equal("Ayşe", vm.OperatorName);

        // Ignored while editing durations
        vm.OpenDurationEditorCommand.Execute(null);
        Assert.True(vm.IsEditingDurations);
        _rfidReader.CardScanned += Raise.Event<EventHandler<RfidCardScannedEvent>>(
            this, new RfidCardScannedEvent("bob2", DateTime.UtcNow));
        await Dispatcher.UIThread.InvokeAsync(() => { });
        Assert.Equal("Ayşe", vm.OperatorName);
        Assert.True(vm.IsEditingDurations);

        // Cancel back to selection, then scan switches operator
        vm.CancelDurationEditorCommand.Execute(null);
        _rfidReader.CardScanned += Raise.Event<EventHandler<RfidCardScannedEvent>>(
            this, new RfidCardScannedEvent("bob2", DateTime.UtcNow));
        await WaitUntilAsync(() => vm.OperatorName == "Burak");

        Assert.True(vm.IsSetupPrompt);
    }

    [AvaloniaFact]
    public void Dispose_UnsubscribesFromEvents()
    {
        var vm = CreateVm();
        vm.Dispose();

        _rfidReader.Received(1).CardScanned -= Arg.Any<EventHandler<RfidCardScannedEvent>>();
        _loc.Received(1).LanguageChanged -= Arg.Any<Action<string>>();
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
