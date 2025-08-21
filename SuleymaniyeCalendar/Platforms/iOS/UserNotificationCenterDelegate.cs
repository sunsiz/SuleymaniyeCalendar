using ObjCRuntime;
using UserNotifications;

namespace SuleymaniyeCalendar
{
    internal class UserNotificationCenterDelegate : IUNUserNotificationCenterDelegate
    {
        public NativeHandle Handle => throw new NotImplementedException();

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}