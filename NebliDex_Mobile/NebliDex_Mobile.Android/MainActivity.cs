using System;
using System.Threading.Tasks;

using Android.App;
using Android.Content.PM;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

namespace NebliDex_Mobile.Droid
{
	[Activity (Label = "NebliDex",LaunchMode = LaunchMode.SingleTop, Theme="@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
	public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
	{

		protected override void OnCreate (Bundle bundle)
		{
			TabLayoutResource = Resource.Layout.Tabbar;
			ToolbarResource = Resource.Layout.Toolbar; 

			base.OnCreate (bundle);

            MainService.NebliDex_Activity = this;

			global::Xamarin.Forms.Forms.Init (this, bundle); // Xamarin forms is the UI for the application, the NebliDex service handles all the logic
            // Xamarin forms is destroyed everytime the activity is destroyed
            LoadApplication (new NebliDex_Mobile.App());

            if (MainService.NebliDex_Service == null)
            {
                //Start the service if it hasn't started yet
                var startIntent = new Intent(this, typeof(MainService));

                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
                {   // if API >= 26
                    StartForegroundService(startIntent);
                }
                else
                {
                    // if API <= 25
                    StartService(startIntent);
                }
            }
		}

        protected override void OnResume()
        {
            base.OnResume();
            //Change the screen to the settings if there is a gap between the current time and last period time
            if(MainService.LastNetworkQueryTime > 0)
            {
                int c_time = MainService.UTCTime();
                if(c_time - MainService.LastNetworkQueryTime > 45)
                {
                    //Force run a periodic query
                    Task.Run(() =>
                    {
                        MainService.PeriodicNetworkQuery(null);
                    });
                }
            }
        }

        protected override void OnDestroy()
        {
            // The activity has been destroyed, but the service may still be running
            base.OnDestroy();
            MainService.NebliDex_UI = null;
            MainService.NebliDex_Activity = null;
            if(MainService.program_loaded == false)
            {
                //The program hasn't fully loaded yet, close it completely
                MainService.ExitProgram();
            }            
        }
    }
}

