using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using NebliDex_Mobile.Droid;

namespace NebliDex_Mobile
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class OrdersPage : ContentPage
	{
        public OrdersPage()
        {
            InitializeComponent();

            //Setup the views
            Open_View.ItemsSource = MainService.MyOpenOrderList;
            Historic_View.ItemsSource = MainService.HistoricalTradeList;

        }

        //Events
        private void Show_Open_Orders(object sender, EventArgs e)
        {
            if (Open_View.IsVisible == true) { return; }
            //Swap Open from Historic View
            Open_View.IsVisible = true;
            Historic_View.IsVisible = false;
            Xamarin.Forms.Color yellow_color = Xamarin.Forms.Color.FromHex("#FFAE13");

            Label_Open.TextColor = yellow_color;
            Label_Open_Border.Color = yellow_color;

            Label_Historic.TextColor = Xamarin.Forms.Color.White;
            Label_Historic_Border.Color = Xamarin.Forms.Color.White;
        }

        private void Show_Historic_Orders(object sender, EventArgs e)
        {
            if(Historic_View.IsVisible == true) { return; }
            //Swap Historic View from Open
            Open_View.IsVisible = false;
            Historic_View.IsVisible = true;
            Xamarin.Forms.Color yellow_color = Xamarin.Forms.Color.FromHex("#FFAE13");

            Label_Historic.TextColor = yellow_color;
            Label_Historic_Border.Color = yellow_color;

            Label_Open.TextColor = Xamarin.Forms.Color.White;
            Label_Open_Border.Color = Xamarin.Forms.Color.White;
        }

        private void Request_Cancel_Order(object sender, EventArgs e)
        {
            //This will cancel an order, however it will not stop a pending payment after my transaction has broadcasted
            //Also for market orders, will attempt to cancel the order requests
            Label selected_label = sender as Label;
            MainService.OpenOrder ord = (MainService.OpenOrder)selected_label.BindingContext;
            if (ord.order_stage >= 3)
            {
                //The maker has an order in which it is waiting for the taker to redeem balance
                MainService.MessageBox("Notice!","Your order is currently involved in a trade. Please try again later.","OK",false);
                return;
            }

            selected_label.IsEnabled = false;
            Task.Run(() =>
            {
                MainService.CancelMyOrder(ord);
                MainService.NebliDex_Activity.RunOnUiThread(() => {
                    selected_label.IsEnabled = true;
                });
            });
        }



        //Menu touch events
        private void GoToMarketPage(object sender, EventArgs e)
        {
            MainService.current_ui_page = 1;
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
}