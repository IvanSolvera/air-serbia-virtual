using System.Net.Http;
using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Acars.Desktop.Services;
using AirSerbiaVirtua.Acars.Desktop.ViewModels;
using AirSerbiaVirtua.Contracts;
using FluentAssertions;
using Moq;

namespace AirSerbiaVirtua.Tests.Unit.Desktop;

public class PilotCentreDispatchTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static BookingInfo Booking(int id, DateOnly date, DateTimeOffset? ready) => new(
        id, 3, $"JU{id:000}", "LYBE", "LOWW", "A319", 55, date, BookingStatus.Open, ready);

    private Mock<IApiService> _api = null!;
    private Mock<ISessionService> _session = null!;
    private FlightSessionState _flightState = null!;
    private PilotCentreViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _api = new Mock<IApiService>();
        _session = new Mock<ISessionService>();
        _session.SetupGet(s => s.Api).Returns(_api.Object);
        _session.SetupGet(s => s.IsAuthenticated).Returns(true);
        _session.SetupGet(s => s.Pilot).Returns((PilotProfile?)null);
        _flightState = new FlightSessionState();

        var sim = new SimulatorService(new FakeSimBridge());
        var dispatch = new DispatchService(
            _session.Object, _flightState, new Mock<INavigationService>().Object, sim);
        _vm = new PilotCentreViewModel(_session.Object, _flightState, dispatch);

        // The ctor eagerly fires one RefreshDispatchAsync (IsAuthenticated is true
        // here). That construction-time call is not what these tests assert on, so
        // forget recorded invocations — Setups are preserved.
        _api.Invocations.Clear();
    }

    [Test]
    public async Task Refresh_ReadyBookingForToday_ShowsCard()
    {
        _api.Setup(a => a.GetActiveBookingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Booking(7, Today, Now)]);

        await _vm.RefreshDispatchAsync();

        _vm.HasDispatch.Should().BeTrue();
        _vm.DispatchTitle.Should().Contain("JU007").And.Contain("LYBE").And.Contain("LOWW");
        _vm.DispatchSub.Should().Contain("A319");
    }

    [Test]
    public async Task Refresh_NewestPreparedWins_WhenSeveralReadyToday()
    {
        _api.Setup(a => a.GetActiveBookingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Booking(1, Today, Now.AddMinutes(-30)),
                Booking(2, Today, Now)
            ]);

        await _vm.RefreshDispatchAsync();

        _vm.DispatchTitle.Should().Contain("JU002");
    }

    [Test]
    public async Task Refresh_ReadyBookingForTomorrow_NoCard()
    {
        _api.Setup(a => a.GetActiveBookingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Booking(7, Today.AddDays(1), Now)]);

        await _vm.RefreshDispatchAsync();

        _vm.HasDispatch.Should().BeFalse();
    }

    [Test]
    public async Task Refresh_UnpreparedBookingToday_NoCard()
    {
        _api.Setup(a => a.GetActiveBookingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Booking(7, Today, null)]);

        await _vm.RefreshDispatchAsync();

        _vm.HasDispatch.Should().BeFalse();
    }

    [Test]
    public async Task Refresh_ActiveFlightSession_NoCard()
    {
        _flightState.Set(new FlightStartResponse(12, 3, 4, "YU-API", Now), "JU360");
        _api.Setup(a => a.GetActiveBookingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Booking(7, Today, Now)]);

        await _vm.RefreshDispatchAsync();

        _vm.HasDispatch.Should().BeFalse();
    }

    [Test]
    public async Task Refresh_ApiFailure_NoCardAndNoThrow()
    {
        _api.Setup(a => a.GetActiveBookingsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"));

        await _vm.RefreshDispatchAsync();

        _vm.HasDispatch.Should().BeFalse();
    }

    [Test]
    public async Task Refresh_NotAuthenticated_NoCardAndNoApiCall()
    {
        _session.SetupGet(s => s.IsAuthenticated).Returns(false);

        await _vm.RefreshDispatchAsync();

        _vm.HasDispatch.Should().BeFalse();
        _api.Verify(a => a.GetActiveBookingsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
