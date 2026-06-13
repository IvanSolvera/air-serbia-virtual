using AirSerbiaVirtua.Contracts;
using AirSerbiaVirtua.Web.Client.Pages;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AirSerbiaVirtua.Tests.Web;

public class PortalBriefingTests : PortalTestContext
{
    private static readonly RouteInfo Route = new(3, "JU360", "LYBE", "LOWW", "A319", 300, 55, [1, 2, 3]);

    private void SetupHappyApi(params BookingInfo[] bookings)
    {
        Api.Setup(a => a.GetActiveBookingsAsync()).ReturnsAsync([.. bookings]);
        Api.Setup(a => a.GetRouteAsync(3)).ReturnsAsync(Route);
        Api.Setup(a => a.GetMetarAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync(
        [
            new MetarInfo("LYBE", "LYBE 061330Z 12008KT CAVOK 24/12 Q1018", DateTimeOffset.UtcNow),
            new MetarInfo("LOWW", "LOWW 061320Z 30010KT 9999 FEW040 21/10 Q1015", DateTimeOffset.UtcNow)
        ]);
    }

    [Test]
    public async Task NoBookings_ShowsEmptyStateLinkingToBook()
    {
        await SignInAsync();
        Api.Setup(a => a.GetActiveBookingsAsync()).ReturnsAsync([]);

        var cut = Render<PortalBriefing>();

        cut.Markup.Should().Contain("No active bookings");
        cut.Find("a[href='portal/book']").Should().NotBeNull();
    }

    [Test]
    public async Task SelectedBooking_RendersRouteFactsFuelAndMetars()
    {
        await SignInAsync();
        SetupHappyApi(Booking(7));

        var cut = Render<PortalBriefing>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("JU360").And.Contain("300 nm");
            // A319 over 300 nm: trip 2100, reserve 2290, total 4390
            cut.Markup.Should().Contain("2,100").And.Contain("2,290").And.Contain("4,390");
            cut.Markup.Should().Contain("LYBE 061330Z").And.Contain("LOWW 061320Z");
        });
    }

    [Test]
    public async Task MarkReady_CallsApi_AndShowsReadyBadgeWithUndo()
    {
        await SignInAsync();
        SetupHappyApi(Booking(7));
        Api.Setup(a => a.MarkDispatchReadyAsync(7)).ReturnsAsync((true, null));
        // after marking, reload returns the flagged booking
        Api.SetupSequence(a => a.GetActiveBookingsAsync())
            .ReturnsAsync([Booking(7)])
            .ReturnsAsync([Booking(7, ready: DateTimeOffset.UtcNow)]);

        var cut = Render<PortalBriefing>();
        cut.WaitForElement("button#mark-ready").Click();

        cut.WaitForAssertion(() =>
        {
            Api.Verify(a => a.MarkDispatchReadyAsync(7), Times.Once);
            cut.Markup.Should().Contain("DISPATCH READY");
            cut.Find("button#undo-ready").Should().NotBeNull();
        });
    }

    [Test]
    public async Task MarkReady_Conflict_ShowsApiMessageInline()
    {
        await SignInAsync();
        SetupHappyApi(Booking(7));
        Api.Setup(a => a.MarkDispatchReadyAsync(7))
            .ReturnsAsync((false, "Booking is Flown — only Open or Confirmed bookings can be dispatched."));

        var cut = Render<PortalBriefing>();
        cut.WaitForElement("button#mark-ready").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Booking is Flown"));
    }

    [Test]
    public async Task DeepLink_BookingIdQuery_PreselectsThatBooking()
    {
        await SignInAsync();
        var other = new BookingInfo(8, 3, "JU361", "LOWW", "LYBE", "A319", 55, Today, BookingStatus.Open, null);
        SetupHappyApi(Booking(7), other);

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("portal/briefing?bookingId=8");

        var cut = Render<PortalBriefing>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("JU361"));
    }
}
