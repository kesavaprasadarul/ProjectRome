using NotificationsExtensions;
using NotificationsExtensions.Toasts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.Proximity;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using ProjectRome.Helpers;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace ProjectRome.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class StartPage : Page
    {
        StreamSocket rSocket;
        FileTransferHelper helper = new FileTransferHelper();
        public StartPage()
        {
            this.InitializeComponent();
            helper.initPingListener().AsAsyncAction().AsTask().Wait();
            helper.initTransferListener().AsAsyncAction().AsTask().Wait();
        }

        private async void sendBtn_Click(object sender, RoutedEventArgs e)
        {
          //Send Project Rome request
        }

        private void receiveBtn_Click(object sender, RoutedEventArgs e)
        {

        }

    }
}
