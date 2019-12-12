
using System;
using System.Data;
using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using Mono.Data.Sqlite;

//This code is designed to handle the Network for electrum servers and Critical Node infrastructure

namespace NebliDex_Mobile.Droid
{
    public partial class MainService : Android.App.Service
    {

        //Used to determine if a message has already been received or not
        public static List<MessageRelay> MessageRelayList = new List<MessageRelay>();

        public class MessageRelay
        {
            public int utctime; //Time message was created
            public string msgnonce; //Nonce of message
        }

        public static bool SubmitMyOrder(OpenOrder ord, DexConnection dex)
        {
            //This function takes the order that we created and broadcast it to the connected critical node
            if (dex == null)
            {
                lock (DexConnectionList)
                {
                    for (int i = 0; i < DexConnectionList.Count; i++)
                    {
                        if (DexConnectionList[i].contype == 3 && DexConnectionList[i].outgoing == true)
                        {
                            dex = DexConnectionList[i]; break; //Found our connection
                        }
                    }
                }
            }

            if (dex == null)
            {
                MessageBox("Notice", "Unable to connect to a Critical Node", "OK", false);
                return false;
            }

            JObject js = new JObject();
            js["cn.method"] = "cn.sendorder";
            js["cn.response"] = 0;
            js["order.nonce"] = ord.order_nonce;
            js["order.market"] = ord.market;
            js["order.type"] = ord.type;
            js["order.price"] = ord.price.ToString(CultureInfo.InvariantCulture);
            js["order.originalamount"] = ord.original_amount.ToString(CultureInfo.InvariantCulture); //This will be the amount when broadcasted
            js["order.min_amount"] = ord.minimum_amount.ToString(CultureInfo.InvariantCulture);
            string json_encoded = JsonConvert.SerializeObject(js);
            string blockdata = "";
            lock (dex.blockhandle)
            {
                dex.blockhandle.Reset();
                SendCNServerAction(dex, 17, json_encoded); //Send to the CN and hopefully its not rejected
                if (dex.open == false) { return false; }
                dex.blockhandle.WaitOne(30000); //This will wait 30 seconds for a response                              
                if (dex.blockdata == "") { return false; }
                blockdata = dex.blockdata;
            }

            //The message will be displayed here
            if (blockdata != "Order OK")
            {
                //The order was rejected
                bool error_ok = CheckErrorMessage(blockdata);
                if (error_ok == false) { return false; } //Error message is not standard, don't show it
                MessageBox("Notice", blockdata, "OK", false);
                return false;
            }

            return true; //Otherwise it is ok to submit our order and post it
        }

        public static bool SubmitMyOrderRequest(OpenOrder ord)
        {
            //This user has opted to create a market order instead of limit order
            //This function takes the order that we created and broadcast it to the connected critical node
            DexConnection dex = null;
            lock (DexConnectionList)
            {
                for (int i = 0; i < DexConnectionList.Count; i++)
                {
                    if (DexConnectionList[i].contype == 3 && DexConnectionList[i].outgoing == true)
                    {
                        dex = DexConnectionList[i]; break; //Found our connection
                    }
                }
            }

            if (dex == null)
            {
                MessageBox("Notice", "Unable to connect to a Critical Node", "OK", false);
                return false;
            }

            JObject js = new JObject();
            js["cn.method"] = "cn.sendorderrequest";
            js["cn.response"] = 0;
            js["order.nonce"] = ord.order_nonce;
            js["order.market"] = ord.market;
            js["order.type"] = ord.type;
            js["order.is_request"] = ord.is_request.ToString();
            js["order.price"] = ord.price.ToString(CultureInfo.InvariantCulture);
            js["order.originalamount"] = ord.original_amount.ToString(CultureInfo.InvariantCulture); //This will be the amount when broadcasted

            if (ord.type == 0)
            {
                //We are buying from seller, so my receive wallet is trade wallet
                js["taker.from_add"] = GetWalletAddress(MarketList[ord.market].base_wallet);
                js["taker.to_add"] = GetWalletAddress(MarketList[ord.market].trade_wallet);
            }
            else
            {
                //We are selling to buyer, so my receive wallet is base wallet
                js["taker.from_add"] = GetWalletAddress(MarketList[ord.market].trade_wallet);
                js["taker.to_add"] = GetWalletAddress(MarketList[ord.market].base_wallet);
            }

            string json_encoded = JsonConvert.SerializeObject(js);
            string blockdata = "";
            lock (dex.blockhandle)
            {
                dex.blockhandle.Reset();
                SendCNServerAction(dex, 17, json_encoded); //Send to the CN and hopefully its not rejected
                if (dex.open == false) { return false; }
                dex.blockhandle.WaitOne(30000); //This will wait 30 seconds for a response                              
                if (dex.blockdata == "") { return false; }
                blockdata = dex.blockdata;
            }

            //The message will be displayed here
            if (blockdata != "Order Request OK")
            {
                //The order was rejected
                bool error_ok = CheckErrorMessage(blockdata);
                if (error_ok == false) { return false; } //Error message is not standard, don't show
                MessageBox("Notice", blockdata, "OK", false);
                return false;
            }

            return true; //Otherwise it is ok to post the request
        }

