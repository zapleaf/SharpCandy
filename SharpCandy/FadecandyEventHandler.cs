using System;
using System.Threading.Tasks;

using Windows.Devices.Enumeration;
using Windows.Devices.Usb;

//using Windows.ApplicationModel;
using Windows.Foundation;

//using Windows.UI.Core;
//using Windows.UI.Xaml;

namespace SharpCandy
{
    /// <summary>
    /// The purpose of this class is to demonstrate the expected application behavior for app events 
    /// such as suspension and resume or when the device is disconnected. In addition to handling
    /// the UsbDevice, the app's state should also be saved upon app suspension (will not be demonstrated here).
    /// 
    /// This class will also demonstrate how to handle device watcher events.
    /// 
    /// For simplicity, this class will only allow at most one device to be connected at any given time. In order
    /// to make this class support multiple devices, make this class a non-singleton and create multiple instances
    /// of this class; each instance should watch one connected device.
    /// </summary>
    public class FadecandyEventHandler
    {
        /// <summary>
        /// Allows for singleton EventHandlerForDevice
        /// </summary>
        private static FadecandyEventHandler eventHandlerForDevice;

        /// <summary>
        /// Used to synchronize threads to avoid multiple instantiations of eventHandlerForDevice.
        /// </summary>
        private static readonly object singletonCreationLock = new Object();

        private TypedEventHandler<DeviceAccessInformation, DeviceAccessChangedEventArgs> deviceAccessEventHandler;

        private MainPage rootPage = MainPage.Current;

        /// <summary>
        /// Enforces the singleton pattern so that there is only one object handling app events
        /// as it relates to the UsbDevice because this sample app only supports communicating with one device at a time. 
        ///
        /// An instance of EventHandlerForDevice is globally available because the device needs to persist across scenario pages.
        ///
        /// If there is no instance of EventHandlerForDevice created before this property is called,
        /// an EventHandlerForDevice will be created.
        /// </summary>
        public static FadecandyEventHandler Current
        {
            get
            {
                if (eventHandlerForDevice == null)
                {
                    lock (singletonCreationLock)
                    {
                        if (eventHandlerForDevice == null)
                        {
                            CreateNewEventHandlerForDevice();
                        }
                    }
                }

                return eventHandlerForDevice;
            }
        }

        /// <summary>
        /// Creates a new instance of EventHandlerForDevice, enables auto reconnect, and uses it as the Current instance.
        /// </summary>
        public static void CreateNewEventHandlerForDevice()
        {
            eventHandlerForDevice = new FadecandyEventHandler();
        }
        
        public TypedEventHandler<FadecandyEventHandler, DeviceInformation> OnDeviceClose { get; set; }

        public TypedEventHandler<FadecandyEventHandler, DeviceInformation> OnDeviceConnected { get; set; }

        public UsbDevice Device { get; private set; }

        /// <summary>
        /// This DeviceInformation represents which device is connected or which device will be reconnected when
        /// the device is plugged in again (if IsEnabledAutoReconnect is true);.
        /// </summary>
        public DeviceInformation DeviceInformation { get; private set; }

        /// <summary>
        /// Returns DeviceAccessInformation for the device that is currently connected using this EventHandlerForDevice
        /// object.
        /// </summary>
        public DeviceAccessInformation DeviceAccessInformation { get; private set; }

        /// <summary>
        /// DeviceSelector AQS used to find this device
        /// </summary>
        public string DeviceSelector { get; private set; }

        /// <summary>
        /// This method opens the device using the WinRT USB API. After the device is opened, save the device
        /// so that it can be used across scenarios.
        ///
        /// It is important that the FromIdAsync call is made on the UI thread because the consent prompt can only be displayed
        /// on the UI thread.
        /// 
        /// This method is used to reopen the device after the device reconnects to the computer and when the app resumes.
        /// </summary>
        /// <param name="deviceInfo">Device information of the device to be opened</param>
        /// <param name="deviceSelector">The AQS used to find this device</param>
        /// <returns>True if the device was successfully opened, false if the device could not be opened for well known reasons.
        /// An exception may be thrown if the device could not be opened for extraordinary reasons.</returns>
        public async Task<bool> OpenDeviceAsync(DeviceInformation deviceInfo, String deviceSelector)
        {
            Device = await UsbDevice.FromIdAsync(deviceInfo.Id);

            Boolean successfullyOpenedDevice = false;
            MainPage.NotifyType notificationStatus;
            String notificationMessage = null;

            // Device could have been blocked by user or the device has already been opened by another app.
            if (Device != null)
            {
                successfullyOpenedDevice = true;

                DeviceInformation = deviceInfo;
                this.DeviceSelector = deviceSelector;

                notificationStatus = MainPage.NotifyType.StatusMessage;
                notificationMessage = "Device Opened";

                // Notify registered callback handle that the device has been opened
                if (OnDeviceConnected != null)
                {
                    OnDeviceConnected(this, DeviceInformation);
                }
            }
            else
            {
                successfullyOpenedDevice = false;

                notificationStatus = MainPage.NotifyType.ErrorMessage;

                DeviceAccessInformation dai = DeviceAccessInformation.CreateFromId(deviceInfo.Id);

                var deviceAccessStatus = dai.CurrentStatus;

                if (deviceAccessStatus == DeviceAccessStatus.DeniedByUser)
                {
                    notificationMessage = "Access to the device was blocked by the user.";
                }
                else if (deviceAccessStatus == DeviceAccessStatus.DeniedBySystem)
                {
                    // This status is most likely caused by app permissions (did not declare the device in the app's package.appxmanifest)
                    // This status does not cover the case where the device is already opened by another app.
                    notificationMessage = "Access to the device was blocked by the system.";
                }
                else
                {
                    // The only time I made it to this error I forgot to add Fadecandy to package.appxmanifest
                    // Most likely the device is opened by another app, but cannot be sure
                    notificationMessage = "Unknown error, possibly opened by another app.";
                }
            }

            MainPage.Current.NotifyUser(notificationMessage, deviceInfo.Id, notificationStatus);

            return successfullyOpenedDevice;
        }
    }
}