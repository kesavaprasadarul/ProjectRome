
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
using System.ComponentModel;
using System.Runtime.CompilerServices;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace ProjectRome.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class StartPage : Page,INotifyPropertyChanged
    {
        StreamSocket rSocket;
        //FileTransferHelper helper = new FileTransferHelper();
        private RemoteSystemWatcher remoteWatcher;
        private RemoteSystem SelectedRemoteSystem = null;
        ObservableCollection<RemoteSystem> _RemoteSystems;
        public const int pingPort = 8081;
        public const int powerPort = 8083; //Transfer Port
        public const int bufferSize = 65536; //in bytes
        StreamSocketListener listener;
        StreamSocketListener pListener;
        StreamSocket transferSocket;
        //StreamSocketInformation remoteHost;
        public HostName remoteHostInfo;
        public uint filenameLength;
        public ulong fileLength;
        public long streamSize = 0;
        public long streamPosition = 0;
        public DataReader rw;
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
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,async () =>
            {
                await getPayloadInfo(args.Socket);
                //originalFilename = "Text.txt";
                var filex = await getFileLocationAsync(Views.PickerType.Save, originalFilename, (originalFilename.Split('.')).Last());
                if (filex != null)
                    await getPayload(args.Socket, filex);
                await new Windows.UI.Popups.MessageDialog("Done").ShowAsync();
            });

        //  var file = await getFileLocationAsync(Views.PickerType.Save, helper.originalFilename, (helper.originalFilename.Split('.')).Last());
            
        }

        private void PListener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            remoteHostInfo = args.Socket.Information.LocalAddress;
            //Forward to send function
            sendPayload(null);
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
            if (SelectedRemoteSystem!=null && file!=null)
            {
                ValueSet data = new ValueSet()
                {
                    {"host", getLocalIP() },
                    {"type","fileTransfer" },
                    {"file",file.Name }
                };
                var response = await sendAppServiceRequest(SelectedRemoteSystem, data);
                var x = response["receiveIP"] as string;
                remoteHostInfo = new HostName(x);
                sendPayload(file);
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
                PackageFamilyName = "dd4ba9a3-acd0-42cf-8bdd-134c36e80ed2_gzkz8cyssw602"
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

        #region TransferPropertiesStrings
        private string _originalFilename;
        private int _fileSize; //in MB
        private double _transferProgress; //100 is max
        private string _sizeProgress;
        public event PropertyChangedEventHandler PropertyChanged;

        public string originalFilename
        {
            get { return _originalFilename; }
            set
            {
                _originalFilename = value;
                NotifyPropertyChanged();
            }
        }

        public int fileSize
        {
            get { return _fileSize; }
            set
            {
                _fileSize = value;
                NotifyPropertyChanged();
            }
        }

        public double transferProgress
        {
            get { return _transferProgress; }
            set
            {
                _transferProgress = value;
                NotifyPropertyChanged();
            }
        }

        public string sizeProgress
        {
            get { return _sizeProgress; }
            set
            {
                _sizeProgress = value;
                NotifyPropertyChanged();
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        #endregion
        #region mainmethods

        private void convertToPercents(ulong current, ulong actual)
        {
            transferProgress = (current*100 / actual);
            sizeProgress = string.Format("{0} MB/ {1} MB", (current / 1048576).ToString(), (actual / 1048576).ToString());
        }

        private void convertToPercents(Int64 current, Int64 actual)
        {
            transferProgress = (current*100/ actual);
            sizeProgress = string.Format("{0} MB/ {1} MB", (current / 1048576).ToString(), (actual / 1048576).ToString());
        }


        public async void sendPayload(StorageFile file)
        {
            if (file == null)
                file = await getFileLocationAsync(PickerType.Open, "", "");
            originalFilename = file.DisplayName;
            transferSocket = new StreamSocket();
            transferSocket.Control.KeepAlive = false;
            if (remoteHostInfo != null)
            {
                await transferSocket.ConnectAsync(remoteHostInfo, powerPort.ToString());
                long streamPosition = 0;
                long streamSize = 0;
                byte[] buff = new byte[bufferSize];
                var properties = await file.GetBasicPropertiesAsync();
                using (var dataWriter = new DataWriter(transferSocket.OutputStream))
                {
                    // Send length of file name
                    dataWriter.WriteInt32(file.Name.Length);
                    // Send filename
                    dataWriter.WriteString(file.Name);
                    // Send file size
                    dataWriter.WriteUInt64(properties.Size);
                    // Send the file
                    Stream fileStream = await file.OpenStreamForReadAsync();
                    streamSize = fileStream.Length;
                    while (streamPosition < streamSize)
                    {
                        int len = 0;
                        fileStream.Position = streamPosition;
                        long memAlloc = fileStream.Length - streamPosition < bufferSize ? fileStream.Length - streamPosition : bufferSize;
                        byte[] buffer = new byte[memAlloc];
                        while (dataWriter.UnstoredBufferLength < memAlloc)
                        {
                            len = fileStream.Read(buffer, 0, buffer.Length);
                            if (len > 0)
                            {
                                dataWriter.WriteBytes(buffer);
                                streamPosition += len;
                            }
                        }
                        convertToPercents(streamPosition, streamSize);
                        try { await dataWriter.StoreAsync(); } catch { Debug.WriteLine("Tranfer Failed"); }
                        GC.Collect();
                    }
                    dataWriter.Dispose();
                }

            }
        }

        public async Task getPayload(StreamSocket socket, StorageFile file)
        {
            var fileStream = await file.OpenStreamForWriteAsync();
            streamSize = Convert.ToInt64(fileLength);
            while (streamPosition < streamSize)
            {
                fileStream.Position = Convert.ToInt64(streamPosition);
                long memAlloc = streamSize - streamPosition < bufferSize ? streamSize - streamPosition : bufferSize;
                byte[] buffer = new byte[memAlloc];
                var lenToRead = memAlloc;
                await rw.LoadAsync((uint)lenToRead);
                rw.ReadBytes(buffer);
                fileStream.Write(buffer, 0, buffer.Length);
                streamPosition += buffer.Length;
                convertToPercents(streamPosition, streamSize);
                GC.Collect();
            }
            rw.DetachStream();
            rw.Dispose();
        }

        public async Task getPayloadInfo(StreamSocket socket)
        {
            streamSize = 0;
            streamPosition = 0;
            rw = new DataReader(socket.InputStream);
                // 1. Read the filename length
                await rw.LoadAsync(sizeof(Int32));
                filenameLength = (uint)rw.ReadInt32();
                // 2. Read the filename
                await rw.LoadAsync(filenameLength);
                originalFilename = rw.ReadString(filenameLength);
                //3. Read the file length
                await rw.LoadAsync(sizeof(UInt64));
                fileLength = rw.ReadUInt64();
                //var file = await Views.StartPage.getFileLocationAsync(Views.PickerType.Save, originalFilename, (originalFilename.Split('.')).Last());
        }
        #endregion
    }

    public enum PickerType
    {
        Open,
        Save
    }
}
