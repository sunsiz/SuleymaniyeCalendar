namespace SuleymaniyeCalendar.Services
{
	public sealed class NullAlarmService : IAlarmService
	{
		public void CancelAlarm() { }
		public void SetAlarm(DateTime date, TimeSpan triggerTimeSpan, int timeOffset, string name) { }
		public void StartAlarmForegroundService() { }
		public void StopAlarmForegroundService() { }
	}
}
