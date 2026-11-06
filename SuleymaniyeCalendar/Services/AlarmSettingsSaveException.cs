namespace SuleymaniyeCalendar.Services;

/// <summary>Reports whether a failed alarm settings save also failed to restore previous alarms.</summary>
public sealed class AlarmSettingsSaveException : Exception
{
    public AlarmSettingsSaveException(bool restoreFailed, Exception innerException)
        : base("Alarm settings could not be saved.", innerException)
    {
        RestoreFailed = restoreFailed;
    }

    public bool RestoreFailed { get; }
}
