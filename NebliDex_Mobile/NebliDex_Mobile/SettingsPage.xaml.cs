using System;
using System.Net;
using System.Runtime;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using Plugin.FilePicker;

using NebliDex_Mobile.Droid;

namespace NebliDex_Mobile
{
	public partial class SettingsPage : ContentPage
	{

        public Label Online_Status = null;

		public SettingsPage ()
		{
			InitializeComponent ();

            Version_Number.Text = MainService.version_text;
            Online_Status = priv_Online_Status;
            if(MainService.my_wallet_pass.Length > 0)
            {
                //Wallet is already encrypted
                Toggle_Encryption_Label.Text = "Decrypt Wallet";
            }

            //Force run a periodic query
            Task.Run(() =>
            {
                MainService.PeriodicNetworkQuery(null); //This will also update the online status
            });
        }

        //Events
        //Most methods ran off UI thread
        private void Toggle_Wallet_Encryption(object sender, EventArgs e)
        {
            
            Task.Run(() =>
            {
                //This will bring up the prompt to encrypt or decrypt the wallet
                if (MainService.my_wallet_pass.Length > 0)
                {
                    //Encryption is present
                    string resp = MainService.UserPrompt("Please enter your wallet password\nto decrypt wallet.", "OK", "Cancel", true);
                    if (resp.Equals(MainService.my_wallet_pass) == false)
                    {
                        MainService.MessageBox("Notice!","You've entered an incorrect password.","OK",false);
                    }
                    else
                    {
                        MainService.DecryptWalletKeys();
                        MainService.my_wallet_pass = "";
                        MainService.MessageBox("Notice!", "Your wallet has been fully decrypted.", "OK", false);

                        //Run on UI thread
                        MainService.NebliDex_Activity.RunOnUiThread(() =>
                        {
                            Toggle_Encryption_Label.Text = "Encrypt Wallet";
                        });
                        
                    }
                }
                else
                {
                    string pass1 = MainService.UserPrompt("Please enter a new password\nto encrypt your wallet.", "OK", "Cancel", true);
                    if (pass1.Length > 0)
                    {
                        string pass2 = "";
                        while (pass2 != pass1)
                        {
                            pass2 = "";
                            pass2 = MainService.UserPrompt("For confirmation, please re-enter previously entered password. Do not lose this password. There is no option to recover it!", "OK", "Cancel", true);
                            if (pass2 != pass1 && pass2.Length > 0)
                            {
                                MainService.MessageBox("Notice", "The password doesn't match the previously entered.", "OK", true);
                            }
                            else if (pass2.Length == 0)
                            {
                                break; //User doesn't want a password anymore
                            }
                        }
                        if (pass2.Length > 0)
                        {
                            MainService.my_wallet_pass = pass2;
                            MainService.EncryptWalletKeys();

                            //Run on UI thread
                            MainService.NebliDex_Activity.RunOnUiThread(() =>
                            {
                                Toggle_Encryption_Label.Text = "Decrypt Wallet";
                            });
                            
                        }
                    }
                }
            });
        }

        private void Backup_Wallet(object sender, EventArgs e)
        {
            //This will create a clone of the wallet to an external (outside app) location
            string external_path = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
            if (external_path == null) { return; }
            string wallet_name = "account_backup";
            int num = 0;
            while (true)
            {                
                string filename;
                if (num == 0)
                {
                    filename = wallet_name + ".dat";
                }else
                {
                    filename = wallet_name + "_" + num + ".dat";
                }
                string thePath = Path.Combine(external_path, filename);
                num++;
                if(File.Exists(thePath) == false)
                {
                    //Copy to the new path
                    try
                    {
                        File.Copy(MainService.App_Path + "/account.dat", thePath);
                        MainService.MessageBox("Notice!", "Saved account backup to Downloads as: "+filename, "OK", false);
                    }
                    catch (Exception ex)
                    {
                        MainService.NebliDexNetLog("Failed to create wallet backup, error: "+ex.ToString());
                        MainService.MessageBox("Notice!", "Failed to create wallet backup", "OK", false);
                    }
                    break;
                }
            }
        }

