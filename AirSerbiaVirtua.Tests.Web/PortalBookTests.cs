using AirSerbiaVirtua.Web.Client.Pages;
using Bunit;
using FluentAssertions;
using Moq;

namespace AirSerbiaVirtua.Tests.Web;

public class PortalBookTests : PortalTestContext
{
    [Test]
    public async Task ReadyBooking_ShowsReadyBadge_AndPrepareLink()
    {
        await SignInAsync();
        Api.Setup(a => a.GetRoutesAsync()).ReturnsAsync([]);
        Api.Setup(a => a.GetMyBookingsAsync())
            .ReturnsAsync([Booking(7, ready: DateTimeOffset.UtcNow)]);

        var cut = Render<PortalBook>();

        cut.Markup.Should().Contain("READY");
        cut.Find($"a[href='portal/briefing?bookingId=7']").TextContent.Should().Contain("Prepare");
    }

    [Test]
    public async Task UnpreparedBooking_HasPrepareLink_ButNoReadyBadge()
    {
        await SignInAsync();
        Api.Setup(a => a.GetRoutesAsync()).ReturnsAsync([]);
        Api.Setup(a => a.GetMyBookingsAsync()).ReturnsAsync([Booking(7)]);

        var cut = Render<PortalBook>();

        cut.Markup.Should().NotContain("READY");
        cut.FindAll("a[href='portal/briefing?bookingId=7']").Should().HaveCount(1);
    }
}
