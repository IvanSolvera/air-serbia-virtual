using AirSerbiaVirtua.Contracts;
using FluentAssertions;

namespace AirSerbiaVirtua.Tests.Unit.Contracts;

public class FuelEstimatorTests
{
    // burn/nm: A319=7.0, A320=7.4, A321=8.2, ATR72=2.6, E195=5.6, default=6.5
    // reserve = round(burn * 360 * 0.75) + 400

    [TestCase("A319", 300, 2100, 2290)]
    [TestCase("A320", 300, 2220, 2398)]
    [TestCase("A321", 100, 820, 2614)]
    [TestCase("ATR72", 200, 520, 1102)]
    [TestCase("E195", 100, 560, 1912)]
    [TestCase("B738", 100, 650, 2155)]    // unknown type -> default burn
    public void Estimate_ComputesTripAndReserve(string type, int nm, int expTrip, int expReserve)
    {
        var (trip, reserve) = FuelEstimator.Estimate(type, nm);
        trip.Should().Be(expTrip);
        reserve.Should().Be(expReserve);
    }

    [Test]
    public void Estimate_IsCaseInsensitive()
    {
        FuelEstimator.Estimate("a319", 300).Should().Be(FuelEstimator.Estimate("A319", 300));
    }

    [Test]
    public void Estimate_ZeroDistance_HasZeroTripButFullReserve()
    {
        var (trip, reserve) = FuelEstimator.Estimate("A319", 0);
        trip.Should().Be(0);
        reserve.Should().Be(2290);
    }
}
