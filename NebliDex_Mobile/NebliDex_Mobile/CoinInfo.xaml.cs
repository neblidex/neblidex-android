using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using NebliDex_Mobile.Droid;
using NBitcoin;
using Nethereum;

namespace NebliDex_Mobile
{
	public partial class CoinInfo : ContentPage
	{

        MainService.Wallet wallet;

		public CoinInfo (MainService.Wallet wal)
		{
			InitializeComponent ();
            wallet = wal;

            Coin_Name.Text = wallet.Coin;
            Balance_Amount.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", wallet.balance) + " " + wallet.Coin;
            Status.Text = wallet.S_Status;
            string addre = wallet.address;
            if(wallet.blockchaintype == 4)
            {
                //Convert to Cash Address
                addre = SharpCashAddr.Converter.ToCashAddress(addre); //Show the cash address for deposits
            }
            My_Address.Text = addre;
        }

        //Events
        private void Confirm_Withdraw(object sender, EventArgs e)
        {
            //User wants to withdraw, process it
            if (MainService.running_consolidation_check == true)
            {
                //Advise user to wait while wallet is performing consolidation check
                MainService.MessageBox("Notice!","Wallet is currently performing consolidation check. Please try again soon.", "OK", false);
                return;
            }

            //Get the destination
            string destination = Destination.Text.Trim();
            if (destination.Length == 0) { return; }

            //Get the amount
            if (MainService.IsNumber(Amount_Input.Text) == false) { return; }
            if (Amount_Input.Text.IndexOf(",") >= 0)
            {
                MainService.MessageBox("Notice!", "NebliDex does not recognize commas for decimals at this time.", "OK", false);
                return;
            }

            decimal amount = Math.Round(decimal.Parse(Amount_Input.Text, CultureInfo.InvariantCulture), 8);
            if (amount <= 0) { return; }

            //Run rest of code on its own thread
            Task.Run(() =>
            {
                if (MainService.my_wallet_pass.Length > 0)
                {
                    //User needs to enter password
                    string resp = MainService.UserPrompt("Please enter your wallet password\nto withdraw.","OK","Cancel",false); //Window
                    if (resp.Equals(MainService.my_wallet_pass) == false)
                    {
                        MainService.MessageBox("Notice!", "You've entered an incorrect password.", "OK", false);
                        return;
                    }
                }
               
                int mywallet = wallet.type;

                //Now check the balance
                string msg = "";
                bool good = MainService.CheckWalletBalance(mywallet, amount, ref msg);
                if (good == false)
                {
                    //Not enough funds or wallet unavailable
                    MainService.MessageBox("Notice!", msg, "OK", false);
                    return;
                }

                //If sending out tokens, make sure that account has enough NEBL for gas
                int wallet_blockchain = MainService.GetWalletBlockchainType(mywallet);
                if (wallet_blockchain == 0 && MainService.IsWalletNTP1(mywallet) == true)
                {
                    decimal nebl_bal = MainService.GetWalletAmount(0);
                    if (nebl_bal < MainService.blockchain_fee[0] * 3)
                    {
                        //We need at least 0.00033 to send out tokens
                        MainService.MessageBox("Notice!", "You do not have enough NEBL (" + String.Format(CultureInfo.InvariantCulture, "{0:0.########}", MainService.blockchain_fee[0] * 3) + " NEBL) to withdraw tokens!", "OK", false);
                        return;
                    }
                }
                else
                {
                    if (wallet_blockchain != 6)
                    {
                        //Make sure what we are sending is greater than the dust balance
                        if (amount < MainService.dust_minimum[wallet_blockchain])
                        {
                            MainService.MessageBox("Notice!", "This amount is too small to send as it is lower than the dust minimum", "OK", false);
                            return;
                        }
                    }
                    else
                    {
                        //Ethereum
                        decimal eth_bal = MainService.GetWalletAmount(17);
                        if (eth_bal <= MainService.GetEtherWithdrawalFee(MainService.Wallet.CoinERC20(mywallet)))
                        {
                            MainService.MessageBox("Notice!", "Your ETH balance is too small as it is lower than the gas fee to send this amount", "OK", false);
                            return;
                        }
                    }
                }

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
                    MainService.MessageBox("Notice!", "An order is currently involved in trade. Please wait and try again.", "OK", false);
                    return;
                }

                string suffix = " " + wallet.Coin;

                bool ok = MainService.PromptUser("Confirmation","Are you sure you want to send " + String.Format(CultureInfo.InvariantCulture, "{0:0.########}", amount) + suffix + " to " + destination + "?", "Yes","No");
                if (ok == true)
                {

                    //Queue all the open orders if any present
                    if (MainService.MyOpenOrderList.Count > 0)
                    {
                        MainService.QueueAllOpenOrders();
                    }

                    if (wallet_blockchain == 4)
                    {
                        //Bitcoin Cash
                        //Try to convert to old address, exception will be thrown if already old address
                        try
                        {
                            destination = SharpCashAddr.Converter.ToOldAddress(destination);
                        }
                        catch (Exception) { }
                    }

                    //Make sure to run in UI thread
                    MainService.NebliDex_Activity.RunOnUiThread(() => {
                        //Disable withdrawal button and prevent multiple taps
                        Withdraw_Button.IsEnabled = false;
                    });                   

                    ok = PerformWithdrawal(mywallet, amount, destination);
                    if (ok == true)
                    {
                        MainService.MessageBox("Notice!", "Posted withdrawal transaction successfully", "OK", false);
                        //Close the Modal and write a message
                        MainService.NebliDex_Activity.RunOnUiThread(() => {
                            //Go back to the wallet
                            GoBackToWallet(null,null);
                        });
                    }
                    else
                    {
                        MainService.MessageBox("Notice!", "Failed to create or post withdrawal transaction!", "OK", false);
                        //Run in UI thread
                        MainService.NebliDex_Activity.RunOnUiThread(() => {
                            Withdraw_Button.IsEnabled = true; //Re-enable withdrawal button
                        });                        
                    }
                }
            });            
        }

