﻿
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
using Windows.UI.ViewManagement;

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
        public HostName remoteHostInfo;
        public uint filenameLength;
        public ulong fileLength;
        public long streamSize = 0;
        public long streamPosition = 0;
        public DataReader rw;
        public bool isWD = false;
        public payloadType type;
        public StorageFile shareFile = null;
        public ScrollViewer sViewer = new ScrollViewer();
        public List<int> StackIndex =new List<int>();
        public ObservableCollection<PeerInformation> peerList = new ObservableCollection<PeerInformation>();
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
            this.SizeChanged += StartPage_SizeChanged;
            StartPage_SizeChanged(this, null);
            loadingVisible = Visibility.Collapsed;
            mainPresenter.Loaded += MainPresenter_Loaded;
            ApplicationView.GetForCurrentView().VisibleBoundsChanged += (s, e) => { StartPage_SizeChanged(this, null); };
            SystemNavigationManager.GetForCurrentView().BackRequested += StartPage_BackRequested;
            ////End Test
            enableWiFiDirect();
            initProjectRomeAPI();
            
        }
#region Wi-Fi Direct Helpers

        public async void enableWiFiDirect()
        {
            if(PeerFinder.SupportedDiscoveryTypes!= PeerDiscoveryTypes.None)
            {
                PeerFinder.AllowWiFiDirect = true;
                PeerFinder.Start();
                PeerFinder.AllowInfrastructure = true;
            }
        }

        private void Watcher_Removed(PeerWatcher sender, PeerInformation args)
        {
            peerList.Remove(args);
        }

        private void Watcher_Added(PeerWatcher sender, PeerInformation args)
        {
            peerList.Add(args);
        }
        public void initWatcher()
        {
            PeerWatcher watcher = PeerFinder.CreateWatcher();
            watcher.Added += Watcher_Added;
            watcher.Removed += Watcher_Removed;
            watcher.Start();
        }
        public async void connectToPeer(string ip,StorageFile file)
        {
            PeerWatcher dWDWatcher = PeerFinder.CreateWatcher();
            dWDWatcher.Added += async (s, e) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    if (e.HostName.CanonicalName == ip)
                    {
                        transferSocket = await PeerFinder.ConnectAsync(e);
                        isWD = true;
                        dWDWatcher.Stop();
                        await sendPayload(file);
                        returnToMain();
                    }
                });
               
            };
            dWDWatcher.Start();
            
            //foreach(var device in peerList)
            //{
            //    if(device.HostName.CanonicalName == ip)
            //    {
            //        transferSocket = await PeerFinder.ConnectAsync(device);
            //        isWD = true;
            //    }
            //}
        }



