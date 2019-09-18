// This service is the main NebliDex program
// It represents the non-UI start point of the codebase

using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace NebliDex_Mobile.Droid
{
    [Service]
    public partial class MainService : Android.App.Service
    {

        // Handle to the linked service to this activity
        public static Android.App.Service NebliDex_Service = null; // This runs all the code
        public static App NebliDex_UI = null;
        public static Activity NebliDex_Activity = null;

        public const int FORSERVICE_NOTIFICATION_ID = 20000;
        public const string MAIN_ACTIVITY_ACTION = "Show_UI";

        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        public override void OnCreate()
        {
            base.OnCreate();
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            if(NebliDex_Service != null) { return StartCommandResult.Sticky; } //Do not reload the program again
            NebliDex_Service = this; // Reference itself so it can be used by other members
            RegisterForegroundService();

            Task.Run(() => Start());  // The program will initialize in another thread

            return StartCommandResult.Sticky;
        }

        private void RegisterForegroundService()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                string CHANNEL_ID = "default";
                var channel = new NotificationChannel(CHANNEL_ID, "Channel", NotificationImportance.Default)
                {
                    Description = "Foreground Service Channel"
                };

                var notificationManager = (NotificationManager)GetSystemService(NotificationService);
                notificationManager.CreateNotificationChannel(channel);

                var notification = new Notification.Builder(this, CHANNEL_ID)
                .SetContentTitle("NebliDex")
                .SetContentText("NebliDex is running")
                .SetSmallIcon(Resource.Drawable.icon)
                .SetContentIntent(BuildIntentToShowNebliDexUI())
                .SetOngoing(true)
                .Build();

                // Enlist this instance of the service as a foreground service
                StartForeground(FORSERVICE_NOTIFICATION_ID, notification);
            }
            else
            {
                var notification = new Notification.Builder(this)
                .SetContentTitle("NebliDex")
                .SetContentText("NebliDex is running")
                .SetSmallIcon(Resource.Drawable.icon)
                .SetContentIntent(BuildIntentToShowNebliDexUI())
                .SetOngoing(true)
                .Build();

                // Enlist this instance of the service as a foreground service
                StartForeground(FORSERVICE_NOTIFICATION_ID, notification);
            }
        }

        PendingIntent BuildIntentToShowNebliDexUI()
        {
            var notificationIntent = new Intent(this, typeof(MainActivity));
            notificationIntent.SetAction(MAIN_ACTIVITY_ACTION);
            notificationIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTask);

            var pendingIntent = PendingIntent.GetActivity(this, 0, notificationIntent, PendingIntentFlags.UpdateCurrent);
            return pendingIntent;
        }

        public override void OnDestroy()
        {
            // Remove the notification from the status bar.
            // And close the service
            var notificationManager = (NotificationManager)GetSystemService(NotificationService);
            notificationManager.Cancel(FORSERVICE_NOTIFICATION_ID);
            StopSelf();
            StopForeground(true);

            base.OnDestroy();
            NebliDex_Service = null;
            ExitProgram();
        }
    }
}