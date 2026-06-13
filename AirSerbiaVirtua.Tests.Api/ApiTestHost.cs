using System.Net.Http.Headers;
using AirSerbiaVirtua.Api.Auth;
using AirSerbiaVirtua.Api.Data;
using AirSerbiaVirtua.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AirSerbiaVirtua.Tests.Api;

/// <summary>
/// One API host for the whole test assembly, bound to a dedicated local Postgres
/// database (<c>airserbiavirtua_test</c>).
///
/// The plan's first choice was a Testcontainers Postgres, but Docker is not
/// available on this machine, so we use the local server per the plan's
/// documented fallback. The test database is dropped and re-migrated once per
/// run, which also proves the whole migration chain applies cleanly on a blank
/// database. <see cref="Program"/> auto-migrates in the Development environment.
///
/// SAFETY: the resolved database name MUST end in "_test" or setup throws, so we
/// can never point at the real dev database (airserbiavirtua) by mistake. The
/// connection string is supplied via in-memory configuration layered LAST, which
/// reliably wins over the API's user-secrets (a plain UseSetting would not).
/// </summary>
[SetUpFixture]
public sealed class ApiTestHost
{
    public static WebApplicationFactory<Program> Factory { get; private set; } = null!;
    private static string _testConnectionString = null!;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        _testConnectionString = BuildTestConnectionString();

        // Blank slate: drop any leftover test DB; the host's Development-mode
        // Database.Migrate() (Program.cs) recreates and migrates it on boot.
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_testConnectionString).Options;
        await using (var db = new AppDbContext(opts))
            await db.Database.EnsureDeletedAsync();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            // In-memory config is applied after the app's own sources (appsettings,
            // user-secrets, env), so it overrides the dev connection string.
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _testConnectionString
                }));
        });

        // Boot now (runs EF migrations) so the first test isn't slow/flaky.
        _ = Factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
    }

    /// <summary>
    /// Builds the test connection string from the API's dev connection string
    /// (user-secrets), swapping the database name to a dedicated <c>*_test</c>
    /// database so the dev data is never touched.
    /// </summary>
    private static string BuildTestConnectionString()
    {
        var apiConfig = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Program).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var baseConn = apiConfig.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=airserbiavirtua;Username=postgres;Password=postgres";

        var b = new NpgsqlConnectionStringBuilder(baseConn) { Database = "airserbiavirtua_test" };

        if (!b.Database!.EndsWith("_test", StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Refusing to run integration tests against '{b.Database}': " +
                "the test database name must end in '_test'.");

        return b.ConnectionString;
    }

    /// <summary>
    /// Authenticated client WITHOUT going through /auth/login — mints an access
    /// token directly via the host's <see cref="JwtTokenService"/>. Keeps us clear
    /// of the 10/min auth rate limit (all test traffic shares one IP partition).
    /// </summary>
    public static HttpClient CreateClientFor(Pilot pilot)
    {
        var client = Factory.CreateClient();
        var jwt = Factory.Services.GetRequiredService<JwtTokenService>();
        var (token, _) = jwt.CreateAccessToken(pilot);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
