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
using Windows.UI.Notifications;

namespace WarpzoneBackend
{
    public sealed class Inventory : IBackgroundTask
    {
        private BackgroundTaskDeferral backgroundTaskDeferral;
        private AppServiceConnection appServiceconnection;

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
            processData(data);
            //main code
            messageDeferral.Complete();
        }


        public void processData(ValueSet data)
        {
            string header = "Undefined";
            string desc = "Oolala";
            foreach( var item in data)
            {
                if(item.Key== "type")
                {
                    switch (item.Value)
                    {
                        case "fileTransfer":
                            header = "File Transfer Request";
                            desc = data.Last().Value.ToString();
                            break;
                        default:
                            header = "Undefined request";
                            break;
                    }
                }
                
            }
            sendToast(header, desc);
        }

        public void sendToast(string header, string desc)
        {
            ToastContent content = new ToastContent()
            {
                Launch = "app-defined-string",
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

        public void sendNotification()
        {
            var xmlToastTemplate = "<toast launch=\"app-defined-string\">" +
                         "<visual>" +
                           "<binding template =\"ToastGeneric\">" +
                             "<text>Sample Notification</text>" +
                             "<text>" +
                               "Test Notification" +
                             "</text>" +
                           "</binding>" +
                         "</visual>" +
                       "</toast>";

            // load the template as XML document
            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(xmlToastTemplate);

            // create the toast notification and show to user
            var toastNotification = new ToastNotification(xmlDocument);
            var notification = ToastNotificationManager.CreateToastNotifier();
            notification.Show(toastNotification);
            
        }


        private void OnTaskCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            if (this.backgroundTaskDeferral != null)
            {
                // Complete the service deferral.
                this.backgroundTaskDeferral.Complete();
            }
        }
    }
}
