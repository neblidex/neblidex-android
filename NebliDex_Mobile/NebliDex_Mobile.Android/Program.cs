// This file is similar to the program.cs from NebliDex Linux

using System;
using System.Threading;
using Mono.Data.Sqlite;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Globalization;
using NBitcoin;
using System.Reflection;
using System.ComponentModel;

namespace NebliDex_Mobile.Droid
{
    public partial class MainService : Android.App.Service
    {
        //Header files
        public static int current_ui_page = 0; //0 - loading screen
        public static bool program_loaded = false; //Returns true when the program has fully loaded

        //Mainnet version
        public static int protocol_version = 10; //My protocol version
        public static int protocol_min_version = 10; //Minimum accepting protocol version
        public static string version_text = "v10.0.1";
        public static bool run_headless = false; //If true, this software is ran in critical node mode without GUI on startup
        public static bool http_open_network = true; //This becomes false if user closes window
        public static int sqldatabase_version = 3;
        public static int accountdat_version = 1; //The version of the account wallet

        //Lowest testnet version: 10
        //Lowest mainnet version: 10

        //Version 10
        //Allow for very small ERC20 order amounts (ideal for wBTC)
        //Updated DAI contract to new version
        //Fixed ETH confirmation bug
        //Allow for new markets without protocol changes	

        public static string App_Path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal); //This will be the folder for application data

        public static bool critical_node = false; //Not critical node by default
        public static bool critical_node_pending = false; //This is for a node that is just connecting to the network (it cannot relay).
        public static int critical_node_port = 55364; //This is our critical node port < 65000
        public static int cn_ndex_minimum = 39000; //The amount required to become a critical node
        public static int cn_num_validating_tx = 0; //The amount of transactions being validated by the CN
        public static string my_external_ip = ""; //Cache our IP address

        public static string Default_DNS_SEED = "https://neblidex.xyz/seed"; //The default seed, returns IP list of CNs
        public static string DNS_SEED = Default_DNS_SEED;
        public static int DNS_SEED_TYPE = 0; //Http protocol, 1 = Direct IP
        public static int wlan_mode = 0; //0 = Internet, 1 = WLAN, 2 = Localhost (This is for CN IP addresses returned)

        public static int exchange_market = 2; //NDEX/NEBL
        public static int total_markets = 48;
        public static int total_scan_markets = total_markets; // This number will vary if we are updating the markets
        public static int total_cointypes = 7;
        //The total amount of cointypes supported by NebliDex
        //Possible cointypes are:
        //0 - Neblio based (including tokens)
        //1 - Bitcoin based
        //2 - Litecoin based
        //3 - Groestlcoin based
        //4 - Bitcoin Cash (ABC) based
        //5 - Monacoin based
        //6 - Ethereum based (including tokens)

        public static Random app_random_gen = new Random(); //App random number generator
        public static string my_rsa_privkey, my_rsa_pubkey; //These are used to exchange a one time use password nonce between validator and TN
        public static string my_wallet_pass = ""; //Password used to load the wallet
        public static Timer PeriodicTimer; //Timer for balance and other things, ran every 5 seconds
        public static Android.OS.PowerManager.WakeLock PeriodicTimer_WakeLock; //A wakelock to prevent cpu sleep if my open orders present
        public static Timer ConsolidateTimer; //Ran every 6 hours
        public static Timer CandleTimer; //This is a timer ran every 15 minutes
        public static Timer HeadlessTimer; //Only used for headless mode
        public static int next_candle_time = 0; //This is the time in seconds of the next candle
        public static int candle_15m_interval = 0; //4 of these is 90 minutes (one 7 day candle)
        public static int max_transaction_wait = 60 * 60 * 3; //The maximum amount to wait (in seconds)for a transaction to confirm before deeming it failed
        public static double double_epsilon = 0.00000001;
        public static decimal max_order_price = 10000000; //Maximum price is 10,000,000 ratio

        //Market Info
        //Will Make Market Info modular and connect to certain wallet types
        public static List<Market> MarketList = new List<Market>();
        public class Market
        {
            public int index; //0 The location index of the market
            public string base_symbol; //The symbol for the base coin
            public int base_wallet; //The wallet connected to the base coin
            public string trade_symbol;
            public int trade_wallet;
            public bool active = true;

            public Market(int i)
            {
                index = i;
                if (index == 0)
                {
                    //NEBL/BTC
                    base_symbol = "BTC";
                    trade_symbol = "NEBL";
                    base_wallet = 1; //BTC wallet
                    trade_wallet = 0; //NEBL wallet
                }
                else if (index == 1)
                {
                    //NEBL/LTC
                    base_symbol = "LTC";
                    trade_symbol = "NEBL";
                    base_wallet = 2; //LTC wallet
                    trade_wallet = 0; //NEBL wallet                 
                }
                else if (index == 2)
                {
                    //NDEX/NEBL
                    base_symbol = "NEBL";
                    trade_symbol = "NDEX";
                    base_wallet = 0; //NEBL wallet
                    trade_wallet = 3; //NDEX wallet                 
                }
                else if (index == 3)
                {
                    //NDEX/BTC
                    base_symbol = "BTC";
                    trade_symbol = "NDEX";
                    base_wallet = 1; //BTC wallet
                    trade_wallet = 3; //NDEX wallet                 
                }
                else if (index == 4)
                {
                    //NDEX/LTC
                    base_symbol = "LTC";
                    trade_symbol = "NDEX";
                    base_wallet = 2; //LTC wallet
                    trade_wallet = 3; //NDEX wallet                 
                }
                else if (index == 5)
                {
                    //TRIF/NEBL
                    base_symbol = "NEBL";
                    trade_symbol = "TRIF";
                    base_wallet = 0; //NEBL wallet
                    trade_wallet = 4; //TRIF wallet                 
                }
                else if (index == 6)
                {
                    //QRT/NEBL
                    base_symbol = "NEBL";
                    trade_symbol = "QRT";
                    base_wallet = 0; //NEBL wallet
                    trade_wallet = 5; //QRT wallet

                    active = false; //Deactivate this market
                }
                else if (index == 7)
                {
                    //PTN/NEBL
                    base_symbol = "NEBL";
                    trade_symbol = "PTN";
                    base_wallet = 0; //NEBL wallet
                    trade_wallet = 6; //PTN wallet

                    active = false;
                }
                else if (index == 8)
                {
                    //NAUTO/NEBL
                    base_symbol = "NEBL";
                    trade_symbol = "NAUTO";
                    base_wallet = 0; //NEBL wallet
                    trade_wallet = 7; //NAUTO wallet                    
                }
                else if (index == 9)
                {
                    //NCC/NEBL
                    base_symbol = "NEBL";
                    trade_symbol = "NCC";
                    base_wallet = 0; //NEBL wallet
                    trade_wallet = 8; //NCC wallet                  
                }
                else if (index == 10)
                {
                    //CHE/NEBL
                    base_symbol = "NEBL";
                    trade_symbol = "CHE";
                    base_wallet = 0; //NEBL wallet
                    trade_wallet = 9; //CHE wallet 

                    active = false;
                }
                else if (index == 11)
                {
                    //HODLR/NEBL
                    base_symbol = "NEBL";
                    trade_symbol = "HODLR";
                    base_wallet = 0; //NEBL wallet
                    trade_wallet = 10; //HODLR wallet                   
                }
                else if (index == 12)
                {
                    //NTD/NEBL
                    base_symbol = "NEBL";
                    trade_symbol = "NTD";
                    base_wallet = 0; //NEBL wallet
                    trade_wallet = 11; //NTD wallet                 
                }
                else if (index == 13)
                {
                    //TGL/NEBL
                    base_symbol = "NEBL";
                    trade_symbol = "TGL";
                    base_wallet = 0; //NEBL wallet
                    trade_wallet = 12; //TGL wallet  

                    active = false;
                }
                else if (index == 14)
                {
                    //IMBA/NEBL
                    base_symbol = "NEBL";
                    trade_symbol = "IMBA";
                    base_wallet = 0; //NEBL wallet
                    trade_wallet = 13; //IMBA wallet                 
                }
                else if (index == 15)
                {
                    base_symbol = "BTC";
                    trade_symbol = "LTC";
                    base_wallet = 1; //BTC
                    trade_wallet = 2; //LTC
                }
                else if (index == 16)
                {
                    base_symbol = "BTC";
                    trade_symbol = "BCH";
                    base_wallet = 1; //BTC
                    trade_wallet = 15; //BCH
                }
                else if (index == 17)
                {
                    base_symbol = "BTC";
                    trade_symbol = "ETH";
                    base_wallet = 1; //BTC
                    trade_wallet = 17; //ETH
                }
                else if (index == 18)
                {
                    base_symbol = "BTC";
                    trade_symbol = "MONA";
                    base_wallet = 1;
                    trade_wallet = 16; //MONA
                }
                else if (index == 19)
                {
                    base_symbol = "NEBL";
                    trade_symbol = "GRS";
                    base_wallet = 0;
                    trade_wallet = 14; //GRS
                }
                else if (index == 20)
                {
                    base_symbol = "LTC";
                    trade_symbol = "ETH";
                    base_wallet = 2;
                    trade_wallet = 17; //ETH
                }
                else if (index == 21)
                {
                    base_symbol = "LTC";
                    trade_symbol = "MONA";
                    base_wallet = 2;
                    trade_wallet = 16; //MONA
                }
                else if (index == 22)
                {
                    base_symbol = "GRS";
                    trade_symbol = "NDEX";
                    base_wallet = 14;
                    trade_wallet = 3; //NDEX
                }
                else if (index == 23)
                {
                    base_symbol = "MONA";
                    trade_symbol = "NDEX";
                    base_wallet = 16;
                    trade_wallet = 3; //NDEX
                }
                else if (index == 24)
                {
                    base_symbol = "BCH";
                    trade_symbol = "NDEX";
                    base_wallet = 15;
                    trade_wallet = 3; //NDEX
                }
                else if (index == 25)
                {
                    base_symbol = "ETH";
                    trade_symbol = "NDEX";
                    base_wallet = 17;
                    trade_wallet = 3; //NDEX
                }
                else if (index == 26)
                {
                    base_symbol = "DAI";
                    trade_symbol = "NDEX";
                    base_wallet = 18; //DAI
                    trade_wallet = 3; //NDEX
                }
                else if (index == 27)
                {
                    base_symbol = "USDC";
                    trade_symbol = "NDEX";
                    base_wallet = 19; //USDC
                    trade_wallet = 3; //NDEX
                }
                else if (index == 28)
                {
                    base_symbol = "DAI";
                    trade_symbol = "NEBL";
                    base_wallet = 18;
                    trade_wallet = 0; //NEBL
                }
                else if (index == 29)
                {
                    base_symbol = "USDC";
                    trade_symbol = "NEBL";
                    base_wallet = 19;
                    trade_wallet = 0; //NEBL
                }
                else if (index == 30)
                {
                    base_symbol = "DAI";
                    trade_symbol = "LTC";
                    base_wallet = 18;
                    trade_wallet = 2; //LTC
                }
                else if (index == 31)
                {
                    base_symbol = "USDC";
                    trade_symbol = "LTC";
                    base_wallet = 19;
                    trade_wallet = 2; //LTC
                }
                else if (index == 32)
                {
                    base_symbol = "DAI";
                    trade_symbol = "BTC";
                    base_wallet = 18;
                    trade_wallet = 1; //BTC
                }
                else if (index == 33)
                {
                    base_symbol = "USDC";
                    trade_symbol = "BTC";
                    base_wallet = 19;
                    trade_wallet = 1; //BTC
                }
                else if (index == 34)
                {
                    base_symbol = "DAI";
                    trade_symbol = "BCH";
                    base_wallet = 18;
                    trade_wallet = 15; //BCH
                }
                else if (index == 35)
                {
                    base_symbol = "DAI";
                    trade_symbol = "GRS";
                    base_wallet = 18;
                    trade_wallet = 14; //GRS
                }
                else if (index == 36)
                {
                    base_symbol = "DAI";
                    trade_symbol = "MONA";
                    base_wallet = 18;
                    trade_wallet = 16; //MONA
                }
                else if (index == 37)
                {
                    base_symbol = "NEBL";
                    trade_symbol = "ZOM";
                    base_wallet = 0;
                    trade_wallet = 20; //ZOM
                }
                else if (index == 38)
                {
                    base_symbol = "LTC";
                    trade_symbol = "ZOM";
                    base_wallet = 2;
                    trade_wallet = 20;
                }
                else if (index == 39)
                {
                    base_symbol = "BTC";
                    trade_symbol = "ZOM";
                    base_wallet = 1;
                    trade_wallet = 20;
                }
                else if (index == 40)
                {
                    base_symbol = "LTC";
                    trade_symbol = "MKR";
                    base_wallet = 2;
                    trade_wallet = 21; //MKR
                }
                else if (index == 41)
                {
                    base_symbol = "BTC";
                    trade_symbol = "MKR";
                    base_wallet = 1;
                    trade_wallet = 21;
                }
                else if (index == 42)
                {
                    base_symbol = "NEBL";
                    trade_symbol = "LINK";
                    base_wallet = 0;
                    trade_wallet = 22; //LINK
                }
                else if (index == 43)
                {
                    base_symbol = "LTC";
                    trade_symbol = "LINK";
                    base_wallet = 2;
                    trade_wallet = 22;
                }
                else if (index == 44)
                {
                    base_symbol = "BTC";
                    trade_symbol = "LINK";
                    base_wallet = 1;
                    trade_wallet = 22;
                }
                else if (index == 45)
                {
                    base_symbol = "LTC";
                    trade_symbol = "BAT";
                    base_wallet = 2;
                    trade_wallet = 23; //BAT
                }
                else if (index == 46)
                {
                    base_symbol = "BTC";
                    trade_symbol = "BAT";
                    base_wallet = 1;
                    trade_wallet = 23;
                }
                else if (index == 47)
                {
                    base_symbol = "BTC";
                    trade_symbol = "WBTC";
                    base_wallet = 1;
                    trade_wallet = 24; //WBTC
                }
            }

            public string format_market
            {
                get
                {
                    return trade_symbol + "/" + base_symbol;
                }
            }
        }

        //Wallet Info
        //Last pair for each wallet is used to trade
        //Cannot change address if open orders present
        public static ObservableCollection<Wallet> WalletList = new ObservableCollection<Wallet>();

        public class Wallet : System.ComponentModel.INotifyPropertyChanged
        {
            public int type;
            public string private_key;
            public string address;

            //Internal values
            private decimal internal_balance;
            private int internal_status; //0 - avail, 1 - pending, 2 - waiting
            public int blockchaintype = 0;

            public static int total_coin_num = 25; //Total number of possible different wallet coins

            public event PropertyChangedEventHandler PropertyChanged;

            private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyName = "")
            {
                if (NebliDex_UI == null) { return; }
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }

            public decimal balance
            {
                get
                {
                    return internal_balance;
                }
                set
                {
                    internal_balance = value;
                    NotifyPropertyChanged("Amount");
                }
            }
            public int status
            {
                get
                {
                    return internal_status;
                }
                set
                {
                    internal_status = value;
                    NotifyPropertyChanged("S_Status");
                }
            }

            public static bool CoinActive(int ctype)
            { //Coins that are not active anymore
                if (ctype == 5 || ctype == 6 || ctype == 9 || ctype == 12)
                { //QRT, CHE, PTN, TGL are not active anymore
                    return false;
                }
                else
                {
                    return true;
                }
            }

            public static bool CoinERC20(int ctype)
            {
                if (ctype >= 18 && ctype <= 24)
                { //New ETH based ERC20 tokens
                    return true;
                }
                return false;
            }

            public static bool CoinNTP1(int ctype)
            {
                //Returns true if the type is a NTP1 type
                if (ctype >= 3 && ctype <= 13)
                {
                    return true;
                }
                return false;
            }

