using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuleymaniyeCalendar.Models
{
	public class Sound
	{
		public Sound(string fileName, string name)
		{
			FileName = fileName;
			Name = name;
		}
		
		public string FileName { get; set; }
		public string Name { get; set; }

		public override string ToString()
		{
			// Allow Picker to display item text without ItemDisplayBinding
			return Name ?? base.ToString();
		}
	}
}
