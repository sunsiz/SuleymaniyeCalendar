using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace SuleymaniyeCalendar.Views
{
    public partial class ModernDialog : ContentView
    {
        private TaskCompletionSource<bool> _tcs;

        public ModernDialog()
        {
            InitializeComponent();
            this.IsVisible = false;
        }

        public Task<bool> ShowAsync(string title, string message, string primaryText = "OK", string secondaryText = null)
        {
            DialogTitle.Text = title;
            DialogMessage.Text = message;
            PrimaryButton.Text = primaryText;
            SecondaryButton.IsVisible = !string.IsNullOrEmpty(secondaryText);
            SecondaryButton.Text = secondaryText ?? string.Empty;
            this.IsVisible = true;
            _tcs = new TaskCompletionSource<bool>();
            return _tcs.Task;
        }

        private void OnPrimaryClicked(object sender, EventArgs e)
        {
            this.IsVisible = false;
            _tcs?.TrySetResult(true);
        }

        private void OnSecondaryClicked(object sender, EventArgs e)
        {
            this.IsVisible = false;
            _tcs?.TrySetResult(false);
        }
    }
}
