using FluentAssertions;

namespace AirSerbiaVirtua.Tests.Unit;

public class CanaryTests
{
    [Test]
    public void TestInfrastructure_Works() => true.Should().BeTrue();
}
