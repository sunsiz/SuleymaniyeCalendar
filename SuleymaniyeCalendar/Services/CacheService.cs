using Microsoft.Extensions.Logging;

namespace SuleymaniyeCalendar.Services
{
    /// <summary>
    /// Simple prayer cache helper - works with DataService's existing cache system
    /// The app already has excellent built-in caching, this just provides utility methods
    /// </summary>
    public class CacheService
    {
        private readonly ILogger<CacheService> _logger;
        private readonly string _cacheDirectory;

        public CacheService(ILogger<CacheService> logger)
        {
            _logger = logger;
            _cacheDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        /// <summary>
        /// Check if prayer time cache files exist (used by DataService)
        /// </summary>
        public bool HasPrayerCache()
        {
            try
            {
                var jsonFile = Path.Combine(_cacheDirectory, "monthlycalendar.json");
                var xmlFile = Path.Combine(_cacheDirectory, "monthlycalendar.xml");
                
                return File.Exists(jsonFile) || File.Exists(xmlFile);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to check prayer cache status");
                return false;
            }
        }

        /// <summary>
        /// Get simple cache statistics 
        /// </summary>
        public CacheStatistics GetCacheStatistics()
        {
            try
            {
                var stats = new CacheStatistics();
                var jsonFile = Path.Combine(_cacheDirectory, "monthlycalendar.json");
                var xmlFile = Path.Combine(_cacheDirectory, "monthlycalendar.xml");

                if (File.Exists(jsonFile))
                {
                    var fileInfo = new FileInfo(jsonFile);
                    stats.TotalSizeBytes += fileInfo.Length;
                    stats.TotalEntries++;
                }

                if (File.Exists(xmlFile))
                {
                    var fileInfo = new FileInfo(xmlFile);
                    stats.TotalSizeBytes += fileInfo.Length;
                    stats.TotalEntries++;
                }

                return stats;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get cache statistics");
                return new CacheStatistics();
            }
        }

        /// <summary>
        /// Clear prayer cache files (utility method for DataService)
        /// </summary>
        public bool ClearPrayerCache()
        {
            try
            {
                var jsonFile = Path.Combine(_cacheDirectory, "monthlycalendar.json");
                var xmlFile = Path.Combine(_cacheDirectory, "monthlycalendar.xml");

                var clearedFiles = 0;
                if (File.Exists(jsonFile))
                {
                    File.Delete(jsonFile);
                    clearedFiles++;
                }

                if (File.Exists(xmlFile))
                {
                    File.Delete(xmlFile);
                    clearedFiles++;
                }

                _logger?.LogInformation("Cleared {Count} prayer cache files", clearedFiles);
                return clearedFiles > 0;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to clear prayer cache");
                return false;
            }
        }
    }

    /// <summary>
    /// Simple cache statistics
    /// </summary>
    public class CacheStatistics
    {
        public int TotalEntries { get; set; }
        public long TotalSizeBytes { get; set; }
        
        public string TotalSizeFormatted => FormatBytes(TotalSizeBytes);

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }
}
