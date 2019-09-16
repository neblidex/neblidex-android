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
	public partial class PlaceOrder : ContentPage
	{

        int order_type = 0;
        bool input_updating = false;

        public PlaceOrder (int type)
		{
			InitializeComponent ();
            order_type = type;
            decimal balance = MainService.GetMarketBalance(MainService.exchange_market, type);

            decimal price = 0;
            if (MainService.ChartLastPrice[0].Count > 0)
            {

                //Get the last trade price for the market as default (on 24 hr chart)
                for (int i = MainService.ChartLastPrice[0].Count - 1; i >= 0; i--)
                {
                    if (MainService.ChartLastPrice[0][i].market == MainService.exchange_market)
                    {
                        price = MainService.ChartLastPrice[0][i].price; break;
                    }
                }
                Price_Input.Text = "" + String.Format(CultureInfo.InvariantCulture, "{0:0.########}", price);
            }

            string trade_symbol = MainService.MarketList[MainService.exchange_market].trade_symbol;
            string base_symbol = MainService.MarketList[MainService.exchange_market].base_symbol;

            if (type == 0)
            {
                //Buy Order
                Order_Header.Text = "Buy " + trade_symbol;
                My_Balance.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", balance) + " " + base_symbol;
                Price_Header.Text = "Price (" + base_symbol + "):";
                Amount_Header.Text = "Amount (" + trade_symbol + "):";
                Min_Amount_Header.Text = "Minimum Match (" + trade_symbol + "):";
                Total_Header.Text = "Total Cost (" + base_symbol + "):";
                Order_Button.BackgroundColor = Xamarin.Forms.Color.FromHex("#63AB1D");
            }
            else
            {
                //Sell Order
                Order_Header.Text = "Sell " + trade_symbol;
                My_Balance.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", balance) + " " + trade_symbol;
                Price_Header.Text = "Price (" + base_symbol + "):";
                Amount_Header.Text = "Amount (" + trade_symbol + "):";
                Min_Amount_Header.Text = "Minimum Match (" + trade_symbol + "):";
                Total_Header.Text = "Total Receive (" + base_symbol + "):";
                Order_Button.BackgroundColor = Xamarin.Forms.Color.FromHex("#EA0070");
            }
        }

        //Events
        private void Price_KeyUp(object sender, TextChangedEventArgs e)
        {
            if (input_updating == true) { return; }
            try
            {
                input_updating = true;
                if (MainService.IsNumber(Price_Input.Text) == false) { return; }
                decimal price = decimal.Parse(Price_Input.Text, CultureInfo.InvariantCulture);
                if (price <= 0) { return; }

                if (MainService.IsNumber(Amount_Input.Text) == false) { return; }
                decimal amount = decimal.Parse(Amount_Input.Text, CultureInfo.InvariantCulture);
                if (amount <= 0) { return; }
                Total_Input.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", amount * price);
            }
            finally
            {
                input_updating = false;
            }
        }

        private void Amount_KeyUp(object sender, TextChangedEventArgs e)
        {
            if (input_updating == true) { return; }
            try
            {
                input_updating = true;
                if (MainService.IsNumber(Price_Input.Text) == false) { return; }
                decimal price = decimal.Parse(Price_Input.Text, CultureInfo.InvariantCulture);
                if (price <= 0) { return; }

                if (MainService.IsNumber(Amount_Input.Text) == false) { return; }
                decimal amount = decimal.Parse(Amount_Input.Text, CultureInfo.InvariantCulture);
                if (amount <= 0) { return; }
                Total_Input.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", amount * price);
                //The default minimum
                decimal min_amount = amount / 100m;
                if (MainService.IsWalletNTP1(MainService.MarketList[MainService.exchange_market].trade_wallet) == true)
                {
                    min_amount = Math.Round(min_amount); //Round to nearest whole number
                    if (min_amount == 0) { min_amount = 1; }
                }
                Min_Amount_Input.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", min_amount);
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
                if (MainService.IsNumber(Price_Input.Text) == false) { return; }
                decimal price = decimal.Parse(Price_Input.Text, CultureInfo.InvariantCulture);
                if (price <= 0) { return; }

                if (MainService.IsNumber(Total_Input.Text) == false) { return; }
                decimal total = decimal.Parse(Total_Input.Text, CultureInfo.InvariantCulture);
                if (total <= 0) { return; }
                decimal amount = total / price;
                if (MainService.IsWalletNTP1(MainService.MarketList[MainService.exchange_market].trade_wallet) == true)
                {
                    amount = Math.Round(amount); //Round to nearest whole number
                    if (amount == 0) { amount = 1; }
                }
                Amount_Input.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", amount);
                decimal min_amount = amount / 100m;
                if (MainService.IsWalletNTP1(MainService.MarketList[MainService.exchange_market].trade_wallet) == true)
                {
                    min_amount = Math.Round(min_amount); //Round to nearest whole number
                    if (min_amount == 0) { min_amount = 1; }
                }
                Min_Amount_Input.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", min_amount);
            }
            finally
            {
                input_updating = false;
            }
        }

        //Match Order
        private void Make_Order(object sender, EventArgs e)
        {
            //Create our order!
            //Get the price

            if (MainService.IsNumber(Price_Input.Text) == false) { return; }
            if (Price_Input.Text.IndexOf(",", StringComparison.InvariantCulture) >= 0)
            {
                MainService.MessageBox("Notice", "NebliDex does not recognize commas for decimals at this time.", "OK", false);
                return;
            }
            decimal price = decimal.Parse(Price_Input.Text, CultureInfo.InvariantCulture);
            if (price <= 0) { return; }
            if (price > MainService.max_order_price)
            {
                //Price cannot exceed the max
                MainService.MessageBox("Notice", "This price is higher than the maximum price of 10 000 000", "OK", false);
                return;
            }

            //Get the amount
            if (MainService.IsNumber(Amount_Input.Text) == false) { return; }
            if (Amount_Input.Text.IndexOf(",", StringComparison.InvariantCulture) >= 0)
            {
                MainService.MessageBox("Notice", "NebliDex does not recognize commas for decimals at this time.", "OK", false);
                return;
            }
            decimal amount = decimal.Parse(Amount_Input.Text, CultureInfo.InvariantCulture);
            if (amount <= 0) { return; }

            if (MainService.IsNumber(Min_Amount_Input.Text) == false) { return; }
            if (Min_Amount_Input.Text.IndexOf(",", StringComparison.InvariantCulture) >= 0)
            {
                MainService.MessageBox("Notice", "NebliDex does not recognize commas for decimals at this time.", "OK", false);
                return;
            }
            decimal min_amount = decimal.Parse(Min_Amount_Input.Text, CultureInfo.InvariantCulture);
            if (min_amount <= 0)
            {
                MainService.MessageBox("Notice", "The minimum amount is too small.", "OK", false);
                return;
            }
            if (min_amount > amount)
            {
                MainService.MessageBox("Notice", "The minimum amount cannot be greater than the amount.", "OK", false);
                return;
            }

            decimal total = Math.Round(price * amount, 8);
            if (Total_Input.Text.IndexOf(",", StringComparison.InvariantCulture) >= 0)
            {
                MainService.MessageBox("Notice", "NebliDex does not recognize commas for decimals at this time.", "OK", false);
                return;
            }

            if (MainService.MarketList[MainService.exchange_market].base_wallet == 3 || MainService.MarketList[MainService.exchange_market].trade_wallet == 3)
            {
                //Make sure amount is greater than ndexfee x 2
                if (amount < MainService.ndex_fee * 2)
                {
                    MainService.MessageBox("Notice", "This order amount is too small. Must be at least twice the CN fee (" + String.Format(CultureInfo.InvariantCulture, "{0:0.########}", MainService.ndex_fee * 2) + " NDEX)", "OK", false);
                    return;
                }
            }

            int wallet = 0;
            string msg = "";
            bool good = false;
            if (order_type == 0)
            {
                //This is a buy order we are making, so we need base market balance
                wallet = MainService.MarketList[MainService.exchange_market].base_wallet;
                good = MainService.CheckWalletBalance(wallet, total, ref msg);
                if (good == true)
                {
                    //Now check the fees
                    good = MainService.CheckMarketFees(MainService.exchange_market, order_type, total, ref msg, false);
                }
            }
            else
            {
                //Selling the trade wallet amount
                wallet = MainService.MarketList[MainService.exchange_market].trade_wallet;
                good = MainService.CheckWalletBalance(wallet, amount, ref msg);
                if (good == true)
                {
                    good = MainService.CheckMarketFees(MainService.exchange_market, order_type, amount, ref msg, false);
                }
            }

            //Show error messsage if balance not available
            if (good == false)
            {
                //Not enough funds or wallet unavailable
                MainService.MessageBox("Notice", msg, "OK", false);
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
                MainService.MessageBox("Notice", "This trade amount is too small to create because it is lower than the blockchain fee.", "OK", false);
                return;
            }

            //ERC20 only check
            //We need to check if the ERC20 token contract allows us to pull tokens to the atomic swap contract
            bool sending_erc20 = false;
            decimal erc20_amount = 0;
            int erc20_wallet = 0;
            if (order_type == 0 && MainService.Wallet.CoinERC20(MainService.MarketList[MainService.exchange_market].base_wallet) == true)
            {
                //Buying trade with ERC20
                sending_erc20 = true;
                erc20_amount = total;
                erc20_wallet = MainService.MarketList[MainService.exchange_market].base_wallet;
            }
            else if (order_type == 1 && MainService.Wallet.CoinERC20(MainService.MarketList[MainService.exchange_market].trade_wallet) == true)
            {
                //Selling trade that is also an ERC20
                sending_erc20 = true;
                erc20_amount = amount;
                erc20_wallet = MainService.MarketList[MainService.exchange_market].trade_wallet;
            }

            //Run the rest as a separate thread
            Task.Run(() => {
                if (sending_erc20 == true)
                {
                    //Make sure the allowance is there already
                    decimal allowance = MainService.GetERC20AtomicSwapAllowance(MainService.GetWalletAddress(erc20_wallet), MainService.ERC20_ATOMICSWAP_ADDRESS, erc20_wallet);
                    if (allowance < 0)
                    {
                        MainService.MessageBox("Notice", "Error determining ERC20 token contract allowance, please try again.", "OK", false);
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
                            MainService.MessageBox("Notice", "Now please wait for your approval to be confirmed by the Ethereum network then try again.", "OK", false);
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
                        MainService.MessageBox("Notice", "All NTP1 tokens are indivisible at this time. Must be whole amounts.", "OK", false);
                        return;
                    }
                    amount = Math.Round(amount);

                    if (Math.Abs(Math.Round(min_amount) - min_amount) > 0)
                    {
                        MainService.MessageBox("Notice", "All NTP1 tokens are indivisible at this time. Must be whole minimum amounts.", "OK", false);
                        return;
                    }
                    min_amount = Math.Round(min_amount);
                }

                //Check to see if any other open orders of mine
                if (MainService.MyOpenOrderList.Count >= MainService.total_markets)
                {
                    MainService.MessageBox("Notice", "You have exceed the maximum amount (" + MainService.total_markets + ") of open orders.", "OK", false);
                    return;
                }

                MainService.OpenOrder ord = new MainService.OpenOrder();
                ord.order_nonce = MainService.GenerateHexNonce(32);
                ord.market = MainService.exchange_market;
                ord.type = order_type;
                ord.price = Math.Round(price, 8);
                ord.amount = Math.Round(amount, 8);
                ord.minimum_amount = Math.Round(min_amount, 8);
                ord.original_amount = amount;
                ord.order_stage = 0;
                ord.my_order = true; //Very important, it defines how much the program can sign automatically

                //Run this on UI thread
                MainService.NebliDex_Activity.RunOnUiThread(() => {
                    //Disable the order button
                    Order_Button.IsEnabled = false;
                    Order_Button.Text = "Contacting CN...";
                });

                //Try to submit order to CN
                bool worked = MainService.SubmitMyOrder(ord, null);
                if (worked == true)
                {
                    //Add to lists and close order
                    lock (MainService.MyOpenOrderList)
                    {
                        MainService.MyOpenOrderList.Add(ord); //Add to our own personal list
                    }
                    lock (MainService.OpenOrderList[MainService.exchange_market])
                    {
                        MainService.OpenOrderList[MainService.exchange_market].Add(ord);
                    }
                    MainService.NebliDex_UI.AddOrderToView(ord);
                    MainService.AddSavedOrder(ord);
                    MainService.NebliDex_Activity.RunOnUiThread(() => {
                        //Close the Model and go back to Market Page on UI thread
                        Navigation.PopModalAsync();
                    });
                    return;
                }
                //Otherwise re-enable the button
                MainService.NebliDex_Activity.RunOnUiThread(() => {
                    //Reset the button
                    Order_Button.IsEnabled = true;
                    Order_Button.Text = "Create Order";
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