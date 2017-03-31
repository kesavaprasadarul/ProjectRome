using NotificationsExtensions;
using NotificationsExtensions.Toasts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.Foundation.Collections;
using System.Xml.Serialization;
using Windows.UI.Notifications;
using Microsoft.QueryStringDotNET;
using Windows.ApplicationModel.DataTransfer;
using System.Threading;
using Newtonsoft.Json;
using Windows.UI.Core;
using Windows.Networking.Connectivity;
using Windows.Networking;
using Windows.Networking.Proximity;

namespace WarpzoneBackend
{
    public sealed class Inventory : IBackgroundTask
    {
        private BackgroundTaskDeferral backgroundTaskDeferral;
        private AppServiceConnection appServiceconnection;
        private string HostIP = "Empty";
        private bool WDRequest = false;

        [STAThread]
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            this.backgroundTaskDeferral = taskInstance.GetDeferral(); // Get a deferral so that the service isn't terminated.
            taskInstance.Canceled += OnTaskCanceled; // Associate a cancellation handler with the background task.

            // Retrieve the app service connection and set up a listener for incoming app service requests.
            var details = taskInstance.TriggerDetails as AppServiceTriggerDetails;
            appServiceconnection = details.AppServiceConnection;
            appServiceconnection.RequestReceived += OnRequestReceived;
        }

        private void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var messageDeferral = args.GetDeferral();
            var data = args.Request.Message;
            processParameters(data,args);
            //main code
            messageDeferral.Complete();
        }



        public static void sendToast(string header, string desc,string subDesc)
        {

            ToastContent content = new ToastContent()
            {
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children ={
                        new AdaptiveText()
                        {
                            Text = header
                        },

                        new AdaptiveText()
                        {
                            Text = desc
                        },

                        new AdaptiveText()
                        {
                            Text = subDesc
                        }
                    },

                    }
                },

                Actions = new ToastActionsCustom()
                {
                    Buttons =
                    {
                        new ToastButton("check", "check")
                        {

                        },

                        new ToastButton("cancel", "cancel")
                        {
                              ActivationType= ToastActivationType.Background
                        }
                    }
                }
            };

            var toastNotification = new ToastNotification(content.GetXml());
            ToastNotificationManager.CreateToastNotifier().Show(toastNotification);
        }

        private void OnTaskCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            if (this.backgroundTaskDeferral != null)
            {
                // Complete the service deferral.
                this.backgroundTaskDeferral.Complete();
            }
        }

        HostName getLocalIP()
        {
            return NetworkInformation.GetHostNames().Single(r => r.Type == HostNameType.Ipv4);
        }

        void processParameters(ValueSet data, AppServiceRequestReceivedEventArgs args)
        {
            //var HostIP = data["host"].ToString();
            //sendToast("Request", "Done!");
            if (data["isWdS"].ToString() == "true")
                WDRequest = true;
            switch (data["type"].ToString())
            {
                case "fileTransfer":
                    //code for file transfer, include notification
                    sendToast("File Transfer", "Done!",data["host"].ToString());
                    
                    var payload = new ValueSet()
                    {
                        {"type","fileTransfer" },
                        {"receiveIP", getLocalIP().CanonicalName.ToString()},
                        {"isWDs", PeerFinder.SupportedDiscoveryTypes!=PeerDiscoveryTypes.None?"true":"false" }
                    };
                    args.Request.SendResponseAsync(payload).AsTask().Wait();
                    break;
                case "clipboard":
                    sendToast("Clipboard Died", "On Progess, hold on for updates!!", data["host"].ToString());
                    //code for clipboard, include notification
                    break;
                default:
                    //Notify for undefined parameter reception, ask to update the receiver
                    break;
            }
        } 

    }
}
