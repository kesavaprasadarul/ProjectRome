
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
using Newtonsoft.Json;
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

        ObservableCollection<RemoteSystem> _RemoteSystems;
        public ObservableCollection<RemoteSystem> RemoteSystems
        {
            get { return _RemoteSystems; }
            set
            {
                _RemoteSystems = value;
            }
        }

        public StartPage()
        {
            this.InitializeComponent();
            //helper.initPingListener().AsAsyncAction().AsTask().Wait();
            //helper.initTransferListener().AsAsyncAction().AsTask().Wait();

            ////Test
            StreamSocketListener listener;
            StreamSocketListener pListener;
            const int pingPort = 8081;
            const int powerPort = 8083; //Transfer Port
            const int bufferSize = 65536;
            listener = new StreamSocketListener();
            listener.ConnectionReceived += OnConnectionAsync;
            listener.BindServiceNameAsync(powerPort.ToString()).AsTask().Wait();

            pListener = new StreamSocketListener();
            pListener.ConnectionReceived += PListener_ConnectionReceived;
            pListener.BindServiceNameAsync(pingPort.ToString()).AsTask().Wait();

            ////End Test
            initProjectRomeAPI();
        }

        public async Task<StorageFile> getFileLocationAsync(PickerType type, string name, string extension = "txt")
        {
            StorageFile file = null;

            switch (type)
            {
                case PickerType.Open:
                    var pickerO = new Windows.Storage.Pickers.FileOpenPicker();
                    pickerO.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                    pickerO.FileTypeFilter.Add("*");
                    file = await pickerO.PickSingleFileAsync();                    
                    break;
                case PickerType.Save:
                    var pickerS = new Windows.Storage.Pickers.FileSavePicker();
                    pickerS.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                    pickerS.FileTypeChoices.Add(extension, new List<string> { "."+extension});                    
                    pickerS.SuggestedFileName = name;
                    // this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { file =  pickerS.PickSaveFileAsync(); });
                   file = await pickerS.PickSaveFileAsync();


                    break;
            }

            return file;
        }

        private async void OnConnectionAsync(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
          StorageFile filex = null;
          await helper.getPayloadInfo(args.Socket);
         await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => {
                filex = await getFileLocationAsync(Views.PickerType.Save, helper.originalFilename, (helper.originalFilename.Split('.')).Last());
                if (filex != null)
                    await helper.getPayload(args.Socket, filex);
                await new Windows.UI.Popups.MessageDialog("Done").ShowAsync();
            });
        //  var file = await getFileLocationAsync(Views.PickerType.Save, helper.originalFilename, (helper.originalFilename.Split('.')).Last());
            
        }

        private void PListener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            helper.remoteHostInfo = args.Socket.Information.LocalAddress;
            //Forward to send function
            helper.sendPayload(null);
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
           await this.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                remoteSystemList.Items.Add(args.RemoteSystem);
                
            });
           
        }

        private async void sendBtn_Click(object sender, RoutedEventArgs e)
        {
            ////Send Project Rome request
            var pickerO = new Windows.Storage.Pickers.FileOpenPicker();
            pickerO.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            pickerO.FileTypeFilter.Add("*");
            var file = await pickerO.PickSingleFileAsync();
            if (SelectedRemoteSystem!=null)
            {
                ValueSet data = new ValueSet()
                {
                    {"host", getLocalIP() },
                    {"type","fileTransfer" },
                    {"file",file.Name }
                };
                var response = await sendAppServiceRequest(SelectedRemoteSystem, data);
                var x = response["receiveIP"] as string;
                helper.remoteHostInfo = new HostName(x);
                helper.sendPayload(file);
            }

        }


        public string getLocalIP()
        {
            var x = NetworkInformation.GetHostNames().Single(r => r.Type == HostNameType.Ipv4);
            return x.CanonicalName.ToString();
        }

        private async void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedRemoteSystem = ((ListView)sender).SelectedItem as RemoteSystem;

        }

        private async Task<ValueSet> sendAppServiceRequest(RemoteSystem remotesys, ValueSet props)
        {
            AppServiceConnection connection = new AppServiceConnection
            {
                AppServiceName = "com.warpzone.inventory",
                PackageFamilyName = "dd4ba9a3-acd0-42cf-8bdd-134c36e80ed2_drfpcsz9zq1kw"
            };
            if (remotesys == null)
            {
                return null;
            }
            RemoteSystemConnectionRequest connectionRequest = new RemoteSystemConnectionRequest(remotesys);
            AppServiceConnectionStatus status = await connection.OpenRemoteAsync(connectionRequest);
            if (status != AppServiceConnectionStatus.Success)
            {               
                return null;
            }
            var y = await connection.SendMessageAsync(props);
            return y.Message; 
        }

        private async void sendLinkBtn_Click(object sender, RoutedEventArgs e)
        {
            var link = addressBar.Text;
            if(SelectedRemoteSystem!=null)
            await RemoteLauncher.LaunchUriAsync(
                    new RemoteSystemConnectionRequest(SelectedRemoteSystem),
                    new Uri(link));
        }
    }

    public enum PickerType
    {
        Open,
        Save
    }
}
