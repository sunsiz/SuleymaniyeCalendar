using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SuleymaniyeCalendar.Models
{
	public partial class Prayer : ObservableObject
	{
		[ObservableProperty]
		private string id;

		[ObservableProperty]
		private string name;

		[ObservableProperty]
		private string time;

		[ObservableProperty]
		private string state;

		[ObservableProperty]
		private bool enabled;

		// Enhanced visual state properties for better UI/UX
		[ObservableProperty]
		private string stateDescription;

		[ObservableProperty]
		private bool isActive;

		[ObservableProperty]
		private bool isPast;

		[ObservableProperty]
		private bool isUpcoming;

		[ObservableProperty]
		private double opacity = 1.0;

		// Default constructor
		public Prayer()
		{
			Id = string.Empty;
			Name = string.Empty;
			Time = string.Empty;
			State = string.Empty;
			StateDescription = string.Empty;
		}

		// Helper method to update visual state based on prayer timing
		public void UpdateVisualState()
		{
			switch (State?.ToLower())
			{
				case "happening":
				case "current":
					IsActive = true;
					IsPast = false;
					IsUpcoming = false;
					Opacity = 1.0;
					StateDescription = "Current";
					break;

				case "passed":
				case "completed":
					IsActive = false;
					IsPast = true;
					IsUpcoming = false;
					Opacity = 0.9;
					StateDescription = "Passed";
					break;

				case "waiting":
				case "upcoming":
				case "next":
					IsActive = false;
					IsPast = false;
					IsUpcoming = true;
					Opacity = 1.0;
					StateDescription = "Upcoming";
					break;

				default:
					IsActive = false;
					IsPast = false;
					IsUpcoming = false;
					Opacity = 1.0;
					StateDescription = "";
					break;
			}
		}
	}
}
