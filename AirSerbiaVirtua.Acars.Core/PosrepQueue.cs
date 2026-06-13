using System.Text.Json;
using AirSerbiaVirtua.Contracts;

namespace AirSerbiaVirtua.Acars.Core;

/// <summary>One queued POSREP: the session it belongs to plus the report itself.</summary>
public sealed record PosrepEnvelope(int FlightSessionId, PositionReport Report);

/// <summary>
/// Thrown by a <see cref="PosrepQueue"/> sender to signal that the sample can
/// never be accepted (e.g. the session is closed or the request is malformed),
/// so the queue should drop it rather than retry forever. Transient failures
/// (network, timeout, 401, 5xx) must throw any other exception to be retried.
/// </summary>
public sealed class PosrepPermanentException(string message) : Exception(message);

/// <summary>
/// Durable, ordered, at-least-once send queue for POSREPs (production-readiness
/// item #6). Each sample is written to its own file under
/// <c>%LOCALAPPDATA%/AirSerbiaVirtua/posrep/</c> the instant it is produced, so a
/// network blip — or a client crash — never loses a sample. A background worker
/// drains the queue in timestamp order with exponential backoff + jitter, and
/// only deletes a file once the server has acknowledged it.
///
/// Combined with server-side idempotency (item #5), a retry after an ambiguous
/// timeout is safe: the duplicate is accepted, the file is dropped.
///
/// Filenames are <c>{unixMillis:D13}_{reportId:N}.json</c> so a lexical sort is a
/// chronological sort. Writes publish atomically via a temp file + rename.
/// </summary>
public sealed class PosrepQueue : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(60);

    private readonly string _dir;
    private readonly Func<PosrepEnvelope, CancellationToken, Task> _send;
    private readonly Action<string>? _log;
    private readonly SemaphoreSlim _signal = new(0);

    private CancellationTokenSource? _cts;
    private Task? _worker;

    /// <summary>Number of POSREPs still waiting to be acknowledged by the server.</summary>
    public int PendingCount { get; private set; }

    /// <summary>UTC time of the last POSREP the server acknowledged, if any.</summary>
    public DateTimeOffset? LastAckUtc { get; private set; }

    /// <summary>Raised (on the worker thread) when <see cref="PendingCount"/> or <see cref="LastAckUtc"/> changes.</summary>
    public event Action? Changed;

    /// <param name="baseDir">Queue directory; created if missing. Defaults to %LOCALAPPDATA%/AirSerbiaVirtua/posrep.</param>
    /// <param name="send">Sends one envelope to the server; must throw on any failure so the item is retried.</param>
    /// <param name="log">Optional diagnostic log sink.</param>
    public PosrepQueue(
        Func<PosrepEnvelope, CancellationToken, Task> send,
        string? baseDir = null,
        Action<string>? log = null)
    {
        _send = send;
        _log = log;
        _dir = baseDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AirSerbiaVirtua", "posrep");
        Directory.CreateDirectory(_dir);
        PendingCount = OrderedFiles().Count;
    }

    /// <summary>Starts the background drain worker. Idempotent.</summary>
    public void Start()
    {
        if (_worker is { IsCompleted: false }) return;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _worker = Task.Run(() => WorkerAsync(token), token);
        // Wake the worker in case files survived a previous run.
        if (PendingCount > 0) _signal.Release();
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        try { _cts.Cancel(); } catch { /* ignore */ }
        if (_worker is not null)
        {
            try { await _worker.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* expected */ }
            catch (Exception ex) { _log?.Invoke($"POSREP queue worker stopped with: {ex.Message}"); }
        }
        _cts.Dispose();
        _cts = null;
        _worker = null;
    }

    /// <summary>
    /// Persists a POSREP and signals the worker. Never throws on network state —
    /// the only failure mode is local disk, which is logged and swallowed so the
    /// sim poll loop is never blocked.
    /// </summary>
    public void Enqueue(PosrepEnvelope envelope)
    {
        try
        {
            var ms = envelope.Report.Timestamp.ToUnixTimeMilliseconds();
            var name = $"{ms:D13}_{envelope.Report.ClientReportId:N}.json";
            var finalPath = Path.Combine(_dir, name);
            var tmpPath = finalPath + ".tmp";

            File.WriteAllText(tmpPath, JsonSerializer.Serialize(envelope, Json));
            File.Move(tmpPath, finalPath, overwrite: true);   // atomic publish

            UpdateState(OrderedFiles().Count, LastAckUtc);
            _signal.Release();
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Failed to enqueue POSREP: {ex.Message}");
        }
    }

    /// <summary>
    /// Waits until the queue is empty (all POSREPs acknowledged) or the timeout
    /// elapses. Used before PIREP submission so the recorded track is complete.
    /// Returns true if drained.
    /// </summary>
    public async Task<bool> WaitForDrainAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (OrderedFiles().Count > 0)
        {
            if (DateTimeOffset.UtcNow >= deadline) return false;
            try { await Task.Delay(250, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return false; }
        }
        return true;
    }

    private async Task WorkerAsync(CancellationToken ct)
    {
        var delay = BaseDelay;
        while (!ct.IsCancellationRequested)
        {
            var files = OrderedFiles();
            if (files.Count == 0)
            {
                try { await _signal.WaitAsync(ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                delay = BaseDelay;
                continue;
            }

            var sentAny = false;
            var backoff = false;
            foreach (var file in files)
            {
                if (ct.IsCancellationRequested) return;

                var envelope = TryRead(file);
                if (envelope is null)
                {
                    // Corrupt / unreadable — drop it rather than wedging the queue.
                    _log?.Invoke($"Dropping unreadable POSREP file {Path.GetFileName(file)}");
                    TryDelete(file);
                    continue;
                }

                try
                {
                    await _send(envelope, ct).ConfigureAwait(false);
                    TryDelete(file);
                    sentAny = true;
                    UpdateState(OrderedFiles().Count, DateTimeOffset.UtcNow);
                    delay = BaseDelay;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (PosrepPermanentException ex)
                {
                    // The server will never accept this sample (closed session,
                    // bad request) — drop it and keep draining the rest.
                    _log?.Invoke($"Dropping POSREP (permanent): {ex.Message}");
                    TryDelete(file);
                    sentAny = true;
                    UpdateState(OrderedFiles().Count, LastAckUtc);
                    delay = BaseDelay;
                }
                catch (Exception ex)
                {
                    // Network / auth / timeout — keep the file, preserve order, back off.
                    _log?.Invoke($"POSREP send failed, will retry: {ex.Message}");
                    backoff = true;
                    break;
                }
            }

            if (backoff || !sentAny)
            {
                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
                try { await Task.Delay(delay + jitter, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                delay = TimeSpan.FromMilliseconds(Math.Min(MaxDelay.TotalMilliseconds, delay.TotalMilliseconds * 2));
            }
        }
    }

    private List<string> OrderedFiles()
    {
        try
        {
            var files = Directory.GetFiles(_dir, "*.json");
            Array.Sort(files, StringComparer.Ordinal);   // filename = chronological
            return new List<string>(files);
        }
        catch (DirectoryNotFoundException)
        {
            Directory.CreateDirectory(_dir);
            return new List<string>();
        }
    }

    private PosrepEnvelope? TryRead(string path)
    {
        try { return JsonSerializer.Deserialize<PosrepEnvelope>(File.ReadAllText(path), Json); }
        catch { return null; }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* a later pass will retry */ }
    }

    private void UpdateState(int pending, DateTimeOffset? lastAck)
    {
        var changed = pending != PendingCount || lastAck != LastAckUtc;
        PendingCount = pending;
        LastAckUtc = lastAck;
        if (changed) Changed?.Invoke();
    }

    public void Dispose()
    {
        try { StopAsync().GetAwaiter().GetResult(); } catch { /* best effort */ }
        _signal.Dispose();
    }
}
