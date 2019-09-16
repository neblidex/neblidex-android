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
	public partial class WalletPage : ContentPage
	{
		public WalletPage ()
		{
			InitializeComponent ();

            Wallet_View.ItemsSource = MainService.WalletList;

            //Force run a periodic query
            Task.Run(() =>
            {
                MainService.PeriodicNetworkQuery(null);
            });
        }

        //Events
        bool loading_coininfo = false;

        private async void GoToCoinInfo(object sender, EventArgs e)
        {
            //Find wallet that links to tapped item
            if(loading_coininfo == true) { return; }
            loading_coininfo = true;
            Grid selected_grid = sender as Grid;
            MainService.Wallet wal = (MainService.Wallet)selected_grid.BindingContext;
            //Now open a modal that loads this specific wallet's info
            await Navigation.PushModalAsync(new CoinInfo(wal));
            loading_coininfo = false;
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

        private void Exit_Touched(object sender, EventArgs e)
        {
            App.Exit_Requested();
        }
    }
}