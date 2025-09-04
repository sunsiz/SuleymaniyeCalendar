using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SuleymaniyeCalendar.Services
{
    /// <summary>
    /// Simple performance monitoring service to track loading times
    /// </summary>
    public class PerformanceService
    {
        private readonly ILogger<PerformanceService> _logger;

        public PerformanceService(ILogger<PerformanceService> logger = null)
        {
            _logger = logger;
        }

        public IDisposable StartTimer(string operationName)
        {
            return new PerformanceTimer(operationName, _logger);
        }

        private class PerformanceTimer : IDisposable
        {
            private readonly string _operationName;
            private readonly ILogger _logger;
            private readonly DateTime _startTime;

            public PerformanceTimer(string operationName, ILogger logger)
            {
                _operationName = operationName;
                _logger = logger;
                _startTime = DateTime.UtcNow;
                
                System.Diagnostics.Debug.WriteLine($"⏱️ Started: {_operationName}");
            }

            public void Dispose()
            {
                var elapsed = DateTime.UtcNow - _startTime;
                var message = $"⏱️ Completed: {_operationName} in {elapsed.TotalMilliseconds:F1}ms";
                
                System.Diagnostics.Debug.WriteLine(message);
                _logger?.LogInformation(message);
            }
        }
    }
}
