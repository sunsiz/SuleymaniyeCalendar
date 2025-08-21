using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuleymaniyeCalendar.Models
{
	public class Calendar
	{
		public double Latitude { get; set; }

		public double Longitude { get; set; }

		public double Altitude { get; set; }

		public double TimeZone { get; set; }

		public double DayLightSaving { get; set; }

		public string FalseFajr { get; set; }

		public string Fajr { get; set; }

		public string Sunrise { get; set; }

		public string Dhuhr { get; set; }

		public string Asr { get; set; }

		public string Maghrib { get; set; }

		public string Isha { get; set; }

		public string EndOfIsha { get; set; }
		public string Date { get; set; }
	}
}
