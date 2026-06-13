using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Acars.Desktop.Services;
using AirSerbiaVirtua.Contracts;
using FluentAssertions;
using Moq;

namespace AirSerbiaVirtua.Tests.Unit.Desktop;

public class DispatchServiceTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly BookingInfo Booking = new(
        7, 3, "JU360", "LYBE", "LOWW", "A319", 55, Today, BookingStatus.Open, DateTimeOffset.UtcNow);
    private static readonly AircraftInfo Airframe = new(4, "A319", "YU-API", "Active", "LYBE");
    private static readonly FlightStartResponse Start = new(12, 3, 4, "YU-API", DateTimeOffset.UtcNow);

    private Mock<IApiService> _api = null!;
    private Mock<ISessionService> _session = null!;
    private Mock<INavigationService> _navigation = null!;
    private FlightSessionState _flightState = null!;
    private SimulatorService _sim = null!;
    private DispatchService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _api = new Mock<IApiService>();
        _session = new Mock<ISessionService>();
        _session.SetupGet(s => s.Api).Returns(_api.Object);
        _session.SetupGet(s => s.IsAuthenticated).Returns(true);
        _navigation = new Mock<INavigationService>();
        _flightState = new FlightSessionState();
        _sim = new SimulatorService(new FakeSimBridge());
        _sut = new DispatchService(_session.Object, _flightState, _navigation.Object, _sim);
    }

    [TearDown]
    public void TearDown() => _sim.Dispose();

    [Test]
    public async Task Start_HappyPath_StartsFlightSetsStateAndNavigates()
    {
        _api.Setup(a => a.GetAircraftAsync("A319", "Active", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Airframe]);
        _api.Setup(a => a.StartFlightAsync(3, 4, Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Start);

        var result = await _sut.StartAsync(Booking);

        result.Success.Should().BeTrue();
        result.Registration.Should().Be("YU-API");
        _flightState.HasActiveSession.Should().BeTrue();
        _flightState.FlightSessionId.Should().Be(12);
        _flightState.FlightNumber.Should().Be("JU360");
        _navigation.Verify(n => n.NavigateTo(NavTarget.Acars), Times.Once);
    }

    [Test]
    public async Task Start_NoAirframeAvailable_FailsWithoutTouchingState()
    {
        _api.Setup(a => a.GetAircraftAsync("A319", "Active", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.StartAsync(Booking);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("A319");
        _flightState.HasActiveSession.Should().BeFalse();
        _api.Verify(a => a.StartFlightAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateOnly>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _navigation.Verify(n => n.NavigateTo(It.IsAny<NavTarget>()), Times.Never);
    }

    [Test]
    public async Task Start_ApiConflict_SurfacesMessageAndLeavesStateClean()
    {
        _api.Setup(a => a.GetAircraftAsync("A319", "Active", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Airframe]);
        _api.Setup(a => a.StartFlightAsync(3, 4, Today, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("Flight start failed: 409 Conflict. aircraft in flight",
                System.Net.HttpStatusCode.Conflict));

        var result = await _sut.StartAsync(Booking);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("409");
        _flightState.HasActiveSession.Should().BeFalse();
        _navigation.Verify(n => n.NavigateTo(It.IsAny<NavTarget>()), Times.Never);
    }

    [Test]
    public async Task Start_SessionAlreadyActive_RefusesImmediately()
    {
        _flightState.Set(Start, "JU999");

        var result = await _sut.StartAsync(Booking);

        result.Success.Should().BeFalse();
        _api.Verify(a => a.GetAircraftAsync(It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Start_NotAuthenticated_Refuses()
    {
        _session.SetupGet(s => s.IsAuthenticated).Returns(false);

        var result = await _sut.StartAsync(Booking);

        result.Success.Should().BeFalse();
    }
}
