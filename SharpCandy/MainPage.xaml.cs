using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Usb;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SharpCandy
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public static MainPage Current;

        private Dictionary<DeviceWatcher, String> mapDeviceWatchersToDeviceSelector;

        public MainPage()
        {
            this.InitializeComponent();
            Current = this;

            mapDeviceWatchersToDeviceSelector = new Dictionary<DeviceWatcher, String>();
        }

        /// <summary>
        /// Create the DeviceWatcher object for Fadcandy when the user navigates to this page.
        /// </summary>
        /// <param name="eventArgs">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs eventArgs)
        {
            //// If we are connected to the device or planning to reconnect, we should disable the list of devices
            //// to prevent the user from opening a device without explicitly closing or disabling the auto reconnect
            //if (EventHandlerForDevice.Current.IsDeviceConnected
            //    || (EventHandlerForDevice.Current.IsEnabledAutoReconnect
            //    && EventHandlerForDevice.Current.DeviceInformation != null))
            //{
            //    // These notifications will occur if we are waiting to reconnect to device when we start the page
            //    EventHandlerForDevice.Current.OnDeviceConnected = this.OnDeviceConnected;
            //    EventHandlerForDevice.Current.OnDeviceClose = this.OnDeviceClosing;
            //}

            // Begin watching out for events
            //StartHandlingAppEvents();

            // Initialize the desired device watchers so that we can watch for when devices are connected/removed
            InitializeFadecandyDeviceWatcher();
            StartDeviceWatchers();
        }

        /// <summary>
        /// Registers for Added, Removed, and Enumerated events on the provided deviceWatcher before adding it to an internal list.
        /// </summary>
        /// <param name="deviceWatcher"></param>
        /// <param name="deviceSelector">The AQS used to create the device watcher</param>
        private void InitializeFadecandyDeviceWatcher()
        {
            var deviceSelector = UsbDevice.GetDeviceSelector(Fadecandy.DeviceVid, Fadecandy.DevicePid);

            // Create a device watcher to look for instances of the SuperMUTT device
            var deviceWatcher = DeviceInformation.CreateWatcher(deviceSelector);

            deviceWatcher.Added += new TypedEventHandler<DeviceWatcher, DeviceInformation>(this.OnDeviceAdded);
            deviceWatcher.Removed += new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>(this.OnDeviceRemoved);

            mapDeviceWatchersToDeviceSelector.Add(deviceWatcher, deviceSelector);
        }

        /// <summary>
        /// We will remove the device from the UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="deviceInformationUpdate"></param>
        private async void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate deviceInformationUpdate)
        {
            string[] idPieces = deviceInformationUpdate.Id.Split('&');
            string intNumber = idPieces[2];

            if (intNumber == "MI_00#6")
            {
                await Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                new DispatchedHandler(() =>
                {
                    NotifyUser("Device Removed", deviceInformationUpdate.Id, NotifyType.StatusMessage);
                }));
            }
        }

        /// <summary>
        /// This function will add the device to the listOfDevices so that it shows up in the UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="deviceInformation"></param>
        private async void OnDeviceAdded(DeviceWatcher sender, DeviceInformation deviceInformation)
        {
            string[] idPieces = deviceInformation.Id.Split('&');
            string intNumber = idPieces[2];

            if (deviceInformation.Name == Fadecandy.Name && intNumber == "MI_00#6")
            {
                await Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    new DispatchedHandler(() =>
                    {
                        ConnectToDevice(deviceInformation, mapDeviceWatchersToDeviceSelector[sender]);
                    }));
            }
        }

        private async void ConnectToDevice(DeviceInformation deviceInfo, string deviceSelector)
        {
            // Create an EventHandlerForDevice to watch for the device we are connecting to
            FadecandyEventHandler.CreateNewEventHandlerForDevice();

            // Get notified when the device was successfully connected to or about to be closed
            FadecandyEventHandler.Current.OnDeviceConnected = this.OnDeviceConnected;
            FadecandyEventHandler.Current.OnDeviceClose = this.OnDeviceClosing;

            // It is important that the FromIdAsync call is made on the UI thread because the consent prompt, when present,
            // can only be displayed on the UI thread. Since this method is invoked by the UI, we are already in the UI thread.
            Boolean openSuccess = await FadecandyEventHandler.Current.OpenDeviceAsync(deviceInfo, deviceSelector);

            if (openSuccess)
                NotifyUser("Device Connected.", deviceInfo.Id, NotifyType.StatusMessage);
            else
                NotifyUser("Device Failed.", deviceInfo.Id, NotifyType.ErrorMessage);
        }

        //private void StartHandlingAppEvents()
        //{

        //}

        /// <summary>
        /// If all the devices have been enumerated, select the device in the list we connected to. Otherwise let the EnumerationComplete event
        /// from the device watcher handle the device selection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="deviceInformation"></param>
        private void OnDeviceConnected(FadecandyEventHandler sender, DeviceInformation deviceInformation)
        {
            NotifyUser("Connected to Device", FadecandyEventHandler.Current.DeviceInformation.Id, NotifyType.StatusMessage);
        }

        /// <summary>
        /// The device was closed. If we will autoreconnect to the device, reflect that in the UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="deviceInformation"></param>
        private async void OnDeviceClosing(FadecandyEventHandler sender, DeviceInformation deviceInformation)
        {
            await Dispatcher.RunAsync(
               CoreDispatcherPriority.Normal,
               new DispatchedHandler(() =>
               {

               }));
        }

        private async void ButtonSendDefaultLUT_Click(object sender, RoutedEventArgs e)
        {
            AddMessage("Send Default LUT Pressed", null);

            await SendLUTArray();
        }

        private void ButtonAlienSignal_Click(object sender, RoutedEventArgs e)
        {
            int delay = Convert.ToInt16(txtDelayAlien.Text);
            int density = Convert.ToInt16(txtCountAlien.Text);
            byte brightness = Convert.ToByte(txtBrightnessAlien.Text);
            AlienSignal(delay, density, brightness);
        }

        private void ButtonWhiteFillTopDown_Click(object sender, RoutedEventArgs e)
        {
            AddMessage("Fill Top Down Pressed", null);

            int delay = Convert.ToInt16(txtDelayFillDown.Text);
            byte brightness = Convert.ToByte(txtBrightnessFillDown.Text);

            FillDown(delay, brightness);
        }

        private void ButtonRandomFlash_Click(object sender, RoutedEventArgs e)
        {
            AddMessage("Random Flash Pressed", null);

            int delay = Convert.ToInt16(txtDelayFlash.Text);
            int frames = Convert.ToInt16(txtFramesFlash.Text);
            int flashCount = Convert.ToInt16(txtCountFlash.Text);
            byte brightness = Convert.ToByte(txtBrightnessFlash.Text);

            Flash(delay, frames, flashCount, brightness);
        }

        private async void ButtonRandomFlashTrailing_Click(object sender, RoutedEventArgs e)
        {
            AddMessage("Random Flash Trailing Pressed", null);

            Fadecandy.LoadVideoBlank();
            await SendVideoArray();

            int currentLED = 0;
            Queue<int> leds = new Queue<int>();

            int density = Convert.ToInt16(txtCountFlashTrail.Text);
            for (int c = 0; c < density; c++)
            {
                leds.Enqueue(0);
            }

            Random rndLocation = new Random(DateTime.Now.Millisecond);

            for (int c = 0; c < 200; c++)
            {
                int lastLED = leds.Dequeue();
                Fadecandy.VideoArray[lastLED] = 0x00;
                Fadecandy.VideoArray[lastLED + 1] = 0x00;
                Fadecandy.VideoArray[lastLED + 2] = 0x00;

                int col = rndLocation.Next(0, 8);
                int row = rndLocation.Next(0, 8);

                currentLED = Fadecandy.LED8x8Map[0, col, row];
                Fadecandy.VideoArray[currentLED] = 0x30;
                Fadecandy.VideoArray[currentLED + 1] = 0x30;
                Fadecandy.VideoArray[currentLED + 2] = 0x30;
                await SendVideoArray();
                await Task.Delay(Convert.ToInt16(txtDelayFlashTrail.Text));

                leds.Enqueue(currentLED);
            }
        }

        private void ButtonStars_Click(object sender, RoutedEventArgs e)
        {
            AddMessage("Random Stars Pressed", null);

            int starCount = Convert.ToInt16(txtCountStars.Text);
            int delay = Convert.ToInt16(txtDelayStars.Text);
            int brightness = Convert.ToInt16(txtBrightnessStars.Text);
            Stars(delay, starCount, brightness);
        }

        private async void ButtonRandomSnake_Click(object sender, RoutedEventArgs e)
        {
            AddMessage("Random Flash Trailing Pressed", null);

            Fadecandy.LoadVideoBlank();
            await SendVideoArray();

            int currentLED = 0;
            Queue<int> leds = new Queue<int>();

            int length = Convert.ToInt16(txtCountSnake.Text);
            for (int c = 0; c < length; c++)
            {
                leds.Enqueue(0);
            }

            Random rndLocation = new Random(DateTime.Now.Millisecond);
            int col = rndLocation.Next(0, 8);
            int row = rndLocation.Next(0, 8);

            for (int c = 0; c < 100; c++)
            {
                if (rndLocation.Next(0, 50) > 25)
                {
                    if (rndLocation.Next(0, 50) > 25)
                    {
                        col++;
                        if (col > 7 || col < 0) col--;
                    }
                    else
                    {
                        col--;
                        if (col > 7 || col < 0) col++;
                    }

                }
                else
                {
                    if (rndLocation.Next(0, 50) > 25)
                    {
                        row++;
                        if (row > 7 || row < 0) row--;
                    }
                    else
                    {
                        row--;
                        if (row > 7 || row < 0) row++;
                    }
                }

                int lastLED = leds.Dequeue();
                Fadecandy.VideoArray[lastLED + 1] = 0x00;
                Fadecandy.VideoArray[lastLED + 2] = 0x00;

                currentLED = Fadecandy.LED8x8Map[0, col, row];
                leds.Enqueue(currentLED);

                List<int> turnAllOn = leds.ToList();
                foreach (int x in turnAllOn)
                {
                    Fadecandy.VideoArray[x + 1] = 0x00;
                    Fadecandy.VideoArray[x + 2] = 0x30;
                }

                Fadecandy.VideoArray[currentLED + 1] = 0x30;
                Fadecandy.VideoArray[currentLED + 2] = 0x00;

                await SendVideoArray();
                await Task.Delay(Convert.ToInt16(txtDelaySnake.Text));
            }
        }

        private async void ButtonLEDOff_Click(object sender, RoutedEventArgs e)
        {
            await FadecandyLEDOffAsync();
        }

        private async void ButtonLEDOn_Click(object sender, RoutedEventArgs e)
        {
            await FadecandyLEDOnAsync();
        }



        private async void AlienSignal(int delay, int density, byte brightness)
        {
            Fadecandy.LoadVideoBlank();
            await SendVideoArray();

            int currentLED = 0;
            int color = 0;
            Random rnd = new Random(DateTime.Now.Millisecond);

            for (int t = 0; t < 50; t++)
            {
                for (int c = 0; c < 8; c++)
                {
                    for (int r = 0; r < 8; r++)
                    {
                        currentLED = Fadecandy.LED8x8Map[0, c, r];
                        Fadecandy.VideoArray[currentLED] = 0x00;
                        Fadecandy.VideoArray[currentLED + 1] = 0x00;
                        Fadecandy.VideoArray[currentLED + 2] = 0x00;

                        // Sets the color by populating just one of the RGBs
                        if ((byte)rnd.Next(0, 100) < density)
                            Fadecandy.VideoArray[currentLED + color] = brightness;
                    }
                }

                if (color > 1) color = 0;
                else color++;

                await SendVideoArray();
                await Task.Delay(delay);
            }
        }

        private async void FillDown(int delay, byte brightness)
        {
            Fadecandy.LoadVideoBlank();
            await SendVideoArray();

            int currentLED = 0;
            Random rndLocation = new Random(DateTime.Now.Millisecond);

            for (int c = 0; c < 8; c++)
            {
                for (int r = 0; r < 8; r++)
                {
                    currentLED = Fadecandy.LED8x8Map[0, c, r];
                    Fadecandy.VideoArray[currentLED] = brightness;
                    Fadecandy.VideoArray[currentLED + 1] = brightness;
                    Fadecandy.VideoArray[currentLED + 2] = brightness;
                    await SendVideoArray();
                    await Task.Delay(delay);
                }
            }
        }

        private async void Flash(int delay, int frames, int flashCount, byte brightness)
        {
            Fadecandy.LoadVideoBlank();
            await SendVideoArray();

            int currentLED = 0;

            Random rndLocation = new Random(DateTime.Now.Millisecond);

            for (int f = 0; f < frames; f++)
            {
                for (int c = 0; c < flashCount; c++)
                {
                    int col = rndLocation.Next(0, 8);
                    int row = rndLocation.Next(0, 8);

                    currentLED = Fadecandy.LED8x8Map[0, col, row];
                    Fadecandy.VideoArray[currentLED] = brightness;
                    Fadecandy.VideoArray[currentLED + 1] = brightness;
                    Fadecandy.VideoArray[currentLED + 2] = brightness;
                }
                await SendVideoArray();
                Fadecandy.LoadVideoBlank();
                await Task.Delay(delay);
            }
        }

        private async void Stars(int delay, int starCount, int brightness)
        {
            Fadecandy.LoadVideoRandomWhite((byte)(brightness - 20), (byte)(brightness + 20));
            await SendVideoArray();

            int currentLED = 0;

            Random rndLocation = new Random(DateTime.Now.Millisecond);

            for (int c = 0; c < 200; c++)
            {
                // How many pixels to change at once
                for (int l = 0; l < starCount; l++)
                {
                    int col = rndLocation.Next(0, 8);
                    int row = rndLocation.Next(0, 8);

                    currentLED = Fadecandy.LED8x8Map[0, col, row];
                    byte rndBrightness = (byte)rndLocation.Next(brightness - 20, brightness + 20);
                    Fadecandy.VideoArray[currentLED] = rndBrightness;
                    Fadecandy.VideoArray[currentLED + 1] = rndBrightness;
                    Fadecandy.VideoArray[currentLED + 2] = rndBrightness;
                }

                await SendVideoArray();
                await Task.Delay(delay);
            }
        }

        private async Task FadecandyLEDOffAsync()
        {
            await SendConfigArray(Fadecandy.ConfigurationSettings.TurnLEDOff);
        }

        private async Task FadecandyLEDOnAsync()
        {
            await SendConfigArray(Fadecandy.ConfigurationSettings.TurnLEDOn);
        }

        private static async Task SendLUTArray()
        {
            await SendPackets(Fadecandy.LookUpTable);
        }

        private static async Task SendVideoArray()
        {
            await SendPackets(Fadecandy.VideoArray);
        }

        private static async Task SendConfigArray(byte configuation)
        {
            byte[] contentBytes = new byte[2] { Fadecandy.ControlByte.Configuration, configuation };
            await SendPackets(contentBytes);
        }

        /// <summary>
        /// Creates a packet to send to Fadecandy from a 64 byte array
        /// </summary>
        /// <param name="packetContents"></param>
        /// <returns></returns>
        private static async Task SendPackets(byte[] packetBytes)
        {
            UsbDevice usbDevice = FadecandyEventHandler.Current.Device;
            UsbBulkOutPipe writePipe = usbDevice.DefaultInterface.BulkOutPipes[0];

            var stream = writePipe.OutputStream;
            DataWriter writer = new DataWriter(stream);

            writer.WriteBytes(packetBytes);

            uint bytesWritten = 0;

            try
            {
                bytesWritten = await writer.StoreAsync();
            }
            catch (Exception exception)
            {
                MainPage.Current.AddMessage(exception.Message.ToString(), null);
            }
            finally
            {
                MainPage.Current.UpdateStatus($"Data written: {bytesWritten} bytes.", NotifyType.StatusMessage);
            }
        }

        private void AddMessage(string message, string device)
        {
            if (!string.IsNullOrWhiteSpace(device))
            {
                ActionsTBox.Inlines.Insert(0, new LineBreak());
                ActionsTBox.Inlines.Insert(0, new Run { Text = device });
            }
            ActionsTBox.Inlines.Insert(0, new LineBreak());
            ActionsTBox.Inlines.Insert(0, new Run { Text = message });
        }

        public enum NotifyType
        {
            StatusMessage,
            ErrorMessage
        };

        /// <summary>
        /// Display a message to the user.
        /// This method may be called from any thread.
        /// </summary>
        /// <param name="strMessage"></param>
        /// <param name="type"></param>
        public void NotifyUser(string strMessage, string strDevice, NotifyType type)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                AddMessage(strMessage, strDevice);
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateStatus(strMessage, type));
            }
        }

        private void UpdateStatus(string strMessage, NotifyType type)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    StatusTBox.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    StatusTBox.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            StatusTBox.Text = $"{strMessage} : {timestamp}";
        }

        /// <summary>
        /// Starts all device watchers including ones that have been individually stopped.
        /// </summary>
        private void StartDeviceWatchers()
        {
            foreach (DeviceWatcher deviceWatcher in mapDeviceWatchersToDeviceSelector.Keys)
            {
                if ((deviceWatcher.Status != DeviceWatcherStatus.Started)
                    && (deviceWatcher.Status != DeviceWatcherStatus.EnumerationCompleted))
                {
                    deviceWatcher.Start();
                }
            }
        }
    }
}