        public static bool CancelMarketOrder(JObject jord, bool checkip)
        {
            //This will attempt to find the order and remove it
            OpenOrder ord = new OpenOrder();
            ord.order_nonce = jord["order.nonce"].ToString();
            ord.type = Convert.ToInt32(jord["order.type"].ToString());
            if (ord.type != 0 && ord.type != 1) { return false; }
            ord.market = Convert.ToInt32(jord["order.market"].ToString());
            ord.is_request = Convert.ToBoolean(jord["order.is_request"].ToString());
            if (ord.market < 0 || ord.market >= total_markets) { return false; } //Unsupported data

            string ip = "";
            string port = "";
            string cn_ip = "";

            if (checkip == true)
            {
                ip = jord["order.ip"].ToString();
                port = jord["order.port"].ToString();
                cn_ip = jord["order.cn_ip"].ToString();
            }

            if (ord.is_request == true) { return false; } //Cannot remove order request this way

            bool order_exist = false;
            lock (OpenOrderList[ord.market])
            {
                for (int i = OpenOrderList[ord.market].Count - 1; i >= 0; i--)
                {
                    if (OpenOrderList[ord.market][i].order_nonce.Equals(ord.order_nonce) == true)
                    {
                        //Found order
                        if (checkip == true)
                        {
                            //CNs needs to validate that the cancellation request was made by the person who created it
                            if (OpenOrderList[ord.market][i].ip_address_port[0].Equals(ip) == true && OpenOrderList[ord.market][i].ip_address_port[1].Equals(port) == true && OpenOrderList[ord.market][i].cn_relayer_ip.Equals(cn_ip) == true)
                            {
                                OpenOrderList[ord.market].RemoveAt(i);
                                order_exist = true;
                            }
                            else
                            {
                                return false; //Someone elses order
                            }
                        }
                        else
                        {
                            OpenOrderList[ord.market].RemoveAt(i); //Assume the CN did its job
                            order_exist = true;
                        }
                    }
                }
            }

            //If the order doesn't exist yet, it may be on the way, store as cancellation token for when it gets here
            if (order_exist == false)
            {
                lock (CancelOrderTokenList)
                {
                    CancelOrderToken tk = new CancelOrderToken();
                    tk.arrivetime = UTCTime(); //Will delete in 5 minutes if no order arrives
                    tk.order_nonce = ord.order_nonce;
                    CancelOrderTokenList.Add(tk);
                }
            }
            else
            {
                //Now remove from view
                if (NebliDex_UI != null)
                {
                    NebliDex_UI.RemoveOrderFromView(ord);
                }
            }

            //Also remove the order request that this order was linked to
            lock (MyOpenOrderList)
            {
                for (int i = MyOpenOrderList.Count - 1; i >= 0; i--)
                {
                    if (MyOpenOrderList[i].order_nonce.Equals(ord.order_nonce) == true && MyOpenOrderList[i].queued_order == false)
                    {

                        if (program_loaded == true)
                        {
                            if (MyOpenOrderList[i].is_request == true)
                            {
                                //let the user know that the other user closed the order
                                ShowTradeMessage("Trade Failed:\nOrder was cancelled by the creator or the creator is offline!");
                            }
                        }

                        if (MyOpenOrderList[i].is_request == true)
                        {
                            OpenOrder myord = MyOpenOrderList[i];
                            MyOpenOrderList.RemoveAt(i);
                        }
                        else
                        {
                            QueueMyOrderNoLock(MyOpenOrderList[i], null);
                        }

                    }
                }
            }

            //Successfully removed order
            return true;
        }

