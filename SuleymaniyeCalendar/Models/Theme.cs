using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuleymaniyeCalendar.Models
{
	public class Theme
	{
		//0 is dark, 1 is light
		private const int tema = 1;
		public static int Tema
		{
			get => Preferences.Get(nameof(Tema), tema);
			set => Preferences.Set(nameof(Tema), value);
		}
	}
}