        private void Import_Wallet(object sender, EventArgs e)
        {

            Task.Run(async () =>
            {

                if (MainService.MyOpenOrderList.Count > 0)
                {
                    MainService.MessageBox("Notice!", "Cannot load wallet with open orders present.", "OK", false);
                    return;
                }

                if (MainService.CheckPendingPayment() == true)
                {
                    MainService.MessageBox("Notice", "There is at least one pending payment to this current address.", "OK", false);
                    return;
                }

                bool moveable = true;
                for (int i = 0; i < MainService.WalletList.Count; i++)
                {
                    if (MainService.WalletList[i].status != 0)
                    {
                        moveable = false; break;
                    }
                }
                if (moveable == false)
                {
                    MainService.MessageBox("Notice!", "There is at least one wallet unavailable to change the current address", "OK", false);
                    return;
                }

                bool result = MainService.PromptUser("Confirmation", "Importing a previous NebliDex wallet will replace the current wallet Do you want to continue?", "Yes", "No");
                if (result == false)
                {
                    return;
                }

                //File Picker Dialog
                try
                {
                    //May need to run on UI thread
                    Plugin.FilePicker.Abstractions.FileData fileData = await CrossFilePicker.Current.PickFile();
                    if (fileData == null)
                    {
                        MainService.NebliDexNetLog("User canceled loading backup from file");
                        return; // user canceled file picking
                    }

                    if (fileData.FilePath == null)
                    {
                        MainService.MessageBox("Notice!", "Unable to locate this file", "OK", false);
                        return;
                    }
                    if(File.Exists(fileData.FilePath) == false)
                    {
                        MainService.MessageBox("Notice!", "Unable to locate this file", "OK", false);
                        return;
                    }

                    if (File.Exists(MainService.App_Path + "/account_old.dat") != false)
                    {
                        File.Delete(MainService.App_Path + "/account_old.dat");
                    }
                    //Move the account.dat to the new location (old file) until the copy is complete
                    File.Move(MainService.App_Path + "/account.dat", MainService.App_Path + "/account_old.dat");
                    if (File.Exists(fileData.FilePath) == false)
                    {
                        //Revert the changes
                        File.Move(MainService.App_Path + "/account_old.dat", MainService.App_Path + "/account.dat");
                        MainService.MessageBox("Notice!", "Unable to import this wallet location.", "OK", false);
                        return;
                    }
                    File.Copy(fileData.FilePath, MainService.App_Path + "/account.dat");
                    MainService.my_wallet_pass = ""; //Remove the wallet password
                    MainService.CheckWallet(); //Ran inline and load password if necessary
                    MainService.LoadWallet();
                    //Now delete the old wallet
                    File.Delete(MainService.App_Path + "/account_old.dat");
                    MainService.MessageBox("Notice!", "Imported the wallet successfully.", "OK", false);
                    MainService.NebliDex_Activity.RunOnUiThread(() =>
                    {
                        if(MainService.my_wallet_pass.Length > 0)
                        {
                            Toggle_Encryption_Label.Text = "Decrypt Wallet";
                        }else
                        {
                            Toggle_Encryption_Label.Text = "Encrypt Wallet";
                        }
                        
                    });
                }
                catch (Exception ex)
                {
                    MainService.NebliDexNetLog("Failed to load wallet, error: "+ex.ToString());
                    MainService.MessageBox("Notice!", "Failed to load imported NebliDex wallet.", "OK", false);
                    try
                    {
                        //Revert the file back to what is was
                        if (File.Exists(MainService.App_Path + "/account_old.dat") != false)
                        {
                            //Move the old wallet back to the current position
                            if (File.Exists(MainService.App_Path + "/account.dat") != false)
                            {
                                File.Delete(MainService.App_Path + "/account.dat");
                            }
                            File.Move(MainService.App_Path + "/account_old.dat", MainService.App_Path + "/account.dat");
                        }
                    }
                    catch (Exception) { }
                }
            });               
        }

