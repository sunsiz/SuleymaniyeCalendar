using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace SuleymaniyeCalendar.Services
{
    /// <summary>
    /// Lightweight performance monitoring service to track timings and counts.
    /// - Use StartTimer("Name") in a using block to record elapsed ms
    /// - Aggregates metrics in-memory and can print a summary report
    /// </summary>
    public class PerformanceService
    {
        private readonly ILogger<PerformanceService> _logger;
        private readonly ConcurrentDictionary<string, Metric> _metrics = new();

        public PerformanceService(ILogger<PerformanceService> logger = null)
        {
            _logger = logger;
        }

        public IDisposable StartTimer(string operationName)
        {
            return new PerformanceTimer(operationName, _logger, UpdateMetric);
        }

        public (string Report, int Items) GetSummary()
        {
            var items = _metrics.ToArray().OrderBy(kv => kv.Key).ToArray();
            var lines = items.Select(kv =>
                $"{kv.Key}: n={kv.Value.Count}, last={kv.Value.LastMs:F1}ms, avg={(kv.Value.TotalMs / Math.Max(1, kv.Value.Count)):F1}ms, min={kv.Value.MinMs:F1}ms, max={kv.Value.MaxMs:F1}ms");
            return (string.Join(" | ", lines), items.Length);
        }

        public void LogSummary(string tag = null)
        {
            var (report, items) = GetSummary();
            var header = $"📊 Perf Summary{(string.IsNullOrWhiteSpace(tag) ? string.Empty : $" [{tag}]")}: {items} metrics";
            System.Diagnostics.Debug.WriteLine(header);
            System.Diagnostics.Debug.WriteLine($"📊 Perf Report: {report}");
            _logger?.LogInformation(header);
            if (!string.IsNullOrWhiteSpace(report)) _logger?.LogInformation($"📊 Perf Report: {report}");
        }

        public void Reset() => _metrics.Clear();

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

        private class PerformanceTimer : IDisposable
        {
            private readonly string _operationName;
            private readonly ILogger _logger;
            private readonly Stopwatch _sw;
            private readonly Action<string, double> _onStop;

            public PerformanceTimer(string operationName, ILogger logger, Action<string, double> onStop)
            {
                _operationName = operationName;
                _logger = logger;
                _onStop = onStop;
                _sw = Stopwatch.StartNew();
                
                System.Diagnostics.Debug.WriteLine($"⏱️ Started: {_operationName}");
            }

            public void Dispose()
            {
                _sw.Stop();
                var ms = _sw.Elapsed.TotalMilliseconds;
                var message = $"⏱️ Completed: {_operationName} in {ms:F1}ms";
                
                System.Diagnostics.Debug.WriteLine(message);
                _logger?.LogInformation(message);
                _onStop?.Invoke(_operationName, ms);
            }
        }
    }
}
