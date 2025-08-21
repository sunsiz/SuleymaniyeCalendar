using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuleymaniyeCalendar.Models
{
	public class Prayer
	{
		public string Id { get; set; }
		public string Name { get; set; }
		public string Time { get; set; }
		public string State { get; set; }
		public bool Enabled { get; set; }
	}
}