        private void Change_Wallet_Address(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                //This will request a change to all the addresses
                if (MainService.MyOpenOrderList.Count > 0)
                {
                    MainService.MessageBox("Notice!","Cannot change addresses with open orders present.","OK",false);
                    return;
                }

                bool result = MainService.PromptUser("Confirmation","Are you sure you want to change all your wallet addresses?", "OK","Cancel");
                if (result == true)
                {
                    MainService.ChangeWalletAddresses();
                    MainService.MessageBox("Notice!", "Wallet addresses have been changed to new ones.", "OK", false);
                }
            });
        }

        private void Export_Trade_Data(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                //This will export the trade history to an external folder
                string external_path = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
                if (external_path == null) { return; }
                string wallet_name = "neblidex_tradehistory";
                int num = 0;
                while (true)
                {
                    string filename;
                    if (num == 0)
                    {
                        filename = wallet_name + ".csv";
                    }
                    else
                    {
                        filename = wallet_name + "_" + num + ".csv";
                    }
                    string thePath = Path.Combine(external_path, filename);
                    num++;
                    if (File.Exists(thePath) == false)
                    {
                        //Copy to the new path
                        try
                        {
                            MainService.ExportTradeHistory(thePath);
                            MainService.MessageBox("Notice!", "Exported trade history to Downloads as: " + filename, "OK", false);
                        }
                        catch (Exception ex)
                        {
                            MainService.MessageBox("Notice!", "Failed to export trade history", "OK", false);
                            MainService.NebliDexNetLog("Failed to export trade history, error: "+ex.ToString());
                        }
                        break;
                    }
                }
            });
        }

        private void Change_DNS_Seed(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                string user_seed = MainService.UserPrompt("Enter A New DNS Seed (URL or IP)", "OK", "Cancel", false, MainService.DNS_SEED);
                if (user_seed.Length > 0 && user_seed.Equals(MainService.DNS_SEED) == false)
                {
                    MainService.DNS_SEED = user_seed.Trim();
                    MainService.DNS_SEED_TYPE = 0;
                    IPAddress ip_result;
                    if (IPAddress.TryParse(MainService.DNS_SEED, out ip_result))
                    {
                        MainService.DNS_SEED_TYPE = 1; //This is an IP address
                    }
                    File.Delete(MainService.App_Path + "/cn_list.dat"); //Delete the old list, then reload from the new seed
                    MainService.FindCNServers(true);
                }
            });
        }

        private void Clear_Electrum_List(object sender, EventArgs e)
        {
            File.Delete(MainService.App_Path + "/electrum_peers.dat");
            MainService.MessageBox("Notice!", "Cleared Electrum List", "OK", false);
        }

        private void Clear_CN_List(object sender, EventArgs e)
        {
            File.Delete(MainService.App_Path + "/cn_list.dat");
            MainService.MessageBox("Notice!", "Cleared CN List", "OK", false);
        }

        private void Export_Debug_Log(object sender, EventArgs e)
        {
            //This will create a clone of the wallet to an external (outside app) location
            string external_path = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
            if (external_path == null) { return; }
            string wallet_name = "neblidex_debug";
            int num = 0;
            while (true)
            {
                string filename;
                if (num == 0)
                {
                    filename = wallet_name + ".log";
                }
                else
                {
                    filename = wallet_name + "_" + num + ".log";
                }
                string thePath = Path.Combine(external_path, filename);
                num++;
                if (File.Exists(thePath) == false)
                {
                    //Copy to the new path
                    try
                    {
                        File.Copy(MainService.App_Path + "/debug.log", thePath);
                        MainService.MessageBox("Notice!", "Saved debug log to Downloads as: " + filename, "OK", false);
                    }
                    catch (Exception)
                    {
                        MainService.NebliDexNetLog("Failed to copy debug log to downloads");
                        MainService.MessageBox("Notice!", "Failed to export debug log to external storage", "OK", false);
                    }
                    break;
                }
            }
        }

        //Menu Events
        private void GoToMarketPage(object sender, EventArgs e)
        {
            MainService.current_ui_page = 1;
            MainService.NebliDex_UI.LoadPage(MainService.current_ui_page);
        }

        private void GoToOrdersPage(object sender, EventArgs e)
        {
            MainService.current_ui_page = 2;
            MainService.NebliDex_UI.LoadPage(MainService.current_ui_page);
        }

        private void GoToWalletPage(object sender, EventArgs e)
        {
            MainService.current_ui_page = 3;
            MainService.NebliDex_UI.LoadPage(MainService.current_ui_page);
        }

        private void Exit_Touched(object sender, EventArgs e)
        {
            App.Exit_Requested();
        }
    }
}