using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuleymaniyeCalendar.Models
{
	public class Language
	{
		public Language(string name, string ci)
		{
			Name = name;
			CI = ci;
		}
		public string Name { get; set; }
		public string CI { get; set; }
	}
}