            public static int WalletType(int btype)
            {
                //Returns type based on the blockchain type
                if (btype < 3)
                {
                    return btype;
                }
                else
                {
                    if (btype == 3)
                    {
                        //GRS Wallet
                        return 14;
                    }
                    else if (btype == 4)
                    {
                        //BCH
                        return 15;
                    }
                    else if (btype == 5)
                    {
                        //MONA
                        return 16;
                    }
                    else if (btype == 6)
                    {
                        //ETH
                        return 17;
                    }
                }
                return 0; //Otherwise, its neblio based
            }

            public static int BlockchainType(int type)
            {
                if (type == 0 || Wallet.CoinNTP1(type) == true)
                {
                    return 0; //Neblio based (including tokens)
                }
                else if (type == 1)
                {
                    return 1;
                }
                else if (type == 2)
                {
                    return 2;
                }
                else if (type == 14)
                {
                    return 3;
                }
                else if (type == 15)
                {
                    return 4;
                }
                else if (type == 16)
                {
                    return 5;
                }
                else if (type == 17 || Wallet.CoinERC20(type) == true)
                {
                    return 6; //Ethereum based (including tokens)
                }
                return 0;
            }

            public string Coin
            {
                get
                {
                    if (type == 0)
                    {
                        return "NEBL";
                    }
                    else if (type == 1)
                    {
                        return "BTC";
                    }
                    else if (type == 2)
                    {
                        return "LTC";
                    }
                    else if (type == 3)
                    { //Important wallet
                        return "NDEX";
                    }
                    else if (type == 4)
                    {
                        return "TRIF"; //3rd party NTP1 token
                    }
                    else if (type == 5)
                    {
                        return "QRT"; //QRT 3rd party token
                    }
                    else if (type == 6)
                    {
                        return "PTN"; //PTN 3rd party token
                    }
                    else if (type == 7)
                    {
                        return "NAUTO"; //NAUTO 3rd party token
                    }
                    else if (type == 8)
                    {
                        return "NCC"; //NCC 3rd party token
                    }
                    else if (type == 9)
                    {
                        return "CHE"; //CHE 3rd party token
                    }
                    else if (type == 10)
                    {
                        return "HODLR"; //HODLR 3rd party token
                    }
                    else if (type == 11)
                    {
                        return "NTD"; //NTD 3rd party token
                    }
                    else if (type == 12)
                    {
                        return "TGL"; //TGL 3rd party token
                    }
                    else if (type == 13)
                    {
                        return "IMBA"; //IMBA 3rd party token
                    }
                    else if (type == 14)
                    {
                        return "GRS"; //GRS coin
                    }
                    else if (type == 15)
                    {
                        return "BCH"; //Bitcoin Cash
                    }
                    else if (type == 16)
                    {
                        return "MONA"; //Monacoin
                    }
                    else if (type == 17)
                    {
                        return "ETH"; //Ethereum
                    }
                    else if (type == 18)
                    {
                        return "DAI"; //DAI ERC20 stablecoin
                    }
                    else if (type == 19)
                    {
                        return "USDC"; //USDC ERC20 stablecoin
                    }
                    else if (type == 20)
                    {
                        return "ZOM"; //ZOM ERC20 token
                    }
                    else if (type == 21)
                    {
                        return "MKR"; //MKR ERC20 token
                    }
                    else if (type == 22)
                    {
                        return "LINK"; //LINK ERC20 token
                    }
                    else if (type == 23)
                    {
                        return "BAT"; //BAT ERC20 token
                    }
                    else if (type == 24)
                    {
                        return "WBTC"; //wBTC ERC20 token
                    }
                    return "";
                }
            }

            public string ERC20Contract
            {
                get
                {
                    if (blockchaintype != 6)
                    { //Not ETH
                        return "";
                    }
                    else if (type == 18)
                    { //DAI Contract
                        if (testnet_mode == false)
                        {
                            return "0x6B175474E89094C44Da98b954EedeAC495271d0F"; // Version 2 of contract
                        }
                        else
                        {
                            return "0xDE24730E12C76a269E99b8E7668A0b73102AfCa1"; //Using REP Rinkeby testnet tokens as DAI doesn't have any
                        }
                    }
                    else if (type == 19)
                    { //USDC Proxy Contract
                        //USDC has upgradeable contracts
                        if (testnet_mode == false)
                        {
                            return "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
                        }
                        else
                        {
                            return ""; //USDC doesn't have a testnet
                        }
                    }
                    else if (type == 20)
                    { //ZOM Contract
                        if (testnet_mode == false)
                        {
                            return "0x42382F39e7C9F1ADD5fa5f0c6e24aa62f50be3b3";
                        }
                        else
                        {
                            return ""; //ZOM doesn't have a testnet
                        }
                    }
                    else if (type == 21)
                    { //MKR Contract
                        if (testnet_mode == false)
                        {
                            return "0x9f8F72aA9304c8B593d555F12eF6589cC3A579A2";
                        }
                        else
                        {
                            return ""; //Not interacting with testnet
                        }
                    }
                    else if (type == 22)
                    { //LINK Contract
                        if (testnet_mode == false)
                        {
                            return "0x514910771AF9Ca656af840dff83E8264EcF986CA";
                        }
                        else
                        {
                            return ""; //Not interacting with testnet
                        }
                    }
                    else if (type == 23)
                    { //BAT Contract
                        if (testnet_mode == false)
                        {
                            return "0x0D8775F648430679A709E98d2b0Cb6250d2887EF";
                        }
                        else
                        {
                            return ""; //Not interacting with testnet
                        }
                    }
                    else if (type == 24)
                    { //wBTC Contract
                        if (testnet_mode == false)
                        {
                            return "0x2260FAC5E5542a773Aa44fBCfeDf7C193bc2C599";
                        }
                        else
                        {
                            return ""; //Not interacting with testnet
                        }
                    }
                    return "";
                }
            }

            //The amount of decimal places for the token
            public decimal ERC20Decimals
            {
                get
                {
                    if (blockchaintype != 6)
                    { //Not ETH
                        return 8;
                    }
                    else if (type == 18)
                    { //DAI Contract
                        return 18; //Dai contracts have 18 decimal places
                    }
                    else if (type == 19)
                    {
                        return 6; //USDC has 6 decimal places
                    }
                    else if (type == 20)
                    {
                        return 18; //ZOM has 18 decimal places
                    }
                    else if (type == 21)
                    {
                        return 18; //MKR has 18 decimal places
                    }
                    else if (type == 22)
                    {
                        return 18; //LINK has 18 decimal places
                    }
                    else if (type == 23)
                    {
                        return 18; //BAT has 18 decimal places
                    }
                    else if (type == 24)
                    {
                        return 8; //wBTC has 8 decimal places
                    }
                    return 8;
                }
            }

            public string TokenID
            {
                get
                {
                    if (type == 0 || blockchaintype != 0)
                    { //Not Neblio token
                        return "";
                    }
                    else if (type == 3)
                    { //Important wallet
                        if (testnet_mode == false)
                        {
                            return "LaAHPkQRtb9AFKkACMhEPR58STgCirv7RheEfk"; //NDEX
                        }
                        else
                        {
                            return "La7ma9nkcNTi7g4kQs4ewXHHD5fDRRXmespWMX"; //NDEX testnet token
                        }
                    }
                    else if (type == 4)
                    {
                        if (testnet_mode == false)
                        {
                            return "La3QxvUgFwKz2jjQR2HSrwaKcRgotf4tGVkMJx"; //TRIF NTP1 token
                        }
                        else
                        {
                            return "La5rZ4dkUi6cnFiex8Hmts6zagLipP28CRWVhx"; //TRIF testnet token
                        }
                    }
                    else if (type == 5)
                    {
                        if (testnet_mode == false)
                        {
                            return "La59cwCF5aF2HCMvqXok7Htn6fBE2kQnA96rrj"; //QRT NTP1 token
                        }
                        else
                        {
                            return "NoTestnetToken"; //QRT testnet token
                        }
                    }
                    else if (type == 6)
                    {
                        if (testnet_mode == false)
                        {
                            return "La5NtFaP8EB6ozdqXWdWvzxuZuk3Q3VLic8sQJ"; //PTN NTP1 token
                        }
                        else
                        {
                            return "NoTestnetToken"; //PTN testnet token
                        }
                    }
                    else if (type == 7)
                    {
                        if (testnet_mode == false)
                        {
                            return "La3DmJcJo162g54jj3rKunSkD7aw9Foj3y8CSK"; //NAUTO NTP1 token
                        }
                        else
                        {
                            return "NoTestnetToken"; //NAUTO testnet token
                        }
                    }
                    else if (type == 8)
                    {
                        if (testnet_mode == false)
                        {
                            return "La4sfZJmmfjoNbSjAy4868ftkPAqWrH97bVDE3"; //NCC NTP1 token
                        }
                        else
                        {
                            return "NoTestnetToken"; //NCC testnet token
                        }
                    }
                    else if (type == 9)
                    {
                        if (testnet_mode == false)
                        {
                            return "LaA7RwxDAzQjeYBGueqws25tNbJvTCyKYQ9pS4"; //CHE NTP1 token
                        }
                        else
                        {
                            return "NoTestnetToken"; //CHE testnet token
                        }
                    }
                    else if (type == 10)
                    {
                        if (testnet_mode == false)
                        {
                            return "La6ojSJKYiHMBBRCwnFt2Xn8acUDRmzYyjg9LL"; //HODLR NTP1 token
                        }
                        else
                        {
                            return "NoTestnetToken"; //HODLR testnet token
                        }
                    }
                    else if (type == 11)
                    {
                        if (testnet_mode == false)
                        {
                            return "La2r6UYDYR7YJTVZMK1hp8WQ5KRfgcCw6s5uZV"; //NTD NTP1 token
                        }
                        else
                        {
                            return "NoTestnetToken"; //NTD testnet token
                        }
                    }
                    else if (type == 12)
                    {
                        if (testnet_mode == false)
                        {
                            return "La8Ntf8zGgYXVtpzVZKxtRptyyP1jwm2RshumQ"; //TGL NTP1 token
                        }
                        else
                        {
                            return "LaAFsP9LkJsfBGRaqcscUQz6evueMyPumqDw5d"; //TGL testnet token
                        }
                    }
                    else if (type == 13)
                    {
                        if (testnet_mode == false)
                        {
                            return "La6H1AekHNgh8jKcQirh12cQ23wTSMX9Th84a2"; //IMBA NTP1 token
                        }
                        else
                        {
                            return "NoTestnetToken"; //IMBA testnet token
                        }
                    }
                    return "";
                }
            }

            public string Amount
            {
                get { return String.Format(CultureInfo.InvariantCulture, "{0:0.########}", balance); }
            }

