using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuleymaniyeCalendar.Services
{
	public interface IAlarmService
	{
		void SetAlarm(DateTime date, TimeSpan triggerTimeSpan, int timeOffset, string name);
		void CancelAlarm();
		void StartAlarmForegroundService();
		void StopAlarmForegroundService();
	}
}
