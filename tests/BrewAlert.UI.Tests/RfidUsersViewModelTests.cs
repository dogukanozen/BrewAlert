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

public class RfidUsersViewModelTests
{
    private readonly IRfidUserRepository _repository = Substitute.For<IRfidUserRepository>();
    private readonly IRfidReaderService _rfidReader = Substitute.For<IRfidReaderService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly ILocalizationService _loc;
    private readonly RfidUserService _userService;

    public RfidUsersViewModelTests()
    {
        _userService = new RfidUserService(_repository);
        _loc = Substitute.For<ILocalizationService>();
        _loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        _loc.CurrentLanguage.Returns("Turkish");
        _loc.CurrentCulture.Returns(CultureInfo.GetCultureInfo("tr-TR"));
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<RfidUser>());
    }

    private RfidUsersViewModel CreateVm() => new(
        _userService,
        _rfidReader,
        _navigation,
        _loc,
        NullLogger<RfidUsersViewModel>.Instance);

    [AvaloniaFact]
    public async Task BeginAdd_ScanCard_SaveUser_PersistsAndReturnsToList()
    {
        var savedList = new List<RfidUser>();
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<RfidUser>>(savedList.ToList()));
        _repository.GetByCardIdAsync("AABBCC11", Arg.Any<CancellationToken>()).Returns((RfidUser?)null);
        _repository.When(r => r.SaveAsync(Arg.Any<RfidUser>(), Arg.Any<CancellationToken>()))
            .Do(call => savedList.Add(call.Arg<RfidUser>()));

        using var vm = CreateVm();
        Assert.True(vm.IsListMode);

        vm.BeginAddCommand.Execute(null);
        Assert.True(vm.IsWaitingForCard);
        Assert.True(vm.IsAwaitingCardScan);

        _rfidReader.CardScanned += Raise.Event<EventHandler<RfidCardScannedEvent>>(
            this, new RfidCardScannedEvent("aabbcc11", DateTime.UtcNow));
        await WaitUntilAsync(() => vm.IsEnteringName);

        Assert.Equal("AABBCC11", vm.ScannedCardId);
        vm.UserNameInput = "Doğukan";

        await vm.SaveUserCommand.ExecuteAsync(null);

        Assert.True(vm.IsListMode);
        Assert.True(vm.HasUsers);
        Assert.Single(vm.Users);
        Assert.Equal("Doğukan", vm.Users[0].Name);
    }

    [AvaloniaFact]
    public async Task StartRegistrationWithCard_ExistingCard_PrefillsNameAndNotice()
    {
        var existing = new RfidUser { CardId = "CARD42", Name = "Burak" };
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([existing]);
        _repository.GetByCardIdAsync("CARD42", Arg.Any<CancellationToken>()).Returns(existing);

        using var vm = CreateVm();
        await WaitUntilAsync(() => vm.HasUsers);

        vm.StartRegistrationWithCard("card42");

        Assert.True(vm.IsEnteringName);
        Assert.Equal("CARD42", vm.ScannedCardId);
        Assert.Equal("Burak", vm.UserNameInput);
        Assert.NotEmpty(vm.ExistingCardNotice);
    }

    [AvaloniaFact]
    public async Task EditAndDeleteUser_UpdateList()
    {
        var user = new RfidUser { CardId = "998877", Name = "Selin" };
        var list = new List<RfidUser> { user };
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<RfidUser>>(list.ToList()));
        _repository.When(r => r.DeleteAsync(user.Id, Arg.Any<CancellationToken>()))
            .Do(_ => list.Clear());

        using var vm = CreateVm();
        await WaitUntilAsync(() => vm.HasUsers);

        vm.EditUserCommand.Execute(user);
        Assert.True(vm.IsEnteringName);
        Assert.Equal("Selin", vm.UserNameInput);

        vm.CancelRegistrationCommand.Execute(null);
        Assert.True(vm.IsListMode);

        await vm.DeleteUserCommand.ExecuteAsync(user);
        Assert.Empty(vm.Users);
        Assert.False(vm.HasUsers);
    }

    [AvaloniaFact]
    public async Task BeginAddWithoutCard_SavesCardlessOperator()
    {
        var savedList = new List<RfidUser>();
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<RfidUser>>(savedList.ToList()));
        _repository.When(r => r.SaveAsync(Arg.Any<RfidUser>(), Arg.Any<CancellationToken>()))
            .Do(call => savedList.Add(call.Arg<RfidUser>()));

        using var vm = CreateVm();
        vm.BeginAddWithoutCardCommand.Execute(null);

        Assert.True(vm.IsEnteringName);
        Assert.False(vm.HasScannedCard);

        vm.UserNameInput = "Canan";
        await vm.SaveUserCommand.ExecuteAsync(null);

        Assert.True(vm.IsListMode);
        var saved = Assert.Single(vm.Users);
        Assert.Equal("Canan", saved.Name);
        Assert.Null(saved.CardId);
    }

    [AvaloniaFact]
    public async Task EditCardlessOperator_AttachAndRemoveCard_PreservesOperatorId()
    {
        var cardless = new RfidUser { CardId = null, Name = "Emre" };
        var list = new List<RfidUser> { cardless };
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<RfidUser>>(list.ToList()));

        using var vm = CreateVm();
        await WaitUntilAsync(() => vm.HasUsers);

        vm.EditUserCommand.Execute(cardless);
        Assert.True(vm.IsEnteringName);
        Assert.False(vm.HasScannedCard);

        vm.ScanOrChangeCardCommand.Execute(null);
        Assert.True(vm.IsWaitingForCard);

        _rfidReader.CardScanned += Raise.Event<EventHandler<RfidCardScannedEvent>>(
            this, new RfidCardScannedEvent("55667788", DateTime.UtcNow));
        await WaitUntilAsync(() => vm.IsEnteringName);

        Assert.Equal("55667788", vm.ScannedCardId);
        Assert.Equal("Emre", vm.UserNameInput);
        await vm.SaveUserCommand.ExecuteAsync(null);
        Assert.Equal("55667788", cardless.CardId);

        vm.EditUserCommand.Execute(cardless);
        Assert.True(vm.HasScannedCard);
        vm.RemoveCardCommand.Execute(null);
        Assert.False(vm.HasScannedCard);
        await vm.SaveUserCommand.ExecuteAsync(null);
        Assert.Null(cardless.CardId);
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
