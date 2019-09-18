using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;

using Xamarin.Forms;
using NebliDex_Mobile.Droid;

namespace NebliDex_Mobile
{
	public partial class App : Application
	{

        public static Xamarin.Forms.Color green_candle = Xamarin.Forms.Color.FromHex("#63AB1D");
        public static Xamarin.Forms.Color red_candle = Xamarin.Forms.Color.FromHex("#EA0070");
        public static Xamarin.Forms.Color grey_color = Xamarin.Forms.Color.FromHex("#9B9B9B");
        public static Xamarin.Forms.Color blue_color = Xamarin.Forms.Color.FromHex("#09B7FF");
        public static bool force_reload_marketdata = false;

        public App ()
		{
            InitializeComponent();

            MainService.NebliDex_UI = this;

            LoadPage(MainService.current_ui_page);            
		}

        public static void UpdateSplashPageStatus(string msg)
        {
            // This will update the text on the splashpage if present
            if(MainService.NebliDex_UI == null) { return; }
            if(MainService.NebliDex_UI.MainPage is SplashPage)
            {
                SplashPage page = (SplashPage)MainService.NebliDex_UI.MainPage;
                page.ChangeText(msg);
            }
        }

        public void LoadPage(int page_num)
        {
            if(page_num == 0)
            {
                //Splash Screen
                MainPage = new SplashPage();
            }
            else if(page_num == 1)
            {
                //Market Page
                MainPage = new MarketPage();
            }else if(page_num == 2)
            {
                //Orders Page
                MainPage = new OrdersPage();
            }else if(page_num == 3)
            {
                //Wallet Page
                MainPage = new WalletPage();
            }else if(page_num == 4)
            {
                //Settings Page
                MainPage = new SettingsPage();
            }
            Task.Run(() =>
            {
                GC.Collect(); //Run garbage collection
            });
        }

        public static void Exit_Requested()
        {
            Task.Run(() => {
                string msg = "Are you sure you want to exit NebliDex?";

                //Check to see if there are any pending orders that are being matched
                //Since Atomic Swaps are timelocked, it is not advised to leave program when actively involved in swap
                lock (MainService.MyOpenOrderList)
                {
                    for (int i = 0; i < MainService.MyOpenOrderList.Count; i++)
                    {
                        if (MainService.MyOpenOrderList[i].order_stage >= 3 && MainService.MyOpenOrderList[i].is_request == false)
                        {
                            msg = "You are involved in a trade. If you close now, you may lose trade amount. Are you sure you want to exit NebliDex?";
                            break;
                        }
                    }
                }

                if (MainService.CheckPendingTrade() == true)
                {
                    msg = "You are involved in a trade. If you close now, you may lose trade amount. Are you sure you want to exit NebliDex?";
                }

                bool ok = MainService.PromptUser("Confirmation", msg, "Yes", "No");
                if(ok == true)
                {
                    MainService.ExitProgram();
                }
            });
        }

