﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace NebliDex_Mobile
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SplashPage : ContentPage
	{
		public SplashPage()
		{
			InitializeComponent();
		}

        public void ChangeText(string text)
        {
            Status.Text = text; // Update was is being displayed
        }
	}
}
