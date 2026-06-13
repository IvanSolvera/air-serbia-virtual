using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Contracts;
using FluentAssertions;

namespace AirSerbiaVirtua.Tests.Unit.Core;

public class PositionReportsTests
{
    [Test]
    public void FromTelemetry_MapsFieldsAndAssignsFreshReportId()
    {
        var sample = new FlightData
        {
            SampleTimeUtc = new DateTimeOffset(2026, 6, 6, 12, 0, 0, TimeSpan.Zero),
            LatitudeDeg = 44.818,
            LongitudeDeg = 20.309,
            AltitudeFt = 35012.4,
            GroundSpeedKts = 446.7
        };

        var report = PositionReports.FromTelemetry(sample, FlightState.Cruise);

        report.ClientReportId.Should().NotBeEmpty();
        report.Timestamp.Should().Be(sample.SampleTimeUtc);
        report.Lat.Should().Be(44.818);
        report.Lon.Should().Be(20.309);
        report.AltFt.Should().Be(35012);
        report.GsKts.Should().Be(447);
        report.Phase.Should().Be(FlightPhase.Cruise);
    }

    [Test]
    public void FromTelemetry_TwoCalls_GetDistinctReportIds()
    {
        var sample = new FlightData { SampleTimeUtc = DateTimeOffset.UtcNow };
        PositionReports.FromTelemetry(sample, FlightState.Taxi).ClientReportId
            .Should().NotBe(PositionReports.FromTelemetry(sample, FlightState.Taxi).ClientReportId);
    }

    [TestCase(FlightState.Boarding, FlightPhase.Preflight)]
    [TestCase(FlightState.Arrived, FlightPhase.Shutdown)]
    [TestCase(FlightState.Landing, FlightPhase.Landing)]
    public void PhaseMapping_CoversClientOnlyStates(FlightState state, FlightPhase expected)
    {
        state.ToApiPhase().Should().Be(expected);
    }
}