        private bool PerformWithdrawal(int wallet, decimal amount, string des)
        {
            int blockchain = MainService.GetWalletBlockchainType(wallet);
            if (blockchain != 6)
            {
                Transaction tx = MainService.CreateSignedP2PKHTx(wallet, amount, des, true, false);
                //Then add to database
                if (tx != null)
                {
                    //Now write to the transaction log
                    MainService.AddMyTxToDatabase(tx.GetHash().ToString(), MainService.GetWalletAddress(wallet), des, amount, wallet, 2, -1); //Withdrawal
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                //This is Ethereum transaction out
                Nethereum.Signer.TransactionChainId tx = null;
                if (MainService.Wallet.CoinERC20(wallet) == true)
                {
                    //ERC20 tokens only move amounts around at a specific contract, tokens are stored in the contract but allocated to the address
                    string token_contract = MainService.GetWalletERC20TokenContract(wallet);
                    BigInteger int_amount = MainService.ConvertToERC20Int(amount, MainService.GetWalletERC20TokenDecimals(wallet));
                    string transfer_data = MainService.GenerateEthereumERC20TransferData(des, int_amount);
                    tx = MainService.CreateSignedEthereumTransaction(wallet, token_contract, amount, false, 0, transfer_data);
                }
                else
                {
                    //This function will estimate the gas used if sending to a contract
                    tx = MainService.CreateSignedEthereumTransaction(wallet, des, amount, false, 0, "");
                }
                //Then add to database
                if (tx != null)
                {
                    //Broadcast this transaction, and write to log regardless of whether it returns a hash or not
                    //Now write to the transaction log
                    bool timeout;
                    MainService.TransactionBroadcast(wallet, tx.Signed_Hex, out timeout);
                    if (timeout == false)
                    {
                        MainService.UpdateWalletStatus(wallet, 2); //Set to wait
                        MainService.AddMyTxToDatabase(tx.HashID, MainService.GetWalletAddress(wallet), des, amount, wallet, 2, -1); //Withdrawal
                        return true;
                    }
                    else
                    {
                        MainService.NebliDexNetLog("Transaction broadcast timed out, not connected to internet");
                    }
                }
                return false;
            }
        }

        private void CopyAddress(object sender, EventArgs e)
        {
            //Copy the address field, very Android specific
            string addre = wallet.address;
            if (wallet.blockchaintype == 4)
            {
                //Convert to Cash Address
                addre = SharpCashAddr.Converter.ToCashAddress(addre); //Show the cash address for deposits
            }
            Android.Content.ClipboardManager clip_manager = (Android.Content.ClipboardManager)Forms.Context.GetSystemService(MainActivity.ClipboardService);
            Android.Content.ClipData clip = Android.Content.ClipData.NewPlainText(wallet.Coin + " Address", addre);
            clip_manager.PrimaryClip = clip; //Set to Clipboard
            MainService.ShowToastMessage("Copied Address To Clipboard");
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

        private void GoToSettingsPage(object sender, EventArgs e)
        {
            MainService.current_ui_page = 4;
            MainService.NebliDex_UI.LoadPage(MainService.current_ui_page);
        }

        private void GoBackToWallet(object sender, EventArgs e)
        {
            Navigation.PopModalAsync();
        }

        private void Exit_Touched(object sender, EventArgs e)
        {
            App.Exit_Requested();
        }
    }
}