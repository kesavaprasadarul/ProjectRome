using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;

namespace ProjectRome.Helpers
{
    public class FileTransferHelper
    {
        public const int pingPort = 8081;
        public const int powerPort = 8083; //Transfer Port
        public const int bufferSize = 65536; //in bytes
        public int transferProgress = 0; //Maximum is 100;
        StreamSocketListener listener;
        StreamSocketListener pListener;
        StreamSocket transferSocket;
        StreamSocketInformation remoteHost;

        public async Task<bool> initTransferListener()
        {
            try
            {
                listener = new StreamSocketListener();
                listener.ConnectionReceived += OnConnectionAsync;
                await listener.BindServiceNameAsync(powerPort.ToString());
                return true;
            }
            catch { return false; }
        }

        public async Task<bool> initPingListener()
        {
            try
            {
                pListener = new StreamSocketListener();
                pListener.ConnectionReceived += PListener_ConnectionReceived;
                await pListener.BindServiceNameAsync(pingPort.ToString());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void PListener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            Debug.WriteLine("Ping received Successfully");
            remoteHost = args.Socket.Information;
            //Forward to send function
            sendPayload();
        }

        private void OnConnectionAsync(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            Debug.WriteLine("Transfer Request Received Successfully.");
            //Call another function to receive file
            getPayload(args.Socket);
        }

        private void convertToPercents(Int64 current, Int64 actual)
        {
            transferProgress = (Convert.ToInt16(current / actual)) * 100;
        }

        public async void sendPayload()
        {
            var file = await getFileLocationAsync(PickerType.Open, "", "");
            transferSocket = new StreamSocket();
            transferSocket.Control.KeepAlive = false;
            if(remoteHost!=null)
            {
                await transferSocket.ConnectAsync(remoteHost.LocalAddress, powerPort.ToString());
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

        public async void getPayload (StreamSocket socket)
        {
            long streamSize = 0;
            long streamPosition = 0;
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
                var file = await getFileLocationAsync(PickerType.Save, (originalFilename.Split('.')[1]), originalFilename);
                var fileStream = await file.OpenStreamForWriteAsync();
                streamSize = Convert.ToInt64(fileLength);
                while (streamPosition < streamSize)
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
                    convertToPercents(streamPosition, streamSize);
                }
                rw.DetachStream();
                rw.Dispose();
            }
        }

        public async Task<StorageFile> getFileLocationAsync(PickerType type, string name, string extension = ".txt")
        {
            StorageFile file = null;
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            async () =>
            {
                switch (type)
                {
                    case PickerType.Open:
                        var pickerO = new Windows.Storage.Pickers.FileOpenPicker();
                        pickerO.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                        file =  await pickerO.PickSingleFileAsync();
                        break;
                    case PickerType.Save:
                        var pickerS = new Windows.Storage.Pickers.FileSavePicker();
                        pickerS.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                        pickerS.SuggestedFileName = name;
                        pickerS.FileTypeChoices.Add("", new List<string> { extension });
                        file = await pickerS.PickSaveFileAsync();
                        break;
                }
            });
            return file;
        }

        public enum PickerType
        {
            Open,
            Save
        }
    }
}