        public static void CNRequestCancelOrderNonce(int market, string order_nonce)
        {
            lock (OpenOrderList[market])
            {
                for (int pos = OpenOrderList[market].Count - 1; pos >= 0; pos--)
                {
                    if (OpenOrderList[market][pos].order_nonce == order_nonce)
                    {
                        OpenOrderList[market][pos].deletequeue = true; //Set order to be deleted
                        break;
                    }
                }
            }
        }

        public static void QueueAllOpenOrders()
        {
            //Queues all the open orders
            DexConnection dex = null;
            lock (DexConnectionList)
            {
                for (int i = 0; i < DexConnectionList.Count; i++)
                {
                    if (DexConnectionList[i].contype == 3 && DexConnectionList[i].outgoing == true && DexConnectionList[i].open == true)
                    {
                        dex = DexConnectionList[i]; break; //Found our connection
                    }
                }
            }

            //Queue all the open orders not already queued
            lock (MyOpenOrderList)
            {
                for (int i = MyOpenOrderList.Count - 1; i >= 0; i--)
                {
                    if (MyOpenOrderList[i].queued_order == false)
                    {
                        QueueMyOrderNoLock(MyOpenOrderList[i], dex);
                    }
                }
            }
        }

        public static void QueueAllButOpenOrders(OpenOrder ord)
        {
            //Queues all but certain open orders
            DexConnection dex = null;
            lock (DexConnectionList)
            {
                for (int i = 0; i < DexConnectionList.Count; i++)
                {
                    if (DexConnectionList[i].contype == 3 && DexConnectionList[i].outgoing == true && DexConnectionList[i].open == true)
                    {
                        dex = DexConnectionList[i]; break; //Found our connection
                    }
                }
            }

            //Queue all the open orders not already queued that don't match our open order
            lock (MyOpenOrderList)
            {
                for (int i = MyOpenOrderList.Count - 1; i >= 0; i--)
                {
                    if (MyOpenOrderList[i].queued_order == false && MyOpenOrderList[i].order_nonce.Equals(ord.order_nonce) == false)
                    {
                        QueueMyOrderNoLock(MyOpenOrderList[i], dex);
                    }
                }
            }
        }

        public static void QueueMyOrderNoLock(OpenOrder ord, DexConnection dex)
        {
            //This function will queue an open order
            bool found = false;
            for (int i = 0; i < MyOpenOrderList.Count; i++)
            {
                if (MyOpenOrderList[i].order_nonce.Equals(ord.order_nonce) == true && MyOpenOrderList[i].is_request == false)
                {
                    ord.queued_order = true; //Set queue status to true
                    ord.order_stage = 0;
                    ord.pendtime = UTCTime();
                    found = true;
                    break;
                }
            }

            if (found == false) { return; }

            //Now remove the order from my order book
            if (ord.is_request == false)
            {
                lock (OpenOrderList[ord.market])
                {
                    for (int i = OpenOrderList[ord.market].Count - 1; i >= 0; i--)
                    {
                        //Remove any order that matches our nonce
                        if (OpenOrderList[ord.market][i].order_nonce.Equals(ord.order_nonce) == true)
                        {
                            OpenOrderList[ord.market].RemoveAt(i);
                        }
                    }
                }

                //Now remove the order from view
                if (NebliDex_UI!=null)
                {
                    NebliDex_UI.RemoveOrderFromView(ord);
                }
            }

            if (dex == null) { return; }

            //Next broadcast to CN to cancel the order
            JObject js = new JObject();
            js["cn.method"] = "cn.cancelorder";
            js["cn.response"] = 0;
            js["order.nonce"] = ord.order_nonce;
            js["order.market"] = ord.market;
            js["order.type"] = ord.type;
            js["order.is_request"] = ord.is_request.ToString();

            string json_encoded = JsonConvert.SerializeObject(js);
            try
            {
                SendCNServerAction(dex, 22, json_encoded); //Send cancel request, no need to wait
            }
            catch (Exception)
            {
                NebliDexNetLog("Failed to broadcast cancellation request");
            }

            //Now the order is queued and will attempt to rebroadcast every 30 seconds
        }