            public string S_Status
            {
                get
                {
                    if (status == 0)
                    {
                        return "Available";
                    }
                    else if (status == 1)
                    {
                        return "Incoming";
                    }
                    else if (status == 2)
                    {
                        return "Waiting";
                    }
                    return "";
                }

            }
        }

        public static List<CancelOrderToken> CancelOrderTokenList = new List<CancelOrderToken>();

        public class CancelOrderToken
        {
            //This is for situations where the cancel request arrives before the open order does
            //Otherwise order is removed without cancel token
            public string order_nonce; //The string associated with the Order
            public int arrivetime; //These tokens delete after 5 minutes
        }

        public static List<OpenOrder>[] OpenOrderList = new List<OpenOrder>[total_markets]; //Create array of type list

        public class OpenOrder : System.ComponentModel.INotifyPropertyChanged
        {
            public string order_nonce; //Not really a hash, but a one-time code used to define order
            public string[] ip_address_port = new string[2]; //Holds both port and IP address (Only CNs have this)
            public string cn_relayer_ip = ""; //This is the CN that first received the order 
            public int market;
            public int type; //0 or 1 (Buy or Sell)
            public decimal price; //Decimal value representing price
            public bool is_request = false; //This will show if this is a market order (taker order)
            public decimal internal_amount;
            public decimal original_amount; //Amount started available / requested (Only used for my orders)
            public decimal minimum_amount; //This is a user-set minimum order size
            public bool my_order = false; //True if this is my order
            public int internal_order_stage;

            public int order_stage //This has become a property to trigger UI changes
            {
                get { return internal_order_stage; }
                set
                {
                    internal_order_stage = value;
                    NotifyPropertyChanged("In_Order");
                }
            }

            public decimal amount //Amount currently available / requesting
            {
                get { return internal_amount; }
                set
                {
                    internal_amount = value;
                    NotifyPropertyChanged("Format_Filled");
                }
            }

            //0 - available to trade (visible)
            //Maker Information
            //1 - maker order is hidden from view (pended by CN)
            //2 - maker accepted trade request
            //3 - maker received taker information
            // In stage 3, maker will wait until taker contract has correct balance before broadcasting maker contract
            //4 - maker has tx sent to validator to broadcast 
            // In stage 4, maker cannot close program now as may miss time when taker pulls from maker contract
            // Maker contract is continuously monitored for spending transaction
            // Once taker has funded contract, maker extracts secret and pulls from taker contract immediately
            // After successful pull, maker is available to trade again

            //Taker Information
            //1 - Maker has accepted taker request
            //2 - Taker sent contract information and tx to validator
            // In this stage, taker may receive notice of cancelation from validator, this closes request but taker will
            // continue to monitor contract address for balance after refund time just in case
            //3 - Taker received maker information
            // In this stage, taker waits for maker contract to fund, once funded, pulls entire balance then closes request
            //4 - Request is canceled by validator

            public int pendtime; //The time when the order is pended
            public uint cooldownend = 0; //The time when the order is available for trading again
            public bool deletequeue = false; //This is for CN deletes only
            public bool validating = false; //This will be true if this person is choosing the validator

            private bool internal_queued_order = false; //Queued orders will try to repost every 30 seconds
                                                        //Fees are determined based on candles from 7 day charts: average of (N-2 & N-3)
                                                        //If not enough data, defaults to 10
            public bool queued_order
            {
                get
                {
                    return internal_queued_order;
                }
                set
                {
                    internal_queued_order = value;
                    NotifyPropertyChanged("Format_Type");
                }
            }

            public OpenOrder()
            {
                ip_address_port[0] = "";
                ip_address_port[1] = "";
            }

            //Properties for the UI
            public string Format_Price
            {
                get
                {
                    return String.Format(CultureInfo.InvariantCulture, "{0:0.########}", price);
                }
            }

            public string Format_Amount
            {
                get
                {
                    return String.Format(CultureInfo.InvariantCulture, "{0:0.########}", amount);
                }
            }

            public string Format_Original_Amount
            {
                get { return String.Format(CultureInfo.InvariantCulture, "{0:0.########}", original_amount); }
            }

            public string Format_Market
            {
                get
                {
                    string mark_string = MarketList[market].trade_symbol + "/" + MarketList[market].base_symbol;
                    return mark_string;
                }
            }

            public string Format_Type
            {
                get
                {
                    if (type == 0)
                    {
                        if (queued_order == true) { return "QUEUED BUY"; }
                        if (is_request == false) { return "BUY"; } else { return "MARKET BUY"; }
                    }
                    else
                    {
                        if (queued_order == true) { return "QUEUED SELL"; }
                        if (is_request == false) { return "SELL"; } else { return "MARKET SELL"; }
                    }
                }
            }

            public string Format_Filled
            {
                get
                {
                    if (is_request == false)
                    {
                        return Convert.ToString(Math.Round((1m - amount / original_amount) * 100m, 2)) + "%";
                    }
                    else
                    {
                        return "Processing";
                    }
                }
            }

            public string Format_Total
            {
                get
                {
                    return String.Format(CultureInfo.InvariantCulture, "{0:0.########}", Math.Round(amount * price, 8));
                }
            }

            public bool Cancel_Visible //If we can see the cancel checkbox or not
            {
                get
                {
                    if (is_request == false)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            //Open Orders in an order are hidden from view
            public bool In_Order
            {
                get
                {
                    if (order_stage > 0)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyName = "")
            {
                if(NebliDex_UI == null) { return; }
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }

        }

        //Simple class and list for historical trades
        public static ObservableCollection<MyTrade> HistoricalTradeList = new ObservableCollection<MyTrade>();

        public class MyTrade : System.ComponentModel.INotifyPropertyChanged
        {
            public string _date;
            public string _pair;
            public string _type;
            public string _price;
            public string _amount;
            public string TxID;

            public event PropertyChangedEventHandler PropertyChanged;

            private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyName = "")
            {
                if(NebliDex_UI == null) { return; }
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }

            //Properties
            public string Date
            {
                get
                {
                    return _date;
                }
                set
                {
                    _date = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged("Line_Color");
                }
            }

            public string Pair
            {
                get
                {
                    return _pair;
                }
                set
                {
                    _pair = value;
                }
            }

            public string Type
            {
                get
                {
                    return _type;
                }
                set
                {
                    _type = value;
                }
            }

            public string Amount
            {
                get
                {
                    return _amount;
                }
                set
                {
                    _amount = value;
                }
            }

            public string Price
            {
                get
                {
                    return _price;
                }
                set
                {
                    _price = value;
                }
            }

            public Xamarin.Forms.Color Line_Color
            {
                get
                {
                    if(Date == "PENDING")
                    {
                        return App.blue_color;
                    }else
                    {
                        return Xamarin.Forms.Color.White;
                    }
                }
            }
        }

        //Only 24 hours are stored in recent trade list
        public static ObservableCollection<RecentTrade>[] RecentTradeList = new ObservableCollection<RecentTrade>[total_markets];

        public class RecentTrade
        {
            public int utctime;
            public int market;
            public int type;
            public decimal price;
            public decimal amount;

            //UI Properties

            public string Format_Price
            {
                get { return String.Format(CultureInfo.InvariantCulture, "{0:0.########}", price); }
            }

            public string Format_Amount
            {
                get { return String.Format(CultureInfo.InvariantCulture, "{0:0.########}", amount); }
            }

            public string Format_Time
            {
                get { return UTC2DateTime(utctime).ToString("HH: mm: ss"); }
            }

            public string Format_Type
            {
                get
                {
                    if (type == 0)
                    {
                        return "BUY";
                    }
                    else
                    {
                        return "SELL";
                    }
                }
            }
        }

        public static ObservableCollection<OpenOrder> MyOpenOrderList = new ObservableCollection<OpenOrder>(); //One list for my open orders

        //Dictionary to CN Nodes
        public static Dictionary<string, CriticalNode> CN_Nodes_By_IP = new Dictionary<string, CriticalNode>(); //IP refers to Neblio Address
        public class CriticalNode
        {
            public string ip_add; //Only field populated by all nodes
            public int total_markets = MainService.total_markets; //The total markets for the node, this can vary
            public string signature_ip = null; //Used to verify IP address
            public decimal ndex; //Used to get validation node, amount of ndex
            public string pubkey; //Used to verify
            public int lastchecked; //The last time the node was checked for balance (checks at most every 15 minutes)
            public uint strikes; //In case the critical node acts up, 10 strikes and blacklisted for 10 days
            public bool rebroadcast = false; //Flag that is true when we are rebroadcasting the node
        }
        //Critical nodes only add other critical nodes after verified signed message from address with account minimum threshold of NDEX

        //Store the TNs that connect to the critical nodes by IP, we want to prevent 1 IP from having too many connections
        public static Dictionary<string, int> TN_Connections = new Dictionary<string, int>();

        //These classes are not being used
        public static List<CoolDownTrader> CoolDownList = new List<CoolDownTrader>(); //List for all cooldown traders (for CNs only)

        public class CoolDownTrader
        {
            public int utctime; //In seconds
            public int cointype; //0 = nebl blockchain, 1 = btc blockchain, 2 = ltc blockchain
            public string address; //The address of the recent requester
        }

        public static List<LastPriceObject>[] ChartLastPrice = new List<LastPriceObject>[2]; //Two Lists, 15 minutes and 90 minutes worth for each market
        public static int ChartLastPrice15StartTime = 0; //The time the list started collecting data

        public class LastPriceObject
        {
            public int market;
            public decimal price;
            public int atime = 0; //The time the trade was completed
        }

        //Candles are made after 15 minutes and 90 minutes respectively and stored in SQLite DB
        public static List<Candle> VisibleCandles = new List<Candle>(); //The candles visible on screen
                                                                        //Loaded from the database and shown on screen, there is a maximum of 100 visible candles

        public class Candle
        {
            public double high;
            public double low;
            public double open;
            public double close;

            public Candle() { }
            public Candle(double op)
            {
                high = op; open = op; close = op; low = op;
            }
        }

        //Global exception handler, write to file
        public static void SetupExceptionHandlers()
        {
#if !DEBUG
            NebliDexNetLog("Loading release mode exception handlers");
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LastExceptionHandler((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");


            TaskScheduler.UnobservedTaskException += (s, e) =>
                LastExceptionHandler(e.Exception, "TaskScheduler.UnobservedTaskException");

            Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (s, e) =>
                LastExceptionHandler(e.Exception, "AndroidEnvironment.UnhandledExceptionRaiser");
#endif
        }

        public static void LastExceptionHandler(Exception e, string source)
        {
            //Only for release mode, otherwise we want it to crash
            if (IgnorableException(e) == true)
            {
                NebliDexNetLog("A non-fatal uncaught error has occurred: " + source + ": " + e);
                return;
            }
            NebliDexNetLog("A fatal unhandled error has occurred");
            if (e != null)
            {
                NebliDexNetLog(source + ": " + e); //Write the exception to file
            }
            program_loaded = false;

            ExitProgram();
        }

        public static bool IgnorableException(Exception e)
        {
            //This will return true if the exception is ignorable thus return to program
            string etext = e.ToString();
            if (etext.IndexOf("MyGetResponseAsync", StringComparison.InvariantCulture) > -1)
            {
                //This error will propagate due to bug in Mono
                return true;
            }
            if (etext.IndexOf("MobileAuthenticatedStream", StringComparison.InvariantCulture) > -1)
            {
                //This error will propagate due to bug in newer versions for Mono
                return true;
            }
            return false;
        }

        //All methods here are static (1 application)
        public static void Start()
        {
            //Create the Market Lists
            for (int it = 0; it < total_markets; it++)
            {
                //These market based lists make it easier to index orders
                OpenOrderList[it] = new List<OpenOrder>();
                //Create the Recent Trade Lists
                RecentTradeList[it] = new ObservableCollection<RecentTrade>();
            }

            //Add market list, the order is very important
            for (int im = 0; im < total_markets; im++)
            {
                Market mark = new Market(im);
                MarketList.Add(mark);
            }

            //Create the chart plotter
            ChartLastPrice[0] = new List<LastPriceObject>(); //24 hour
            ChartLastPrice[1] = new List<LastPriceObject>(); //7 Day

            //Set the default fees
            //Neblio rounds up to closest 0.0001
            blockchain_fee[0] = 0.00011m;  //This is fee per 1000 bytes (2000 hex characters)
            blockchain_fee[1] = 0.0012m; //Default fees per kb
            blockchain_fee[2] = 0.0020m;
            blockchain_fee[3] = 0.0020m;
            blockchain_fee[4] = 0.00002m; //BCH (2000 sat/kb default)
            blockchain_fee[5] = 0.0020m; //MONA
            blockchain_fee[6] = 5; //ETH default gas price in gwei

            //Set the dust minimums as well
            dust_minimum[0] = 0.0001m; //Cannot send an output less than this
            dust_minimum[1] = 0.0000547m;
            dust_minimum[2] = 0.0000547m;
            dust_minimum[3] = 0.0000547m;
            dust_minimum[4] = 0.0000001m; //BCH (Very low dust minimum)
            dust_minimum[5] = 0.001m; //MONA
            dust_minimum[6] = 0.000000001m; //ETH

            if (testnet_mode == true)
            {
                critical_node_port--; //Testnet is one below mainnet
            }

            //Now create database
            if (NebliDex_Activity != null)
            {
                NebliDex_Activity.RunOnUiThread(() => {
                    App.UpdateSplashPageStatus("Loading Databases");
                });
            }
            bool ok = CreateDatabase();
            if (ok == false) { return; } //This only occurs if lock file was already open

            //Load the wallet
            if (NebliDex_Activity != null)
            {
                NebliDex_Activity.RunOnUiThread(() => {
                    App.UpdateSplashPageStatus("Loading Wallet");
                });
            }

            NebliDexNetLog("Loading the wallet");
            CheckWallet();
            ok = LoadWallet();
            if (ok == false) { return; } //Failed to load wallet

            //Create the RSA keys
            GC.Collect(0); //Program runs out of memory around here
            NebliDexNetLog("Creating RSA Keys");
            string[] rsakeys = GenerateRSAKeys(1024);
            my_rsa_pubkey = rsakeys[0]; //Public key
            my_rsa_privkey = rsakeys[1]; //Private key

            //Connecting to Electrum
            if (NebliDex_Activity != null)
            {
                NebliDex_Activity.RunOnUiThread(() => {
                    App.UpdateSplashPageStatus("Finding Electrum Servers");
                });
            }
            NebliDexNetLog("Finding Electrum Servers");

            FindElectrumServers();

            //Perform first major cleanup
            GC.Collect();

            //Get the correct DNS 
            if (NebliDex_Activity != null)
            {
                NebliDex_Activity.RunOnUiThread(() => {
                    App.UpdateSplashPageStatus("Finding Critical Nodes");
                });
            }

            NebliDexNetLog("Finding Critical Nodes");
            if (File.Exists(App_Path + "/cn_list.dat") == false)
            {
                string user_seed = UserPrompt("Enter A DNS Seed (URL or IP)","OK","Cancel",false,Default_DNS_SEED);
                if(user_seed.Length > 0)
                {
                    DNS_SEED = user_seed.Trim();
                    IPAddress ip_result;
                    if(IPAddress.TryParse(DNS_SEED,out ip_result))
                    {
                        DNS_SEED_TYPE = 1; //This is an IP address
                    }
                }
                FindCNServers(true);
            }

            //Perform second major cleanup
            GC.Collect();

            bool electrum_connect = false;
            bool cn_connect = false;

            NebliDexNetLog("Connecting Critical Node Server");
            if (NebliDex_Activity != null)
            {
                NebliDex_Activity.RunOnUiThread(() => {
                    App.UpdateSplashPageStatus("Connecting Critical Node Server");
                });
            }
            cn_connect = ConnectCNServer(true);

            bool cncount_ok =  AccurateCNOnlineCount();
            if (cncount_ok == false)
            {
                //We need to update the list of connected nodes
                if (NebliDex_Activity != null)
                {
                    NebliDex_Activity.RunOnUiThread(() => {
                        App.UpdateSplashPageStatus("Updating Critical Nodes List");
                    });
                }

                FindCNServers(false);
                if (File.Exists(App_Path + "/cn_list.dat") == true)
                {
                    LoadCNList();
                }
            }

            if (NebliDex_Activity != null)
            {
                NebliDex_Activity.RunOnUiThread(() => {
                    App.UpdateSplashPageStatus("Connecting Electrum Servers");
                });
            }
            NebliDexNetLog("Connecting Electrum Servers");
            electrum_connect = ConnectElectrumServers(-1);

            if (NebliDex_Activity != null)
            {
                NebliDex_Activity.RunOnUiThread(() => {
                    App.UpdateSplashPageStatus("Syncing Electrum Servers");
                });
            }

            NebliDexNetLog("Syncing Electrum Servers");
            CheckElectrumServerSync();

            if (NebliDex_Activity != null)
            {
                NebliDex_Activity.RunOnUiThread(() => {
                    App.UpdateSplashPageStatus("Retrieving Chart Data");
                });
            }

            //Load the UI look
            Load_UI_Config(); //Loads the look and the market

            GetCNMarketData(exchange_market); //This will get the market data from the critical node
            LoadTradeHistory(); //Load historical trade data

            if (NebliDex_Activity != null)
            {
                NebliDex_Activity.RunOnUiThread(() => {
                    App.UpdateSplashPageStatus("Loading Charts");
                });
            }
            NebliDexNetLog("NebliDex is opening now");

            bool savedord = CheckSavedOrders();
            if (savedord == true)
            {
                Prompt_Load_Saved_Orders();
            }
            LegalWarning();

            //Setup the timers that are run every 30 seconds
            //Mostly for Keep Alive and balance checking
            PeriodicTimer = new Timer(new TimerCallback(PeriodicNetworkQuery), null, 0, 30000);
            //Also create wakelock that we may need to use when orders present
            Android.OS.PowerManager pw_mgr = (Android.OS.PowerManager)NebliDex_Service.GetSystemService("power");
            PeriodicTimer_WakeLock = pw_mgr.NewWakeLock(Android.OS.WakeLockFlags.Partial, "NebliDex_Wakelock");

            //Link timer to updating charts
            int waittime = next_candle_time - UTCTime();
            if (waittime < 0)
            {
                waittime = 0;
            } //Sync candle times across clients

            CandleTimer = new Timer(new TimerCallback(PeriodicCandleMaker), null, waittime * 1000, System.Threading.Timeout.Infinite);

            //Ran every 6 hours and consolidates UTXOs, starts 30 seconds in after program load
            ConsolidateTimer = new Timer(new TimerCallback(WalletConsolidationCheck), null, 30 * 1000, 60000 * 60 * 6);

            program_loaded = true;            
            current_ui_page = 1; //Market is the next page to load
            if(cn_connect == false || electrum_connect == false)
            {
                current_ui_page = 4; //Change page to settings so user know full connection not there yet
            }

            if(NebliDex_UI != null)
            {
                NebliDex_Activity.RunOnUiThread(() => {
                    NebliDex_UI.LoadPage(current_ui_page);
                });
            }
        }

        public static bool CreateDatabase()
        {
            //This function creates the databases and loads the user data
            //Anything that gets deleted when user closes program is not stored in database

            if (File.Exists(App_Path + "/debug.log") == true)
            {
                long filelength = new System.IO.FileInfo(App_Path + "/debug.log").Length;
                if (filelength > 10000000)
                { //Debug log is greater than 10MB
                    lock (debugfileLock)
                    {
                        File.Delete(App_Path + "/debug.log"); //Clear the old log
                    }
                }
            }

            NebliDexNetLog("Loading new instance of NebliDex Android version: " + version_text);
            if (testnet_mode == true)
            {
                NebliDexNetLog("Testnet mode is on");
            }

            SetupExceptionHandlers();

            if (File.Exists(App_Path + "/neblidex.db") == false)
            {
                NebliDexNetLog("Creating databases");
                SqliteConnection.CreateFile(App_Path + "/neblidex.db");
                //Now create the tables
                string myquery;
                SqliteConnection mycon = new SqliteConnection("Data Source=\"" + App_Path + "/neblidex.db\";Version=3;");
                mycon.Open();

                //Create My Tradehistory table
                myquery = "Create Table MYTRADEHISTORY";
                myquery += " (nindex Integer Primary Key ASC, utctime Integer, market Integer, type Integer, price Text, amount Text, txhash Text, pending Integer)";
                SqliteCommand statement = new SqliteCommand(myquery, mycon);
                statement.ExecuteNonQuery();
                statement.Dispose();

                //Create Transaction table
                myquery = "Create Table MYTRANSACTIONS";
                myquery += " (nindex Integer Primary Key ASC, utctime Integer, txhash Text, from_add Text, to_add Text, cointype Integer, amount Text,";
                myquery += " custodial_redeemscript_add Text,  custodial_redeemscript Text, counterparty_cointype Integer, type Integer, waittime Integer,";
                myquery += " order_nonce_ref Text, req_utctime_ref Integer, validating_nodes Text, makertxhash Text, atomic_unlock_time Integer, atomic_refund_time Integer,";
                myquery += " receive_amount Text, to_add_redeemscript Text, atomic_secret_hash Text, atomic_secret Text)";
                //Validating nodes are divided by | separator, first one is main validator, the others are auditors
                statement = new SqliteCommand(myquery, mycon);
                statement.ExecuteNonQuery();
                statement.Dispose();

                //Create Validating Transaction table
                myquery = "Create Table VALIDATING_TRANSACTIONS";
                myquery += " (nindex Integer Primary Key ASC, utctime Integer, order_nonce_ref Text, maker_pubkey Text, taker_pubkey Text, custodial_privkey Text, maker_from_add Text,";
                myquery += " maker_feetx Text, maker_feetx_hash Text, maker_tx Text, maker_txhash Text, status Integer, redeemscript Text, ndex_fee Text, rbalance Text, claimed Integer,";
                myquery += " taker_feetx Text, taker_feetx_hash Text, validating_cn_pubkey Text, market Integer, reqtype Integer, redeemscript_add Text, waittime Integer,";
                myquery += " maker_sendamount Text, taker_receive_add Text, taker_tx Text)";
                statement = new SqliteCommand(myquery, mycon);
                statement.ExecuteNonQuery();
                statement.Dispose();

                //Mostly for CNs to block known bad actors
                //Type can be 0 for IPs and 1 for Neblio addresses
                myquery = "Create Table BLACKLIST";
                myquery += " (nindex Integer Primary Key ASC, utctime Integer, type Integer, value Text)";
                statement = new SqliteCommand(myquery, mycon);
                statement.ExecuteNonQuery();
                statement.Dispose();

                //Create Candlestick table (24 hour)
                myquery = "Create Table CANDLESTICKS24H";
                myquery += " (nindex Integer Primary Key ASC, utctime Integer, market Integer, highprice Text, lowprice Text, open Text, close Text)";
                statement = new SqliteCommand(myquery, mycon);
                statement.ExecuteNonQuery();
                statement.Dispose();

                //Create Candlestick table (7 day)
                myquery = "Create Table CANDLESTICKS7D";
                myquery += " (nindex Integer Primary Key ASC, utctime Integer, market Integer, highprice Text, lowprice Text, open Text, close Text)";
                statement = new SqliteCommand(myquery, mycon);
                statement.ExecuteNonQuery();
                statement.Dispose();

                //Create CN Transaction fee data table
                myquery = "Create Table CNFEES";
                myquery += " (nindex Integer Primary Key ASC, utctime Integer, market Integer, fee Text)";
                statement = new SqliteCommand(myquery, mycon);
                statement.ExecuteNonQuery();
                statement.Dispose();

                //Create a table for version control
                myquery = "Create Table FILEVERSION (nindex Integer Primary Key ASC, version Integer)";
                statement = new SqliteCommand(myquery, mycon);
                statement.ExecuteNonQuery();
                statement.Dispose();
                //Insert a row with version
                myquery = "Insert Into FILEVERSION (version) Values (" + sqldatabase_version + ");";
                statement = new SqliteCommand(myquery, mycon);
                statement.ExecuteNonQuery();
                statement.Dispose();

                //Create Saved Orders data 
                myquery = "Create Table SAVEDORDERS";
                myquery += " (nindex Integer Primary Key ASC, market Integer, type Integer, nonce Text, price Text, amount Text, min_amount Text)";
                statement = new SqliteCommand(myquery, mycon);
                statement.ExecuteNonQuery();
                statement.Dispose();

                mycon.Dispose();
            }

            string myquery2;
            SqliteConnection mycon2 = new SqliteConnection("Data Source=\"" + App_Path + "/neblidex.db\";Version=3;");
            mycon2.Open();

            //Delete the Candles Database as they have come out of sync and obtain new chart from another server
            myquery2 = "Delete From CANDLESTICKS7D";
            SqliteCommand statement2 = new SqliteCommand(myquery2, mycon2);
            statement2.ExecuteNonQuery();
            statement2.Dispose();

            myquery2 = "Delete From CANDLESTICKS24H";
            statement2 = new SqliteCommand(myquery2, mycon2);
            statement2.ExecuteNonQuery();
            statement2.Dispose();

            //Additional params in case of older versions
            UpdateDatabase(mycon2);

            mycon2.Close();
            return true;
        }

        public static void UpdateDatabase(SqliteConnection dat)
        {
            //First check to see the version control database exists
            string myquery = "Select name From sqlite_master Where type='table' And name='FILEVERSION'";
            SqliteCommand statement = new SqliteCommand(myquery, dat);
            SqliteDataReader statement_reader = statement.ExecuteReader();
            bool dataavail = statement_reader.Read();
            statement_reader.Close();
            statement.Dispose();
            int database_version = 0;
            if (dataavail == true)
            {
                //Go through the database and find the value
                myquery = "Select version From FILEVERSION";
                statement = new SqliteCommand(myquery, dat);
                statement_reader = statement.ExecuteReader();
                dataavail = statement_reader.Read();
                if (dataavail == true)
                {
                    database_version = Convert.ToInt32(statement_reader["version"].ToString()); //Get the database version
                }
                statement_reader.Close(); //Make sure these are closed
                statement.Dispose();
            }

            if (database_version >= sqldatabase_version) { return; } //No update available
            NebliDexNetLog("Updating database");

            if (database_version == 0)
            {
                //Create the table for database versions
                myquery = "Create Table FILEVERSION (nindex Integer Primary Key ASC, version Integer)";
                statement = new SqliteCommand(myquery, dat);
                statement.ExecuteNonQuery();
                statement.Dispose();
                //Insert a row with version
                myquery = "Insert Into FILEVERSION (version) Values (" + sqldatabase_version + ");";
                statement = new SqliteCommand(myquery, dat);
                statement.ExecuteNonQuery();
                statement.Dispose();

                //Now alter table for Validating Transactions
                myquery = "Alter Table VALIDATING_TRANSACTIONS Add Column maker_sendamount Text;";
                statement = new SqliteCommand(myquery, dat);
                statement.ExecuteNonQuery();
                statement.Dispose();

                //Add another column
                myquery = "Alter Table VALIDATING_TRANSACTIONS Add Column taker_receive_add Text;";
                statement = new SqliteCommand(myquery, dat);
                statement.ExecuteNonQuery();
                statement.Dispose();
                database_version++;
            }

            if (database_version == 1)
            {
                //Create Saved Orders data 
                myquery = "Create Table SAVEDORDERS";
                myquery += " (nindex Integer Primary Key ASC, market Integer, type Integer, nonce Text, price Text, amount Text, min_amount Text)";
                statement = new SqliteCommand(myquery, dat);
                statement.ExecuteNonQuery();
                statement.Dispose();
                database_version++;
            }

            if (database_version == 2)
            { //Moving from database version 2 to version 3             
              //Add columns
                myquery = "Alter Table MYTRANSACTIONS Add Column atomic_unlock_time Integer;";
                myquery += " Alter Table MYTRANSACTIONS Add Column atomic_refund_time Integer;";
                myquery += " Alter Table MYTRANSACTIONS Add Column receive_amount Text;";
                myquery += " Alter Table MYTRANSACTIONS Add Column to_add_redeemscript Text;";
                myquery += " Alter Table MYTRANSACTIONS Add Column atomic_secret_hash Text;";
                myquery += " Alter Table MYTRANSACTIONS Add Column atomic_secret Text;";
                statement = new SqliteCommand(myquery, dat);
                statement.ExecuteNonQuery();
                statement.Dispose();

                myquery = "Alter Table VALIDATING_TRANSACTIONS Add Column taker_tx Text;";
                statement = new SqliteCommand(myquery, dat);
                statement.ExecuteNonQuery();
                statement.Dispose();
                database_version++;
            }

            //Future database updates go here

            //Now update the version for the file
            myquery = "Update FILEVERSION Set version = " + sqldatabase_version;
            statement = new SqliteCommand(myquery, dat);
            statement.ExecuteNonQuery();
            statement.Dispose();
        }

        public static void LoadTradeHistory()
        {
            //Load the Historical trades into the list
            HistoricalTradeList.Clear();
            SqliteConnection mycon = new SqliteConnection("Data Source=\"" + MainService.App_Path + "/neblidex.db\";Version=3;");
            mycon.Open();

            //Set our busy timeout, so we wait if there are locks present
            SqliteCommand statement = new SqliteCommand("PRAGMA busy_timeout = 5000", mycon); //Create a transaction to make inserts faster
            statement.ExecuteNonQuery();
            statement.Dispose();
            string myquery = "Select utctime, market, type, price, amount, pending, txhash From MYTRADEHISTORY Order By utctime DESC";
            statement = new SqliteCommand(myquery, mycon);
            SqliteDataReader statement_reader = statement.ExecuteReader();
            while (statement_reader.Read())
            {
                string format_date = "";
                string format_type;
                string format_market = "";
                string txhash = statement_reader["txhash"].ToString();
                int utctime = Convert.ToInt32(statement_reader["utctime"]);
                if (Convert.ToInt32(statement_reader["pending"]) == 0)
                {
                    format_date = UTC2DateTime(utctime).ToString("yyyy-MM-dd");
                }
                else if (Convert.ToInt32(statement_reader["pending"]) == 1)
                {
                    format_date = "PENDING";
                }
                else
                {
                    format_date = "CANCELLED";
                }
                if (Convert.ToInt32(statement_reader["type"]) == 0)
                {
                    format_type = "BUY";
                }
                else
                {
                    format_type = "SELL";
                }
                int market = Convert.ToInt32(statement_reader["market"]);
                decimal price = Decimal.Parse(statement_reader["price"].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);
                decimal amount = Decimal.Parse(statement_reader["amount"].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);
                format_market = MarketList[market].format_market;
                MyTrade mt = new MyTrade { Date = format_date, Pair = format_market, Type = format_type, Price = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", price), Amount = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", amount), TxID = txhash };
                HistoricalTradeList.Add(mt);
            }
            statement_reader.Close();
            statement.Dispose();
            mycon.Close();
        }

        public static bool VerifyTradeRequest(JObject tjs)
        {
            //We will verify the trade request against our open orders
            string nonce = tjs["cn.order_nonce"].ToString();
            decimal amount = Convert.ToDecimal(tjs["cn.amount"].ToString(), CultureInfo.InvariantCulture);

            //Find my open order matching this nonce
            OpenOrder myord = null;
            lock (MyOpenOrderList)
            {
                for (int i = 0; i < MyOpenOrderList.Count; i++)
                {
                    if (MyOpenOrderList[i].order_nonce.Equals(nonce) == true && MyOpenOrderList[i].is_request == false && MyOpenOrderList[i].queued_order == false)
                    {
                        //Found the order, it matches
                        myord = MyOpenOrderList[i]; break;
                    }
                }
            }

            if (myord == null) { return false; }
            if (amount > myord.amount) { return false; } //Somehow amount was too big
            decimal min_amount = myord.minimum_amount;
            if (min_amount > myord.amount) { min_amount = myord.amount; }
            if (amount < min_amount) { return false; } //Request size too small

            int sendwallet = 0;
            int receivewallet = 0; //Upload the addresses for the maker
            decimal sendamount = 0;
            if (myord.type == 0)
            {
                sendwallet = MarketList[myord.market].base_wallet;
                receivewallet = MarketList[myord.market].trade_wallet;
                sendamount = Math.Round(amount * myord.price, 8);
            }
            else
            {
                sendwallet = MarketList[myord.market].trade_wallet;
                receivewallet = MarketList[myord.market].base_wallet;
                sendamount = amount;
            }

            bool ntp1_wallet = IsWalletNTP1(sendwallet);
            if (ntp1_wallet == false)
            {
                //I am going to send a non-token, get the blockchain fee
                int wallet_blockchaintype = GetWalletBlockchainType(sendwallet);
                if (wallet_blockchaintype != 6)
                {
                    if (sendamount < blockchain_fee[wallet_blockchaintype]) { return false; } //Request is too small
                }
                else
                {
                    if (sendamount < GetEtherContractTradeFee(Wallet.CoinERC20(sendwallet))) { return false; }
                }
            }
            else
            {
                //Sending a token
                if (sendamount < 1) { return false; } //Cannot send 0 token
                                                      //Also check to make sure token amount is whole number
                if (Math.Abs(Math.Round(sendamount) - sendamount) > 0)
                {
                    return false;
                }
            }

            if (sendwallet == 3)
            {
                //Sending NDEX
                //Make sure its at least twice the ndex fee
                if (amount < ndex_fee * 2)
                {
                    return false;
                }
            }

            tjs["trade.maker_send_add"] = GetWalletAddress(sendwallet);
            tjs["trade.maker_receive_add"] = GetWalletAddress(receivewallet);

            //This function will also evaluate whether the wallet is available or not
            string msg = "";
            bool walletavail = CheckWalletBalance(sendwallet, sendamount, ref msg);
            if (walletavail == false)
            { //No money or not available
                return false;
            }

            bool fees_ok = CheckMarketFees(myord.market, myord.type, sendamount, ref msg, false);
            if (fees_ok == false)
            { //Not enough to cover the fees
                return false;
            }

            myord.validating = false; //Reset the validation value

            if (myord.queued_order == true) { return false; }

            //Now we will queue all the other open orders
            if (MyOpenOrderList.Count > 1)
            {
                QueueAllButOpenOrders(myord); //Queues all but this one
            }

            if (myord.queued_order == true)
            {
                NebliDexNetLog("Race condition detected between two orders");
                return false;
            } //Used in case of race condition and both get queued

            return true;
        }

        public static bool EvaluateRelayedOrder(DexConnection con, JObject jord, OpenOrder ord, bool cnmode)
        {
            //This function will evaluate a relayed order for addition to market
            //It doesn't actual check wallet balance. This has been deferred to validation node.

            //Bad numbers may cause overflows, but since its in try catch statement, should not crash program
            ord.order_nonce = jord["order.nonce"].ToString();
            ord.market = Convert.ToInt32(jord["order.market"].ToString());
            ord.type = Convert.ToInt32(jord["order.type"].ToString());
            ord.original_amount = Math.Round(Convert.ToDecimal(jord["order.originalamount"].ToString(), CultureInfo.InvariantCulture), 8);
            ord.price = Math.Round(Convert.ToDecimal(jord["order.price"].ToString(), CultureInfo.InvariantCulture), 8);
            ord.amount = ord.original_amount;
            ord.minimum_amount = Math.Round(Convert.ToDecimal(jord["order.min_amount"].ToString(), CultureInfo.InvariantCulture), 8);
            ord.order_stage = 0;
            ord.cooldownend = 0;

            //Check if cancellation token present
            lock (CancelOrderTokenList)
            {
                for (int i = 0; i < CancelOrderTokenList.Count; i++)
                {
                    if (CancelOrderTokenList[i].Equals(ord.order_nonce) == true)
                    {
                        //This order was previously cancelled by token
                        CancelOrderTokenList.RemoveAt(i); //Take out of list
                        return false; //Do not relay/add this order
                    }
                }
            }

            if (ord.order_nonce.Length != 32)
            {
                //Should be 32 characters long
                return false;
            }
            if (ord.market < 0 || ord.market >= total_markets)
            {
                return false;
            }
            if (ord.type != 0 && ord.type != 1)
            {
                return false;
            }
            if (ord.original_amount < 0)
            {
                return false;
            }
            if (ord.minimum_amount <= 0)
            {
                return false;
            }
            if (ord.minimum_amount > ord.original_amount)
            {
                return false;
            }
            if (ord.price <= 0)
            {
                return false;
            }
            if (ord.price > max_order_price)
            {
                return false;
            }

            if (cnmode == true)
            {
                ord.ip_address_port[0] = jord["order.ip"].ToString();
                ord.ip_address_port[1] = jord["order.port"].ToString();
                ord.cn_relayer_ip = jord["order.cn_ip"].ToString();
            }

            //Detect if order nonce is unique for all markets
            for (int market = 0; market < total_markets; market++)
            {
                lock (OpenOrderList[market])
                {
                    for (int i = 0; i < OpenOrderList[market].Count; i++)
                    {
                        if (OpenOrderList[market][i].order_nonce.Equals(ord.order_nonce) == true)
                        {
                            //Someone already has this nonce
                            return false;
                        }
                    }
                }
            }

            lock (OpenOrderList[ord.market])
            {
                //If everything is good, add order
                if (critical_node == true || ord.market == exchange_market)
                {
                    OpenOrderList[ord.market].Add(ord);
                }
                //Add to market list

                if(NebliDex_UI != null)
                {
                    NebliDex_UI.AddOrderToView(ord);
                }
                return true;
            }
        }

        public static void RemoveMyOrderNoLock(OpenOrder ord)
        {
            lock (OpenOrderList[ord.market])
            {
                for (int i = OpenOrderList[ord.market].Count - 1; i >= 0; i--)
                {
                    //Remove any order that matches our nonce
                    if (OpenOrderList[ord.market][i].order_nonce.Equals(ord.order_nonce) == true && OpenOrderList[ord.market][i].is_request == ord.is_request)
                    {
                        OpenOrderList[ord.market].RemoveAt(i);
                    }
                }
            }

            //Now remove the order from view
            if (NebliDex_UI != null)
            {
                NebliDex_UI.RemoveOrderFromView(ord);
            }
        }

        public static void CheckWallet()
        {
            //Wallet version is now 1

            //This function checks wallet format and if encryption present
            if (File.Exists(App_Path + "/account.dat") == false)
            {
                //Prompt user to enter password for future wallet
                my_wallet_pass = "";
                string pass1 = UserPrompt("Please enter a password for your new\nwallet. (Leave blank if one not desired)", "OK", "Cancel", true);
                if(pass1.Length > 0)
                {
                    string pass2 = "";
                    while(pass2 != pass1)
                    {
                        pass2 = "";
                        pass2 = UserPrompt("For confirmation, please re-enter previously entered password. Do not lose this password. There is no option to recover it!", "OK", "Cancel", true);
                        if(pass2 != pass1 && pass2.Length > 0)
                        {
                            MessageBox("Notice", "The password doesn't match the previously entered.", "OK", true);
                        }else if(pass2.Length == 0)
                        {
                            break; //User doesn't want a password anymore
                        }
                    }
                    if(pass2.Length > 0)
                    {
                        my_wallet_pass = pass2;
                    }
                }
            }
            else
            {
                //File does exist, check wallet if password does exist
                int version = 0;
                int encrypted = 0;

                using (System.IO.StreamReader file =
                    new System.IO.StreamReader(@App_Path + "/account.dat", false))
                {
                    string first_line = file.ReadLine(); //In old version, this is master key
                    version = Convert.ToInt32(first_line);
                    encrypted = Convert.ToInt32(file.ReadLine());
                }

                if (encrypted > 0)
                {
                    NebliDexNetLog("Wallet is encrypted");
                    //Need the password to decrypt the wallet

                    my_wallet_pass = UserPrompt("\nPlease enter your wallet password.", "OK", "Cancel", true);
                }
                if (version < accountdat_version)
                {
                    NebliDexNetLog("Wallet needs to be upgraded");
                }
            }
        }

        public static bool VerifyWalletPassword(string privkey, string address, int type)
        {
            //This will return true if the wallet was decrypted successfully
            if (GetWalletBlockchainType(type) == 6)
            {
                //Eth
                string my_eth_add = GenerateEthAddress(privkey);
                if (my_eth_add.Equals(address) == true)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            try
            {
                lock (transactionLock)
                {
                    //Prevent more than one thread from using the network
                    Network my_net;
                    if (testnet_mode == false)
                    {
                        my_net = Network.Main;
                    }
                    else
                    {
                        my_net = Network.TestNet;
                    }

                    //Change the network
                    ChangeVersionByte(type, ref my_net); //NEBL network
                    ExtKey priv_key = ExtKey.Parse(privkey, my_net);
                    string my_add = priv_key.PrivateKey.PubKey.GetAddress(my_net).ToString();
                    if (address.Equals(my_add) == true)
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                NebliDexNetLog("Error decrypting wallet: " + e.ToString());
            }
            NebliDexNetLog("Failed to open wallet");
            return false;
        }

        public static bool LoadWallet()
        {
            //The the wallet for this user or create if none exists
            WalletList.Clear(); //Remove old wallet list

            if (File.Exists(App_Path + "/account.dat") == false)
            {
                //Create the wallet

                //Using statement closes file if error occurs
                using (System.IO.StreamWriter file =
                    new System.IO.StreamWriter(@App_Path + "/account.dat", false))
                {
                    int i;
                    file.WriteLine(accountdat_version); //Version of account file
                    if (my_wallet_pass.Length > 0)
                    {
                        file.WriteLine(1); //This file will be encrypted
                    }
                    else
                    {
                        file.WriteLine(0); //No encryption present
                    }
                    for (i = 0; i < total_cointypes; i++)
                    {
                        //Create wallet accounts
                        //Only blockchains involved
                        string masterkey = GenerateMasterKey();
                        if (my_wallet_pass.Length > 0)
                        {
                            file.WriteLine(AESEncrypt(masterkey, my_wallet_pass));
                        }
                        else
                        {
                            file.WriteLine(masterkey);
                        }
                        file.WriteLine(i); //Wallet blockchain type
                        file.WriteLine("1"); //Only 1 account
                        ExtKey priv_key = GeneratePrivateKey(masterkey, 0);
                        Network my_net;
                        if (testnet_mode == false)
                        {
                            my_net = Network.Main;
                        }
                        else
                        {
                            my_net = Network.TestNet;
                        }
                        string privatekey = priv_key.ToString(my_net);
                        string myaddress = GenerateCoinAddress(priv_key, GetWalletType(i));
                        if (i == 6)
                        {
                            //Calculate the Ethereum address from the private key
                            myaddress = GenerateEthAddress(privatekey);
                        }
                        if (my_wallet_pass.Length > 0)
                        {
                            privatekey = AESEncrypt(privatekey, my_wallet_pass);
                        }
                        file.WriteLine(privatekey);
                        file.WriteLine(myaddress);
                    }
                    file.Flush();
                }
            }

            //Now load the wallet information
            int this_wallet_version = 0;
            using (System.IO.StreamReader file2 =
                new System.IO.StreamReader(@App_Path + "/account.dat", false))
            {
                int i;
                this_wallet_version = Convert.ToInt32(file2.ReadLine()); //Wallet version
                int max_wallets = total_cointypes;
                if (this_wallet_version == 0)
                {
                    max_wallets = 3;
                }
                int enc = Convert.ToInt32(file2.ReadLine());
                for (i = 0; i < max_wallets; i++)
                {
                    //Load the wallet
                    Wallet wal = new Wallet();
                    file2.ReadLine(); //Skip the master key
                    wal.blockchaintype = Convert.ToInt32(file2.ReadLine()); //Get the wallet blockchain type
                    wal.type = GetWalletType(wal.blockchaintype);

                    int amount = Convert.ToInt32(file2.ReadLine()); //Amounts of addresses
                    for (int i2 = 1; i2 <= amount; i2++)
                    {
                        if (i2 == amount)
                        {
                            //This is the one we want
                            wal.private_key = file2.ReadLine();
                            wal.address = file2.ReadLine();
                            if (enc > 0)
                            {
                                //This wallet is encrypted so decrypt it
                                bool good = true;
                                try
                                {
                                    //This might fail if the wallet cannot be decrypted
                                    wal.private_key = AESDecrypt(wal.private_key, my_wallet_pass);
                                }
                                catch (Exception)
                                {
                                    NebliDexNetLog("Failed to decrypt this wallet");
                                    good = false;
                                }

                                //Now verify if the wallet was decrypted successfully
                                if (good == true)
                                {
                                    good = VerifyWalletPassword(wal.private_key, wal.address, wal.type);
                                    if (good == false)
                                    {
                                        NebliDexNetLog("Verify password failed.");
                                    }
                                }

                                if (good == false)
                                {
                                    //This will block until the message box is pressed ok
                                    MessageBox("Error!", "Failed to decrypt your wallet due to wrong password!", "OK", true);

                                    ExitProgram(); //Quit the program now
                                    return false;
                                }
                            }
                        }
                        else
                        {
                            file2.ReadLine();
                            file2.ReadLine(); //Skip these lines
                        }
                    }

                    if (WalletList.Count == 0)
                    {
                        WalletList.Add(wal); //Add to the wallet list
                    }
                    else
                    {
                        //Load BTC after Neblio and LTC after BTC
                        WalletList.Insert(wal.blockchaintype, wal);
                    }
                    if (wal.type == 0)
                    { //NDEX and other NTP1 tokens
                      //Load all NEBL based tokens from this private key

                        //NDEX based on NEBL
                        //Make a clone of this wallet but with a different type
                        for (int i2 = 0; i2 < Wallet.total_coin_num; i2++)
                        {
                            if (Wallet.CoinNTP1(i2) == true && Wallet.CoinActive(i2) == true)
                            {
                                //Wallet is Active and NTP1, add it to neblio wallet address                                
                                Wallet wal2 = new Wallet();
                                wal2.type = i2;
                                wal2.private_key = wal.private_key;
                                wal2.address = wal.address;
                                wal2.blockchaintype = 0; //Neblio based
                                WalletList.Add(wal2);
                            }
                        }
                    }
                    else if (wal.type == 17)
                    {
                        //ERC20 Tokens
                        for (int i2 = 0; i2 < Wallet.total_coin_num; i2++)
                        {
                            if (Wallet.CoinERC20(i2) == true && Wallet.CoinActive(i2) == true)
                            {
                                //Wallet is Active and ERC20, add it to ethereum wallet address                             
                                Wallet wal2 = new Wallet();
                                wal2.type = i2;
                                wal2.private_key = wal.private_key;
                                wal2.address = wal.address;
                                wal2.blockchaintype = 6; //ETH based
                                WalletList.Add(wal2);
                            }
                        }
                    }
                }
            }

            if (this_wallet_version < accountdat_version)
            {
                NebliDexNetLog("Upgrading the wallet now");
                string[] wallet_lines = File.ReadAllLines(@App_Path + "/account.dat"); //Get all the lines from the current account
                wallet_lines[0] = accountdat_version.ToString();
                File.WriteAllLines(@App_Path + "/account_new.dat", wallet_lines); //Copys information to new file
                if (this_wallet_version == 0)
                {
                    //Update it by adding new wallets
                    int pos = 3;
                    int max_wallet = 7;
                    using (System.IO.StreamWriter file =
                        new System.IO.StreamWriter(@App_Path + "/account_new.dat", true))
                    {
                        for (int i = pos; i < max_wallet; i++)
                        {
                            //Create wallet accounts
                            //Only blockchains involved
                            string masterkey = GenerateMasterKey();
                            if (my_wallet_pass.Length > 0)
                            {
                                file.WriteLine(AESEncrypt(masterkey, my_wallet_pass));
                            }
                            else
                            {
                                file.WriteLine(masterkey);
                            }
                            file.WriteLine(i); //Wallet blockchain type
                            file.WriteLine("1"); //Only 1 account
                            ExtKey priv_key = GeneratePrivateKey(masterkey, 0);
                            Network my_net;
                            if (testnet_mode == false)
                            {
                                my_net = Network.Main;
                            }
                            else
                            {
                                my_net = Network.TestNet;
                            }
                            string privatekey = priv_key.ToString(my_net);
                            string myaddress = GenerateCoinAddress(priv_key, GetWalletType(i));
                            if (i == 6)
                            {
                                //Calculate the Ethereum address from the private key
                                myaddress = GenerateEthAddress(privatekey);
                            }
                            //And now add the wallet files
                            Wallet wal = new Wallet();
                            wal.blockchaintype = i;
                            wal.type = GetWalletType(i);
                            wal.address = myaddress;
                            wal.private_key = privatekey; //Get the private key before it is encrypted
                            WalletList.Insert(wal.blockchaintype, wal);
                            if (my_wallet_pass.Length > 0)
                            {
                                privatekey = AESEncrypt(privatekey, my_wallet_pass);
                            }
                            file.WriteLine(privatekey);
                            file.WriteLine(myaddress);

                            //Add the ERC20 tokens
                            if (wal.type == 17)
                            {
                                //ERC20 Tokens
                                for (int i2 = 0; i2 < Wallet.total_coin_num; i2++)
                                {
                                    if (Wallet.CoinERC20(i2) == true && Wallet.CoinActive(i2) == true)
                                    {
                                        //Wallet is Active and ERC20, add it to ethereum wallet address                             
                                        Wallet wal2 = new Wallet();
                                        wal2.type = i2;
                                        wal2.private_key = wal.private_key;
                                        wal2.address = wal.address;
                                        wal2.blockchaintype = 6; //ETH based
                                        WalletList.Add(wal2);
                                    }
                                }
                            }
                        }
                        file.Flush();
                    }
                    this_wallet_version++;
                }

                //Future versions will go here

                //Move the files
                if (File.Exists(App_Path + "/account_new.dat") != false)
                {
                    File.Delete(App_Path + "/account.dat");
                    File.Move(App_Path + "/account_new.dat", App_Path + "/account.dat");
                }

                //Also delete the old electrum nodes list as the client will find the new nodes
                if (File.Exists(App_Path + "/electrum_peers.dat") == true)
                {
                    File.Delete(App_Path + "/electrum_peers.dat");
                }
            }

            // Now sort the Wallet List by name
            WalletList = SortedWallets(WalletList);

            return true;
        }

        public static ObservableCollection<Wallet> SortedWallets(ObservableCollection<Wallet> original)
        {
            ObservableCollection<Wallet> temp_wall = new ObservableCollection<Wallet>();
            int total_num = original.Count;
            for (int i = 0; i < total_num; i++)
            {
                bool inserted = false;
                for (int i2 = 0; i2 < temp_wall.Count; i2++)
                {
                    if (String.Compare(original[i].Coin, temp_wall[i2].Coin) < 0)
                    {
                        //Insert it before
                        temp_wall.Insert(i2, original[i]);
                        inserted = true;
                        break;
                    }
                }
                if (inserted == false)
                {
                    temp_wall.Add(original[i]); //Add to end of wallet list
                }
            }
            return temp_wall;
        }

        public static bool CheckPendingPayment()
        {
            if (running_consolidation_check == true) { return true; } //Wallet is checking for too many UTXOs

            //This will return true if there is a pending payment being processed
            SqliteConnection mycon = new SqliteConnection("Data Source=\"" + App_Path + "/neblidex.db\";Version=3;");
            mycon.Open();

            //Set our busy timeout, so we wait if there are locks present
            SqliteCommand statement = new SqliteCommand("PRAGMA busy_timeout = 5000", mycon); //Create a transaction to make inserts faster
            statement.ExecuteNonQuery();
            statement.Dispose();

            //Select rows that have not been finalized
            string myquery = "Select nindex From MYTRANSACTIONS Where type < 3";
            statement = new SqliteCommand(myquery, mycon);
            SqliteDataReader statement_reader = statement.ExecuteReader();
            bool dataavail = statement_reader.Read();
            statement_reader.Close();
            statement.Dispose();
            mycon.Close();

            return dataavail;
        }

        public static bool CheckPendingTrade()
        {
            //This will return true if there is a pending payment being processed
            SqliteConnection mycon = new SqliteConnection("Data Source=\"" + App_Path + "/neblidex.db\";Version=3;");
            mycon.Open();

            //Set our busy timeout, so we wait if there are locks present
            SqliteCommand statement = new SqliteCommand("PRAGMA busy_timeout = 5000", mycon); //Create a transaction to make inserts faster
            statement.ExecuteNonQuery();
            statement.Dispose();

            //Now select trade history that have not been finalized
            string myquery = "Select nindex From MYTRADEHISTORY Where pending = 1";
            statement = new SqliteCommand(myquery, mycon);
            SqliteDataReader statement_reader = statement.ExecuteReader();
            bool dataavail = statement_reader.Read();
            statement_reader.Close();
            statement.Dispose();
            mycon.Close();

            return dataavail;
        }

        public static void ChangeWalletAddresses()
        {
            //This will attempt to change all the wallet addresses to a new address

            //Check if any pending payments from recent trades
            bool dataavail = CheckPendingPayment();

            if (dataavail == true)
            {
                //There are pending payments
                MessageBox("Notice", "There is at least one pending payment to this current address", "OK",false);
                return;
            }

            bool moveable = true;
            for (int i = 0; i < WalletList.Count; i++)
            {
                if (WalletList[i].status != 0)
                {
                    moveable = false; break;
                }
            }

            if (moveable == false)
            {
                MessageBox("Notice", "There is at least one wallet unavailable to change the current address", "OK", false);
                return;
            }

            //TODO: Figure out way to transfer ETH tokens as well in one transaction, otherwise, skip ETH transfer and tell user
            bool skip_eth = false;
            for (int i = 0; i < WalletList.Count; i++)
            {
                if (Wallet.CoinERC20(WalletList[i].type) == true)
                {
                    if (WalletList[i].balance > 0)
                    {
                        skip_eth = true; break; //There are tokens present, cannot change eth address
                    }
                }
            }

            if (skip_eth == true)
            {
                MessageBox("Notice", "Changing all addresses except Ethereum due to ERC20 tokens present at address", "OK",false);
            }

            //This function will add a private key (with address as well) for all wallets 1 by 1
            for (int i = 0; i < total_cointypes; i++)
            {

                //So first do a test transaction to make sure the transaction can be created
                int wallet_type = GetWalletType(i);
                string curr_add = GetWalletAddress(wallet_type);
                decimal bal = 0;
                Transaction testtx = null;
                Nethereum.Signer.TransactionChainId testeth_tx = null;
                if (wallet_type == 0)
                { //Neblio
                    testtx = CreateNTP1AllTokenTransfer(curr_add);
                }
                else
                {
                    bal = GetWalletAmount(wallet_type); //Get blockchain balance
                    if (bal > 0)
                    {
                        if (i != 6)
                        {
                            testtx = CreateSignedP2PKHTx(wallet_type, bal, curr_add, false, false);
                        }
                        else if (skip_eth == false)
                        {
                            testeth_tx = CreateSignedEthereumTransaction(wallet_type, curr_add, bal, false, 0, "");
                        }
                    }
                }

                //All Neblio based tokens are the same address as the neblio wallet
                string new_add = "";
                if (testtx != null || testeth_tx != null)
                {
                    new_add = AddNewWalletAddress(wallet_type); //Will give us our new address that we own
                                                                //Then we need to send to this address
                                                                //Now create a real transaction
                    if (wallet_type == 0)
                    { //Neblio
                      //First send all the tokens
                      //There is a specific transaction for neblio tokens + neblio
                        Transaction tx = CreateNTP1AllTokenTransfer(new_add); //This will create a transaction that sends all tokens to another address
                        if (tx != null)
                        {
                            //Broadcast and save the result
                            bool timeout;
                            string txhash = TransactionBroadcast(wallet_type, tx.ToHex(), out timeout);
                            if (txhash.Length > 0)
                            {
                                bal = GetWalletAmount(wallet_type); //Get blockchain balance
                                AddMyTxToDatabase(tx.GetHash().ToString(), curr_add, new_add, bal, 0, 2, UTCTime());
                                UpdateWalletStatus(0, 2); //Wait mode
                            }
                        }
                    }
                    else
                    {
                        bal = GetWalletAmount(wallet_type); //Get blockchain balance
                        if (testtx != null)
                        {
                            Transaction tx = CreateSignedP2PKHTx(wallet_type, bal, new_add, true, false);
                            if (tx != null)
                            {
                                AddMyTxToDatabase(tx.GetHash().ToString(), curr_add, new_add, bal, wallet_type, 2, UTCTime());
                            }
                        }
                        else if (testeth_tx != null)
                        {
                            Nethereum.Signer.TransactionChainId eth_tx = CreateSignedEthereumTransaction(wallet_type, new_add, bal, false, 0, "");
                            bool timeout;
                            TransactionBroadcast(wallet_type, eth_tx.Signed_Hex, out timeout);
                            if (timeout == false)
                            {
                                UpdateWalletStatus(wallet_type, 2); //Set to wait
                                AddMyTxToDatabase(eth_tx.HashID, curr_add, new_add, bal, wallet_type, 2, -1); //Withdrawal
                            }
                        }
                    }
                }
            }

            //Now Load the wallet again
            LoadWallet();

        }

        public static string AddNewWalletAddress(int wallet)
        {
            //Open the wallet and get all the info
            string new_add = "";
            int blockchain = GetWalletBlockchainType(wallet);
            using (System.IO.StreamReader file_in =
                new System.IO.StreamReader(@App_Path + "/account.dat", false))
            {
                using (System.IO.StreamWriter file_out =
                    new System.IO.StreamWriter(@App_Path + "/account_new.dat", false))
                {
                    file_in.ReadLine(); //Skip version
                    int enc = Convert.ToInt32(file_in.ReadLine()); //Find out if encrypted

                    file_out.WriteLine(accountdat_version);
                    file_out.WriteLine(enc);
                    for (int i = 0; i < total_cointypes; i++)
                    {
                        string master = file_in.ReadLine(); //Get master key
                        file_out.WriteLine(master);
                        int wtype = Convert.ToInt32(file_in.ReadLine()); //Get the wallet blockchain type
                        int amount = Convert.ToInt32(file_in.ReadLine()); //Get number of subkeys for master
                        file_out.WriteLine(wtype);
                        if (blockchain == wtype)
                        {
                            //This is the wallet we are updating
                            file_out.WriteLine(amount + 1);
                        }
                        else
                        {
                            file_out.WriteLine(amount);
                        }
                        for (int i2 = 1; i2 <= amount; i2++)
                        {
                            //Go through each line and read then write them
                            file_out.WriteLine(file_in.ReadLine());
                            file_out.WriteLine(file_in.ReadLine());
                        }
                        if (blockchain == wtype)
                        {
                            //Now add our new address to the wallet
                            if (enc > 0)
                            {
                                master = AESDecrypt(master, my_wallet_pass);
                            }
                            ExtKey my_new_key = GeneratePrivateKey(master, amount);
                            Network my_net;
                            if (testnet_mode == false)
                            {
                                my_net = Network.Main;
                            }
                            else
                            {
                                my_net = Network.TestNet;
                            }
                            string privatekey = my_new_key.ToString(my_net);
                            string myaddress = GenerateCoinAddress(my_new_key, wallet);
                            if (blockchain == 6)
                            {
                                //Calculate the Ethereum address from the private key
                                myaddress = GenerateEthAddress(privatekey);
                            }
                            new_add = myaddress;
                            if (enc > 0)
                            {
                                privatekey = AESEncrypt(privatekey, my_wallet_pass);
                            }
                            file_out.WriteLine(privatekey);
                            file_out.WriteLine(myaddress);
                        }
                    }
                }
            }

            if (File.Exists(App_Path + "/account_new.dat") != false)
            {
                File.Delete(App_Path + "/account.dat");
                File.Move(App_Path + "/account_new.dat", App_Path + "/account.dat");
            }
            return new_add;
        }

        public static void EncryptWalletKeys()
        {
            //Will return a wallet with encrypted keys
            using (System.IO.StreamReader file_in =
                new System.IO.StreamReader(@App_Path + "/account.dat", false))
            {
                using (System.IO.StreamWriter file_out =
                    new System.IO.StreamWriter(@App_Path + "/account_new.dat", false))
                {
                    file_in.ReadLine(); //Skip version
                    int enc = Convert.ToInt32(file_in.ReadLine()); //Find out if encrypted
                    if (enc > 0) { return; }

                    file_out.WriteLine(accountdat_version);
                    file_out.WriteLine(1);
                    for (int i = 0; i < total_cointypes; i++)
                    {
                        string master = file_in.ReadLine(); //Get master key
                        file_out.WriteLine(AESEncrypt(master, my_wallet_pass));
                        int wtype = Convert.ToInt32(file_in.ReadLine()); //Get the wallet type
                        int amount = Convert.ToInt32(file_in.ReadLine()); //Get number of subkeys for master
                        file_out.WriteLine(wtype);
                        file_out.WriteLine(amount);
                        for (int i2 = 1; i2 <= amount; i2++)
                        {
                            //Go through each line and read then write them
                            string priv_key = file_in.ReadLine();
                            file_out.WriteLine(AESEncrypt(priv_key, my_wallet_pass));
                            file_out.WriteLine(file_in.ReadLine());
                        }
                    }
                }
            }

            if (File.Exists(App_Path + "/account_new.dat") != false)
            {
                File.Delete(App_Path + "/account.dat");
                File.Move(App_Path + "/account_new.dat", App_Path + "/account.dat");
            }
        }

        public static void DecryptWalletKeys()
        {
            //Will return a wallet with encrypted keys
            using (System.IO.StreamReader file_in =
                new System.IO.StreamReader(@App_Path + "/account.dat", false))
            {
                using (System.IO.StreamWriter file_out =
                    new System.IO.StreamWriter(@App_Path + "/account_new.dat", false))
                {
                    file_in.ReadLine(); //Skip version
                    int enc = Convert.ToInt32(file_in.ReadLine()); //Find out if encrypted
                    if (enc == 0) { return; }

                    file_out.WriteLine(accountdat_version); //File version
                    file_out.WriteLine(0);
                    for (int i = 0; i < total_cointypes; i++)
                    {
                        string master = file_in.ReadLine(); //Get master key
                        file_out.WriteLine(AESDecrypt(master, my_wallet_pass));
                        int wtype = Convert.ToInt32(file_in.ReadLine()); //Get the wallet type
                        int amount = Convert.ToInt32(file_in.ReadLine()); //Get number of subkeys for master
                        file_out.WriteLine(wtype);
                        file_out.WriteLine(amount);
                        for (int i2 = 1; i2 <= amount; i2++)
                        {
                            //Go through each line and read then write them
                            string priv_key = file_in.ReadLine();
                            file_out.WriteLine(AESDecrypt(priv_key, my_wallet_pass));
                            file_out.WriteLine(file_in.ReadLine());
                        }
                    }
                }
            }

            if (File.Exists(App_Path + "/account_new.dat") != false)
            {
                File.Delete(App_Path + "/account.dat");
                File.Move(App_Path + "/account_new.dat", App_Path + "/account.dat");
            }
        }

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0,
                                                                  DateTimeKind.Utc);

        //Converts UTC seconds to DateTime object
        public static DateTime UTC2DateTime(int utc_seconds)
        {
            return UnixEpoch.AddSeconds(utc_seconds);
        }

        public static int UTCTime()
        {
            //Returns time since epoch
            TimeSpan t = DateTime.UtcNow - UnixEpoch;
            return (int)t.TotalSeconds;
        }

        public static double GetRandomNumber(double minimum, double maximum)
        {
            lock (app_random_gen)
            {
                return (double)app_random_gen.NextDouble() * (maximum - minimum) + minimum;
            }
        }

        public static decimal GetRandomDecimalNumber(decimal minimum, decimal maximum)
        {
            lock (app_random_gen)
            {
                //It returns a decimal that we convert to a decimal and scale
                return Convert.ToDecimal(app_random_gen.NextDouble()) * (maximum - minimum) + minimum;
            }
        }

        public static string GenerateHexNonce(int length)
        {
            //Generates a random hex sequence
            //Not cryptographically secure so not used for key creation

            byte[] buffer = new byte[length / 2];
            lock (app_random_gen)
            {
                app_random_gen.NextBytes(buffer);
            }
            string[] result = new string[length / 2];
            for (int i = 0; i < buffer.Length; i++)
            {
                result[i] = buffer[i].ToString("X2").ToLower(); //Hex format
            }
            if (length % 2 == 0)
            { //Even length
                return String.Concat(result); //Returns all the strings together
            }
            //Odd length
            lock (app_random_gen)
            {
                return (String.Concat(result) + app_random_gen.Next(16).ToString("X").ToLower());
            }
        }

        public static bool IsNumber(string s)
        {
            //This will check to see if the string is a valid number
            if (s.Length > 32) { return false; }
            if (s.Trim().Length == 0) { return false; }
            decimal number = 0;
            bool myint = decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out number);
            if (myint == false) { return false; }
            try
            {
                number = decimal.Parse(s, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public static decimal GetMarketBalance(int market, int type)
        {
            //Helper function to quickly get amount needed for trade type
            int my_wallet = 0;
            if (type == 0)
            {
                //We want to buy NEBL, so we need BTC
                my_wallet = MarketList[market].base_wallet;
            }
            else
            {
                //We want to sell NEBL
                my_wallet = MarketList[market].trade_wallet;
            }

            for (int i = 0; i < WalletList.Count; i++)
            {
                if (WalletList[i].type == my_wallet)
                {
                    //Find the wallet and return the balance
                    return WalletList[i].balance;
                }
            }
            return 0;
        }

        public static decimal GetWalletAmount(int wallet)
        {
            for (int i = 0; i < WalletList.Count; i++)
            {
                if (WalletList[i].type == wallet)
                {
                    //Find the wallet and return the balance
                    return WalletList[i].balance;
                }
            }
            return 0;
        }

        public static string GetWalletAddress(int wallet)
        {
            for (int i = 0; i < WalletList.Count; i++)
            {
                if (WalletList[i].type == wallet)
                {
                    //Find the wallet and return the balance
                    return WalletList[i].address;
                }
            }
            return "";
        }

        public static string GetWalletTokenID(int wallet)
        {
            for (int i = 0; i < WalletList.Count; i++)
            {
                if (WalletList[i].type == wallet)
                {
                    //Find the wallet and return the ID
                    return WalletList[i].TokenID;
                }
            }
            return "";
        }

        public static string GetWalletERC20TokenContract(int wallet)
        {
            for (int i = 0; i < WalletList.Count; i++)
            {
                if (WalletList[i].type == wallet)
                {
                    //Find the wallet and return the Contract
                    return WalletList[i].ERC20Contract;
                }
            }
            return "";
        }

        public static decimal GetWalletERC20TokenDecimals(int wallet)
        {
            for (int i = 0; i < WalletList.Count; i++)
            {
                if (WalletList[i].type == wallet)
                {
                    //Find the wallet and return the Contract
                    return WalletList[i].ERC20Decimals;
                }
            }
            return 0;
        }

        public static void UpdateWalletStatus(int wallettype, int status)
        {
            for (int i2 = 0; i2 < WalletList.Count; i2++)
            {
                if (WalletList[i2].type == wallettype)
                {
                    WalletList[i2].status = status;

                    if (status != 1)
                    {
                        //Pending only applies to one
                        for (int i3 = 0; i3 < WalletList.Count; i3++)
                        {
                            if (WalletList[i3].address == WalletList[i2].address)
                            {
                                //NTP1 token
                                WalletList[i3].status = WalletList[i2].status; //They share the same status
                            }
                        }
                    }
                    break;
                }
            }

        }

        public static void UpdateWalletBalance(int type, decimal balance, decimal ubalance)
        {
            for (int i = 0; i < WalletList.Count; i++)
            {
                if (WalletList[i].type == type)
                {
                    //Find the wallet and change the amount
                    WalletList[i].balance = balance;

                    if (ubalance > 0 && WalletList[i].status == 0)
                    {
                        //There is an unconfirmed amount
                        //Change this to pending
                        WalletList[i].status = 1;
                    }
                    else if (ubalance == 0 && WalletList[i].status == 1)
                    {
                        WalletList[i].status = 0; //Available
                    }

                    break;
                }
            }

        }

        public static bool CheckWalletBalance(int type, decimal amount, ref string msg)
        {
            //This will check if the amount if spendable by the wallet and give message if not
            for (int i = 0; i < WalletList.Count; i++)
            {
                if (WalletList[i].type == type)
                {
                    if (WalletList[i].status == 2)
                    {
                        //Wallet not available to spend
                        msg = WalletList[i].Coin + " wallet currently unavailable to use.";
                        return false;
                    }
                    if (WalletList[i].balance < amount)
                    {
                        //Trying to spend too much of balance
                        msg = "This amount exceeds " + WalletList[i].Coin + " wallet balance.";
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }

        public static bool CheckMarketFees(int market, int type, decimal amount, ref string msg, bool taker)
        {
            //The wallet must have enough funds to cover the market fees as well
            decimal vn_fee = 0;
            decimal block_fee = 0;
            int sendwallet = 0;
            int redeemwallet = 0;
            if (type == 0)
            {
                //We are buying
                if (MarketList[market].trade_wallet == 3)
                {
                    //Ndex
                    vn_fee = 0;
                }
                else
                {
                    vn_fee = ndex_fee / 2;
                }

                sendwallet = MarketList[market].base_wallet;
                redeemwallet = MarketList[market].trade_wallet;
                int sendwallet_blockchaintype = GetWalletBlockchainType(sendwallet);
                if (sendwallet_blockchaintype == 0)
                {
                    block_fee = blockchain_fee[0] * 4; //Expected amount of Neblio to spend
                }
                else
                {
                    if (sendwallet_blockchaintype != 6)
                    {
                        block_fee = blockchain_fee[sendwallet_blockchaintype]; //Overestimate the fee (average tx is 225 bytes), need more since sending to contract
                    }
                    else
                    {
                        block_fee = GetEtherContractTradeFee(Wallet.CoinERC20(sendwallet));
                    }
                }
            }
            else
            {
                //Selling
                if (MarketList[market].trade_wallet == 3)
                {
                    vn_fee = ndex_fee; //We cover the entire fee                    
                }
                else
                {
                    vn_fee = ndex_fee / 2;
                }

                sendwallet = MarketList[market].trade_wallet;
                redeemwallet = MarketList[market].base_wallet;
                int sendwallet_blockchaintype = GetWalletBlockchainType(sendwallet);
                if (sendwallet_blockchaintype == 0)
                {
                    block_fee = blockchain_fee[0] * 4; //Expected amount of Neblio to spend
                    int basewallet_blockchaintype = GetWalletBlockchainType(MarketList[market].base_wallet);
                    if (sendwallet == 3 && basewallet_blockchaintype != 0)
                    { //Sending to non-Neblio wallet
                        block_fee = blockchain_fee[0] * 14; //When selling NDEX to those who don't hold NEBL, give them extra 5 trades
                    }
                }
                else
                {
                    if (sendwallet_blockchaintype != 6)
                    {
                        block_fee = blockchain_fee[sendwallet_blockchaintype]; //Overestimate the fee (average tx is 225 bytes), need more since sending to contract
                    }
                    else
                    {
                        //We are sending ethereum to eventual contract, will need at least 265,000 units of gas to cover transfer
                        block_fee = GetEtherContractTradeFee(Wallet.CoinERC20(sendwallet));
                    }
                }
            }

            //This is unique to Ethereum, but both the sender and receiver must have a little ethereum to interact with the ethereum contract
            if (GetWalletBlockchainType(MarketList[market].trade_wallet) == 6 || GetWalletBlockchainType(MarketList[market].base_wallet) == 6)
            {
                decimal ether_fee = GetEtherContractRedeemFee(Wallet.CoinERC20(redeemwallet));
                if (GetWalletAmount(GetWalletType(6)) < ether_fee)
                {
                    msg = "Your Ether wallet requires a small amount of Ether (" + String.Format(CultureInfo.InvariantCulture, "{0:0.########}", ether_fee) + " ETH) to interact with the Swap contract.";
                    return false;
                }
            }

            decimal mybalance = 0;
            bool ntp1_wallet = IsWalletNTP1(sendwallet);
            if (ntp1_wallet == true)
            {
                //NTP1 transactions, only balance that matters is neblio for fees
                mybalance = GetWalletAmount(0);
                if (mybalance < block_fee)
                {
                    //Not enough to pay for fees
                    msg = "This NTP1 token requires a small NEBL balance (" + String.Format(CultureInfo.InvariantCulture, "{0:0.########}", block_fee) + " NEBL) to pay for blockchain fees.";
                    return false;
                }
            }
            else
            {
                if (Wallet.CoinERC20(sendwallet) == false)
                {
                    mybalance = GetWalletAmount(sendwallet);
                    if (mybalance - amount < block_fee)
                    {
                        msg = "Your future balance after the trade is not high enough to pay for the blockchain fees.";
                        return false;
                    }
                }
                else
                {
                    //Sending an ERC20
                    //Eth balance needs to be greater than block_fee to send tokens
                    mybalance = GetWalletAmount(17); //ETH wallet
                    if (mybalance < block_fee)
                    {
                        //Not enough to pay for ETH fees
                        msg = "This ERC20 token requires a small ETH balance (" + String.Format(CultureInfo.InvariantCulture, "{0:0.########}", block_fee) + " ETH) to pay for blockchain fees.";
                        return false;
                    }
                }
            }

            decimal ndex_balance = GetWalletAmount(3);
            if (sendwallet == 3)
            {
                ndex_balance -= amount; //In case we are sending NDEX
            }
            if (ndex_balance < vn_fee)
            {
                msg = "You do not have enough NDEX to pay for the validation fees for this trade.";
                return false;
            }

            if (vn_fee > 0)
            {
                //Check NDEX wallet availability
                for (int i = 0; i < WalletList.Count; i++)
                {
                    if (WalletList[i].type == 3)
                    { //NDEX wallet
                        if (WalletList[i].status == 2)
                        {
                            msg = "The NDEX wallet is unavailable currently so it cannot pay the fee. Please wait.";
                            return false;
                        }
                        break;
                    }
                }

                //Also make sure that we have enough NEBL for gas for the fee
                mybalance = GetWalletAmount(0);
                if (mybalance < blockchain_fee[0] * 5)
                {
                    //Not enough to pay for fees
                    msg = "This transaction requires a small NEBL balance (" + String.Format(CultureInfo.InvariantCulture, "{0:0.########}", blockchain_fee[0] * 5) + " NEBL) to pay for NDEX blockchain fees.";
                    return false;
                }
            }

            return true;
        }

        public static string GetWalletPubkey(int type)
        {
            //This will return the public key for a wallet
            for (int i = 0; i < WalletList.Count; i++)
            {
                if (WalletList[i].type == type)
                {
                    //Desired wallet
                    lock (transactionLock)
                    {
                        //Prevent more than one thread from using the network
                        Network my_net;
                        if (testnet_mode == false)
                        {
                            my_net = Network.Main;
                        }
                        else
                        {
                            my_net = Network.TestNet;
                        }

                        //Change the network
                        ChangeVersionByte(WalletList[i].type, ref my_net); //NEBL network
                        ExtKey priv_key = ExtKey.Parse(WalletList[i].private_key, my_net);
                        return priv_key.PrivateKey.PubKey.ToString(); //Will transmit the pubkey
                    }
                }
            }
            return "";
        }

        public static int GetWalletBlockchainType(int type)
        {
            //This will return the blockchain type of the selected wallet type
            return Wallet.BlockchainType(type);
        }

        public static bool IsWalletNTP1(int type)
        {
            //This will return true if wallet is of NTP1 type
            return Wallet.CoinNTP1(type);
        }

        public static int GetWalletType(int blockchaintype)
        {
            //Returns the wallet type based on the blockchain
            return Wallet.WalletType(blockchaintype);
        }

        //Check orders, load orders clear orders, remove 1 order, add 1 order, update 1 order
        //This will only store maker orders
        public static bool CheckSavedOrders()
        {
            //This function will check the database if you had open orders and return true if you did
            SqliteConnection mycon = new SqliteConnection("Data Source=\"" + App_Path + "/neblidex.db\";Version=3;");
            mycon.Open();

            //Set our busy timeout, so we wait if there are locks present
            SqliteCommand statement = new SqliteCommand("PRAGMA busy_timeout = 5000", mycon); //Create a transaction to make inserts faster
            statement.ExecuteNonQuery();
            statement.Dispose();

            statement = new SqliteCommand("Select Count(nindex) From SAVEDORDERS", mycon);
            statement.CommandType = System.Data.CommandType.Text;
            int saved_ord = Convert.ToInt32(statement.ExecuteScalar().ToString());
            statement.Dispose();
            mycon.Close();
            if (saved_ord > 0)
            { //We have some orders that are not saved
                return true;
            }
            return false;
        }

        public static void LoadSavedOrders()
        {
            //This function will check the database if you had open orders and return true if you did
            SqliteConnection mycon = new SqliteConnection("Data Source=\"" + App_Path + "/neblidex.db\";Version=3;");
            mycon.Open();

            //Set our busy timeout, so we wait if there are locks present
            SqliteCommand statement = new SqliteCommand("PRAGMA busy_timeout = 5000", mycon); //Create a transaction to make inserts faster
            statement.ExecuteNonQuery();
            statement.Dispose();

            string myquery = "Select type, market, nonce, price, amount, min_amount From SAVEDORDERS";
            statement = new SqliteCommand(myquery, mycon);
            SqliteDataReader statement_reader = statement.ExecuteReader();
            int ctime = UTCTime();
            while (statement_reader.Read())
            {
                //Add each saved order to the order queueu
                int market = Convert.ToInt32(statement_reader["market"]);
                int type = Convert.ToInt32(statement_reader["type"]);
                string nonce = statement_reader["nonce"].ToString();
                decimal price = Decimal.Parse(statement_reader["price"].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);
                decimal amount = Decimal.Parse(statement_reader["amount"].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);
                decimal min_amount = Decimal.Parse(statement_reader["min_amount"].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);

                OpenOrder ord = new OpenOrder();
                ord.order_nonce = nonce;
                ord.market = market;
                ord.type = type;
                ord.price = Math.Round(price, 8);
                ord.amount = Math.Round(amount, 8);
                ord.minimum_amount = Math.Round(min_amount, 8);
                ord.original_amount = amount;
                ord.order_stage = 0;
                ord.my_order = true;
                ord.queued_order = true;
                ord.pendtime = ctime;

                //Now add to the queue
                lock (MyOpenOrderList)
                {
                    MyOpenOrderList.Add(ord); //Add to our own personal list
                }
            }

            statement_reader.Close();
            statement.Dispose();
            mycon.Close();
        }

        public static void ClearSavedOrders()
        {
            //This function will clear the database of savedorders
            SqliteConnection mycon = new SqliteConnection("Data Source=\"" + App_Path + "/neblidex.db\";Version=3;");
            mycon.Open();

            //Set our busy timeout, so we wait if there are locks present
            SqliteCommand statement = new SqliteCommand("PRAGMA busy_timeout = 5000", mycon); //Create a transaction to make inserts faster
            statement.ExecuteNonQuery();
            statement.Dispose();

            statement = new SqliteCommand("Delete From SAVEDORDERS", mycon);
            statement.ExecuteNonQuery();
            statement.Dispose();
            mycon.Close();
        }

        public static void RemoveSavedOrder(OpenOrder ord)
        {
            //This will delete a specific order from the table that has the same nonce
            SqliteConnection mycon = new SqliteConnection("Data Source=\"" + App_Path + "/neblidex.db\";Version=3;");
            mycon.Open();

            //Set our busy timeout, so we wait if there are locks present
            SqliteCommand statement = new SqliteCommand("PRAGMA busy_timeout = 5000", mycon); //Create a transaction to make inserts faster
            statement.ExecuteNonQuery();
            statement.Dispose();

            statement = new SqliteCommand("Delete From SAVEDORDERS Where nonce = @non", mycon);
            statement.Parameters.AddWithValue("@non", ord.order_nonce);
            statement.ExecuteNonQuery();
            statement.Dispose();
            mycon.Close();
        }

        public static void UpdateSavedOrder(OpenOrder ord)
        {
            //This will update the amount of a specific order
            SqliteConnection mycon = new SqliteConnection("Data Source=\"" + App_Path + "/neblidex.db\";Version=3;");
            mycon.Open();

            //Set our busy timeout, so we wait if there are locks present
            SqliteCommand statement = new SqliteCommand("PRAGMA busy_timeout = 5000", mycon); //Create a transaction to make inserts faster
            statement.ExecuteNonQuery();
            statement.Dispose();

            statement = new SqliteCommand("Update SAVEDORDERS Set amount = @val, min_amount = @val2 Where nonce = @non", mycon);
            statement.Parameters.AddWithValue("@non", ord.order_nonce);
            statement.Parameters.AddWithValue("@val", ord.amount.ToString(CultureInfo.InvariantCulture));
            statement.Parameters.AddWithValue("@val2", ord.minimum_amount.ToString(CultureInfo.InvariantCulture));
            statement.ExecuteNonQuery();
            statement.Dispose();
            mycon.Close();
        }

        public static void AddSavedOrder(OpenOrder ord)
        {
            //This will add a specific order to the table
            SqliteConnection mycon = new SqliteConnection("Data Source=\"" + App_Path + "/neblidex.db\";Version=3;");
            mycon.Open();

            //Set our busy timeout, so we wait if there are locks present
            SqliteCommand statement = new SqliteCommand("PRAGMA busy_timeout = 5000", mycon); //Create a transaction to make inserts faster
            statement.ExecuteNonQuery();
            statement.Dispose();

            statement = new SqliteCommand("Insert Into SAVEDORDERS (type,market,nonce,price,amount,min_amount) Values (@typ,@mark,@non,@pri,@amo,@min_amo);", mycon);
            statement.Parameters.AddWithValue("@typ", ord.type);
            statement.Parameters.AddWithValue("@mark", ord.market);
            statement.Parameters.AddWithValue("@non", ord.order_nonce);
            statement.Parameters.AddWithValue("@pri", ord.price.ToString(CultureInfo.InvariantCulture));
            statement.Parameters.AddWithValue("@amo", ord.amount.ToString(CultureInfo.InvariantCulture));
            statement.Parameters.AddWithValue("@min_amo", ord.minimum_amount.ToString(CultureInfo.InvariantCulture));
            statement.ExecuteNonQuery();
            statement.Dispose();
            mycon.Close();
        }

        //Taken from ExchangeWindow.cs
        public static void ShowTradeMessage(string msg)
        {
            if (msg.Length > 200) { return; } //Really long message, do not show
            MessageBox("NebliDex: Trade Notice!", msg, "OK", false);
        }

        public static void LegalWarning()
        {
            MessageBox("DISCLAIMER", "Do not use NebliDex if its use is unlawful in your local jurisdiction.\nCheck your local laws before use.", "OK", false);
        }

        public static void Load_UI_Config()
        {
            if (File.Exists(App_Path + "/ui.ini") == false)
            {
                return; //Use the default themes as no UI file exists
            }
            int version = 0;
            try
            {
                using (System.IO.StreamReader file =
                    new System.IO.StreamReader(@App_Path + "/ui.ini", false))
                {
                    while (!file.EndOfStream)
                    {
                        string line_data = file.ReadLine();
                        line_data = line_data.ToLower();
                        if (line_data.IndexOf("=", StringComparison.InvariantCulture) > -1)
                        {
                            string[] variables = line_data.Split('=');
                            string key = variables[0].Trim();
                            string data = variables[1].Trim();
                            if (key == "version")
                            {
                                version = Convert.ToInt32(data);
                            }
                            else if (key == "default_market")
                            {
                                exchange_market = Convert.ToInt32(data);
                                if (exchange_market > total_markets) { exchange_market = total_markets; }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                NebliDexNetLog("Failed to read user interface data from file");
            }
        }

        public static void Save_UI_Config()
        {
            //Saves the UI information to a file
            try
            {
                using (System.IO.StreamWriter file =
                    new System.IO.StreamWriter(App_Path + "/ui.ini", false))
                {
                    file.WriteLine("Version = 1");
                    file.WriteLine("Default_Market = " + exchange_market);
                }
            }
            catch (Exception)
            {
                NebliDexNetLog("Failed to save user interface data to file");
            }
        }

        public static async void Prompt_Load_Saved_Orders()
        {
            bool result = PromptUser("Load Saved Orders", "Would you like to repost your previously loaded open orders?", "Yes", "No");
            if (result == true)
            {
                await Task.Run(() => LoadSavedOrders());
            }
            else
            {
                await Task.Run(() => ClearSavedOrders());
            }
        }

        public static void PendOrder(string nonce)
        {
            //This will hide the order from the view if present
            //This is a CN to CN or to TN function
            OpenOrder ord = null;
            for (int market = 0; market < total_markets; market++)
            {
                lock (OpenOrderList[market])
                {
                    for (int i = 0; i < OpenOrderList[market].Count; i++)
                    {
                        if (OpenOrderList[market][i].order_nonce.Equals(nonce) == true)
                        {
                            if (OpenOrderList[market][i].order_stage > 0) { break; } //Shouldn't happen normally
                            OpenOrderList[market][i].pendtime = UTCTime(); //This pended order will remove itself in 3 hours if still pending
                            OpenOrderList[market][i].order_stage = 1; //Pending
                            ord = OpenOrderList[market][i];
                            break;
                        }
                    }
                }
            }
        }

        public static bool ShowOrder(string nonce)
        {
            //This is a CN to CN and to TN function
            OpenOrder ord = null;
            for (int market = 0; market < total_markets; market++)
            {
                lock (OpenOrderList[market])
                {
                    for (int i = 0; i < OpenOrderList[market].Count; i++)
                    {
                        if (OpenOrderList[market][i].order_nonce.Equals(nonce) == true)
                        {
                            if (OpenOrderList[market][i].my_order == true)
                            {
                                //This order nonce belongs to me
                                if (OpenOrderList[market][i].order_stage > 0) { break; } //Shouldn't happen normally
                            }
                            OpenOrderList[market][i].order_stage = 0; //Show order
                            ord = OpenOrderList[market][i];
                            break;
                        }
                    }
                }
            }

            if (ord == null) { return false; }
            return true;
        }

        public static void InsertChartLastPriceByTime(List<LastPriceObject> mylist, LastPriceObject lp)
        {
            //The most recent chartlastprice is in the end
            bool inserted = false;
            for (int i = mylist.Count - 1; i >= 0; i--)
            {
                //Insert based on first the time
                LastPriceObject plp = mylist[i];
                if (plp.market == lp.market)
                {
                    //Must be same market
                    if (plp.atime < lp.atime)
                    {
                        //Place the last chart time here
                        mylist.Insert(i + 1, lp);
                        inserted = true;
                        break;
                    }
                    else if (plp.atime == lp.atime)
                    {
                        //These trades were made at the same time
                        //Compare the prices
                        if (plp.price > lp.price)
                        {
                            mylist.Insert(i + 1, lp);
                            inserted = true;
                            break;
                        }
                    }
                }
            }
            if (inserted == false)
            {
                mylist.Insert(0, lp); //Add to beginning of the list
            }
        }

        public static void InsertRecentTradeByTime(RecentTrade rt)
        {
            //This will insert the recent trade into the list by time (higher first), and then price (lower first)
            //Most recent trade is first on list
            lock (RecentTradeList[rt.market])
            {
                bool inserted = false;
                for (int i = 0; i < RecentTradeList[rt.market].Count; i++)
                {
                    //Insert based on first the time
                    RecentTrade prt = RecentTradeList[rt.market][i];
                    if (prt.utctime < rt.utctime)
                    {
                        //Place the recent trade here
                        RecentTradeList[rt.market].Insert(i, rt);
                        inserted = true;
                        break;
                    }
                    else if (prt.utctime == rt.utctime)
                    {
                        //These trades were made at the same time
                        //Compare the prices
                        if (prt.price > rt.price)
                        {
                            RecentTradeList[rt.market].Insert(i, rt);
                            inserted = true;
                            break;
                        }
                    }
                }
                if (inserted == false)
                {
                    RecentTradeList[rt.market].Add(rt); //Add to end of list, old trade
                }
            }
        }

        public static bool TryUpdateOldCandle(int market, double price, int time, int timescale)
        {
            //True means successfully updated the candle, no need to put in current candle

            //This will go through the database and update an old candle
            string myquery;
            SqliteConnection mycon = new SqliteConnection("Data Source=\"" + App_Path + "/neblidex.db\";Version=3;");
            mycon.Open();

            SqliteCommand statement = new SqliteCommand("PRAGMA busy_timeout = 5000", mycon); //Create a transaction to make inserts faster
            statement.ExecuteNonQuery();
            statement.Dispose();

            string table;
            int timeforward = 0;
            if (timescale == 0)
            {
                //24 hour chart
                table = "CANDLESTICKS24H";
                timeforward = 60 * 15;
            }
            else
            {
                table = "CANDLESTICKS7D";
                timeforward = 60 * 90;
            }

            //First update the most recent 24 hour candle
            int backtime = UTCTime() - 60 * 60 * 3;
            myquery = "Select highprice, lowprice, open, close, nindex, utctime From " + table + " Where market = @mark And utctime > @time Order By utctime DESC Limit 1"; //Get most recent candle
            statement = new SqliteCommand(myquery, mycon);
            statement.Parameters.AddWithValue("@time", backtime);
            statement.Parameters.AddWithValue("@mark", market);
            SqliteDataReader statement_reader = statement.ExecuteReader();
            bool dataavail = statement_reader.Read();
            double high, low, close, open;
            int nindex = -1;
            if (dataavail == true)
            {
                high = Convert.ToDouble(statement_reader["highprice"].ToString(), CultureInfo.InvariantCulture);
                low = Convert.ToDouble(statement_reader["lowprice"].ToString(), CultureInfo.InvariantCulture);
                open = Convert.ToDouble(statement_reader["open"].ToString(), CultureInfo.InvariantCulture);
                close = Convert.ToDouble(statement_reader["close"].ToString(), CultureInfo.InvariantCulture);
                nindex = Convert.ToInt32(statement_reader["nindex"].ToString());
                int starttime = Convert.ToInt32(statement_reader["utctime"].ToString());
                statement_reader.Close();
                statement.Dispose();
                if (starttime + timeforward > time)
                {
                    //This candle needs to be updated
                    if (price > high)
                    {
                        high = price;
                    }
                    else if (price < low)
                    {
                        low = price;
                    }
                    close = price;
                    myquery = "Update " + table + " Set highprice = @hi, lowprice = @lo, close = @clo Where nindex = @in";
                    statement = new SqliteCommand(myquery, mycon);
                    statement.Parameters.AddWithValue("@hi", high.ToString(CultureInfo.InvariantCulture));
                    statement.Parameters.AddWithValue("@lo", low.ToString(CultureInfo.InvariantCulture));
                    statement.Parameters.AddWithValue("@clo", close.ToString(CultureInfo.InvariantCulture));
                    statement.Parameters.AddWithValue("@in", nindex);
                    statement.ExecuteNonQuery();
                    statement.Dispose();
                    mycon.Close();

                    //Candle was updated
                    return true;
                }
            }
            else
            {
                //No candle exists      
            }
            statement_reader.Close();
            statement.Dispose();
            mycon.Close();
            return false;
        }

        public static void ClearMarketData(int market)
        {
            //Remove all market data for our market
            //This function is not performed by critical nodes
            //Market -1 means remove all markets data if there

            //Clear all the candles
            string myquery;
            SqliteConnection mycon = new SqliteConnection("Data Source=\"" + App_Path + "/neblidex.db\";Version=3;");
            mycon.Open();

            SqliteCommand statement = new SqliteCommand("PRAGMA busy_timeout = 5000", mycon); //Create a transaction to make inserts faster
            statement.ExecuteNonQuery();
            statement.Dispose();

            //Delete the Candles Database as they have come out of sync and obtain new chart from another server
            myquery = "Delete From CANDLESTICKS7D";
            statement = new SqliteCommand(myquery, mycon);
            statement.ExecuteNonQuery();
            statement.Dispose();

            myquery = "Delete From CANDLESTICKS24H";
            statement = new SqliteCommand(myquery, mycon);
            statement.ExecuteNonQuery();
            statement.Dispose();
            mycon.Close();

            //Now clear the open orders for the market
            if (market > 0)
            {
                lock (OpenOrderList[market])
                {
                    OpenOrderList[market].Clear(); //We don't need to see those orders
                }
                lock (RecentTradeList[market])
                {
                    RecentTradeList[market].Clear();
                }
            }
            else
            {
                for (int mark = 0; mark < total_markets; mark++)
                {
                    lock (OpenOrderList[mark])
                    {
                        OpenOrderList[mark].Clear(); //We don't need to see those orders
                    }
                    lock (RecentTradeList[mark])
                    {
                        RecentTradeList[mark].Clear();
                    }
                }
            }
            lock (ChartLastPrice)
            {
                ChartLastPrice[0].Clear();
                ChartLastPrice[1].Clear();
            }

        }

        public static void PeriodicCandleMaker(object state)
        {
            //Update the candles at this set interval

            //Now move the candle watching time forward
            int utime = UTCTime();
            if (next_candle_time == 0)
            {
                next_candle_time = utime + 60 * 15;
            }
            else
            {
                next_candle_time += 60 * 15;
            }

            //Set the 15 minute candle time to this
            ChartLastPrice15StartTime = next_candle_time - 60 * 15;

            //Because System timers are inprecise and lead to drift, we must update time manually
            int waittime = next_candle_time - utime;
            if (waittime < 0) { waittime = 0; } //Shouldn't be possible
            if (CandleTimer == null) { return; }
            CandleTimer.Change(waittime * 1000, System.Threading.Timeout.Infinite);

            lock (ChartLastPrice)
            {
                string myquery = "";
                double high, low, open, close;

                candle_15m_interval++;
                int end_time = 1;
                if (candle_15m_interval == 6)
                {
                    candle_15m_interval = 0;
                    end_time = 2;
                    //Create a candle using our lastpriceobject list for each market (if CN) or 1 market for regular node
                }

                int start_market = 0;
                int end_market = total_markets;
                if (critical_node == false)
                {
                    start_market = exchange_market; //Only store for
                    end_market = start_market + 1;
                }

                //CNs are only ones required to store all candle data from all markets
                //Do the same for the 15 minute lastpriceobject
                //Create a transaction
                SqliteConnection mycon = new SqliteConnection("Data Source=\"" + App_Path + "/neblidex.db\";Version=3;");
                mycon.Open();

                //Set our busy timeout, so we wait if there are locks present
                SqliteCommand statement = new SqliteCommand("PRAGMA busy_timeout = 5000", mycon); //Create a transaction to make inserts faster
                statement.ExecuteNonQuery();
                statement.Dispose();

                statement = new SqliteCommand("BEGIN TRANSACTION", mycon); //Create a transaction to make inserts faster
                statement.ExecuteNonQuery();
                statement.Dispose();

                for (int time = 0; time < end_time; time++)
                {
                    for (int market = start_market; market < end_market; market++)
                    {
                        //Go through the 7 day table if necessary
                        int numpts = ChartLastPrice[time].Count; //All the pounts for the timeline
                        open = -1;
                        close = -1;
                        high = -1;
                        low = -1;

                        for (int pos = 0; pos < ChartLastPrice[time].Count; pos++)
                        { //This should be chronological
                            if (ChartLastPrice[time][pos].market == market)
                            {
                                double price = Convert.ToDouble(ChartLastPrice[time][pos].price);
                                if (open < 0) { open = price; }
                                if (price > high)
                                {
                                    high = price;
                                }
                                if (low < 0 || price < low)
                                {
                                    low = price;
                                }
                                close = price; //The last price will be the close
                                               //Which will also be the open for the next candle
                            }
                        }

                        //Then delete all the ones except the last one
                        bool clear = false;
                        //Reset all chart last prices
                        //Remove all the prices except the last one, this will be our new open
                        for (int pos = ChartLastPrice[time].Count - 1; pos >= 0; pos--)
                        {
                            if (ChartLastPrice[time][pos].market == market)
                            {
                                if (clear == false)
                                {
                                    //This is the one to save, most recent lastpriceobject
                                    clear = true;
                                    close = Convert.ToDouble(ChartLastPrice[time][pos].price);
                                }
                                else if (clear == true)
                                {
                                    //Remove all else
                                    ChartLastPrice[time].RemoveAt(pos); //Take it out
                                }
                            }
                        }

                        if (market == exchange_market && NebliDex_UI!=null)
                        {
                            if (NebliDex_UI.MainPage is MarketPage)
                            {
                                MarketPage page = (MarketPage)NebliDex_UI.MainPage;
                                //Now if this is the active market, add a new candle based on the charttimeline
                                //Now modify the visible candles
                                if (VisibleCandles.Count > 0 && close > 0)
                                {
                                    if (page.chart_timeline == 0 && time == 0)
                                    {
                                        //24 Hr                           
                                        Candle can = new Candle(close);
                                        page.PlaceCandleInChart(can);
                                        NebliDex_Activity.RunOnUiThread(() =>
                                        {                                           
                                            page.AdjustCandlePositions();
                                        });
                                    }
                                    else if (page.chart_timeline == 1 && time == 1)
                                    {
                                        //Only place new candle on this timeline every 90 minutes
                                        Candle can = new Candle(close);
                                        page.PlaceCandleInChart(can);
                                        NebliDex_Activity.RunOnUiThread(() =>
                                        {                                           
                                            page.AdjustCandlePositions();
                                        });
                                    }
                                }
                            }
                        }

                        if (open > 0)
                        {
                            //May not have any activity on that market yet
                            //If there is at least 1 trade on market, this will add to database
                            //Insert old candle into database
                            int ctime = UTCTime();
                            if (time == 0)
                            {
                                ctime -= 60 * 15; //This candle started 15 minutes ago
                                myquery = "Insert Into CANDLESTICKS24H";
                            }
                            else if (time == 1)
                            {
                                ctime -= 60 * 90; //This candle started 90 minutes ago
                                myquery = "Insert Into CANDLESTICKS7D";
                            }

                            //Insert to the candle database

                            myquery += " (utctime, market, highprice, lowprice, open, close)";
                            myquery += " Values (@time, @mark, @high, @low, @op, @clos);";
                            statement = new SqliteCommand(myquery, mycon);
                            statement.Parameters.AddWithValue("@time", ctime);
                            statement.Parameters.AddWithValue("@mark", market);
                            statement.Parameters.AddWithValue("@high", high);
                            statement.Parameters.AddWithValue("@low", low);
                            statement.Parameters.AddWithValue("@op", open);
                            statement.Parameters.AddWithValue("@clos", close);
                            statement.ExecuteNonQuery();
                            statement.Dispose();

                        }
                    }
                }


                //Close the connection
                statement = new SqliteCommand("COMMIT TRANSACTION", mycon); //Close the transaction
                statement.ExecuteNonQuery();
                statement.Dispose();
                mycon.Close();

            }

            //Finally check the electrum server sync
            CheckElectrumServerSync();
            CheckCNBlockHelperServerSync();
        }
    }
}