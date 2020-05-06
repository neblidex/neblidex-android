using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Mono.Data.Sqlite;
using System.Globalization;
using System.Linq;
using System.Data;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using NebliDex_Mobile.Droid;

namespace NebliDex_Mobile
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class MarketPage : ContentPage
	{

        //Globals
        public ObservableCollection<MainService.OpenOrder> Selling_View_List = new ObservableCollection<MainService.OpenOrder>();
        public ObservableCollection<MainService.OpenOrder> Buying_View_List = new ObservableCollection<MainService.OpenOrder>();

        public int last_candle_time = 0; //The utctime of the last candle
        public int chart_timeline = 0; //24 hours, 1 = 7 days
        public double chart_low = 0, chart_high = 0;
        public bool market_loading = false;
        public int tapped_amount = 0; //This keeps track of the amount of times an order is tapped

        //Reuseable Boxview lists for candles
        public List<BoxView> chart_candles_rect = new List<BoxView>();
        public List<BoxView> chart_candles_line = new List<BoxView>();

        //Public fields for private views
        public Label CN_Fee;
        public Label Taker_Fee;
        public Label Trade_Fee;
        public Label Base_Fee;

        public MarketPage ()
		{
            market_loading = true;
            InitializeComponent ();

            //Make public members for private fiels
            CN_Fee = priv_CN_Fee;
            Taker_Fee = priv_Taker_Fee;
            Trade_Fee = priv_Trade_Fee;
            Base_Fee = priv_Base_Fee;

            LoadMarketPageUI();
		}

        private void LoadMarketPageUI()
        {
            //This function will load the UI based on the data present
            CreateMarketsList();

            //Change the Sell and Buy labels
            Buy_Button.Text = "Buy " + MainService.MarketList[MainService.exchange_market].trade_symbol;
            Sell_Button.Text = "Sell " + MainService.MarketList[MainService.exchange_market].trade_symbol;

            //Set the Recent trade list to our list
            Recent_Trade_List.HeightRequest = 100;
            Recent_Trade_List.ItemsSource = MainService.RecentTradeList[MainService.exchange_market];
            Selling_View.ItemsSource = Selling_View_List;
            Buying_View.ItemsSource = Buying_View_List;

            //Load the re-useable boxviews for the chart
            chart_candles_line.Clear();
            chart_candles_rect.Clear();
            for (int i = 0; i < 100; i++)
            {
                //Add the line to the view
                BoxView bv = new BoxView();
                bv.IsVisible = false;
                Chart_Canvas.Children.Add(bv);
                chart_candles_line.Add(bv);
                
                //Add the rect to the view
                bv = new BoxView();
                bv.IsVisible = false;
                Chart_Canvas.Children.Add(bv);
                chart_candles_rect.Add(bv);
            }

            //This will be ran in a separate thread due to SQLite access that may be slow
            //Update the Candles and populate the order lists
            Task.Run(() => {
                if(App.force_reload_marketdata == true)
                {
                    //This will only run if client has been out of sync (sleeping)
                    MainService.ClearMarketData(MainService.exchange_market);
                    MainService.GetCNMarketData(MainService.exchange_market);
                    App.force_reload_marketdata = false;
                }
                PopulateOrderList();
                UpdateCandles();
            });
            
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            //Finally, update the blockrates view
            //Show the initial fees as well
            MainService.NebliDex_UI.UpdateBlockrates();
            market_loading = false;
        }

        public void AddSortedOrderToViewList(ObservableCollection<MainService.OpenOrder> nodelist, MainService.OpenOrder ord, bool highlow)
        {
            //Sort the collection by price
            for(int it = 0;it < nodelist.Count;it++)
            {
                MainService.OpenOrder old_ord = nodelist[it];
                if(old_ord == ord) { return; } //Order already in list
                if(old_ord.order_nonce == ord.order_nonce) { return; } //Order already in list
                if(highlow == true)
                {
                    if(old_ord.price <= ord.price)
                    {
                        nodelist.Insert(it, ord);
                        return;
                    }
                }else
                {
                    if(old_ord.price >= ord.price)
                    {
                        nodelist.Insert(it, ord);
                        return;
                    }
                }
            }
            nodelist.Add(ord);
        }

        public void RefreshUI()
        {
            //This will reload the visuals on all the lists and charts for a new market

            //Update the buttons
            Buy_Button.Text = "Buy " + MainService.MarketList[MainService.exchange_market].trade_symbol;
            Sell_Button.Text = "Sell " + MainService.MarketList[MainService.exchange_market].trade_symbol;

            //Change the recent trade list
            Recent_Trade_List.HeightRequest = 100;
            Recent_Trade_List.ItemsSource = MainService.RecentTradeList[MainService.exchange_market];

            //Update the Candles and populate the order lists
            Task.Run(() => {
                PopulateOrderList();
                UpdateCandles();
                });

            //Update block rates
            MainService.NebliDex_UI.UpdateBlockrates();

        }

        public void CreateMarketsList()
        {
            //Update combo box to show markets
            Market_Box.Items.Clear();
            for (int i = 0; i < MainService.MarketList.Count; i++)
            {
                if (MainService.MarketList[i].active == false)
                {
                    continue;
                }
                string format_market = MainService.MarketList[i].format_market;
                //We are going to alphabetically sort the marketlist
                bool not_found = true;
                for (int i2 = 0; i2 < Market_Box.Items.Count; i2++)
                {
                    string item_detail = (string)Market_Box.Items[i2];
                    int compare = String.Compare(format_market, item_detail, true);
                    if (compare < 0)
                    {
                        not_found = false;
                        //Format Market precedes item_detail, add it in front
                        Market_Box.Items.Insert(i2, MainService.MarketList[i].format_market);
                        break;
                    }
                }
                if (not_found == true)
                {
                    Market_Box.Items.Add(MainService.MarketList[i].format_market);
                }
            }
            //Now go through the market box list and find that match to the exchange market
            for (int i = 0; i < Market_Box.Items.Count; i++)
            {
                string item_detail = (string)Market_Box.Items[i];
                if (MainService.MarketList[MainService.exchange_market].format_market == item_detail)
                {
                    //This is our default selected market
                    Market_Box.SelectedIndex = i;
                    break;
                }
            }
        }

        private void PopulateOrderList()
        {
            //This method will populate the order lists with current orders
            Selling_View_List.Clear();
            Buying_View_List.Clear();
            Selling_View.HeightRequest = 100;
            Buying_View.HeightRequest = 100;

            //Now add the orders into the view and sort them
            //Populate the sell list first
            lock (MainService.OpenOrderList[MainService.exchange_market])
            {
                for (int i = 0; i < MainService.OpenOrderList[MainService.exchange_market].Count; i++)
                {
                    if (MainService.OpenOrderList[MainService.exchange_market][i].type == 1)
                    {
                        //Sell Orders
                        AddSortedOrderToViewList(Selling_View_List, MainService.OpenOrderList[MainService.exchange_market][i], false);
                    }
                }

                //And buy list then
                for (int i = 0; i < MainService.OpenOrderList[MainService.exchange_market].Count; i++)
                {
                    if (MainService.OpenOrderList[MainService.exchange_market][i].type == 0)
                    {
                        //Buy Orders
                        AddSortedOrderToViewList(Buying_View_List, MainService.OpenOrderList[MainService.exchange_market][i], true);
                    }

                }
            }
        }

        private void Selected_Order(object sender, SelectedItemChangedEventArgs e)
        {
            //This fires when the order has been selected for the first time
            tapped_amount = 0; //Reset the tap
        }

        private async void DoubleTapped_Order(object sender, ItemTappedEventArgs e)
        {
            if (market_loading == true) { return; }

            ListView view = sender as ListView;
            if (view == null) { return; }
            if (view.SelectedItem == null) { return; }
            tapped_amount++;
            if(tapped_amount < 2) { return; } //Must tap this order at least twice before trying to trade
            tapped_amount = 0;

            MainService.OpenOrder ord = (MainService.OpenOrder)view.SelectedItem; //This is order

            //Verify that order is not our own order
            bool notmine = true;
            lock (MainService.MyOpenOrderList)
            {
                for (int i = 0; i < MainService.MyOpenOrderList.Count; i++)
                {
                    if (MainService.MyOpenOrderList[i].order_nonce == ord.order_nonce)
                    {
                        notmine = false; break;
                    }
                }
            }

            if (notmine == true)
            {
                await Navigation.PushModalAsync(new MatchOrder(ord));
            }
            else
            {
                MainService.MessageBox("Alert", "Cannot match with your own order!", "OK", false);
            }

        }

        private int Selected_Market(string mform)
        {
            for (int i = 0; i < MainService.MarketList.Count; i++)
            {
                if (mform == MainService.MarketList[i].trade_symbol + "/" + MainService.MarketList[i].base_symbol)
                {
                    return i;
                }
            }
            return -1;
        }

        private void AutosizeListView(object sender, ItemVisibilityEventArgs e)
        {
            ListView view = sender as ListView;
            double rowHeight = Label_24H.FontSize;
            double height = 100;
            if (view == Buying_View)
            {
                rowHeight += 5; //Add extra height for spacing
                height = Buying_View_List.Count * rowHeight;               
            }else if(view == Selling_View)
            {
                rowHeight += 5; //Add extra height for spacing
                height = Selling_View_List.Count * rowHeight;
            }else if(view == Recent_Trade_List)
            {
                rowHeight = 15;
                height = (MainService.RecentTradeList[MainService.exchange_market].Count+1) * rowHeight;
            }
            //Increase size by factor of 100
            height = Math.Ceiling((height+1) / 100.0) * 100;
            view.HeightRequest = height;
        }

        private void Toggle_Fee_Menu(object sender, EventArgs e)
        {
            if (Fee_Menu.IsVisible == false)
            {
                Fee_Menu.IsVisible = true;
            }
            else
            {
                Fee_Menu.IsVisible = false;
            }
        }

        //Buttons and Options
        private void Change_Chart_Timeline(object sender, EventArgs e)
        {
            if(market_loading == true) { return; }
            StackLayout content = (StackLayout)sender;
            if(content == Container_7D)
            {
                if(chart_timeline == 1) { return; } // Already 7D, nothing to change
                lock (MainService.ChartLastPrice)
                {
                    chart_timeline = 1;
                    //Toggle Colors
                    Label_7D.TextColor = Label_24H.TextColor;
                    Label_7D_Border.Color = Label_24H_Border.Color;
                    Label_24H.TextColor = Color.White;
                    Label_24H_Border.Color = Color.White;
                }
            }else if(content == Container_24H)
            {
                if (chart_timeline == 0) { return; } // Already 24H, nothing to change
                lock (MainService.ChartLastPrice)
                {
                    chart_timeline = 0;
                    Label_24H.TextColor = Label_7D.TextColor;
                    Label_24H_Border.Color = Label_7D_Border.Color;
                    Label_7D.TextColor = Color.White;
                    Label_7D_Border.Color = Color.White;
                }
            }
            Chart_Last_Price.Text = "LOADING...";
            Task.Run(() => UpdateCandles()); //Update the candles based on the timeline
        }

        private async void Open_Buy(object sender, EventArgs e)
        {
            if(market_loading == true) { return; } //Cannot buy in between markets
            Buy_Button.IsEnabled = false;
            await Navigation.PushModalAsync(new PlaceOrder(0));
            Buy_Button.IsEnabled = true;
        }

        private async void Open_Sell(object sender, EventArgs e)
        {
            if (market_loading == true) { return; } //Cannot sell in between markets
            Sell_Button.IsEnabled = false;
            await Navigation.PushModalAsync(new PlaceOrder(1));
            Sell_Button.IsEnabled = true;
        }

        private void Chart_Size_Changed(object sender, EventArgs e)
        {
            AdjustCandlePositions(); //Fix the candle positions
        }

        private void Change_Market(object sender, EventArgs e)
        {
            if(market_loading == true) { return; }

            PickerFixed pick = sender as PickerFixed;
            if(pick.SelectedIndex < 0) { return; }
            string market_string = pick.Items[pick.SelectedIndex];
            int which_market = Selected_Market(market_string);

            if(which_market == MainService.exchange_market) { return; } //We just selected the same market

            if (which_market > -1)
            {
                int oldmarket = MainService.exchange_market;
                MainService.exchange_market = which_market;

                pick.IsEnabled = false;
                market_loading = true;
                Chart_Last_Price.Text = "LOADING..."; //Put a loading status

                //Clear the old candles and charts and reload the market data for this market
                Task.Run(() => {
                    MainService.ClearMarketData(oldmarket);
                    MainService.GetCNMarketData(MainService.exchange_market);
                    MainService.NebliDex_Activity.RunOnUiThread(() =>
                    {                        
                        RefreshUI();
                        Market_Box.IsEnabled = true; //Re-enable the box
                        market_loading = false;                        
                    });
                    MainService.Save_UI_Config(); //Save the UI Market Info
                    MainService.Load_UI_Config();
                });
            }
        }

        //Candle stuff
        public void PlaceCandleInChart(MainService.Candle can)
        {
            //First adjust the candle high and low if the open and close are the same
            if (can.high == can.low)
            {
                can.high = can.high + MainService.double_epsilon; //Allow us to create a range
                can.low = can.low - MainService.double_epsilon;
                if (can.low < 0) { can.low = 0; }
            }

            //And it will add it to the list
            lock (MainService.ChartLastPrice)
            {
                if (MainService.VisibleCandles.Count >= 100)
                {
                    MainService.VisibleCandles.RemoveAt(0); //Remove the first / oldest candle
                }
                MainService.VisibleCandles.Add(can);
            }
            //Don't adjust candle positions until manually forced
            last_candle_time = MainService.UTCTime();
        }

        public void AddCurrentCandle()
        {
            if (MainService.ChartLastPrice[chart_timeline].Count == 0) { return; }
            //This will add a new candle based on current last chart prices
            //Then Load the current candle into the chart. This candle is not stored in database and based soley and chartlastprice
            double open = -1, close = -1, high = -1, low = -1;
            for (int pos = 0; pos < MainService.ChartLastPrice[chart_timeline].Count; pos++)
            {
                if (MainService.ChartLastPrice[chart_timeline][pos].market == MainService.exchange_market)
                {
                    double price = Convert.ToDouble(MainService.ChartLastPrice[chart_timeline][pos].price);
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
                }
            }
            if (open > 0)
            {
                //May not have any candles for this market
                MainService.Candle new_can = new MainService.Candle();
                new_can.open = open;
                new_can.close = close;
                new_can.high = high;
                new_can.low = low;
                PlaceCandleInChart(new_can);
            }
        }

        public void UpdateCandles()
        {
            //Do the off main thread stuff first, then mainthread stuff
            SqliteConnection mycon = new SqliteConnection("Data Source=\"" + MainService.App_Path + "/neblidex.db\";Version=3;");
            mycon.Open();

            //Set our busy timeout, so we wait if there are locks present
            SqliteCommand statement = new SqliteCommand("PRAGMA busy_timeout = 5000", mycon); //Create a transaction to make inserts faster
            statement.ExecuteNonQuery();
            statement.Dispose();

            string myquery = "";
            int backtime = 0;

            if (chart_timeline == 0)
            { //24 hr
                backtime = MainService.UTCTime() - 60 * 60 * 25;
                myquery = "Select highprice, lowprice, open, close From CANDLESTICKS24H Where market = @mark And utctime > @time Order By utctime ASC";
            }
            else if (chart_timeline == 1)
            { //7 day
                backtime = MainService.UTCTime() - (int)Math.Round(60.0 * 60.0 * 24.0 * 6.25); //Closer to actual time of 100 candles
                myquery = "Select highprice, lowprice, open, close From CANDLESTICKS7D Where market = @mark And utctime > @time Order By utctime ASC";
            }

            statement = new SqliteCommand(myquery, mycon);
            statement.Parameters.AddWithValue("@time", backtime);
            statement.Parameters.AddWithValue("@mark", MainService.exchange_market);
            SqliteDataReader statement_reader = statement.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(statement_reader); //Loads all the data in the table
            statement_reader.Close();
            statement.Dispose();
            mycon.Close();

            //Do most the work on the background thread
            //Clear the Visible Candles and reload the charts for the appropriate timescale and market
            lock (MainService.ChartLastPrice)
            {
                MainService.VisibleCandles.Clear();
                for (int i = 0; i < table.Rows.Count; i++)
                {
                    //Go Candle by Candle to get results
                    MainService.Candle can = new MainService.Candle();
                    //Must use cultureinfo as some countries see . as ,
                    can.open = Convert.ToDouble(table.Rows[i]["open"], CultureInfo.InvariantCulture);
                    can.close = Convert.ToDouble(table.Rows[i]["close"], CultureInfo.InvariantCulture);
                    can.low = Convert.ToDouble(table.Rows[i]["lowprice"], CultureInfo.InvariantCulture);
                    can.high = Convert.ToDouble(table.Rows[i]["highprice"], CultureInfo.InvariantCulture);
                    PlaceCandleInChart(can);
                }

                //Add a recent candle based on the last trade
                AddCurrentCandle();
            }

            //The following will be ran on the UI thread
            //Now work on the data on the main thread        
            MainService.NebliDex_Activity.RunOnUiThread(() => {             

                Market_Percent.TextColor = App.grey_color;
                Market_Percent.Text = "00.00%";
                Chart_Last_Price.Text = "0.00000000";
                
                AdjustCandlePositions();
            });
        }

        public void UpdateLastCandle(double val)
        {
            //This will update the last candle in the view, must be ran from UI thread
            if (MainService.VisibleCandles.Count == 0)
            {
                //Make a new candle, how history exists
                MainService.Candle can = new MainService.Candle();
                can.open = val;
                can.close = val;
                can.high = val;
                can.low = val;
                PlaceCandleInChart(can);
            }
            else
            {
                //This will update the value for the last candle
                MainService.Candle can = MainService.VisibleCandles[MainService.VisibleCandles.Count - 1]; //Get last candle
                if (val > can.high)
                {
                    can.high = val;
                }
                if (val < can.low)
                {
                    can.low = val;
                }

                can.close = val;

                //Look at the last chartlastprice to find the close price for the candle
                int timeline = chart_timeline;
                lock (MainService.ChartLastPrice)
                {
                    for (int i = MainService.ChartLastPrice[timeline].Count - 1; i >= 0; i--)
                    {
                        if (MainService.ChartLastPrice[timeline][i].market == MainService.exchange_market)
                        {
                            can.close = Convert.ToDouble(MainService.ChartLastPrice[timeline][i].price);
                            break;
                        }
                    }
                }                
            }
            AdjustCandlePositions();
        }

        public void AdjustCandlePositions()
        {
            //This function will update the canvas and graphs to make candles appear well
            if (Chart_Canvas.Width < 10) { return; }
            if (Chart_Canvas.Height < 10) { return; }
            if (MainService.VisibleCandles.Count == 0) {
                //Visible Candles
                for (int i = 0; i < 100; i++)
                {
                    chart_candles_line[i].IsVisible = false;
                    chart_candles_rect[i].IsVisible = false;
                }
                return;
            }
            double lowest = -1;
            double highest = -1;
            for (int i = 0; i < MainService.VisibleCandles.Count; i++)
            {
                //Go through each candle to find maximum height and maximum low
                if (MainService.VisibleCandles[i].low < lowest || lowest < 0)
                {
                    lowest = MainService.VisibleCandles[i].low;
                }
                if (MainService.VisibleCandles[i].high > highest || highest < 0)
                {
                    highest = MainService.VisibleCandles[i].high;
                }
            }

            double middle = (highest - lowest) / 2.0; //Should be the middle of the chart
            if (middle <= 0) { return; } //Shouldn't happen unless flat market

            //Make it so that the candles don't hit buttons
            lowest = lowest - (highest - lowest) * 0.1;
            highest = highest + (highest - lowest) * 0.1;

            //Calculate Scales
            double ChartScale = Chart_Canvas.Height / (highest - lowest);
            double width = Chart_Canvas.Width / 100.0;

            //Position Candles based on scale and width
            //Total of 100 candles visible so each candle needs to be 1/100 of chart
            double xpos = 0;
            double ypos = 0;
            double candles_width = MainService.VisibleCandles.Count * width; //The width of the entire set of candles
            double height = 0;
            Rectangle reuseable_rect = new Rectangle();
            int total_rects_used = 0; //We are going to use the reuseable boxviews that are already in the canvas

            lock (MainService.ChartLastPrice)
            {
                for (int i = 0; i < MainService.VisibleCandles.Count; i++)
                {
                    xpos = (Chart_Canvas.Width - candles_width); //Start position
                    xpos = xpos + i * width; //Current Position

                    double rect_xpos = 0;
                    double rect_ypos = 0;
                    double rect_width = 0;
                    double rect_height = 0;

                    rect_width = width; //Set the Width
                    rect_xpos = xpos; //Set the X position of Rect

                    //Calculate height now
                    if (MainService.VisibleCandles[i].open > MainService.VisibleCandles[i].close)
                    {
                        //Red Candle
                        height = (MainService.VisibleCandles[i].open - MainService.VisibleCandles[i].close) * ChartScale; //Calculate Height
                        ypos = Chart_Canvas.Height - (MainService.VisibleCandles[i].open - lowest) * ChartScale; //Top Left Corner is 0,0
                    }
                    else
                    {
                        //Green candle
                        height = (MainService.VisibleCandles[i].close - MainService.VisibleCandles[i].open) * ChartScale; //Calculate Height
                        ypos = Chart_Canvas.Height - (MainService.VisibleCandles[i].close - lowest) * ChartScale; //Top Left Corner is 0,0
                    }

                    if (height < 1) { height = 1; } //Show something
                    rect_height = height;
                    rect_ypos = ypos;

                    //Now adjust the layout based on the candle position
                    reuseable_rect.X = rect_xpos;
                    reuseable_rect.Y = rect_ypos;
                    reuseable_rect.Width = Math.Ceiling(rect_width);
                    reuseable_rect.Height = Math.Ceiling(rect_height);

                    BoxView bv = chart_candles_rect[total_rects_used];
                    AbsoluteLayout.SetLayoutBounds(bv, reuseable_rect);
                    bv.IsVisible = true; //Make it visibile

                    //And color the rect correctly
                    if (MainService.VisibleCandles[i].close < MainService.VisibleCandles[i].open)
                    {
                        //Color this rectangle red
                        bv.Color = App.red_candle;
                    }
                    else
                    {
                        bv.Color = App.green_candle;
                    }

                    //Calculate Outliers
                    bv = chart_candles_line[total_rects_used];
                    if (MainService.VisibleCandles[i].high - MainService.VisibleCandles[i].low >= MainService.double_epsilon * 2.1)
                    {
                        height = (MainService.VisibleCandles[i].high - MainService.VisibleCandles[i].low) * ChartScale;
                        if (height < 1) { height = 1; } //Show something
                        ypos = Chart_Canvas.Height - (MainService.VisibleCandles[i].high - lowest) * ChartScale;
                        xpos = xpos + (width / 2.0);
                        reuseable_rect.X = xpos;
                        reuseable_rect.Y = ypos;
                        reuseable_rect.Width = 1;
                        reuseable_rect.Height = height;

                        AbsoluteLayout.SetLayoutBounds(bv, reuseable_rect);
                        bv.Color = App.grey_color;
                        bv.IsVisible = true;
                    }
                    else
                    {
                        //Very small difference in low and high, essentially none so hide the line
                        bv.IsVisible = false;
                    }
                    total_rects_used++;
                }
            }

            //Now hide the rest of the boxviews if they are visible
            for(int i = total_rects_used;i < 100; i++)
            {
                chart_candles_line[i].IsVisible = false;
                chart_candles_rect[i].IsVisible = false;
            }

            chart_low = lowest;
            chart_high = highest;

            //Change the Market Percent
            double change = Math.Round((MainService.VisibleCandles[MainService.VisibleCandles.Count - 1].close - MainService.VisibleCandles[0].open) / MainService.VisibleCandles[0].open * 100, 2);
            if (change == 0)
            {
                Market_Percent.TextColor = App.grey_color;
                Market_Percent.Text = "00.00%";
            }
            else if (change > 0)
            {
                //Green
                Market_Percent.TextColor = App.green_candle;
                if (change > 10000)
                {
                    Market_Percent.Text = "> +10000%";
                }
                else
                {
                    Market_Percent.Text = "+" + String.Format(CultureInfo.InvariantCulture, "{0,0:N2}", change) + "%";
                }
            }
            else if (change < 0)
            {
                Market_Percent.TextColor = App.red_candle;
                if (change < -10000)
                {
                    Market_Percent.Text = "> -10000%";
                }
                else
                {
                    Market_Percent.Text = "" + String.Format(CultureInfo.InvariantCulture, "{0,0:N2}", change) + "%";
                }
            }
            Chart_Last_Price.Text = String.Format(CultureInfo.InvariantCulture, "{0:0.########}", MainService.VisibleCandles[MainService.VisibleCandles.Count - 1].close);
        }
        //End Candle stuff

        //Menu touch events
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

        private void Exit_Touched(object sender, EventArgs e)
        {
            App.Exit_Requested();
        }
    }

    //This is a custom picker that centers the text
    public class PickerFixed : Picker
    {
        protected override void OnPropertyChanged(string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);

            if (propertyName == Picker.SelectedIndexProperty.PropertyName)
            {
                this.InvalidateMeasure();
            }
        }
    }
}