        //Cross page mixed UI and app methods
        public void UpdateBlockrates()
        {
            //Make sure all the Dex connections exists
            bool not_connected = false;
            //contype 1 now represents all electrum connections but different cointypes
            lock (MainService.DexConnectionList)
            {
                bool connnection_exist;
                for (int cit = 1; cit < MainService.total_cointypes; cit++)
                {
                    //Go through all the blockchain types and make sure an electrum connection exists for it, skip Neblio blockchain as it doesn't use electrum
                    if (cit == 6) { continue; } //Etheruem doesn't use dexconnection
                    connnection_exist = false;
                    for (int i = 0; i < MainService.DexConnectionList.Count; i++)
                    {
                        if (MainService.DexConnectionList[i].open == true && MainService.DexConnectionList[i].contype == 1 && MainService.DexConnectionList[i].blockchain_type == cit)
                        {
                            connnection_exist = true;
                            break;
                        }
                    }
                    if (connnection_exist == false)
                    {
                        not_connected = true;
                        break;
                    }
                }
                //Now detect if client is connected to a CN node
                connnection_exist = false;
                for (int i = 0; i < MainService.DexConnectionList.Count; i++)
                {
                    if (MainService.DexConnectionList[i].open == true && MainService.DexConnectionList[i].contype == 3)
                    {
                        connnection_exist = true;
                        break;
                    }
                }
                if (connnection_exist == false)
                {
                    not_connected = true;
                }
            }

            if (not_connected == false && MainService.ntp1downcounter < 2)
            {
                if(MainPage is MarketPage)
                {
                    //The mainpage is of market page type, update the respective fields

                    //Update the block rate status bar based on the market
                    MarketPage page = (MarketPage)MainPage;

                    if(page.Trade_Fee == null) { return; } //Page hasn't fully loaded yet

                    page.CN_Fee.Text = "CN Fee: " + MainService.ndex_fee;

                    int trade_wallet_blockchaintype = MainService.GetWalletBlockchainType(MainService.MarketList[MainService.exchange_market].trade_wallet);
                    int base_wallet_blockchaintype = MainService.GetWalletBlockchainType(MainService.MarketList[MainService.exchange_market].base_wallet);
                    if (trade_wallet_blockchaintype == 0)
                    {
                        page.Trade_Fee.Text = "NEBL Fee: " + String.Format(CultureInfo.InvariantCulture, "{0:0.########}", Math.Round(MainService.blockchain_fee[trade_wallet_blockchaintype], 8)) + "/kb";
                    }
                    else if (trade_wallet_blockchaintype == 6)
                    {
                        page.Trade_Fee.Text = "ETH Fee: " + String.Format(CultureInfo.InvariantCulture, "{0:0.##}", Math.Round(MainService.blockchain_fee[trade_wallet_blockchaintype], 2)) + " Gwei";
                    }
                    else
                    {
                        page.Trade_Fee.Text = MainService.MarketList[MainService.exchange_market].trade_symbol + " Fee: " + String.Format(CultureInfo.InvariantCulture, "{0:0.########}", Math.Round(MainService.blockchain_fee[trade_wallet_blockchaintype], 8)) + "/kb";
                    }

                    if (trade_wallet_blockchaintype != base_wallet_blockchaintype)
                    {
                        //Show both the trade and base fees
                        if (base_wallet_blockchaintype == 0)
                        {
                            page.Base_Fee.Text = "NEBL Fee: " + String.Format(CultureInfo.InvariantCulture, "{0:0.########}", Math.Round(MainService.blockchain_fee[base_wallet_blockchaintype], 8)) + "/kb";
                        }
                        else if (base_wallet_blockchaintype == 6)
                        {
                            //ETH Market
                            page.Base_Fee.Text = "ETH Fee: " + String.Format(CultureInfo.InvariantCulture, "{0:0.########}", Math.Round(MainService.blockchain_fee[base_wallet_blockchaintype], 2)) + " Gwei";
                        }
                        else
                        {
                            page.Base_Fee.Text = MainService.MarketList[MainService.exchange_market].base_symbol + " Fee: " + String.Format(CultureInfo.InvariantCulture, "{0:0.########}", Math.Round(MainService.blockchain_fee[base_wallet_blockchaintype], 8)) + "/kb";
                        }
                    }
                    else
                    {
                        //Only show the trade fee as they use the same blockchaintype
                        page.Base_Fee.Text = "";
                    }
                }else if(MainPage is SettingsPage)
                {
                    SettingsPage page = (SettingsPage)MainPage;
                    if(page.Online_Status == null) { return; } //Page hasn't fully loaded yet
                    page.Online_Status.Text = "Online";
                    page.Online_Status.TextColor = Xamarin.Forms.Color.White;
                }             

            }
            else
            {
                if (MainPage is SettingsPage)
                {
                    SettingsPage page = (SettingsPage)MainPage;
                    if (page.Online_Status == null) { return; } //Page hasn't fully loaded yet
                    page.Online_Status.Text = "Not Fully Connected";
                    page.Online_Status.TextColor = Xamarin.Forms.Color.Red;
                }
            }

        }

