using System.Collections.Concurrent;
using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Contracts;

namespace AirSerbiaVirtua.Acars.PoC;

/// <summary>
/// Dependency-free self-test for <see cref="PosrepQueue"/> (offline buffer,
/// production-readiness item #6) and the idempotency contract it relies on
/// (item #5). Uses a fake sender + a temp directory; no network or DB. Run via
/// <c>--selftest</c>. Returns the number of failed assertions (0 = pass).
///
/// Covers:
///   A. Durability — a sample is written to disk the instant it is enqueued,
///      before any delivery, so a crash/blip never loses it.
///   B. Crash recovery + ordered, complete delivery — a fresh queue over the
///      same directory picks up the backlog and drains it in timestamp order.
///   C. Idempotent retry — a transient failure is retried with the *same*
///      ClientReportId, so the server can dedupe (no double-insert).
/// </summary>
public static class PosrepQueueTest
{
    public static int Run()
    {
        Console.WriteLine("\n=== PosrepQueue self-test (fake sender, temp dir) ===\n");
        var failures = 0;
        var dir = Path.Combine(Path.GetTempPath(), "asv-posrep-selftest-" + Guid.NewGuid().ToString("N"));

        void Check(bool ok, string label)
        {
            Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {label}");
            if (!ok) failures++;
        }

        try
        {
            var t0 = new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero);
            var reports = Enumerable.Range(0, 5)
                .Select(i => new PositionReport(
                    ClientReportId: Guid.NewGuid(),
                    Timestamp: t0.AddSeconds(i * 30),
                    Lat: 44.8 + i, Lon: 20.3, AltFt: 1000 * i, GsKts: 250, Phase: FlightPhase.Cruise))
                .ToList();

            // ---- A. Durability: enqueue while offline, nothing delivered ----
            using (var offline = new PosrepQueue(
                       send: (_, _) => throw new InvalidOperationException("network down"),
                       baseDir: dir))
            {
                foreach (var r in reports)
                    offline.Enqueue(new PosrepEnvelope(42, r));

                var filesOnDisk = Directory.GetFiles(dir, "*.json").Length;
                Check(filesOnDisk == 5, $"5 samples persisted to disk before delivery (found {filesOnDisk})");
                Check(offline.PendingCount == 5, $"PendingCount reflects backlog ({offline.PendingCount})");
                // Note: worker never started, so nothing could have been delivered.
            }

            // ---- B. Crash recovery + ordered delivery via a NEW queue --------
            var delivered = new ConcurrentQueue<PosrepEnvelope>();
            using (var online = new PosrepQueue(
                       send: (env, _) => { delivered.Enqueue(env); return Task.CompletedTask; },
                       baseDir: dir))
            {
                Check(online.PendingCount == 5, $"fresh queue recovers backlog from disk ({online.PendingCount})");
                online.Start();
                var drained = online.WaitForDrainAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                Check(drained, "queue drained within timeout");
            }

            var got = delivered.ToArray();
            Check(got.Length == 5, $"all 5 samples delivered ({got.Length})");
            var ordered = got.Select(e => e.Report.Timestamp).SequenceEqual(reports.Select(r => r.Timestamp));
            Check(ordered, "delivered in chronological (timestamp) order");
            var idsMatch = got.Select(e => e.Report.ClientReportId).ToHashSet()
                .SetEquals(reports.Select(r => r.ClientReportId));
            Check(idsMatch, "report ids preserved exactly across persist + reload");
            var noFilesLeft = Directory.GetFiles(dir, "*.json").Length == 0;
            Check(noFilesLeft, "acknowledged files removed from disk");

            // ---- C. Idempotent retry: same id on every attempt --------------
            var attemptsById = new ConcurrentDictionary<Guid, int>();
            var flaky = reports[0] with { ClientReportId = Guid.NewGuid() };
            using (var retrying = new PosrepQueue(
                       send: (env, _) =>
                       {
                           var n = attemptsById.AddOrUpdate(env.Report.ClientReportId, 1, (_, c) => c + 1);
                           if (n < 3) throw new TimeoutException("transient");   // fail twice, then succeed
                           return Task.CompletedTask;
                       },
                       baseDir: dir))
            {
                retrying.Enqueue(new PosrepEnvelope(42, flaky));
                retrying.Start();
                var drained = retrying.WaitForDrainAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
                Check(drained, "flaky sample eventually delivered after retries");
            }

            Check(attemptsById.Count == 1, $"exactly one distinct report id retried ({attemptsById.Count})");
            Check(attemptsById.TryGetValue(flaky.ClientReportId, out var attempts) && attempts >= 3,
                $"same id retried until success (attempts={(attemptsById.TryGetValue(flaky.ClientReportId, out var a) ? a : 0)})");

            // ---- D. Permanent failure is dropped, not retried forever --------
            var permAttempts = 0;
            var doomed = reports[0] with { ClientReportId = Guid.NewGuid() };
            using (var dropping = new PosrepQueue(
                       send: (_, _) =>
                       {
                           Interlocked.Increment(ref permAttempts);
                           throw new PosrepPermanentException("session closed (409)");
                       },
                       baseDir: dir))
            {
                dropping.Enqueue(new PosrepEnvelope(99, doomed));
                dropping.Start();
                var drained = dropping.WaitForDrainAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                Check(drained, "permanently-failing sample is dropped so the queue drains");
                Check(permAttempts == 1, $"permanent failure tried once, not retried ({permAttempts})");
            }
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }

        Console.WriteLine($"\nPosrepQueue self-test: {(failures == 0 ? "ALL PASS" : failures + " FAILURE(S)")}\n");
        return failures;
    }
}
