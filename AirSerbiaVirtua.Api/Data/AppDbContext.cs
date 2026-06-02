using System.Text.Json;
using AirSerbiaVirtua.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Route = AirSerbiaVirtua.Api.Models.Route;

namespace AirSerbiaVirtua.Api.Data;

/// <summary>
/// EF Core context for the Air Serbia Virtua platform, targeting PostgreSQL
/// (Npgsql). String/int array properties are persisted as <c>jsonb</c>; enums
/// are persisted as readable strings.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Pilot> Pilots => Set<Pilot>();
    public DbSet<Rank> Ranks => Set<Rank>();
    public DbSet<Aircraft> Aircraft => Set<Aircraft>();
    public DbSet<Airport> Airports => Set<Airport>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Pirep> Pireps => Set<Pirep>();
    public DbSet<PositionLog> PositionLogs => Set<PositionLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ---- jsonb converters for array-valued properties -------------------
        var stringListConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        var stringListComparer = new ValueComparer<List<string>>(
            (a, c) => (a ?? new()).SequenceEqual(c ?? new()),
            v => v.Aggregate(0, (acc, s) => HashCode.Combine(acc, s.GetHashCode())),
            v => v.ToList());

        var intListConverter = new ValueConverter<List<int>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<int>>(v, (JsonSerializerOptions?)null) ?? new List<int>());

        var intListComparer = new ValueComparer<List<int>>(
            (a, c) => (a ?? new()).SequenceEqual(c ?? new()),
            v => v.Aggregate(0, (acc, i) => HashCode.Combine(acc, i)),
            v => v.ToList());

        // ---- Airport --------------------------------------------------------
        b.Entity<Airport>(e =>
        {
            e.HasKey(x => x.Icao);
            e.Property(x => x.Icao).HasMaxLength(4);
            e.Property(x => x.Iata).HasMaxLength(3);
            e.HasIndex(x => x.Iata);
        });

        // ---- Rank -----------------------------------------------------------
        b.Entity<Rank>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.MinHours).HasColumnType("numeric(8,1)");
            e.Property(x => x.AllowedAircraftTypes)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);
            e.Property(x => x.AllowedAircraftTypes).HasColumnType("jsonb");
            e.HasIndex(x => x.Name).IsUnique();
        });

        // ---- Pilot ----------------------------------------------------------
        b.Entity<Pilot>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TotalHours).HasColumnType("numeric(9,1)");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Callsign).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();

            e.HasOne(x => x.Rank)
                .WithMany(r => r.Pilots)
                .HasForeignKey(x => x.RankId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Hub)
                .WithMany()
                .HasForeignKey(x => x.HubId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Aircraft -------------------------------------------------------
        b.Entity<Aircraft>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Registration).IsUnique();

            e.HasOne(x => x.Hub)
                .WithMany()
                .HasForeignKey(x => x.HubId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Route ----------------------------------------------------------
        b.Entity<Route>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FlightNumber).IsUnique();
            e.Property(x => x.Days)
                .HasConversion(intListConverter)
                .Metadata.SetValueComparer(intListComparer);
            e.Property(x => x.Days).HasColumnType("jsonb");

            e.HasOne(x => x.Departure)
                .WithMany()
                .HasForeignKey(x => x.DepIcao)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Arrival)
                .WithMany()
                .HasForeignKey(x => x.ArrIcao)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Booking --------------------------------------------------------
        b.Entity<Booking>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

            e.HasOne(x => x.Pilot)
                .WithMany(p => p.Bookings)
                .HasForeignKey(x => x.PilotId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Route)
                .WithMany(r => r.Bookings)
                .HasForeignKey(x => x.RouteId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.PilotId, x.RouteId, x.Date }).IsUnique();
        });

        // ---- Pirep ----------------------------------------------------------
        b.Entity<Pirep>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Source).HasMaxLength(40);
            e.Property(x => x.RawJson).HasColumnType("jsonb");

            e.HasOne(x => x.Pilot)
                .WithMany(p => p.Pireps)
                .HasForeignKey(x => x.PilotId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Route)
                .WithMany(r => r.Pireps)
                .HasForeignKey(x => x.RouteId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Aircraft)
                .WithMany(a => a.Pireps)
                .HasForeignKey(x => x.AircraftId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.Status);
        });

        // ---- PositionLog ----------------------------------------------------
        b.Entity<PositionLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Phase).HasConversion<string>().HasMaxLength(20);

            e.HasOne(x => x.Pirep)
                .WithMany(p => p.PositionLogs)
                .HasForeignKey(x => x.PirepId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.PirepId, x.Timestamp });
        });

        // ---- RefreshToken ---------------------------------------------------
        b.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Ignore(x => x.IsActive);
            e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.PilotId);

            e.HasOne(x => x.Pilot)
                .WithMany()
                .HasForeignKey(x => x.PilotId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        SeedData.Apply(b);
    }
}