        public void AddOrderToView(MainService.OpenOrder ord)
        {
            //This function adds an order the view
            if (ord.market != MainService.exchange_market) { return; } //Not on this market, do not add to view
            if(!(MainPage is MarketPage)) { return; }
            MarketPage page = (MarketPage)MainPage;

            if (ord.type == 0)
            {
                //Buying view
                lock (page.Buying_View_List)
                {
                    page.AddSortedOrderToViewList(page.Buying_View_List, ord, true);
                }
            }
            else if (ord.type == 1)
            {
                lock (page.Selling_View_List)
                {
                    page.AddSortedOrderToViewList(page.Selling_View_List, ord, false);
                }
            }
        }

        public void RemoveOrderFromView(MainService.OpenOrder ord)
        {
            //This function adds an order the view
            if (ord.market != MainService.exchange_market) { return; } //Not on this market, do not need to remove to view
            if (!(MainPage is MarketPage)) { return; }
            MarketPage page = (MarketPage)MainPage;

            if (ord.type == 0)
            {
                //Buying view
                lock (page.Buying_View_List)
                {
                    for (int i = page.Buying_View_List.Count - 1; i >= 0; i--)
                    {
                        MainService.OpenOrder ord2 = page.Buying_View_List[i];
                        if (ord2.order_nonce.Equals(ord.order_nonce) == true)
                        { //Remove matching nonce
                            page.Buying_View_List.RemoveAt(i);
                        }
                    }
                }
            }
            else if (ord.type == 1)
            {
                lock (page.Selling_View_List)
                {
                    for (int i = page.Selling_View_List.Count - 1; i >= 0; i--)
                    {
                        MainService.OpenOrder ord2 = page.Selling_View_List[i];
                        if (ord2.order_nonce.Equals(ord.order_nonce) == true)
                        {
                            page.Selling_View_List.RemoveAt(i);
                        }
                    }
                }
            }
        }

        public static void UpdateOpenOrderList(int market, string order_nonce)
        {
            //This is not ran on UI thread

            //This function will remove 0 sized orders
            //First find the open order from our list
            MainService.OpenOrder ord = null;
            lock (MainService.OpenOrderList[market])
            {
                for (int i = MainService.OpenOrderList[market].Count - 1; i >= 0; i--)
                {
                    if (MainService.OpenOrderList[market][i].order_nonce == order_nonce && MainService.OpenOrderList[market][i].is_request == false)
                    {
                        ord = MainService.OpenOrderList[market][i];
                        if (MainService.OpenOrderList[market][i].amount <= 0)
                        { //Take off the order if its empty now							
                            MainService.OpenOrderList[market].RemoveAt(i); break;
                        }
                    }
                }
            }

            if (ord == null) { return; }

            //Now remove the order from the Market List if present
            if(MainService.NebliDex_UI == null) { return; } //No Forms app present

            if(MainService.NebliDex_UI.MainPage is MarketPage)
            {
                MarketPage page = (MarketPage)MainService.NebliDex_UI.MainPage;
                //Market Page is open, modify its lists
                if(market != MainService.exchange_market) { return; }
                if (ord.type == 0)
                {
                    //Buying view
                    if (ord.amount <= 0)
                    {
                        page.Buying_View_List.Remove(ord);
                    }
                }
                else if (ord.type == 1)
                {
                    if (ord.amount <= 0)
                    {
                        page.Selling_View_List.Remove(ord);
                    }
                }
            }
        }

