using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace UWP
{
    public sealed partial class MainPage : Page
    {
        private bool KeepSending = true;
        private int loopMessageCount = 0;
        private int totalMessageCount = 0;
        private Stopwatch watch = new Stopwatch();
        private Int64 loopTicksElapsed = 0;

        private ValueSet request = new ValueSet();
        private ValueSet response = new ValueSet();

        private readonly int PORT_COUNT = 200; // updating 200 points

        public MainPage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// When app is loaded, kick off the desktop process
        /// and listen to app service connection events
        /// </summary>
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (ApiInformation.IsApiContractPresent("Windows.ApplicationModel.FullTrustAppContract", 1, 0))
            {
                App.AppServiceConnected += MainPage_AppServiceConnected;
                App.AppServiceDisconnected += MainPage_AppServiceDisconnected;
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
            }
        }

        /// <summary>
        /// When the desktop process is connected, get ready to send/receive requests
        /// </summary>
        private async void MainPage_AppServiceConnected(object sender, AppServiceTriggerDetails e)
        {
            App.Connection.RequestReceived += AppServiceConnection_RequestReceived;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // enable UI to access  the connection
                btnRegKey.IsEnabled = true;
            });
        }

        /// <summary>
        /// When the desktop process is disconnected, reconnect if needed
        /// </summary>
        private async void MainPage_AppServiceDisconnected(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, ()=>
            {
                // disable UI to access the connection
                btnRegKey.IsEnabled = false;

                // ask user if they want to reconnect
                Reconnect();
            });            
        }

        /// <summary>
        /// Send request to query the registry
        /// </summary>
        private async void btnClick_ReadKey(object sender, RoutedEventArgs e)
        {
            request.Clear();

            request.Add("KEY", tbKey.Text);
            AppServiceResponse response = await App.Connection.SendMessageAsync(request);

            // display the response key/value pairs
            tbResult.Text = "";
            foreach (string key in response.Message.Keys)
            {
                tbResult.Text += key + " = " + response.Message[key] + "\r\n";
            }
        }

        /// <summary>
        /// Handle calculation request from desktop process
        /// (dummy scenario to show that connection is bi-directional)
        /// </summary>
        private async void AppServiceConnection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            double d1 = (double)args.Request.Message["D1"];
            double d2 = (double)args.Request.Message["D2"];
            double result = d1 + d2;
         
            ValueSet response = new ValueSet();
            response.Add("RESULT", result);
            await args.Request.SendResponseAsync(response);

            // log the request in the UI for demo purposes
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                tbRequests.Text += string.Format("Request: {0} + {1} --> Response = {2}\r\n", d1, d2, result);
            });
        }

        /// <summary>
        /// Ask user if they want to reconnect to the desktop process
        /// </summary>
        private async void Reconnect()
        {
            if (App.IsForeground)
            {
                MessageDialog dlg = new MessageDialog("Connection to desktop process lost. Reconnect?");
                UICommand yesCommand = new UICommand("Yes", async (r) =>
                {
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                });
                dlg.Commands.Add(yesCommand);
                UICommand noCommand = new UICommand("No", (r) => { });
                dlg.Commands.Add(noCommand);
                await dlg.ShowAsync();
            }
        }

        private async void btnClick_Start(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;

            KeepSending = true;
            totalMessageCount = 0;
            loopMessageCount = 0;
            var start = DateTime.UtcNow;

//            var theList = new List<object>() { 10.0F, 9.0F, 8.0F, 3.0F };
//            var theArray = theList.ToArray();
            int PORT_UPDATE_COUNT = 100;
            bool init = true;

            while (KeepSending)
            {
                bool wasInt = Int32.TryParse(PortCount.Text, out int tmp);
                if ( wasInt && (tmp > 0 && tmp < 1000 ))
                {
                    PORT_UPDATE_COUNT = tmp;
                }

                watch.Restart();
                request.Clear();
                request.Add("PORTS", PORT_UPDATE_COUNT);
                for (int i = 0; i < PORT_UPDATE_COUNT; i++)
                {
                    switch (i % 4)
                    {
                        case 0:
                            request[$"P{i:000}"] = (System.Int32)i;
                            break;
                        case 1:
                            request[$"P{i:000}"] = (System.Single)i;
                            break;
                        case 2:
                            request[$"P{i:000}"] = (System.Byte)i;
                            break;
                        case 3:
                            request[$"P{i:000}"] = (System.UInt16)i;
                            break;
                    }

                    /*
                    if ( !init && i == (PORT_UPDATE_COUNT-1))
                    {
                        break;
                    }
                    */
                }

                init = false;

                AppServiceResponse response = await App.Connection.SendMessageAsync(request);

                int returnCount = (int)response.Message["PORTS"];
                for( int j = 0; j < returnCount; j++ )
                {
                    object ans = response.Message[$"P{j:000}"];
                }

                watch.Stop();

                var ticksElapsed = watch.ElapsedTicks;
                loopTicksElapsed += ticksElapsed;
                int responsePorts = response.Message.Count-1;  // remove the "PORTS" entry
                loopMessageCount++;

                if ( loopMessageCount == 60 )
                {
                    totalMessageCount += loopMessageCount;
                    var elapsedmS = (double) TimeSpan.FromTicks(loopTicksElapsed).TotalMilliseconds/(double)loopMessageCount;
                    loopTicksElapsed = 0;
                    var totalElapsedTime = (DateTime.UtcNow - start).ToString();
                    StatsText.Text = $"{loopMessageCount} msgs ({PORT_UPDATE_COUNT}/{responsePorts}) in {elapsedmS:0.00}mS, {totalMessageCount} messages, elapsed {totalElapsedTime}";
                    loopMessageCount = 0;
                }
            }
        }

        private void btnClick_Stop(object sender, RoutedEventArgs e)
        {
            btnStop.IsEnabled = false;
            btnStart.IsEnabled = true;

            KeepSending = false;
        }
    }
}
