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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace ProjectRome.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class StartPage : Page
    {
        StreamSocket rSocket;
        public List<PeerInformation> nearbyDevices = new List<PeerInformation>();
        StreamSocketListener listener = new StreamSocketListener();
        public StartPage()
        {
            this.InitializeComponent();
            initListenerAsync().AsAsyncAction();
        }

        private async Task initListenerAsync()
        {
            StreamSocketListener listener = new StreamSocketListener();
            listener.ConnectionReceived += OnConnectionAsync;
            await listener.BindServiceNameAsync("8083");
            if (PeerFinder.SupportedDiscoveryTypes != PeerDiscoveryTypes.None)
            {
                PeerFinder.ConnectionRequested += PeerFinder_ConnectionRequested;
                PeerFinder.Stop();
                PeerFinder.Start();
                var watcher = PeerFinder.CreateWatcher();
                watcher.Added += Watcher_Added;
                watcher.Start();
            }
        }

        private void Watcher_Added(PeerWatcher sender, PeerInformation args)
        {
            nearbyDevices.Add(args);
        }

        private async void PeerFinder_ConnectionRequested(object sender, ConnectionRequestedEventArgs args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
             async () =>
             {
                 StreamSocket socket = await PeerFinder.ConnectAsync(args.PeerInformation);
                 await ReceiveFileFomPeer(socket);                 
             });
        }

        private async void OnConnectionAsync(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            rSocket = args.Socket;
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            async () =>
            {
                 await ReceiveFileFomPeer(rSocket);
            });
        }

        public async Task<StreamSocket> getSocketFromProximityAsync()
        {
            StreamSocket x = null;
            if (nearbyDevices.Count != 0)
                x = await PeerFinder.ConnectAsync(nearbyDevices[0]);
            return x;
        }

        private async void sendBtn_Click(object sender, RoutedEventArgs e)
        {        
            StreamSocket socket = new StreamSocket();
            socket.Control.KeepAlive = false;
            var testSocket = await getSocketFromProximityAsync();
            if (testSocket != null)
                socket = testSocket;
            else
            await socket.ConnectAsync(new HostName(ipTxtBox.Text), "8083");
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".mp4");
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.MusicLibrary;
            StorageFile file = await picker.PickSingleFileAsync();
            await SendFileToPeerAsync(socket, file);
        }

        private async Task SendFileToPeerAsync(StreamSocket socket, StorageFile selectedFile)
        {
            long streamPosition = 0;
            long streamSize = 0;
            byte[] buff = new byte[65536];
            var prop = await selectedFile.GetBasicPropertiesAsync();
            using (var dw = new DataWriter(socket.OutputStream))
            {
                // 1. Send the filename length
                dw.WriteInt32(selectedFile.Name.Length);
                // 2. Send the filename
                dw.WriteString(selectedFile.Name);
                // 3. Send the file length
                dw.WriteUInt64(prop.Size);
                // 4. Send the file
                var fileStream = await selectedFile.OpenStreamForReadAsync();
                streamSize = fileStream.Length;
                while (streamPosition < streamSize)
                {                
                int len = 0;                
                fileStream.Position = streamPosition;
                long memAlloc = fileStream.Length - streamPosition < 65536 ? fileStream.Length - streamPosition : 65536;
                byte[] buffer = new byte[memAlloc];
                while (dw.UnstoredBufferLength < memAlloc)
                {
                    len = fileStream.Read(buffer, 0, buffer.Length);
                    if (len > 0)
                    {
                        dw.WriteBytes(buffer);
                        streamPosition += len;
                    }
                }

                try
                {
                    await dw.StoreAsync();
                }
                catch
                {
                    Debug.WriteLine(string.Format("Failed to store {0} bytes.", dw.UnstoredBufferLength));
                }
                GC.Collect();                
                }
                dw.Dispose();
            }
        }


        private async Task<bool> ReceiveFileFomPeer(StreamSocket socket)
        {
            long streamSize = 0;
            long streamPosition = 0;
            StorageFile file =null;
            using (var rw = new DataReader(socket.InputStream))
            {
                // 1. Read the filename length
                await rw.LoadAsync(sizeof(Int32));
                var filenameLength = (uint)rw.ReadInt32();
                // 2. Read the filename
                await rw.LoadAsync(filenameLength);
                var originalFilename = rw.ReadString(filenameLength);
                //3. Read the file length
                await rw.LoadAsync(sizeof(UInt64));
                var fileLength = rw.ReadUInt64();
                // 4. Reading file                
                var picker = new Windows.Storage.Pickers.FileSavePicker();
                picker.FileTypeChoices.Add("Any", new List<string> { ".mp4" });
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                // Dropdown of file types the user can save the file as
                picker.SuggestedFileName = "New Document";
                file = await picker.PickSaveFileAsync();
                var fileStream = await file.OpenStreamForWriteAsync();
                streamSize =Convert.ToInt64(fileLength);
                while(streamPosition<streamSize)
                {
                    fileStream.Position = Convert.ToInt64(streamPosition);
                    long memAlloc = streamSize - streamPosition < 65536 ? streamSize - streamPosition : 65536;
                    byte[] buffer = new byte[memAlloc];
                    while (rw.UnconsumedBufferLength < memAlloc)
                    {
                        var lenToRead = memAlloc;
                        await rw.LoadAsync((uint)lenToRead);
                        var tempBuff = rw.ReadBuffer((uint)lenToRead);
                        fileStream.Write(buffer, 0, buffer.Length);
                        streamPosition += fileStream.Length;
                    }
                    GC.Collect();
                 }
                rw.DetachStream();
                rw.Dispose();

            }
 
            return true;
        }

        private async Task<InMemoryRandomAccessStream> DownloadFile(DataReader rw, ulong fileLength)
        {
            var memStream = new InMemoryRandomAccessStream();

            // Download the file
            while (memStream.Position < fileLength)
            {               
                var lenToRead = Math.Min(65536, fileLength - memStream.Position);
                await rw.LoadAsync((uint)lenToRead);
                var tempBuff = rw.ReadBuffer((uint)lenToRead);
                await memStream.WriteAsync(tempBuff);
            }

            return memStream;
        }

        private void receiveBtn_Click(object sender, RoutedEventArgs e)
        {

        }

    }
}
