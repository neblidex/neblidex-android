using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using NebliDex_Mobile.Droid;

namespace NebliDex_Mobile
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class MatchOrder : ContentPage
	{
        public MainService.OpenOrder window_order;
        public decimal min_ord;
        bool input_updating = false; //Prevents events from firing from another event

        public MatchOrder (MainService.OpenOrder ord)
		{
			InitializeComponent ();

            window_order = ord;

            string trade_symbol = MainService.MarketList[MainService.exchange_market].trade_symbol;
            string base_symbol = MainService.MarketList[MainService.exchange_market].base_symbol;

            min_ord = ord.minimum_amount;
            if (min_ord > ord.amount)
            {
                min_ord = ord.amount;
            }

            if (ord.type == 0)
            {
                //Buy Order
                Header.Text = "Buy Order Details";
                Order_Type.Text = "Requesting:";
                Order_Amount.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", ord.amount);
                Order_Min_Amount.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", min_ord);
                Price.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", ord.price);
                decimal my_balance = MainService.GetMarketBalance(MainService.exchange_market, 1); //If they are buying, we are selling

                Order_Amount.Text += " " + trade_symbol;
                Order_Min_Amount.Text += " " + trade_symbol;
                Price.Text += " " + base_symbol;
                My_Amount_Header.Text = "Amount (" + trade_symbol + "):";
                Total_Cost_Header.Text = "Total Receive (" + base_symbol + "):";
                My_Balance.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", my_balance) + " " + trade_symbol;
                Match_Button.BackgroundColor = Xamarin.Forms.Color.FromHex("#63AB1D");
            }
            else
            {
                //Sell Order
                Header.Text = "Sell Order Details";
                Order_Type.Text = "Available:";
                Order_Amount.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", ord.amount);
                Order_Min_Amount.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", min_ord);
                Price.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", ord.price);
                decimal my_balance = MainService.GetMarketBalance(MainService.exchange_market, 0); //If they are selling, we are buying

                Order_Amount.Text += " " + trade_symbol;
                Order_Min_Amount.Text += " " + trade_symbol;
                Price.Text += " " + base_symbol;
                My_Amount_Header.Text = "Amount (" + trade_symbol + "):";
                Total_Cost_Header.Text = "Total Cost (" + base_symbol + "):";
                My_Balance.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", my_balance) + " " + base_symbol;
                Match_Button.BackgroundColor = Xamarin.Forms.Color.FromHex("#EA0070");
            }
        }

        //Events
        private void Amount_KeyUp(object sender, TextChangedEventArgs e)
        {
            if(input_updating == true) { return; }
            try
            {
                input_updating = true;
                Total_Amount.Text = "";
                if (MainService.IsNumber(My_Amount.Text) == false) { return; }
                decimal amount = decimal.Parse(My_Amount.Text, CultureInfo.InvariantCulture);
                if (amount <= 0) { return; }
                Total_Amount.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", (amount * window_order.price));
            }
            finally
            {
                input_updating = false;
            }
        }

        private void Total_KeyUp(object sender, TextChangedEventArgs e)
        {
            if (input_updating == true) { return; }
            try
            {
                input_updating = true;
                My_Amount.Text = "";
                if (MainService.IsNumber(Total_Amount.Text) == false) { return; }
                decimal total = decimal.Parse(Total_Amount.Text, CultureInfo.InvariantCulture);
                if (total <= 0) { return; }
                My_Amount.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", (total / window_order.price));
            }
            finally
            {
                input_updating = false;
            }

        }

        private void Match_Order(object sender, EventArgs e)
        {

            if (My_Amount.Text.Length == 0 || Total_Amount.Text.Length == 0) { return; }
            if (My_Amount.Text.IndexOf(",") >= 0)
            {
                MainService.MessageBox("Notice!","NebliDex does not recognize commas for decimals at this time.", "OK",false);
                return;
            }
            if (Total_Amount.Text.IndexOf(",") >= 0)
            {
                MainService.MessageBox("Notice!","NebliDex does not recognize commas for decimals at this time.", "OK",false);
                return;
            }
            decimal amount = decimal.Parse(My_Amount.Text, CultureInfo.InvariantCulture);
            decimal total = decimal.Parse(Total_Amount.Text, CultureInfo.InvariantCulture);
            if (amount < min_ord)
            {
                //Cannot be less than the minimum order
                MainService.MessageBox("Notice!","Amount cannot be less than the minimum match.", "OK", false);
                return;
            }
            if (amount > window_order.amount)
            {
                //Cannot be greater than request
                MainService.MessageBox("Notice!", "Amount cannot be greater than the order.", "OK", false);
                return;
            }

            if (MainService.MarketList[MainService.exchange_market].base_wallet == 3 || MainService.MarketList[MainService.exchange_market].trade_wallet == 3)
            {
                //Make sure amount is greater than ndexfee x 2
                if (amount < MainService.ndex_fee * 2)
                {
                    MainService.MessageBox("Notice!", "This order amount is too small. Must be at least twice the CN fee (" + String.Format(CultureInfo.InvariantCulture, "{0:0.########}", MainService.ndex_fee * 2) + " NDEX)", "OK", false);
                    return;
                }
            }

            string msg = "";
            decimal mybalance = 0;
            int mywallet = 0;
            //Now check the balances
            if (window_order.type == 1)
            { //They are selling, so we are buying
                mybalance = total; //Base pair balance
                mywallet = MainService.MarketList[window_order.market].base_wallet; //This is the base pair wallet
            }
            else
            { //They are buying so we are selling
                mybalance = amount; //Base pair balance
                mywallet = MainService.MarketList[window_order.market].trade_wallet; //This is the trade pair wallet				
            }
            bool good = MainService.CheckWalletBalance(mywallet, mybalance, ref msg);
            if (good == true)
            {
                //Now check the fees
                good = MainService.CheckMarketFees(MainService.exchange_market, 1 - window_order.type, mybalance, ref msg, true);
            }

            if (good == false)
            {
                //Not enough funds or wallet unavailable
                MainService.MessageBox("Notice!", msg, "OK", false);
                return;
            }

            //Make sure that total is greater than blockrate for the base market and the amount is greater than blockrate for trade market
            decimal block_fee1 = 0;
            decimal block_fee2 = 0;
            int trade_wallet_blockchaintype = MainService.GetWalletBlockchainType(MainService.MarketList[MainService.exchange_market].trade_wallet);
            int base_wallet_blockchaintype = MainService.GetWalletBlockchainType(MainService.MarketList[MainService.exchange_market].base_wallet);
            block_fee1 = MainService.blockchain_fee[trade_wallet_blockchaintype];
            block_fee2 = MainService.blockchain_fee[base_wallet_blockchaintype];

            //Now calculate the totals for ethereum blockchain
            if (trade_wallet_blockchaintype == 6)
            {
                block_fee1 = MainService.GetEtherContractTradeFee(MainService.Wallet.CoinERC20(MainService.MarketList[MainService.exchange_market].trade_wallet));
            }
            if (base_wallet_blockchaintype == 6)
            {
                block_fee2 = MainService.GetEtherContractTradeFee(MainService.Wallet.CoinERC20(MainService.MarketList[MainService.exchange_market].base_wallet));
            }

            if (total < block_fee2 || amount < block_fee1)
            {
                //The trade amount is too small
                MainService.MessageBox("Notice!", "This trade amount is too small to match because it is lower than the blockchain fee.", "OK", false);
                return;
            }

            //ERC20 only check
            bool sending_erc20 = false;
            decimal erc20_amount = 0;
            int erc20_wallet = 0;
            if (window_order.type == 1 && MainService.Wallet.CoinERC20(MainService.MarketList[MainService.exchange_market].base_wallet) == true)
            {
                //Maker is selling so we are buying trade with ERC20
                sending_erc20 = true;
                erc20_amount = total;
                erc20_wallet = MainService.MarketList[MainService.exchange_market].base_wallet;
            }
            else if (window_order.type == 0 && MainService.Wallet.CoinERC20(MainService.MarketList[MainService.exchange_market].trade_wallet) == true)
            {
                //Maker is buying so we are selling trade that is also an ERC20
                sending_erc20 = true;
                erc20_amount = amount;
                erc20_wallet = MainService.MarketList[MainService.exchange_market].trade_wallet;
            }

            //Start running on background thread
            Task.Run(() =>
            {
                if (sending_erc20 == true)
                {
                    //Make sure the allowance is there already
                    decimal allowance = MainService.GetERC20AtomicSwapAllowance(MainService.GetWalletAddress(erc20_wallet), MainService.ERC20_ATOMICSWAP_ADDRESS, erc20_wallet);
                    if (allowance < 0)
                    {
                        MainService.MessageBox("Notice!", "Error determining ERC20 token contract allowance, please try again.", "OK", false);
                        return;
                    }
                    else if (allowance < erc20_amount)
                    {
                        //We need to increase the allowance to send to the atomic swap contract eventually
                        bool result = MainService.PromptUser("Confirmation", "Permission is required from this token's contract to send this amount to the NebliDex atomic swap contract.", "OK", "Cancel");
                        if (result == true)
                        {
                            //Create a transaction with this permission to send up to this amount
                            allowance = 1000000; //1 million tokens by default
                            if (erc20_amount > allowance) { allowance = erc20_amount; }
                            MainService.CreateAndBroadcastERC20Approval(erc20_wallet, allowance, MainService.ERC20_ATOMICSWAP_ADDRESS);
                            MainService.MessageBox("Notice!", "Now please wait for your approval to be confirmed by the Ethereum network then try again.", "OK", false);
                        }
                        return;
                    }
                }

                //Because tokens are indivisible at the moment, amounts can only be in whole numbers
                bool ntp1_wallet = MainService.IsWalletNTP1(MainService.MarketList[MainService.exchange_market].trade_wallet);
                if (ntp1_wallet == true)
                {
                    if (Math.Abs(Math.Round(amount) - amount) > 0)
                    {
                        MainService.MessageBox("Notice!", "All NTP1 tokens are indivisible at this time. Must be whole amounts.", "OK", false);
                        return;
                    }
                    amount = Math.Round(amount);
                }

                //Cannot match order when another order is involved deeply in trade
                bool too_soon = false;
                lock (MainService.MyOpenOrderList)
                {
                    for (int i = 0; i < MainService.MyOpenOrderList.Count; i++)
                    {
                        if (MainService.MyOpenOrderList[i].order_stage > 0) { too_soon = true; break; } //Your maker order is matching something
                        if (MainService.MyOpenOrderList[i].is_request == true) { too_soon = true; break; } //Already have another taker order
                    }
                }

                if (too_soon == true)
                {
                    MainService.MessageBox("Notice!", "Another order is currently involved in trade. Please wait and try again.", "OK", false);
                    return;
                }

                //Check to see if any other open orders of mine
                if (MainService.MyOpenOrderList.Count >= MainService.total_markets)
                {
                    MainService.MessageBox("Notice!", "You have exceed the maximum amount (" + MainService.total_markets + ") of open orders.", "OK", false);
                    return;
                }

                //Everything is good, create the request now
                //This will be a match open order (different than a general order)
                MainService.OpenOrder ord = new MainService.OpenOrder();
                ord.is_request = true; //Match order
                ord.order_nonce = window_order.order_nonce;
                ord.market = window_order.market;
                ord.type = 1 - window_order.type; //Opposite of the original order type
                ord.price = window_order.price;
                ord.amount = amount;
                ord.original_amount = amount;
                ord.order_stage = 0;
                ord.my_order = true; //Very important, it defines how much the program can sign automatically

                //Try to submit order request to CN
                //Run on UI Thread
                MainService.NebliDex_Activity.RunOnUiThread(() => {
                    Match_Button.IsEnabled = false;
                    Match_Button.Text = "Contacting CN..."; //This will allow us to wait longer as user is notified
                });                

                bool worked = MainService.SubmitMyOrderRequest(ord);
                if (worked == true)
                {
                    //Add to lists and close form
                    if (MainService.MyOpenOrderList.Count > 0)
                    {
                        //Close all the other open orders until this one is finished
                        MainService.QueueAllOpenOrders();
                    }

                    lock (MainService.MyOpenOrderList)
                    {
                        MainService.MyOpenOrderList.Add(ord); //Add to our own personal list
                    }
                    MainService.PendOrder(ord.order_nonce);

                    //Run closing window on UI thread
                    MainService.NebliDex_Activity.RunOnUiThread(() => {
                        //Close the Model and go back to Market Page on UI thread
                        Navigation.PopModalAsync();
                    });
                    return;
                }

                //Run reseting button on UI thread
                MainService.NebliDex_Activity.RunOnUiThread(() => {
                    //Reset the button
                    Match_Button.Text = "Match";
                    Match_Button.IsEnabled = true;
                });
                
            }); 
        }

        //Menu touch events
        private void Exit_Touched(object sender, EventArgs e)
        {
            App.Exit_Requested();
        }

        private void GoBackToMarket(object sender, EventArgs e)
        {
            Navigation.PopModalAsync();
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

        private void GoToSettingsPage(object sender, EventArgs e)
        {
            MainService.current_ui_page = 4;
            MainService.NebliDex_UI.LoadPage(MainService.current_ui_page);
        }
    }
}