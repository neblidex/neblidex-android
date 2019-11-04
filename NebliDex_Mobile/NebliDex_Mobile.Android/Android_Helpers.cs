//Android Helper for PromptUser (for user permission), MessageBox (for notification), UserBox (for input)
//Toast (for notification)

using Android.App;
using System.Threading;
using System.Threading.Tasks;
using Android.Widget;

namespace NebliDex_Mobile.Droid
{
    public partial class MainService : Android.App.Service
    {
        public static void MessageBox(string title, string message, string okstring, bool block)
        {
            // Create an Android message box with an option to block the calling thread
            if(NebliDex_Activity == null) { return; } //Service running in background
            ManualResetEvent trigger = null;
            if(block == true)
            {
                trigger = new ManualResetEvent(false);
            }

            NebliDex_Activity.RunOnUiThread(() => {
                Android.App.AlertDialog.Builder dialog = new AlertDialog.Builder(NebliDex_Activity);
                AlertDialog alert = dialog.Create();
                alert.SetTitle(title);
                alert.SetMessage(message);            
                alert.SetButton(okstring, (c, ev) =>
                {
                   //Nothing happens when the button is pressed
                   if(block == true)
                    {
                        trigger.Set();
                    }
                });          
                alert.Show(); //Show the message box
            });          
            
            if(block == true) //Wait until user response if required
            {
                trigger.WaitOne();
            } 
        }

        public static bool PromptUser(string title, string message, string okstring, string nostring)
        {
            // Create a prompt user, this will block the current thread until the alert builder returns
            // This prompt user cannot be ran in the UI thread
            if (NebliDex_Activity == null) { return false; } //Service running in background
            bool response = false;
            ManualResetEvent trigger = new ManualResetEvent(false);

            NebliDex_Activity.RunOnUiThread(() => {
                Android.App.AlertDialog.Builder dialog = new AlertDialog.Builder(NebliDex_Activity);
                AlertDialog alert = dialog.Create();
                alert.SetTitle(title);
                alert.SetMessage(message);
            
                alert.SetButton(okstring, (c, ev) =>
                {
                    response = true;
                    trigger.Set();
                });
                alert.SetButton2(nostring, (c, ev) =>
                {
                    //Nothing happens when the button is pressed
                    response = false;
                    trigger.Set();
                });          
                alert.Show(); //Show the message box
            });
            trigger.WaitOne(); // This thread will block until it is triggered (advised to call this method with task.run)
            return response;
        }

        public static void ShowToastMessage(string msg)
        {
            if (NebliDex_Activity == null) { return; } //Service running in background
            NebliDex_Activity.RunOnUiThread(() => {
                Toast.MakeText(NebliDex_Activity, msg, ToastLength.Long).Show(); //Show a short android message
            });           
        }

        public static string UserPrompt(string ques,string okstring,string nostring,bool password,string default_content="")
        {
            //This method will block until the user has completed the prompt
            if (NebliDex_Activity == null) { return ""; } //Service running in background
            string user_response = "";
            ManualResetEvent trigger = new ManualResetEvent(false);

            NebliDex_Activity.RunOnUiThread(() => {
                Android.App.AlertDialog.Builder dialog = new AlertDialog.Builder(NebliDex_Activity);
                dialog.SetTitle("Response Required");
                dialog.SetMessage(ques);           

                EditText input_box = new EditText(NebliDex_Activity);
                if(password == true)
                {
                    input_box.InputType = Android.Text.InputTypes.TextVariationPassword | Android.Text.InputTypes.ClassText;
                }else
                {
                    input_box.InputType = Android.Text.InputTypes.TextVariationNormal | Android.Text.InputTypes.ClassText;
                }
                input_box.Text = default_content;

                dialog.SetView(input_box);
                dialog.SetPositiveButton(okstring, (c, ev) =>
                {
                    user_response = input_box.Text;
                    trigger.Set();
                });
                dialog.SetNegativeButton(nostring, (c, ev) =>
                {
                    trigger.Set();
                });
                dialog.Show();
            });

            trigger.WaitOne(); // This thread will block until it is triggered (advised to call this method with task.run)
            return user_response.Trim();
        }

        public static void ExitProgram()
        {
            //This will close the program immediately
            if(MainService.NebliDex_Service != null)
            {
                //And stop the service
                MainService.NebliDex_Service.StopForeground(true);
                MainService.NebliDex_Service.StopSelf();
                MainService.NebliDex_Service = null;
            }
            Java.Lang.JavaSystem.Exit(0); //Force program to close immediately
            
        }
    }
}