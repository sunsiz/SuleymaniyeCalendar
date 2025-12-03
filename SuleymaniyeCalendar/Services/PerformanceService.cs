using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

#nullable enable

namespace SuleymaniyeCalendar.Services;

/// <summary>
/// Lightweight performance monitoring service for tracking operation timings.
/// </summary>
/// <remarks>
/// Usage: Wrap operations in a using block with StartTimer:
/// <code>
/// using (_perf.StartTimer("Operation.Name"))
/// {
///     await DoWorkAsync();
/// }
/// </code>
/// Call <see cref="LogSummary"/> to output aggregated metrics to debug console.
/// </remarks>
public sealed class PerformanceService
{
    private readonly ILogger<PerformanceService>? _logger;
    private readonly ConcurrentDictionary<string, Metric> _metrics = new();

    /// <summary>
    /// Creates a new PerformanceService instance.
    /// </summary>
    /// <param name="logger">Optional logger for structured logging output.</param>
    public PerformanceService(ILogger<PerformanceService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Starts a timer for the named operation. Dispose to stop timing.
    /// </summary>
    /// <param name="operationName">Name of the operation (e.g., "MainPage.LoadPrayers").</param>
    /// <returns>Disposable timer that records elapsed time on disposal.</returns>
    public IDisposable StartTimer(string operationName)
    {
        return new PerformanceTimer(operationName, _logger, UpdateMetric);
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

    #region Nested Types

    /// <summary>Aggregated metric data for a single operation.</summary>
    private sealed class Metric
    {
        public int Count { get; set; }
        public double TotalMs { get; set; }
        public double LastMs { get; set; }
        public double MinMs { get; set; }
        public double MaxMs { get; set; }
    }

    /// <summary>Disposable timer that records elapsed time on disposal.</summary>
    private sealed class PerformanceTimer : IDisposable
    {
        private readonly string _operationName;
        private readonly ILogger? _logger;
        private readonly Stopwatch _sw;
        private readonly Action<string, double> _onStop;

        public PerformanceTimer(string operationName, ILogger? logger, Action<string, double> onStop)
        {
            _operationName = operationName;
            _logger = logger;
            _onStop = onStop;
            _sw = Stopwatch.StartNew();
            Debug.WriteLine($"⏱️ Started: {_operationName}");
        }

        public void Dispose()
        {
            _sw.Stop();
            var ms = _sw.Elapsed.TotalMilliseconds;
            Debug.WriteLine($"⏱️ Completed: {_operationName} in {ms:F1}ms");
            _logger?.LogInformation("⏱️ Completed: {OperationName} in {ElapsedMs:F1}ms", _operationName, ms);
            _onStop?.Invoke(_operationName, ms);
        }
    }

    #endregion
}
