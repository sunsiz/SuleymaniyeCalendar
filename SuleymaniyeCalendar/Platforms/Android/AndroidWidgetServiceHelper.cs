using Android.Content;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.Platforms.Android;

public class AndroidWidgetServiceHelper : IWidgetService
{
    public void UpdateWidget()
    {
        try
        {
            var context = Platform.CurrentActivity ?? global::Android.App.Application.Context;
            context?.StartService(new Intent(context, typeof(WidgetService)));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating widget: {ex.Message}");
        }
    }
}
