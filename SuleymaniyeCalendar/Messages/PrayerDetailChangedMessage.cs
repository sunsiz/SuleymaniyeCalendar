using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SuleymaniyeCalendar.Messages
{
    // Carries the PrayerId that changed
    public sealed class PrayerDetailChangedMessage : ValueChangedMessage<string>
    {
        public PrayerDetailChangedMessage(string prayerId) : base(prayerId) { }
    }
}
