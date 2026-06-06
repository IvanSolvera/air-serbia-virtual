using System.Text;
using System.Threading.RateLimiting;
using AirSerbiaVirtua.Api.Auth;
using AirSerbiaVirtua.Api.Data;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Formatting.Compact;

// ---- Logging (Serilog) — prod item #8 ------------------------------------------
// Bootstrap logger so failures before the host is built (missing Jwt:Key, bad
// connection string) still land somewhere visible.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Console: human-readable in Development, compact JSON elsewhere.
    // File: compact JSON, rolling daily -> logs/asv-YYYYMMDD.log, 30 days kept.
    builder.Host.UseSerilog((ctx, cfg) =>
    {
        cfg.MinimumLevel.Information()
           .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
           .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
           .Enrich.FromLogContext()
           .WriteTo.File(
               new CompactJsonFormatter(),
               path: "logs/asv-.log",
               rollingInterval: RollingInterval.Day,
               retainedFileCountLimit: 30,
               shared: true);

        if (ctx.HostingEnvironment.IsDevelopment())
            cfg.WriteTo.Console();
        else
            cfg.WriteTo.Console(new CompactJsonFormatter());
    });

    // ---- Data ------------------------------------------------------------------
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' not found.")));

    // ---- Auth (JWT bearer) -------------------------------------------------------
    builder.Services.AddSingleton<JwtTokenService>();
    builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
    builder.Services.AddScoped<RefreshTokenService>();

    var jwt = builder.Configuration.GetSection("Jwt");
    var jwtKey = jwt["Key"];
    if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    {
        throw new InvalidOperationException(
            "Jwt:Key is missing or shorter than 32 bytes. " +
            "Set it via user-secrets (dev) or the Jwt__Key environment variable (prod). " +
            "Never commit a signing key to appsettings.json.");
    }

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt["Issuer"],
                ValidAudience = jwt["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });
    builder.Services.AddAuthorization();

    // ---- Rate limiting -----------------------------------------------------------
    // Protects /api/v1/auth/* from brute-force / credential-stuffing over the internet.
    // Other endpoints intentionally have no global limiter — POSREP bursts when an
    // offline buffer flushes after a network blip must not be punished.
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

        options.OnRejected = async (context, ct) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
            await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Too many requests." }, ct);
        };
    });

    // ---- CORS — prod item #9 -------------------------------------------------------
    // Conscious decision (2026-06-06):
    //  * The native WPF ACARS client performs no CORS preflight — unaffected either way.
    //  * The planned web UI (doc/website-design.md) runs on its own origin and sends
    //    JWT in the Authorization header (no cookies), so credentials stay disallowed.
    //  * Allowed origins come from config (Cors:AllowedOrigins). The default — an empty
    //    list — emits no Access-Control-Allow-Origin header at all: browsers are locked
    //    out cross-origin until an origin is deliberately whitelisted per environment.
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .DisallowCredentials();
            }
            // else: default-deny — an empty policy matches no origin.
        });
    });

    // ---- Weather (METAR) ---------------------------------------------------------
    builder.Services.AddHttpClient<AirSerbiaVirtua.Api.Services.MetarService>(c =>
    {
        c.BaseAddress = new Uri("https://aviationweather.gov/");
        c.Timeout = TimeSpan.FromSeconds(8);
        c.DefaultRequestHeaders.UserAgent.ParseAdd("AirSerbiaVirtua-ACARS/1.0");
    });

    // ---- API versioning — prod item #7 ---------------------------------------------
    // URL-segment versioning: /api/v1/... Unversioned routes (/api/auth/...) no longer
    // exist, so an outdated client fails fast with 404 instead of silently mis-pairing
    // with an incompatible server.
    builder.Services
        .AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.ReportApiVersions = true;                       // api-supported-versions header
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddMvc()
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";                     // -> "v1"
            options.SubstituteApiVersionInUrl = true;               // Swagger shows /api/v1/..., not /api/v{version}/...
        });

    // ---- MVC + Swagger -----------------------------------------------------------
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Air Serbia Virtua API", Version = "v1" });
        var scheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        };
        c.AddSecurityDefinition("Bearer", scheme);
        c.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
    });

    var app = builder.Build();

    // ---- Apply migrations on startup (dev convenience) ---------------------------
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    }

    // ---- HTTP pipeline -----------------------------------------------------------
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    // One structured "HTTP GET /api/v1/... responded 200 in 12ms" event per request.
    // PilotId is resolved at response time, after authentication has populated User.
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            var pilotId = httpContext.User.PilotId();
            if (pilotId is not null) diagnosticContext.Set("PilotId", pilotId);
        };
    });

    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();

    // Every log line written by controllers/services during an authenticated request
    // carries PilotId (prod item #8 — pilot context on the request scope).
    app.Use(async (context, next) =>
    {
        var pilotId = context.User.PilotId();
        if (pilotId is null)
        {
            await next();
            return;
        }
        using (LogContext.PushProperty("PilotId", pilotId))
        {
            await next();
        }
    });

    app.UseAuthorization();

    app.MapControllers();
    app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "AirSerbiaVirtua.Api" }))
       .AllowAnonymous();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AirSerbiaVirtua.Api terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
