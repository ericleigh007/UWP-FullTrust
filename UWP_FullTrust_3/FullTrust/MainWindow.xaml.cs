using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace FullTrust
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private double d1, d2;
        private AppServiceConnection connection = null;
        private ValueSet request = new ValueSet();
        private ValueSet response = new ValueSet();

        public MainWindow()
        {
            InitializeComponent();
            InitializeAppServiceConnection();
        }

        /// <summary>
        /// Open connection to UWP app service
        /// </summary>
        private async void InitializeAppServiceConnection()
        {
            response.Clear();

            connection = new AppServiceConnection();
            connection.AppServiceName = "SampleInteropService";
            connection.PackageFamilyName = Package.Current.Id.FamilyName;
            connection.RequestReceived += Connection_RequestReceived;
            connection.ServiceClosed += Connection_ServiceClosed;

            AppServiceConnectionStatus status = await connection.OpenAsync();
            if (status != AppServiceConnectionStatus.Success)
            {
                // something went wrong ...
                MessageBox.Show(status.ToString());
                this.IsEnabled = false;
            }        
        }

        /// <summary>
        /// Handles the event when the app service connection is closed
        /// </summary>
        private void Connection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            // connection to the UWP lost, so we shut down the desktop process
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                Application.Current.Shutdown();
            })); 
        }

        /// <summary>
        /// Handles the event when the desktop process receives a request from the UWP app
        /// </summary>
        private async void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            request = args.Request.Message;
            // retrive the reg key name from the ValueSet in the request
            if (request.ContainsKey("KEY"))
            {
                string key = request["KEY"] as string;
                int index = key.IndexOf('\\');
                if (index > 0)
                {
                    // read the key values from the respective hive in the registry
                    string hiveName = key.Substring(0, key.IndexOf('\\'));
                    string keyName = key.Substring(key.IndexOf('\\') + 1);
                    RegistryHive hive = RegistryHive.ClassesRoot;

                    switch (hiveName)
                    {
                        case "HKLM":
                            hive = RegistryHive.LocalMachine;
                            break;
                        case "HKCU":
                            hive = RegistryHive.CurrentUser;
                            break;
                        case "HKCR":
                            hive = RegistryHive.ClassesRoot;
                            break;
                        case "HKU":
                            hive = RegistryHive.Users;
                            break;
                        case "HKCC":
                            hive = RegistryHive.CurrentConfig;
                            break;
                    }

                    using (RegistryKey regKey = RegistryKey.OpenRemoteBaseKey(hive, "").OpenSubKey(keyName))
                    {
                        // compose the response as ValueSet
                        response.Clear();
                        if (regKey != null)
                        {
                            foreach (string valueName in regKey.GetValueNames())
                            {
                                response.Add(valueName, regKey.GetValue(valueName).ToString());
                            }
                        }
                        else
                        {
                            response.Add("ERROR", "KEY NOT FOUND");
                        }
                        // send the response back to the UWP
                        await args.Request.SendResponseAsync(response);
                    }
                }
                else
                {
                    response.Clear();
                    response.Add("ERROR", "INVALID REQUEST");
                    await args.Request.SendResponseAsync(response);
                }
            }
            else if (request.ContainsKey("PORTS"))
            {
                var need_init = response.Count == 0;
                var PORT_COUNT = (int)request["PORTS"];
                response.Clear();
                response["PORTS"] = (object)PORT_COUNT;
                for( int j = 0; j < PORT_COUNT; j++)
                {
                    request.TryGetValue($"P{j:000}", out object theValue);
                    switch( theValue )
                    {
                        case System.Byte b:
                            theValue = (object)(b + 1);
                            break;
                        case System.UInt16 s:
                            theValue = (object)(s + 1);
                            break;
                        case System.UInt32 ui:
                            theValue = (object)(ui + 1);
                            break;
                        case System.Single f:
                            theValue = (object)(f + 1.0);
                            break;
                    }

                    response[$"P{j:000}"] = theValue;
                }

                await args.Request.SendResponseAsync(response);
            }
        }

        /// <summary>
        /// Sends a request to the UWP app
        /// </summary>
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            // ask the UWP to calculate d1 + d2
            ValueSet request = new ValueSet();
            request.Add("D1", d1);
            request.Add("D2", d2);
            AppServiceResponse response = await connection.SendMessageAsync(request);
            double result = (double)response.Message["RESULT"];
            tbResult.Text = result.ToString();
        }

        /// <summary>
        /// Determines whether the "equals" button should be enabled
        /// based on input in the text boxes
        /// </summary>
        private void tb_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(tb1.Text, out d1) && double.TryParse(tb2.Text, out d2))
            {
                btnCalc.IsEnabled = true;
            }
            else
            {
                btnCalc.IsEnabled = false;
            }
        }
    }
}
