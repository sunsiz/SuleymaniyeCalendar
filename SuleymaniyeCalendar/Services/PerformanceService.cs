using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

#nullable enable

namespace SuleymaniyeCalendar.Services;

public sealed class PerformanceService
{
    private readonly ILogger<PerformanceService>? _logger;
    private readonly ConcurrentDictionary<string, Metric> _metrics = new();

    private static readonly IDisposable NoopTimer = new NoopDisposable();

    public PerformanceService(ILogger<PerformanceService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Enables performance timing and metric collection.
    /// Keep false in production to avoid overhead.
    /// Default: false
    /// </summary>
    public bool VerboseLoggingEnabled { get; set; }

    /// <summary>
    /// Starts a timer for the named operation. Dispose to stop timing.
    /// </summary>
    /// <param name="operationName">Name of the operation (e.g., "MainPage.LoadPrayers").</param>
    /// <returns>Disposable timer that records elapsed time on disposal.</returns>
    public IDisposable StartTimer(string operationName)
    {
        if (!VerboseLoggingEnabled)
            return NoopTimer;

        return new PerformanceTimer(operationName, _logger, UpdateMetric, verbose: true);
    }

    /// <summary>
    /// Gets a formatted summary of all recorded metrics.
    /// </summary>
    /// <returns>Tuple of (formatted report string, metric count).</returns>
    public (string Report, int Items) GetSummary()
    {
        var items = _metrics.ToArray().OrderBy(kv => kv.Key).ToArray();
        var lines = items.Select(kv =>
            $"{kv.Key}: n={kv.Value.Count}, last={kv.Value.LastMs:F1}ms, avg={kv.Value.TotalMs / Math.Max(1, kv.Value.Count):F1}ms, min={kv.Value.MinMs:F1}ms, max={kv.Value.MaxMs:F1}ms");
        return (string.Join(" | ", lines), items.Length);
    }

    /// <summary>
    /// Outputs the performance summary to debug console and logger.
    /// </summary>
    /// <param name="tag">Optional tag to identify the summary context.</param>
    public void LogSummary(string? tag = null)
    {
        if (!VerboseLoggingEnabled)
            return;

        var (report, items) = GetSummary();
        var header = $"📊 Perf Summary{(string.IsNullOrWhiteSpace(tag) ? string.Empty : $" [{tag}]")}: {items} metrics";
        Debug.WriteLine(header);
        Debug.WriteLine($"📊 Perf Report: {report}");
        _logger?.LogInformation("{Header}", header);
        if (!string.IsNullOrWhiteSpace(report))
            _logger?.LogInformation("📊 Perf Report: {Report}", report);
    }

    /// <summary>Clears all recorded metrics.</summary>
    public void Reset() => _metrics.Clear();

    /// <summary>Updates the metric for a named operation.</summary>
    private void UpdateMetric(string name, double elapsedMs)
    {
        _metrics.AddOrUpdate(name,
            _ => new Metric { Count = 1, TotalMs = elapsedMs, LastMs = elapsedMs, MinMs = elapsedMs, MaxMs = elapsedMs },
            (_, m) =>
            {
                m.Count++;
                m.TotalMs += elapsedMs;
                m.LastMs = elapsedMs;
                if (elapsedMs < m.MinMs) m.MinMs = elapsedMs;
                if (elapsedMs > m.MaxMs) m.MaxMs = elapsedMs;
                return m;
            });
    }

    private sealed class Metric
    {
        public int Count { get; set; }
        public double TotalMs { get; set; }
        public double LastMs { get; set; }
        public double MinMs { get; set; }
        public double MaxMs { get; set; }
    }

    private sealed class PerformanceTimer : IDisposable
    {
        private readonly string _operationName;
        private readonly ILogger? _logger;
        private readonly Stopwatch _sw;
        private readonly Action<string, double> _onStop;
        private readonly bool _verbose;

        public PerformanceTimer(string operationName, ILogger? logger, Action<string, double> onStop, bool verbose)
        {
            _operationName = operationName;
            _logger = logger;
            _onStop = onStop;
            _verbose = verbose;
            _sw = Stopwatch.StartNew();
            if (_verbose)
                Debug.WriteLine($"⏱️ Started: {_operationName}");
        }

        public void Dispose()
        {
            _sw.Stop();
            var ms = _sw.Elapsed.TotalMilliseconds;
            if (_verbose)
                Debug.WriteLine($"⏱️ Completed: {_operationName} in {ms:F1}ms");
            _logger?.LogInformation("⏱️ Completed: {OperationName} in {ElapsedMs:F1}ms", _operationName, ms);
            _onStop?.Invoke(_operationName, ms);
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
