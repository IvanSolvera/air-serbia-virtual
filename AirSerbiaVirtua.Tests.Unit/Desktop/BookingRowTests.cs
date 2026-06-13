using AirSerbiaVirtua.Acars.Desktop.ViewModels;
using AirSerbiaVirtua.Contracts;
using FluentAssertions;

namespace AirSerbiaVirtua.Tests.Unit.Desktop;

public class BookingRowTests
{
    private static BookingInfo Booking(DateTimeOffset? ready) => new(
        7, 3, "JU360", "LYBE", "LOWW", "A319", 55,
        new DateOnly(2026, 6, 6), BookingStatus.Open, ready);

    [Test]
    public void IsDispatchReady_TrueWhenFlagSet()
    {
        new BookingRow(Booking(DateTimeOffset.UtcNow)).IsDispatchReady.Should().BeTrue();
    }

    [Test]
    public void IsDispatchReady_FalseWhenNull()
    {
        new BookingRow(Booking(null)).IsDispatchReady.Should().BeFalse();
    }

    [Test]
    public void Source_RoundTripsTheDto()
    {
        var dto = Booking(null);
        new BookingRow(dto).Source.Should().BeSameAs(dto);
    }
}