        public static void AddRecentTradeToView(int market, int type, decimal price, decimal amount, string order_nonce, int time)
        {

            if (amount <= 0) { return; } //Someone is trying to hack the system

            //First check if our open orders matches this recent trade
            MainService.OpenOrder myord = null;
            lock (MainService.MyOpenOrderList)
            {
                for (int i = MainService.MyOpenOrderList.Count - 1; i >= 0; i--)
                {
                    if (MainService.MyOpenOrderList[i].order_nonce == order_nonce)
                    {
                        if (MainService.MyOpenOrderList[i].is_request == false)
                        {
                            myord = MainService.MyOpenOrderList[i];
                            break;
                        }
                        else
                        {
                            //I am requesting this order, should only occur during simultaneous order request
                            //when only one of the orders match. Clear this request and tell user someone else took order
                            MainService.MyOpenOrderList.RemoveAt(i);
                            MainService.ShowTradeMessage("Trade Failed:\nSomeone else matched this order before you!");
                        }
                    }
                }
            }

            if (market != MainService.exchange_market) { return; } //We don't care about recent trades from other markets if not critical node

            //Also modify the open order that is not our order
            lock (MainService.OpenOrderList[market])
            {
                for (int i = MainService.OpenOrderList[market].Count - 1; i >= 0; i--)
                {
                    if (MainService.OpenOrderList[market][i].order_nonce == order_nonce && MainService.OpenOrderList[market][i].is_request == false)
                    {
                        if (MainService.OpenOrderList[market][i] != myord)
                        {
                            //Maker will decrease its own balance separately
                            MainService.OpenOrderList[market][i].amount -= amount; //We already subtracted the amount if my order
                            MainService.OpenOrderList[market][i].amount = Math.Round(MainService.OpenOrderList[market][i].amount, 8);
                        }
                    }
                }
            }

            //This will also calculate the chartlastprice and modify the candle

            MainService.RecentTrade rt = new MainService.RecentTrade();
            rt.amount = amount;
            rt.market = market;
            rt.price = price;
            rt.type = type;
            rt.utctime = time;

            MainService.InsertRecentTradeByTime(rt); //Insert the trade by time

            //First check to see if this time has already passed the current candle
            bool[] updatedcandle = new bool[2];
            if (time < MainService.ChartLastPrice15StartTime)
            {
                //This time is prior to the start of the candle
                updatedcandle[0] = MainService.TryUpdateOldCandle(market, Convert.ToDouble(price), time, 0);
                if (updatedcandle[0] == true)
                {
                    MainService.NebliDexNetLog("Updated a previous 15 minute candle");
                }
                updatedcandle[1] = MainService.TryUpdateOldCandle(market, Convert.ToDouble(price), time, 1);
                if (updatedcandle[1] == true)
                {
                    MainService.NebliDexNetLog("Updated a previous 90 minute candle");
                }
            }

            MainService.LastPriceObject pr = new MainService.LastPriceObject();
            pr.price = price;
            pr.market = market;
            pr.atime = time;
            lock (MainService.ChartLastPrice)
            { //Adding prices is not thread safe
                if (updatedcandle[0] == false)
                {
                    MainService.InsertChartLastPriceByTime(MainService.ChartLastPrice[0], pr);
                }

                if (updatedcandle[1] == false)
                {
                    MainService.InsertChartLastPriceByTime(MainService.ChartLastPrice[1], pr);
                }
            }

            //Update the current candle
            if (MainService.NebliDex_UI == null) { return; } //No Forms app present

            if(market != MainService.exchange_market) { return; }

            if (MainService.NebliDex_UI.MainPage is MarketPage)
            {
                MarketPage page = (MarketPage)MainService.NebliDex_UI.MainPage;

                if (updatedcandle[page.chart_timeline] == false)
                {
                    //This most recent candle hasn't been updated yet
                    //Otherwise run the following code from the UI thread
                    MainService.NebliDex_Activity.RunOnUiThread(() =>
                    {
                        page.UpdateLastCandle(Convert.ToDouble(price));
                    });
                }
                else
                {
                    //Just refresh the view
                    Task.Run(() => page.UpdateCandles());
                }
            }
            UpdateOpenOrderList(market, order_nonce); //This will update the views if necessary and remove the order
        }

        public bool CheckWritePermissions()
        {
            if(Android.Support.V4.Content.ContextCompat.CheckSelfPermission(MainService.NebliDex_Activity,Android.Manifest.Permission.WriteExternalStorage) == Android.Content.PM.Permission.Granted)
            {
                return true;
            }else
            {
                //This will request it if not already granted                
                MainService.NebliDex_Activity.RunOnUiThread(() =>
                {
                    MainService.MessageBox("Notice!", "After you grant permission to export this file data, please try again.", "OK", false);
                    Android.Support.V4.App.ActivityCompat.RequestPermissions(MainService.NebliDex_Activity, new String[] { Android.Manifest.Permission.WriteExternalStorage }, 1);                    
                });                
                return false;
            }
        }

        protected override void OnStart ()
		{
			// Handle when your app starts
		}

		protected override void OnSleep ()
		{
			// Handle when your app sleeps
		}

		protected override void OnResume ()
		{
			// Handle when your app resumes
		}
	}
}
