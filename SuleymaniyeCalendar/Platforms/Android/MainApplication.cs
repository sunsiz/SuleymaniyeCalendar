using Android.App;
using Android.Runtime;

namespace SuleymaniyeCalendar;

[Application]
public class MainApplication : MauiApplication
{
	/// <summary>
	/// Known non-critical Java exception patterns that should be caught instead of crashing.
	/// Reviewed for .NET 10 MAUI — only patterns still observed on OEM devices are kept.
	/// </summary>
	private static readonly string[] NonCriticalExceptionPatterns =
	[
		"RequestManagerRetriever",  // Glide image loading race on Activity destroy
		"dispatchDraw"             // Rare OEM-specific rendering race
	];

	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
		AndroidEnvironment.UnhandledExceptionRaiser += OnAndroidUnhandledException;
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	/// <summary>
	/// Catches Java-side exceptions and suppresses known non-critical MAUI rendering crashes
	/// to prevent unnecessary app termination. Critical exceptions are still propagated.
	/// </summary>
	private static void OnAndroidUnhandledException(object? sender, RaiseThrowableEventArgs e)
	{
		var exceptionMessage = e.Exception?.ToString() ?? string.Empty;
		var isNonCritical = false;

		foreach (var pattern in NonCriticalExceptionPatterns)
		{
			if (exceptionMessage.Contains(pattern, StringComparison.OrdinalIgnoreCase))
			{
				isNonCritical = true;
				break;
			}
		}

		if (isNonCritical)
		{
			System.Diagnostics.Debug.WriteLine($"[CrashGuard] Suppressed non-critical exception: {exceptionMessage}");

			try
				{
					var logPath = System.IO.Path.Combine(FileSystem.AppDataDirectory, "suppressed_crashes.log");

					// Rotate log if it exceeds 100 KB to prevent unbounded growth
					if (System.IO.File.Exists(logPath))
					{
						var info = new System.IO.FileInfo(logPath);
						if (info.Length > 100 * 1024)
							System.IO.File.WriteAllText(logPath, $"[Log rotated at {DateTime.UtcNow:O}]{Environment.NewLine}");
					}

					System.IO.File.AppendAllText(logPath,
						$"{DateTime.UtcNow:O} [Suppressed] {exceptionMessage}{Environment.NewLine}");
				}
			catch
			{
				// Swallow logging errors
			}

			e.Handled = true;
		}
		else
		{
			System.Diagnostics.Debug.WriteLine($"[CrashGuard] Propagating critical exception: {exceptionMessage}");
		}
	}
}
