using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace SuleymaniyeCalendar.Models
{
	public class Language
	{
		public Language(string name, string ci)
		{
			Name = name;
			CI = ci;
			IsRtl = DetermineIfRtl(ci);
			FlowDirection = IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
		}
		
		public string Name { get; set; }
		public string CI { get; set; }
		public bool IsRtl { get; set; }
		public FlowDirection FlowDirection { get; set; }
		
		private static bool DetermineIfRtl(string languageCode)
		{
			var rtlLanguages = new HashSet<string> { "ar", "fa", "he", "ku", "ps", "sd", "ug", "ur", "yi" };
			var langCode = languageCode?.Split('-')[0].ToLower();
			return !string.IsNullOrEmpty(langCode) && rtlLanguages.Contains(langCode);
		}
	}
}
