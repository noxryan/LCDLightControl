using MonitorProfiler.Models.Display;
using MonitorProfiler.Win32;
using System;
using System.Drawing;
using System.Windows;
using UsbHid;
using System.Management;
using System.Timers;
using System.Collections.Generic;
using System.Windows.Interop;

namespace LCDLightControl
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const int USB_VENDOR_ID = 0x04d8;
        const int USB_PRODUCT_ID = 0x003f;
        const byte READ_LIGHT_VALUE = 0x37;
        const int MAX_LIGHT_VALUE = 1023; // 10-bit ADC on chip = max of 1023

        private Dictionary<Monitor, Tuple<int /* cutoffValue */, bool /* isOff */>> monitorProperties;
        
        private readonly MonitorCollection _monitorCollection = new MonitorCollection();
        private Monitor _currentMonitor;

        private UsbHidDevice usb;
        private ManagementEventWatcher w;
        private Timer t;
        private uint lightval = 0;

        public MainWindow()
        {
            InitializeComponent();
            trkCutoff.Maximum = MAX_LIGHT_VALUE;
            prgLightValue.Maximum = MAX_LIGHT_VALUE;
            ReadMonitors();
            USBInit();
        }

        ~MainWindow()
        {
            t.Stop();
            usb.Disconnect();
            if (w != null)
                w.Stop();
        }

        private void ReadMonitors()
        {
            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (IntPtr hMonitor, IntPtr hdcMonitor, ref Rectangle lprcMonitor, IntPtr dwData) =>
                {
                    _monitorCollection.Add(hMonitor);
                    return true;
                }, IntPtr.Zero);
            monitorProperties = new Dictionary<Monitor, Tuple<int, bool>>();
            foreach (Monitor monitor in _monitorCollection)
            {
                cboMonitors.Items.Add(monitor.Name);
                // This should be read from a file
                monitorProperties.Add(monitor, Tuple.Create<int, bool>(0, false));
            }
            if (cboMonitors.Items.Count > 0) cboMonitors.SelectedIndex = 0;
        }

        private void cboMonitors_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _currentMonitor = _monitorCollection[cboMonitors.SelectedIndex];
            RedrawMonitorControls();
        }

        private void RedrawMonitorControls()
        {
            var m = _currentMonitor;

            //Reset
            grpMode.IsEnabled = true;
            grpControls.IsEnabled = true;
            lblDDCCISupport.Visibility = Visibility.Collapsed;
            lblDDCBrightnessSupport.Visibility = Visibility.Collapsed;
            trkBrigtness.Value = 0;
            trkContrast.Value = 0;
            trkCutoff.Value = 0;

            //Set
            if (!m.SupportsDDC)
            {
                lblDDCCISupport.Visibility = Visibility.Visible;
                grpMode.IsEnabled = false;
                grpControls.IsEnabled = false;
            }
            else if (!m.Brightness.Supported)
            {
                lblDDCBrightnessSupport.Visibility = Visibility.Visible;
                grpMode.IsEnabled = false;
                grpControls.IsEnabled = false;
            }
            else
            {
                trkBrigtness.Minimum = (int)m.Brightness.Min;
                trkBrigtness.Maximum = (int)m.Brightness.Max;
                trkBrigtness.Value = (int)m.Brightness.Current;
                trkContrast.Minimum = (int)m.Contrast.Min;
                trkContrast.Maximum = (int)m.Contrast.Max;
                trkContrast.Value = (int)m.Contrast.Current;
            }
            trkCutoff.Value = monitorProperties[_currentMonitor].Item1;
        }

        private void radAuto_Checked(object sender, RoutedEventArgs e)
        {
            if (radManual == null)
                return;
            radManual.IsChecked = false;
            ChangeMonitorMode();
        }

        private void radManual_Checked(object sender, RoutedEventArgs e)
        {
            if (radAuto == null)
                return;
            radAuto.IsChecked = false;
            ChangeMonitorMode();
        }

        private void ChangeMonitorMode()
        {
            if (radManual.IsChecked == true)
            {
                grpControls.IsEnabled = true;
            }
            else if (radAuto.IsChecked == true)
            {
                grpControls.IsEnabled = false;
            }
        }


        private void USBInit()
        {
            t = new Timer(100);
            t.AutoReset = true;
            t.Elapsed += T_Elapsed;
            usb = new UsbHidDevice(USB_VENDOR_ID, USB_PRODUCT_ID);
            usb.OnConnected += Usb_OnConnected;
            usb.OnDisConnected += Usb_OnDisConnected;
            usb.DataReceived += Usb_DataReceived;
            usb.Connect();
            AddUSBHandler();
        }

        private void Usb_DataReceived(byte[] data)
        {
            switch(data[1])
            {
                case READ_LIGHT_VALUE:
                    lightval = (uint)(data[3] << 8) + data[2];
                    Application.Current.Dispatcher.Invoke(new Action(() => 
                    {
                        prgLightValue.Value = prgLightValue.Maximum - lightval;
                    }));
                    break;
            }
        }

        private void T_Elapsed(object sender, ElapsedEventArgs e)
        {
            if(usb.IsDeviceConnected)
            {
                usb.SendCommandMessage(READ_LIGHT_VALUE);
                // Monitor Power
                var props = monitorProperties[_currentMonitor];
                if ((MAX_LIGHT_VALUE - lightval) < props.Item1 && props.Item2 == false)
                {
                    monitorProperties[_currentMonitor] = new Tuple<int, bool>(props.Item1, true);
                    MonitorPower.Off();
                }
                else if ((MAX_LIGHT_VALUE - lightval) > props.Item1 && props.Item2 == true)
                {
                    monitorProperties[_currentMonitor] = new Tuple<int, bool>(props.Item1, false);
                    MonitorPower.On();
                }
                // Auto Mode
                if(_currentMonitor.SupportsDDC && _currentMonitor.Brightness.Supported && radAuto.IsChecked == true)
                {
                    double scale = _currentMonitor.Brightness.Max / MAX_LIGHT_VALUE;
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        trkBrigtness.Value = Math.Round(lightval * scale);
                    }));
                }
            }
        }

        private void Usb_OnDisConnected()
        {
            t.Stop();
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                lblConnected.Visibility = Visibility.Hidden;
                lblNotConnected.Visibility = Visibility.Visible;
            }));
        }

        private void Usb_OnConnected()
        {
            t.Start();
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                lblConnected.Visibility = Visibility.Visible;
                lblNotConnected.Visibility = Visibility.Hidden;
            }));
        }



        void AddUSBHandler()
        {
            WqlEventQuery q;// Represents a WMI event query in WQL format (Windows Query Language)

            ManagementScope scope = new ManagementScope("root\\CIMV2");

            // Represents a scope (namespace) for management operations.

            scope.Options.EnablePrivileges = true;
            try
            {
                q = new WqlEventQuery();
                q.EventClassName = "__InstanceCreationEvent";
                q.WithinInterval = new TimeSpan(0, 0, 3);
                q.Condition = @"TargetInstance ISA 'Win32_USBControllerdevice'";
                w = new ManagementEventWatcher(scope, q);

                //adds event handler that’s is fired when the insertion event occurs
                w.EventArrived += new EventArrivedEventHandler(USBInseted);

                w.Start();//run the watcher
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                if (w != null)
                    w.Stop();
            }
        }


        public void USBInseted(object sender, EventArgs e)
        {
            usb.Connect();
        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void trkCutoff_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var props = monitorProperties[_currentMonitor];
            monitorProperties[_currentMonitor] = new Tuple<int, bool>(Convert.ToInt32(trkCutoff.Value), props.Item2);
        }
    }
}
