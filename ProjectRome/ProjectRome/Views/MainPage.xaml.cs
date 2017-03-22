using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace ProjectRome.Views
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            //sbWarpBackgroundAnimation.Begin();
        }

        private void btnLink_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(Views.LinkPage));
        }

        private void sbHideTypeSelection_Completed(object sender, object e)
        {
            //spSelectType.Visibility = Visibility.Collapsed;

            // TEMP
            //spSelectLink.Visibility = Visibility.Visible;
        }
    }
}