#endregion
        private void StartPage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if(StackIndex.Count!=0)
            {
                ScrollToIndex(mainPresenter, StackIndex.Last(), 0, true);
                StackIndex.Remove(StackIndex.Last());
                e.Handled = true;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        private void MainPresenter_Loaded(object sender, RoutedEventArgs ex)
        {
            sViewer = GetScrollViewer(mainPresenter as ListViewBase);
            sViewer.KeyDown += (s, e) => { if (e.Key == VirtualKey.Down || e.Key == VirtualKey.PageDown || e.Key == VirtualKey.Up || e.Key == VirtualKey.PageUp) e.Handled = true; };
            sViewer.KeyUp += (s, e) => { if (e.Key == VirtualKey.Down || e.Key == VirtualKey.PageDown || e.Key == VirtualKey.Up || e.Key == VirtualKey.PageUp) e.Handled = true; };

        }

        private void StartPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {            
            var height = Window.Current.Bounds;
            var bounds = ApplicationView.GetForCurrentView().VisibleBounds;   
            windowHeight = bounds.Height+ ApplicationView.GetForCurrentView().VisibleBounds.Top;
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
                   file = await pickerS.PickSaveFileAsync();
                    break;
            }

            return file;
        }

        private void returnToMain()
        {
            var timer = new DispatcherTimer();
            timer.Tick += (s, ex) => { ScrollToIndex(mainPresenter,0,0,true); (s as DispatcherTimer).Stop(); };
            StackIndex.Clear();
            timer.Interval = new TimeSpan(0, 0, 3);
            timer.Start();
        }

        private async void OnConnectionAsync(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,async () =>
            {
                await getPayloadInfo(args.Socket);
                ScrollToIndex(mainPresenter, 3,1);
                var filex = await getFileLocationAsync(Views.PickerType.Save, originalFilename, (originalFilename.Split('.')).Last());
                if (filex != null)
                    await getPayload(args.Socket, filex);
                args.Socket.Dispose();
                returnToMain();
            });

            
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

        public string getLocalIP()
        {
            var x = NetworkInformation.GetHostNames().Single(r => r.Type == HostNameType.Ipv4 && r.IPInformation.NetworkAdapter.GetConnectedProfileAsync().AsTask().Result.IsWlanConnectionProfile);
            return x.CanonicalName.ToString();
        }

        private async void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedRemoteSystem = ((ListView)sender).SelectedItem as RemoteSystem;
            if (type == payloadType.File)
            {
                StorageFile file;
                if (shareFile == null)
                {
                    var pickerO = new Windows.Storage.Pickers.FileOpenPicker();
                    pickerO.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                    pickerO.FileTypeFilter.Add("*");
                    file = await pickerO.PickSingleFileAsync();
                }

                else file = shareFile;                
                ScrollToIndex(mainPresenter, 3,2);
                if (SelectedRemoteSystem != null && file != null)
                {
                    ValueSet data = new ValueSet()
                {
                    {"host", getLocalIP() },
                    {"type","fileTransfer" },
                    {"file",file.Name },
                    {"isWDs", PeerFinder.SupportedDiscoveryTypes!=PeerDiscoveryTypes.None?"true":"false" }
                };
                    var response = await sendAppServiceRequest(SelectedRemoteSystem, data);
                    var ip = response["receiveIP"] as string;
                    remoteHostInfo = new HostName(ip);
                    if (response["isWDS"] as string == "true")
                        connectToPeer(ip, file);
                    else
                    {
                        await sendPayload(file);
                        returnToMain();
                    }

                }
            }
            else if(type== payloadType.Link)
            {
                var link = addressBar.Text;
                if (SelectedRemoteSystem != null)
                    if (await RemoteLauncher.LaunchUriAsync(new RemoteSystemConnectionRequest(SelectedRemoteSystem), new Uri(link)) == RemoteLaunchUriStatus.Success)
                        returnToMain();
                    else await (new Windows.UI.Popups.MessageDialog("Error")).ShowAsync();
            }
            remoteSystemList.SelectionChanged -= ListBox_SelectionChanged;
            remoteSystemList.SelectedIndex = -1;
            remoteSystemList.SelectionChanged += ListBox_SelectionChanged;
        }

        private async Task<ValueSet> sendAppServiceRequest(RemoteSystem remotesys, ValueSet props)
        {
            AppServiceConnection connection = new AppServiceConnection
            {
                AppServiceName = "com.warpzone.inventory",
                PackageFamilyName = "dd4ba9a3-acd0-42cf-8bdd-134c36e80ed2_x5ysvzjnrjtce"
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

            //var link = addressBar.Text;
            //var rSystem = remoteSystemList.SelectedItem as RemoteSystem;
            //if (rSystem != null)
            //    await RemoteLauncher.LaunchUriAsync(
            //            new RemoteSystemConnectionRequest(rSystem),
            //            new Uri(link));
        }

        #region PropertyValues
        private double _windowHeight;
        private string _originalFilename;
        private int _fileSize; //in MB
        private double _transferProgress; //100 is max
        private string _sizeProgress;
        private Visibility _loadingVisible;
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

        public double windowHeight
        {
            get { return _windowHeight; }
            set { _windowHeight = value;
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

        public Visibility loadingVisible
        {
            get { return _loadingVisible; }
            set
            {
                _loadingVisible = value;
                NotifyPropertyChanged();
            }
        }

        private void ScrollToIndex(ListViewBase listViewBase, int index, int currentIndex, bool isBack=false)
        {
            if(!isBack)
            StackIndex.Add(currentIndex);
            bool isVirtualizing = default(bool);
            double previousHorizontalOffset = default(double), previousVerticalOffset = default(double);

            // get the ScrollViewer withtin the ListView/GridView
            // get the SelectorItem to scroll to
            var selectorItem = listViewBase.ContainerFromIndex(index) as SelectorItem;

            // when it's null, means virtualization is on and the item hasn't been realized yet
            if (selectorItem == null)
            {
                isVirtualizing = true;

                previousHorizontalOffset = sViewer.HorizontalOffset;
                previousVerticalOffset = sViewer.VerticalOffset;

                // call task-based ScrollIntoViewAsync to realize the item
                listViewBase.ScrollIntoView(listViewBase.Items[index]);

                // this time the item shouldn't be null again
                selectorItem = (SelectorItem)listViewBase.ContainerFromIndex(index);
            }

            // calculate the position object in order to know how much to scroll to
            var transform = selectorItem.TransformToVisual((UIElement)sViewer.Content);
            var position = transform.TransformPoint(new Point(0, 0));
            // when virtualized, scroll back to previous position without animation
            if (isVirtualizing)
            {
                sViewer.ChangeView(previousHorizontalOffset, previousVerticalOffset, 1);
            }
            sViewer.IsFocusEngaged = false;
            // scroll to desired position with animation!
            sViewer.ChangeView(position.X, position.Y, null);
        }

        private ScrollViewer GetScrollViewer( DependencyObject element)
        {
            if (element is ScrollViewer)
            {
                return (ScrollViewer)element;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);

                var result = GetScrollViewer(child);
                if (result == null)
                {
                    continue;
                }
                else
                {
                    return result;
                }
            }

            return null;
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


        public async Task sendPayload(StorageFile file)
        {
            if (file == null)
                file = await getFileLocationAsync(PickerType.Open, "", "");
            originalFilename = file.DisplayName;
            if (remoteHostInfo != null)
            {
                loadingVisible = Visibility.Visible;
                if (isWD)
                {
                    transferSocket = new StreamSocket();
                    transferSocket.Control.KeepAlive = false;
                    await transferSocket.ConnectAsync(remoteHostInfo, powerPort.ToString());
                }
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
                    loadingVisible = Visibility.Collapsed;
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

        private void sendFileNavbtn_Click(object sender, RoutedEventArgs e)
        {
            ScrollToIndex(mainPresenter, 2,0);
            type = payloadType.File;
       }

        private void sendLinkbtn_Click_1(object sender, RoutedEventArgs e)
        {
            ScrollToIndex(mainPresenter, 1, 0);
            type = payloadType.Link;
        }

        private void warpLinkBtn_Click(object sender, RoutedEventArgs e)
        {
            ScrollToIndex(mainPresenter, 2, 1);
        }
    }


    public enum PickerType
    {
        Open,
        Save
    }

    public enum payloadType
    {
        File,
        Link,
        Clipboard
    }
}
