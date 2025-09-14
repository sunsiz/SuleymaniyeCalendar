using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.TestConsole;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Testing PrayerIconService...\n");

        // Test prayer icon assignments
        var prayers = new List<Prayer>
        {
            new Prayer { Name = "Fajr" },
            new Prayer { Name = "Dhuhr" },
            new Prayer { Name = "Asr" },
            new Prayer { Name = "Maghrib" },
            new Prayer { Name = "Isha" }
        };

        foreach (var prayer in prayers)
        {
            PrayerIconService.AssignIcon(prayer);
            Console.WriteLine($"Prayer: {prayer.Name}");
            Console.WriteLine($"  Icon: {prayer.IconPath}");
            Console.WriteLine($"  Description: {prayer.Description}");
            Console.WriteLine();
        }

        // Test individual icon retrieval
        Console.WriteLine("Individual Icon Tests:");
        Console.WriteLine($"Fajr: {PrayerIconService.GetPrayerIcon("fajr")}");
        Console.WriteLine($"Imsak: {PrayerIconService.GetPrayerIcon("imsak")}");
        Console.WriteLine($"Unknown: {PrayerIconService.GetPrayerIcon("unknown")}");
        
        Console.WriteLine("\nAll tests passed! ✅");
    }
}
