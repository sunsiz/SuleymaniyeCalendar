using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuleymaniyeCalendar.Models
{
	public class Theme
	{
		// 0 = Dark, 1 = Light, 2 = System
		private const int tema = 2;
		public static int Tema
		{
			get => Preferences.Get(nameof(Tema), tema);
			set => Preferences.Set(nameof(Tema), value);
		}
	}
}
