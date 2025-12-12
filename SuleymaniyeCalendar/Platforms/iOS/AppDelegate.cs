using Foundation;
using UIKit;
using UserNotifications;
using ObjCRuntime;
using SuleymaniyeCalendar.Platforms.iOS;

namespace SuleymaniyeCalendar;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication app, NSDictionary options)
    {
        // Set up native iOS exception handler for crash logging
        ObjCRuntime.Runtime.MarshalObjectiveCException += OnMarshalObjectiveCException;
        
        // Set up .NET exception handlers that log to iOS system log
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            LogCrash("UnhandledException", ex);
        };
        
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            LogCrash("UnobservedTaskException", e.Exception);
        };
        
        // Initialize notifications
        if (UIDevice.CurrentDevice.CheckSystemVersion(10, 0))
        {
            var center = UNUserNotificationCenter.Current;
            center.Delegate = new UserNotificationCenterDelegate();
            
            // Request permission and setup categories
            Task.Run(async () => await NotificationService.InitializeNotificationsAsync());
        }
        
        NSLog("✅ SuleymaniyeCalendar AppDelegate.FinishedLaunching completed");
        return base.FinishedLaunching(app, options);
    }
    
    private static void OnMarshalObjectiveCException(object? sender, MarshalObjectiveCExceptionEventArgs args)
    {
        var ex = args.Exception;
        var message = $"[ObjC] {ex?.Name}: {ex?.Reason}";
        NSLog($"❌ CRASH {message}");
        
        if (ex?.CallStackSymbols != null)
        {
            foreach (var symbol in ex.CallStackSymbols)
            {
                NSLog($"  {symbol}");
            }
        }
        
        SaveCrashLog("ObjC", message);
    }
    
    private static void LogCrash(string source, Exception? ex)
    {
        if (ex == null) return;
        
        var message = $"[{source}] {ex.GetType().Name}: {ex.Message}";
        NSLog($"❌ CRASH {message}");
        NSLog($"  StackTrace: {ex.StackTrace}");
        
        if (ex.InnerException != null)
        {
            NSLog($"  InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            NSLog($"  InnerStackTrace: {ex.InnerException.StackTrace}");
        }
        
        SaveCrashLog(source, $"{message}\n{ex.StackTrace}");
    }
    
    private static void SaveCrashLog(string source, string message)
    {
        try
        {
            var logPath = System.IO.Path.Combine(FileSystem.AppDataDirectory, "crash.log");
            var entry = $"{DateTime.UtcNow:O} [{source}] {message}\n";
            System.IO.File.AppendAllText(logPath, entry);
        }
        catch
        {
            // Swallow logging errors
        }
    }
    
    /// <summary>
    /// Helper to log to iOS system log (visible in Console.app and Xcode)
    /// </summary>
    private static void NSLog(string message)
    {
        // NSLog goes to iOS unified logging system
        Foundation.NSString.LocalizedFormat(message);
        Console.WriteLine(message); // This also goes to device console
    }
}
