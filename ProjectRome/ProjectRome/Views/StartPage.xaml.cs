
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
using Windows.ApplicationModel.AppService;
using Windows.UI.Xaml.Navigation;
using ProjectRome.Helpers;
using Windows.System.RemoteSystems;
using System.Collections.ObjectModel;
using Windows.System;

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
        private RemoteSystemWatcher remoteWatcher;
        private RemoteSystem SelectedRemoteSystem = null;

        public ObservableCollection<RemoteSystem> RemoteSystems { get; private set; }

        public StartPage()
        {
            this.InitializeComponent();
            helper.initPingListener().AsAsyncAction().AsTask().Wait();
            helper.initTransferListener().AsAsyncAction().AsTask().Wait();
            initProjectRomeAPI();
        }

        public async void initProjectRomeAPI()
        {
            var accessState = await RemoteSystem.RequestAccessAsync();
            if (accessState == RemoteSystemAccessStatus.Allowed)
            {
                RemoteSystems = new ObservableCollection<RemoteSystem>();
                remoteWatcher = RemoteSystem.CreateWatcher();
                remoteWatcher.RemoteSystemAdded += OnRemoteSystemAdded;
                remoteWatcher.Start();
            }
        }

        private async void OnRemoteSystemAdded(RemoteSystemWatcher sender, RemoteSystemAddedEventArgs args)
        {
           await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                //this.RemoteSystems.Add(args.RemoteSystem);
                remoteSystemList.Items.Add(args.RemoteSystem);
            });
        }

        private async void sendBtn_Click(object sender, RoutedEventArgs e)
        {
            //Send Project Rome request
            var pickerO = new Windows.Storage.Pickers.FileOpenPicker();
            pickerO.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            pickerO.FileTypeFilter.Add("*");
           var file = await pickerO.PickSingleFileAsync();
            if (SelectedRemoteSystem!=null)
            {
                ValueSet data = new ValueSet();
                data.Add("type", "fileTransfer");
                data.Add("fileName", file.Name);
                sendAppServiceRequest(SelectedRemoteSystem, data);
               // data.Add("size", file.GetBasicPropertiesAsync().AsTask().Result.Size);
            }

        }

        private void receiveBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedRemoteSystem = ((ListView)sender).SelectedItem as RemoteSystem;
            sendBtn_Click(null, null);
        }

        private async void sendAppServiceRequest(RemoteSystem remotesys, ValueSet props)
        {
            AppServiceConnection connection = new AppServiceConnection
            {
                AppServiceName = "com.warpzone.inventory",
                PackageFamilyName = "dd4ba9a3-acd0-42cf-8bdd-134c36e80ed2_drfpcsz9zq1kw"
            };
            if (remotesys == null)
            {
                return;
            }
            RemoteSystemConnectionRequest connectionRequest = new RemoteSystemConnectionRequest(remotesys);
            AppServiceConnectionStatus status = await connection.OpenRemoteAsync(connectionRequest);
            if (status != AppServiceConnectionStatus.Success)
            {
               
                return;
            }
            var response = await connection.SendMessageAsync(props);
        }
    }
}