        public static void CancelMyOrder(OpenOrder ord)
        {
            //This function takes the order that we created, cancel it then broadcast the request to the connected critical node

            //First things first, cancel the order on my side (so even if server is down, order is still cancelled)
            lock (MyOpenOrderList)
            {
                for (int i = 0; i < MyOpenOrderList.Count; i++)
                {
                    if (MyOpenOrderList[i].order_nonce.Equals(ord.order_nonce) == true)
                    {
                        OpenOrder myord = MyOpenOrderList[i];
                        RemoveSavedOrder(ord); //Take the order out of the saved table
                        MyOpenOrderList.RemoveAt(i);
                        break; //Take it out
                    }
                }
            }

            if (ord.queued_order == true)
            { //Order is already not posted anyway
                return;
            }

            //Next take it out of the open orders
            if (ord.is_request == false)
            {
                lock (OpenOrderList[ord.market])
                {
                    for (int i = OpenOrderList[ord.market].Count - 1; i >= 0; i--)
                    {
                        //Remove any order that matches our nonce
                        if (OpenOrderList[ord.market][i].order_nonce.Equals(ord.order_nonce) == true)
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

            //Now get an CN and send the request
            DexConnection dex = null;
            lock (DexConnectionList)
            {
                for (int i = 0; i < DexConnectionList.Count; i++)
                {
                    if (DexConnectionList[i].contype == 3 && DexConnectionList[i].outgoing == true)
                    {
                        dex = DexConnectionList[i]; break; //Found our connection
                    }
                }
            }

            if (dex == null) { return; }

            JObject js = new JObject();
            js["cn.method"] = "cn.cancelorder";
            js["cn.response"] = 0;
            js["order.nonce"] = ord.order_nonce;
            js["order.market"] = ord.market;
            js["order.type"] = ord.type;
            js["order.is_request"] = ord.is_request.ToString();

            string json_encoded = JsonConvert.SerializeObject(js);
            try
            {
                SendCNServerAction(dex, 22, json_encoded); //Send cancel request, no need to wait
            }
            catch (Exception)
            {
                NebliDexNetLog("Failed to broadcast cancellation request");
            }

            return; //Otherwise it is ok to cancel our order
        }

        public static bool CheckMessageRelay(string nonce, int time)
        {
            //This will check the message relay for its existance, and add it if it doesn't exist
            lock (MessageRelayList)
            {
                for (int i = 0; i < MessageRelayList.Count; i++)
                {
                    if (MessageRelayList[i].msgnonce.Equals(nonce) == true && MessageRelayList[i].utctime == time)
                    {
                        return true; //The message exists already, disregard
                    }
                }
                MessageRelay rl = new MessageRelay();
                rl.msgnonce = nonce;
                rl.utctime = time;
                MessageRelayList.Add(rl);
                if (MessageRelayList.Count > 1000)
                {
                    //Too many things on the list
                    MessageRelayList.RemoveAt(0); //Remove the oldest relay
                }
                return false; //New message
            }
        }

        //Taken from CNNet.cs but used by client
        public static bool CheckCNBlacklist(string ip)
        {
            SqliteConnection mycon = new SqliteConnection("Data Source=\"" + App_Path + "/neblidex.db\";Version=3;");
            mycon.Open();

            //Set our busy timeout, so we wait if there are locks present
            SqliteCommand statement = new SqliteCommand("PRAGMA busy_timeout = 5000", mycon); //Create a transaction to make inserts faster
            statement.ExecuteNonQuery();
            statement.Dispose();

            string myquery = "Select nindex From BLACKLIST Where value = @ip And type = 0";
            statement = new SqliteCommand(myquery, mycon);
            statement.Parameters.AddWithValue("@ip", ip);
            SqliteDataReader statement_reader = statement.ExecuteReader();
            bool dataavail = statement_reader.Read();
            statement_reader.Close();
            statement.Dispose();
            mycon.Close();
            return dataavail; //False = not on blacklist     
        }

        public static void SendCNServerAction(DexConnection con, int action, string extra)
        {
            //This is a modified list of CNServerActions as actions that only include client
            if (con.open == false) { return; } //If the connection is not open, don't send anything to it

            //Must be in try catch statement
            //Like electrum but for critical nodes
            //Make sure this is only accessed by one thread
            lock (con)
            {

                string json_encoded = "";
                if (action == 1)
                {
                    //Reflect IP of sending, for remote IP determination
                    JObject js = new JObject();
                    js["cn.method"] = "cn.reflectip";
                    js["cn.response"] = 1; //This is telling the CN that this is a response
                    js["cn.result"] = con.ip_address[0];
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 3)
                {
                    //This will send the server a version of the client
                    //The connection will be terminated if it receives less than the minimum
                    con.blockdata = ""; //This will be a blocking call
                    JObject js = new JObject();
                    js["cn.method"] = "cn.myversion";
                    js["cn.response"] = 0; //This is telling the CN that this is a response
                    js["cn.result"] = protocol_min_version;
                    js["cn.totalmarkets"] = total_markets; // Tell the CN the markets that we have
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 4)
                {
                    //This will send the client a response to a old version
                    //The connection will be terminated if it receives less than the minimum
                    JObject js = new JObject();
                    js["cn.method"] = "cn.myversion";
                    js["cn.response"] = 1; //This is telling the CN that this is a response
                    js["cn.result"] = extra;
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 5)
                {
                    //Pong for ping
                    //This will send the client a response to a ping
                    JObject js = new JObject();
                    js["cn.method"] = "cn.ping";
                    js["cn.response"] = 1; //This is telling the CN that this is a response
                    js["cn.result"] = "Pong";
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 6)
                {
                    //Request 24 hour chart data for a particular market
                    con.blockdata = "";//This will be a blocking call more than likely
                    JObject js = new JObject();
                    js["cn.method"] = "cn.chart24h";
                    js["cn.response"] = 0;
                    js["cn.result"] = extra;
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 8)
                {
                    //Request 7 days of chart data for a particular market
                    con.blockdata = "";//This will be a blocking call more than likely
                    JObject js = new JObject();
                    js["cn.method"] = "cn.chart7d";
                    js["cn.response"] = 0;
                    js["cn.result"] = extra;
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 10)
                {
                    //Client requesting information on open orders
                    //Because this data can be big, split into pages
                    con.blockdata = "";//This will be a blocking call more than likely
                    JObject js = new JObject();
                    js["cn.method"] = "cn.openorders";
                    js["cn.response"] = 0;
                    string[] parts = extra.Split(':'); //First half market, second half page
                    js["cn.result"] = parts[0];
                    js["cn.page"] = parts[1];
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 12)
                {
                    //Request 10 recent trades for this market
                    con.blockdata = "";//This will be a blocking call more than likely
                    JObject js = new JObject();
                    js["cn.method"] = "cn.recenttrades";
                    js["cn.response"] = 0;
                    js["cn.marketpage"] = extra;
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 14)
                {
                    //Get time objects
                    con.blockdata = "";//This will be a blocking call more than likely
                    JObject js = new JObject();
                    js["cn.method"] = "cn.syncclock";
                    js["cn.response"] = 0;
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 15)
                {
                    //Return time objecct
                    JObject js = new JObject();
                    js["cn.method"] = "cn.syncclock";
                    js["cn.response"] = 1;
                    js["cn.candletime"] = next_candle_time.ToString(); //UTC Time of next candle
                    js["cn.15minterval"] = candle_15m_interval.ToString(); //Interval of 15 minutes
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 16)
                {
                    //Ping for Pong
                    //This will send the server a ping
                    JObject js = new JObject();
                    js["cn.method"] = "cn.ping";
                    js["cn.response"] = 0;
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 17)
                {
                    //The client submits an order or order request
                    con.blockdata = "";
                    json_encoded = extra; //Order is already serialized
                }
                else if (action == 18)
                {
                    //The server responses to that order
                    JObject js = new JObject();
                    js["cn.method"] = "cn.sendorder";
                    js["cn.response"] = 1; //This is telling the CN that this is a response
                    js["cn.result"] = extra; //The order is in this string
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 19)
                {
                    //The server responses to that order
                    JObject js = new JObject();
                    js["cn.method"] = "cn.sendorder";
                    js["cn.response"] = 1; //This is telling the CN that this is a response
                    js["cn.result"] = extra; //The order is in this string
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 20)
                {
                    //The CN sends new order information to trader node
                    json_encoded = extra; //Order is already serialized
                }
                else if (action == 21)
                {
                    //The server responses to the broadcased order
                    JObject js = new JObject();
                    js["cn.method"] = "cn.neworder";
                    js["cn.response"] = 1; //This is telling the CN that this is a response
                    js["cn.result"] = extra; //The order is in this string
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 22)
                {
                    //The client tells the server it cancelled an order, (its already canceled client side)
                    json_encoded = extra; //Order is already serialized
                }
                else if (action == 23)
                {
                    //The server sends the relay to the clients
                    json_encoded = extra;
                }
                else if (action == 24)
                {
                    //The server responses to the cancel order request
                    JObject js = new JObject();
                    js["cn.method"] = "cn.relaycancelorder";
                    js["cn.response"] = 1; //This is telling the CN that this is a response
                    js["cn.result"] = extra; //The order is in this string
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 25)
                {
                    con.blockdata = "";
                    //The clients sends CN status to the server
                    json_encoded = extra;
                }
                else if (action == 26)
                {
                    //The server responses to the broadcased new cn request
                    JObject js = new JObject();
                    js["cn.method"] = "cn.newcn";
                    js["cn.response"] = 1; //This is telling the CN that this is a response
                    js["cn.result"] = extra; //The order is in this string
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 28)
                {
                    //This will request a list be sent to new CN
                    con.blockdata = "";
                    JObject js = new JObject();
                    js["cn.method"] = "cn.getcooldownlist";
                    js["cn.response"] = 0; //This is telling the CN that this is a response
                    js["cn.page"] = Convert.ToInt32(extra);
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 30)
                {
                    //This will request a list be sent to new CN
                    con.blockdata = "";
                    JObject js = new JObject();
                    js["cn.method"] = "cn.getorderrequestlist";
                    js["cn.response"] = 0; //This is telling the CN that this is a response
                    js["cn.page"] = Convert.ToInt32(extra);
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 32)
                {
                    //This will request a CN list be sent to new CN
                    con.blockdata = "";
                    JObject js = new JObject();
                    js["cn.method"] = "cn.getlist";
                    js["cn.authlevel"] = 1;
                    js["cn.response"] = 0; //This is telling the CN that this is a response
                    js["cn.page"] = Convert.ToInt32(extra);
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 33)
                {
                    //This will request a list of the last chart prices
                    con.blockdata = "";
                    JObject js = new JObject();
                    js["cn.method"] = "cn.getchartprices";
                    js["cn.response"] = 0; //This is telling the CN that this is a response
                    js["cn.timepage"] = extra;
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 35)
                {
                    //This will request the volume for a market
                    con.blockdata = "";
                    JObject js = new JObject();
                    js["cn.method"] = "cn.getvolume";
                    js["cn.response"] = 0; //This is telling the CN that this is a response
                    js["cn.market"] = extra;
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 37)
                {
                    //The server responses to that order request
                    JObject js = new JObject();
                    js["cn.method"] = "cn.sendorderrequest";
                    js["cn.response"] = 1; //This is telling the CN that this is a response
                    js["cn.result"] = extra; //The order is in this string
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 38)
                {
                    //The server responses to the broadcased new order request
                    JObject js = new JObject();
                    js["cn.method"] = "cn.relayorderrequest";
                    js["cn.response"] = 1; //This is telling the CN that this is a response
                    js["cn.result"] = extra; //The order is in this string
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 39)
                {
                    //The server is telling its connected TNs that an order is now pending
                    JObject js = new JObject();
                    js["cn.method"] = "cn.pendorder";
                    js["cn.response"] = 0; //This is telling the CN that this is not a response
                    js["cn.result"] = extra; //This is the order nonce
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 40)
                {
                    //The CN send this message to the selected TN involved in the trade
                    json_encoded = extra;
                }
                else if (action == 41)
                {
                    //The server responses to the broadcased trade relay
                    JObject js = new JObject();
                    js["cn.method"] = "cn.relaytradeavail";
                    js["cn.response"] = 1; //This is telling the CN that this is a response
                    js["cn.result"] = extra; //The order is in this string
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 42)
                {
                    //The client is requesting a validation node
                    JObject js = new JObject();
                    js["cn.method"] = "cn.getvalidator";
                    js["cn.response"] = 0;
                    js["cn.order_nonce"] = extra;
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 43)
                {
                    //The server responses to the order request availability
                    JObject js = new JObject();
                    js["cn.method"] = "cn.orderrequestexist";
                    js["cn.response"] = 1; //This is telling the CN that this is a response
                    js["cn.result"] = extra; //The order is in this string
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 44)
                {
                    //The server responses to the broadcased validator relay
                    JObject js = new JObject();
                    js["cn.method"] = "cn.relayvalidator";
                    js["cn.response"] = 1; //This is telling the CN that this is a response
                    js["cn.result"] = extra; //The order is in this string
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 45)
                {
                    //The server responses that info was not found
                    JObject js = new JObject();
                    js["cn.method"] = "cn.relayvalidator";
                    js["cn.response"] = 1; //This is telling the CN that this is a response
                    js["cn.result"] = extra; //The order is in this string
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 46)
                {
                    //Same as 45 but found info and used for prefilled validator info
                    json_encoded = extra;
                }
                else if (action == 47)
                {
                    //Same as 46 but for retrieving block data
                    con.blockdata = "";
                    json_encoded = extra;
                }
                else if (action == 48)
                {
                    //Request the last price
                    JObject js = new JObject();
                    js["cn.method"] = "cn.getlastprice";
                    js["cn.response"] = 0;
                    js["cn.market"] = extra;
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 50)
                {
                    //CN requesting information on open orders
                    //Because this data can be big, split into pages
                    con.blockdata = "";//This will be a blocking call more than likely
                    JObject js = new JObject();
                    js["cn.method"] = "cn.openorders";
                    js["cn.response"] = 0;
                    string[] parts = extra.Split(':'); //First half market, second half page
                    js["cn.result"] = parts[0];
                    js["cn.page"] = parts[1];
                    js["cn.authlevel"] = 1;
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 52)
                {
                    //Request NDEX fee
                    JObject js = new JObject();
                    js["cn.method"] = "cn.getndexfee";
                    js["cn.response"] = 0;
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 53)
                {
                    //Send the NDEX fee
                    JObject js = new JObject();
                    js["cn.method"] = "cn.getndexfee";
                    js["cn.response"] = 1;
                    js["cn.ndexfee"] = ndex_fee.ToString(CultureInfo.InvariantCulture);
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 54)
                {
                    //The server responses with its status
                    JObject js = new JObject();
                    js["cn.method"] = "cn.status";
                    js["cn.response"] = 1; //This is telling the CN that this is a response
                    js["cn.result"] = extra; //The order is in this string
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 55)
                {
                    //Return my version of NebliDex
                    JObject js = new JObject();
                    js["cn.method"] = "cn.getversion";
                    js["cn.response"] = 1; //This is telling the CN that this is a response
                    js["cn.result"] = protocol_version;
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 56)
                {
                    //This will request a CN list be sent to newly connected TN
                    con.blockdata = "";
                    JObject js = new JObject();
                    js["cn.method"] = "cn.getlist";
                    js["cn.response"] = 0; //This is telling the CN that this is a response
                    js["cn.page"] = Convert.ToInt32(extra);
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 57)
                {
                    //RPC request server actions
                    //Let the client know if our local blockhelper is active
                    JObject js = new JObject();
                    js["cn.method"] = "cn.getblockhelper_status";
                    js["cn.response"] = 1; //This is telling the client that this is a response
                    if (using_blockhelper == true)
                    {
                        js["cn.result"] = "Active";
                    }
                    else
                    {
                        js["cn.result"] = "Not Active";
                    }
                    json_encoded = JsonConvert.SerializeObject(js);
                }
                else if (action == 58)
                {
                    //Same as 46 but remove requirement to write log
                    json_encoded = extra;
                }

                if (json_encoded.Length > 0)
                {
                    //Now add data length and send data
                    Byte[] data = System.Text.Encoding.ASCII.GetBytes(json_encoded);
                    uint data_length = (uint)json_encoded.Length;
                    try
                    {
                        con.stream.Write(Uint2Bytes(data_length), 0, 4); //Write the length of the bytes to be received
                        con.stream.Write(data, 0, data.Length);
                        if (action != 58)
                        {
                            //BlockHelper uses a lot of messages so don't write to log
                            NebliDexNetLog("Sent msg: " + json_encoded);
                        }
                    }
                    catch (Exception e)
                    {
                        NebliDexNetLog("Connection Disconnected: " + con.ip_address[0] + ", error: " + e.ToString());
                        con.open = false;
                    }
                }

                con.lasttouchtime = UTCTime(); //Update the lasttouchtime
            }
        }

    }

}