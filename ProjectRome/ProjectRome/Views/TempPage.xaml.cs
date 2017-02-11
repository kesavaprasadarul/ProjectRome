using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.System.RemoteSystems;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using ProjectRome.Service;

// READ ME!!!
// This page is only temporary to build a proof of concept and test UI-stuff.
// Functions still need to be seperated from the UI, and this will be done in seperate pages.
// This page will be removed later on when the code has been seperated into different parts.

namespace ProjectRome.Views
{

    //TODO Seperate UI from code
    //TODO Rename things with a name that makes sense (and not use too similar names, herpderp)
    //TODO Clean up this awful mess I made (Ikarago)
    //TODO UX test on both PC and Mobile (950 + 435) and see what needs to be changed in the UI/UX


    public sealed partial class TempPage : Page
    {
        ShareOperation shareOperation;
        RemoteSystem SelectedDevice;
        public TempPage()
        {
            this.InitializeComponent();            

        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            var isRemoteEnabled =await RomeTask.initializeTask();
            if (isRemoteEnabled == false)
                Debugger.Break();
            RomeTask.SearchByRemoteSystemWatcher();
            try
            {
                shareOperation = (ShareOperation)e.Parameter;
                RomeTask.shareUrl = null;
                //TODO Make ENTER URL show up first, and then Select device --> CONTENT FIRST, DEVICE SECOND!
            }
            catch
            {

            }

        }
        private async void lvDevices_ItemClick(object sender, ItemClickEventArgs e)
        {
            SelectedDevice = (RemoteSystem)e.ClickedItem;
            cdSelectDevice.Hide();
            cdWarping.ShowAsync();


            RemoteLaunchUriStatus launchUriStatus =
            //await RemoteLauncher.LaunchUriAsync(
            //    new RemoteSystemConnectionRequest(SelectedDevice),
            //    new Uri("bingmaps:?cp=47.6204~-122.3491&sty=3d&rad=200&pit=75&hdg=165"));
            await RemoteLauncher.LaunchUriAsync(
                new RemoteSystemConnectionRequest(SelectedDevice),
                new Uri(RomeTask.urlToWarp));

            spButtons.Visibility = Visibility.Collapsed;
            cdWarping.Hide();

            spAllSet.Visibility = Visibility.Visible;
            await Task.Delay(TimeSpan.FromSeconds(3));  // Wait 3 secs before hiding stuff again
            spAllSet.Visibility = Visibility.Collapsed;
            spButtons.Visibility = Visibility.Visible;

            try
            {
                shareOperation.ReportCompleted();
            }
            catch
            {
                try
                {
                    shareOperation.DismissUI();
                }
                catch
                {
                    Debug.WriteLine("No Share operation");
                }
            }
        }

        private async void btnSendUrl_Click(object sender, RoutedEventArgs e)
        {
            RomeTask.urlToWarp = txtWarpLink.Text;
            cdWarpLink.Hide();
            await cdSelectDevice.ShowAsync();

        }

        private async void btnWarpLink_Click(object sender, RoutedEventArgs e)
        {
            lvDevices.SelectedItem = null;

            try
            {
                txtWarpLink.Text = RomeTask.shareUrl.AbsoluteUri;
                RomeTask.setSharedContent(RomeTask.ShareType.Link, new Uri(null));
            }
            catch { }
            await cdWarpLink.ShowAsync();

        }

        private async void lvDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //SelectedDevice = lvDevices.SelectedItem as RemoteSystem;
            //cdSelectDevice.Hide();
            //cdWarpLink.ShowAsync();
        }

        private void btnWarpLinkCancel_Click(object sender, RoutedEventArgs e)
        {
            cdWarpLink.Hide();
            spButtons.Visibility = Visibility.Visible;
        }

        private void cdWarpLink_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            try
            {
                cdSelectDevice.Hide();
            }
            catch
            { }
            try
            {
                cdWarpLink.Hide();
            }
            catch
            { }
            spButtons.Visibility = Visibility.Visible;
        }

        private async void cdWarpLink_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Test URL; if valid go futher, if not; stop and show message


            try
            {
                Uri testUrl = new Uri(txtWarpLink.Text);

                RomeTask.urlToWarp = txtWarpLink.Text;

                try
                {
                    tblNonValidUrl.Visibility = Visibility.Collapsed;
                }
                catch { }
                cdWarpLink.Hide();
                await cdSelectDevice.ShowAsync();
            }
            catch
            {
                try
                {
                    tblNonValidUrl.Visibility = Visibility.Visible;
                }
                catch { }
            }

        }

        private void cbtnAbout_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void btnGoToGitHub_Click(object sender, RoutedEventArgs e)
        {
            var uri = new Uri(@"https://github.com/kesavaprasadarul/ProjectRome");
            var success = await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        private async void cbtnSendFeedback_Click(object sender, RoutedEventArgs e)
        {
            var uri = new Uri(@"https://github.com/kesavaprasadarul/ProjectRome/issues");
            var success = await Windows.System.Launcher.LaunchUriAsync(uri);
        }
    }


    //private void ListB_SelectionChanged(object sender, SelectionChangedEventArgs e)
    //{
    //    SelectedDevice = ListB.SelectedItem as RemoteSystem;
    //}


    public class deviceListHelper : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, string language)
        {
            var param = parameter.ToString();
            var device = value as RemoteSystem;
            switch (param)
            {
                case "Connectivity":
                    {
                        if (device.IsAvailableByProximity == true)
                            return "LAN";
                        else
                            return "Internet";
                    }
                default:
                    break;

                case "Initals":
                    var st = device.DisplayName;
                    return st[0].ToString();
            }
            return "Error";

        }

        public object ConvertBack(object value, Type targetType,
    object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
