using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SuleymaniyeCalendar.Models
{
	// AOT-safe observable model: use explicit properties with SetProperty instead of [ObservableProperty]
	public partial class Prayer : ObservableObject
	{
		private string id = string.Empty;
		public string Id
		{
			get => id;
			set => SetProperty(ref id, value);
		}

		private string name = string.Empty;
		public string Name
		{
			get => name;
			set => SetProperty(ref name, value);
		}

		private string time = string.Empty;
		public string Time
		{
			get => time;
			set => SetProperty(ref time, value);
		}

		private string state = string.Empty;
		public string State
		{
			get => state;
			set => SetProperty(ref state, value);
		}

		private bool enabled;
		public bool Enabled
		{
			get => enabled;
			set => SetProperty(ref enabled, value);
		}

		// Enhanced visual state properties for better UI/UX
		private string stateDescription = string.Empty;
		public string StateDescription
		{
			get => stateDescription;
			set => SetProperty(ref stateDescription, value);
		}

		private bool isActive;
		public bool IsActive
		{
			get => isActive;
			set => SetProperty(ref isActive, value);
		}

		private bool isPast;
		public bool IsPast
		{
			get => isPast;
			set => SetProperty(ref isPast, value);
		}

		private bool isUpcoming;
		public bool IsUpcoming
		{
			get => isUpcoming;
			set => SetProperty(ref isUpcoming, value);
		}

		private double opacity = 1.0;
		public double Opacity
		{
			get => opacity;
			set => SetProperty(ref opacity, value);
		}

		// Default constructor
		public Prayer()
		{
			// Defaults already set via field initializers
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
					StateDescription = string.Empty;
					break;
			}
		}
	}
}
