using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.System.RemoteSystems;

namespace ProjectRome.Service
{
    class RomeTask
    {
        static Uri shareUrl;
        static string urlToWarp;
        static ShareOperation shareOperation;
        static RemoteSystem SelectedDevice;
        static ObservableCollection<RemoteSystem> deviceList = new ObservableCollection<RemoteSystem>();
        static Dictionary<string, RemoteSystem> deviceMap = new Dictionary<string, RemoteSystem>();
        public static RemoteSystemWatcher m_remoteSystemWatcher;

        public enum NotifyType
        {
            StatusMessage,
            ErrorMessage,
            DeviceAdded,
            DeviceUpdated,
            DeviceRemoved
        }

        public enum ShareType
        {
            Text,
            Link,
            Media,
            File
        }

        public static async Task<bool> initializeTask()
        {
            RemoteSystemAccessStatus accessStatus = await RemoteSystem.RequestAccessAsync();
            if (accessStatus == RemoteSystemAccessStatus.Allowed)
            {
                m_remoteSystemWatcher = RemoteSystem.CreateWatcher(makeFilterList());
                return true;
            }
            else
                return false;
        }

        public static async void setSharedContent(ShareType type, object parameter)
        {
            case type
        }

        private static List<IRemoteSystemFilter> makeFilterList()
        {
            // construct an empty list
            List<IRemoteSystemFilter> localListOfFilters = new List<IRemoteSystemFilter>();

            // construct a discovery type filter that only allows "proximal" connections:
            RemoteSystemDiscoveryTypeFilter discoveryFilter = new RemoteSystemDiscoveryTypeFilter(RemoteSystemDiscoveryType.Any);


            // construct a device type filter that only allows desktop and mobile devices:
            // For this kind of filter, we must first create an IIterable of strings representing the device types to allow.
            // These strings are stored as static read-only properties of the RemoteSystemKinds class.
            List<String> listOfTypes = new List<String>();
            listOfTypes.Add(RemoteSystemKinds.Desktop);
            listOfTypes.Add(RemoteSystemKinds.Phone);

            // Put the list of device types into the constructor of the filter
            RemoteSystemKindFilter kindFilter = new RemoteSystemKindFilter(listOfTypes);


            // construct an availibility status filter that only allows devices marked as available:
            RemoteSystemStatusTypeFilter statusFilter = new RemoteSystemStatusTypeFilter(RemoteSystemStatusType.Available);


            // add the 3 filters to the listL
            localListOfFilters.Add(discoveryFilter);
            localListOfFilters.Add(kindFilter);
            localListOfFilters.Add(statusFilter);

            // return the list
            return localListOfFilters;
        }


    }
}
