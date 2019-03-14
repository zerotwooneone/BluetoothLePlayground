using Prism.Commands;
using Prism.Windows.Mvvm;
using Prism.Windows.Navigation;
using SDKTemplate;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace BtServer.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private GattServiceProvider serviceProvider;
        private GattLocalCharacteristic op1Characteristic;
        private int operand1Received;
        private GattLocalCharacteristic op2Characteristic;
        private int operand2Received;
        private GattLocalCharacteristic operatorCharacteristic;
        private CalculatorOperators operatorReceived = 0;


        private GattLocalCharacteristic resultCharacteristic;

        private int resultVal;


        private bool peripheralSupported;
        private Visibility _serverPanelVisible;
        private Visibility _peripheralWarningPanelVisible;
        private string _publishContent;
        private string _operationText;
        private string _operand1Text;
        private string _operand2Text;
        private string _resultText;

        public Visibility ServerPanelVisible
        {
            get => _serverPanelVisible;
            set => SetProperty(ref _serverPanelVisible, value);
        }

        public Visibility PeripheralWarningPanelVisible
        {
            get => _peripheralWarningPanelVisible;
            set => SetProperty(ref _peripheralWarningPanelVisible, value);
        }

        public MainViewModel()
        {
            PublishCommand = new DelegateCommand(PublishButton_ClickAsync);
            PublishContent = InitialPublishContent;
            ConnectButtonCommand = new DelegateCommand(ConnectButton_Click);
            ServiceListSelectionChangedCommand = new DelegateCommand<object>(ServiceList_SelectionChanged);
            CharacteristicListSelectionChangedCommand = new DelegateCommand<object>(CharacteristicList_SelectionChanged);
            CharacteristicReadButtonClickCommand = new DelegateCommand<object>(CharacteristicReadButton_Click);
            CharacteristicWriteButtonClickCommand = new DelegateCommand<object>(CharacteristicWriteButton_Click);
            CharacteristicWriteButtonIntClickCommand = new DelegateCommand<object>(CharacteristicWriteButtonInt_Click);
            ValueChangedSubscribeToggleClickCommand = new DelegateCommand<object>(ValueChangedSubscribeToggle_Click);
        }

        public override async void OnNavigatedTo(
            NavigatedToEventArgs e,
            Dictionary<string, object> viewModelState)
        {
            peripheralSupported = await CheckPeripheralRoleSupportAsync();

            if (peripheralSupported)
            {
                ServerPanelVisible = Visibility.Visible;
                PeripheralWarningPanelVisible = Visibility.Collapsed;
            }
            else
            {
                ServerPanelVisible = Visibility.Collapsed;
                PeripheralWarningPanelVisible = Visibility.Visible;
            }

            if (string.IsNullOrEmpty(SelectedBleDeviceId))
            {
                ConnectButtonEnabled = false;
            }
        }


        public override async void OnNavigatingFrom(
            NavigatingFromEventArgs e,
            Dictionary<string, object> viewModelState,
            bool suspending)

        {
            if (serviceProvider != null)

            {
                if (serviceProvider.AdvertisementStatus != GattServiceProviderAdvertisementStatus.Stopped)
                    serviceProvider.StopAdvertising();

                serviceProvider = null;
            }

            bool success = await ClearBluetoothLEDeviceAsync();
            if (!success)
            {
                //rootPage.NotifyUser("Error: Unable to reset app state", NotifyType.ErrorMessage);
            }
        }

        public const string InitialPublishContent = "Start Service";


        public ICommand PublishCommand { get; }

        public string PublishContent
        {
            get => _publishContent;
            protected set => SetProperty(ref _publishContent, value);
        }

        public async void PublishButton_ClickAsync()

        {
            // Server not initialized yet - initialize it and start publishing

            if (serviceProvider == null)

            {
                bool serviceStarted = await ServiceProviderInitAsync();

                if (serviceStarted)

                {
                    //rootPage.NotifyUser("Service successfully started", NotifyType.StatusMessage);

                    PublishContent = "Stop Service";
                }

                else

                {
                    //rootPage.NotifyUser("Service not started", NotifyType.ErrorMessage);
                }
            }

            else

            {
                // BT_Code: Stops advertising support for custom GATT Service 

                serviceProvider.StopAdvertising();

                serviceProvider = null;

                PublishContent = InitialPublishContent;
            }
        }

        public string OperationText
        {
            get => _operationText;
            set => SetProperty(ref _operationText, value);
        }

        public string Operand1Text
        {
            get => _operand1Text;
            set => SetProperty(ref _operand1Text, value);
        }

        public string Operand2Text
        {
            get => _operand2Text;
            set => SetProperty(ref _operand2Text, value);
        }

        public string ResultText
        {
            get => _resultText;
            set => SetProperty(ref _resultText, value);
        }

        private async void UpdateUX()

        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>

            {
                switch (operatorReceived)

                {
                    case CalculatorOperators.Add:

                        OperationText = "+";

                        break;

                    case CalculatorOperators.Subtract:

                        OperationText = "-";

                        break;

                    case CalculatorOperators.Multiply:

                        OperationText = "*";

                        break;

                    case CalculatorOperators.Divide:

                        OperationText = "/";

                        break;

                    default:

                        OperationText = "INV";

                        break;
                }

                Operand1Text = operand1Received.ToString();

                Operand2Text = operand2Received.ToString();

                resultVal = ComputeResult();

                ResultText = resultVal.ToString();
            });
        }

        private async Task<bool> CheckPeripheralRoleSupportAsync()

        {
            // BT_Code: New for Creator's Update - Bluetooth adapter has properties of the local BT radio.

            BluetoothAdapter localAdapter = await BluetoothAdapter.GetDefaultAsync();


            if (localAdapter != null)
                return localAdapter.IsPeripheralRoleSupported;

            return false;
        }


        /// <summary>
        ///     Uses the relevant Service/Characteristic UUIDs to initialize, hook up event handlers and start a service on the
        ///     local system.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> ServiceProviderInitAsync()

        {
            // BT_Code: Initialize and starting a custom GATT Service using GattServiceProvider.

            GattServiceProviderResult serviceResult = await GattServiceProvider.CreateAsync(Constants.CalcServiceUuid);

            if (serviceResult.Error == BluetoothError.Success)

            {
                serviceProvider = serviceResult.ServiceProvider;
            }

            else

            {
                //rootPage.NotifyUser($"Could not create service provider: {serviceResult.Error}",NotifyType.ErrorMessage);

                return false;
            }


            GattLocalCharacteristicResult result = await serviceProvider.Service.CreateCharacteristicAsync(Constants.Op1CharacteristicUuid,
                Constants.gattOperandParameters);

            if (result.Error == BluetoothError.Success)

            {
                op1Characteristic = result.Characteristic;
            }

            else

            {
                //rootPage.NotifyUser($"Could not create operand1 characteristic: {result.Error}",NotifyType.ErrorMessage);

                return false;
            }

            op1Characteristic.WriteRequested += Op1Characteristic_WriteRequestedAsync;


            result = await serviceProvider.Service.CreateCharacteristicAsync(Constants.Op2CharacteristicUuid,
                Constants.gattOperandParameters);

            if (result.Error == BluetoothError.Success)

            {
                op2Characteristic = result.Characteristic;
            }

            else

            {
                //rootPage.NotifyUser($"Could not create operand2 characteristic: {result.Error}",NotifyType.ErrorMessage);

                return false;
            }


            op2Characteristic.WriteRequested += Op2Characteristic_WriteRequestedAsync;


            result = await serviceProvider.Service.CreateCharacteristicAsync(Constants.OperatorCharacteristicUuid,
                Constants.gattOperatorParameters);

            if (result.Error == BluetoothError.Success)

            {
                operatorCharacteristic = result.Characteristic;
            }

            else

            {
                //rootPage.NotifyUser($"Could not create operator characteristic: {result.Error}",NotifyType.ErrorMessage);

                return false;
            }


            operatorCharacteristic.WriteRequested += OperatorCharacteristic_WriteRequestedAsync;


            // Add presentation format - 32-bit unsigned integer, with exponent 0, the unit is unitless, with no company description

            GattPresentationFormat intFormat = GattPresentationFormat.FromParts(
                GattPresentationFormatTypes.UInt32,
                PresentationFormats.Exponent,
                Convert.ToUInt16(PresentationFormats.Units.Unitless),
                Convert.ToByte(PresentationFormats.NamespaceId.BluetoothSigAssignedNumber),
                PresentationFormats.Description);


            Constants.gattResultParameters.PresentationFormats.Add(intFormat);


            result = await serviceProvider.Service.CreateCharacteristicAsync(Constants.ResultCharacteristicUuid,
                Constants.gattResultParameters);

            if (result.Error == BluetoothError.Success)

            {
                resultCharacteristic = result.Characteristic;
            }

            else

            {
                //rootPage.NotifyUser($"Could not create result characteristic: {result.Error}", NotifyType.ErrorMessage);

                return false;
            }

            resultCharacteristic.ReadRequested += ResultCharacteristic_ReadRequestedAsync;

            resultCharacteristic.SubscribedClientsChanged += ResultCharacteristic_SubscribedClientsChanged;


            // BT_Code: Indicate if your sever advertises as connectable and discoverable.

            GattServiceProviderAdvertisingParameters advParameters = new GattServiceProviderAdvertisingParameters

            {
                // IsConnectable determines whether a call to publish will attempt to start advertising and 

                // put the service UUID in the ADV packet (best effort)

                IsConnectable = peripheralSupported,


                // IsDiscoverable determines whether a remote device can query the local device for support 

                // of this service

                IsDiscoverable = true
            };

            serviceProvider.AdvertisementStatusChanged += ServiceProvider_AdvertisementStatusChanged;

            serviceProvider.StartAdvertising(advParameters);

            return true;
        }


        private void ResultCharacteristic_SubscribedClientsChanged(GattLocalCharacteristic sender, object args)

        {
            //rootPage.NotifyUser($"New device subscribed. New subscribed count: {sender.SubscribedClients.Count}",NotifyType.StatusMessage);
        }


        private void ServiceProvider_AdvertisementStatusChanged(GattServiceProvider sender,
            GattServiceProviderAdvertisementStatusChangedEventArgs args)

        {
            // Created - The default state of the advertisement, before the service is published for the first time.

            // Stopped - Indicates that the application has canceled the service publication and its advertisement.

            // Started - Indicates that the system was successfully able to issue the advertisement request.

            // Aborted - Indicates that the system was unable to submit the advertisement request, or it was canceled due to resource contention.


            //rootPage.NotifyUser($"New Advertisement Status: {sender.AdvertisementStatus}", NotifyType.StatusMessage);
        }

        private async void ResultCharacteristic_ReadRequestedAsync(GattLocalCharacteristic sender,
            GattReadRequestedEventArgs args)

        {
            // BT_Code: Process a read request. 

            using (args.GetDeferral())

            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>

               {
                   // Get the request information.  This requires device access before an app can access the device's request. 

                   GattReadRequest request = await args.GetRequestAsync();

                   if (request == null)

                   {
                       // No access allowed to the device.  Application should indicate this to the user.

                       //rootPage.NotifyUser("Access to device not allowed", NotifyType.ErrorMessage);

                       return;
                   }


                   DataWriter writer = new DataWriter
                   {
                       ByteOrder = ByteOrder.LittleEndian
                   };

                   writer.WriteInt32(resultVal);


                   // Can get details about the request such as the size and offset, as well as monitor the state to see if it has been completed/cancelled externally.

                   // request.Offset

                   // request.Length

                   // request.State

                   // request.StateChanged += <Handler>


                   // Gatt code to handle the response

                   request.RespondWithValue(writer.DetachBuffer());
               });
            }
        }


        private int ComputeResult()

        {
            int computedValue = 0;

            switch (operatorReceived)

            {
                case CalculatorOperators.Add:

                    computedValue = operand1Received + operand2Received;

                    break;

                case CalculatorOperators.Subtract:

                    computedValue = operand1Received - operand2Received;

                    break;

                case CalculatorOperators.Multiply:

                    computedValue = operand1Received * operand2Received;

                    break;

                case CalculatorOperators.Divide:

                    if (operand2Received == 0 || operand1Received == -0x80000000 && operand2Received == -1)
                    {
                        //rootPage.NotifyUser("Division overflow", NotifyType.ErrorMessage);
                    }

                    else
                    {
                        computedValue = operand1Received / operand2Received;
                    }

                    break;

                default:

                    //rootPage.NotifyUser("Invalid Operator", NotifyType.ErrorMessage);

                    break;
            }

            NotifyClientDevices(computedValue);

            return computedValue;
        }


        private async void NotifyClientDevices(int computedValue)

        {
            DataWriter writer = new DataWriter
            {
                ByteOrder = ByteOrder.LittleEndian
            };

            writer.WriteInt32(computedValue);


            // BT_Code: Returns a collection of all clients that the notification was attempted and the result.

            IReadOnlyList<GattClientNotificationResult> results = await resultCharacteristic.NotifyValueAsync(writer.DetachBuffer());


            //rootPage.NotifyUser($"Sent value {computedValue} to clients.", NotifyType.StatusMessage);

            foreach (GattClientNotificationResult result in results)

            {
                // An application can iterate through each registered client that was notified and retrieve the results:

                //

                // result.SubscribedClient: The details on the remote client.

                // result.Status: The GattCommunicationStatus

                // result.ProtocolError: iff Status == GattCommunicationStatus.ProtocolError
            }
        }


        private async void Op1Characteristic_WriteRequestedAsync(GattLocalCharacteristic sender,
            GattWriteRequestedEventArgs args)

        {
            // BT_Code: Processing a write request.

            using (args.GetDeferral())

            {
                // Get the request information.  This requires device access before an app can access the device's request.

                GattWriteRequest request = await args.GetRequestAsync();

                if (request == null) return;

                ProcessWriteCharacteristic(request, CalculatorCharacteristics.Operand1);
            }
        }


        private async void Op2Characteristic_WriteRequestedAsync(GattLocalCharacteristic sender,
            GattWriteRequestedEventArgs args)

        {
            using (args.GetDeferral())

            {
                // Get the request information.  This requires device access before an app can access the device's request.

                GattWriteRequest request = await args.GetRequestAsync();

                if (request == null) return;

                ProcessWriteCharacteristic(request, CalculatorCharacteristics.Operand2);
            }
        }


        private async void OperatorCharacteristic_WriteRequestedAsync(GattLocalCharacteristic sender,
            GattWriteRequestedEventArgs args)

        {
            using (args.GetDeferral())

            {
                // Get the request information.  This requires device access before an app can access the device's request.

                GattWriteRequest request = await args.GetRequestAsync();

                if (request == null) return;

                ProcessWriteCharacteristic(request, CalculatorCharacteristics.Operator);
            }
        }


        /// <summary>
        ///     BT_Code: Processing a write request.Takes in a GATT Write request and updates UX based on opcode.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="opCode">Operand (1 or 2) and Operator (3)</param>
        private void ProcessWriteCharacteristic(GattWriteRequest request, CalculatorCharacteristics opCode)

        {
            if (request.Value.Length != 4)

            {
                // Input is the wrong length. Respond with a protocol error if requested.

                if (request.Option == GattWriteOption.WriteWithResponse)
                    request.RespondWithProtocolError(GattProtocolError.InvalidAttributeValueLength);

                return;
            }


            DataReader reader = DataReader.FromBuffer(request.Value);

            reader.ByteOrder = ByteOrder.LittleEndian;

            int val = reader.ReadInt32();


            switch (opCode)

            {
                case CalculatorCharacteristics.Operand1:

                    operand1Received = val;

                    break;

                case CalculatorCharacteristics.Operand2:

                    operand2Received = val;

                    break;

                case CalculatorCharacteristics.Operator:

                    if (!Enum.IsDefined(typeof(CalculatorOperators), val))

                    {
                        if (request.Option == GattWriteOption.WriteWithResponse)
                            request.RespondWithProtocolError(GattProtocolError.InvalidPdu);

                        return;
                    }

                    operatorReceived = (CalculatorOperators)val;

                    break;
            }

            // Complete the request if needed

            if (request.Option == GattWriteOption.WriteWithResponse) request.Respond();


            UpdateUX();
        }

        public ObservableCollection<BluetoothLEAttributeDisplay> ServiceCollection { get; } =
            new ObservableCollection<BluetoothLEAttributeDisplay>();

        public ObservableCollection<BluetoothLEAttributeDisplay> CharacteristicCollection { get; }
            = new ObservableCollection<BluetoothLEAttributeDisplay>();

        private BluetoothLEDevice bluetoothLeDevice = null;
        private GattCharacteristic selectedCharacteristic;

        // Only one registered characteristic at a time.
        private GattCharacteristic registeredCharacteristic;
        private GattPresentationFormat presentationFormat;

        #region Error Codes
        readonly int E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED = unchecked((int)0x80650003);
        readonly int E_BLUETOOTH_ATT_INVALID_PDU = unchecked((int)0x80650004);
        readonly int E_ACCESSDENIED = unchecked((int)0x80070005);
        readonly int E_DEVICE_NOT_AVAILABLE = unchecked((int)0x800710df); // HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE)
        #endregion

        public bool ConnectButtonEnabled
        {
            get => _connectButtonEnabled;
            set => SetProperty(ref _connectButtonEnabled, value);
        }

        public Visibility ConnectButtonVisibility
        {
            get => _connectButtonVisibility;
            set => SetProperty(ref _connectButtonVisibility, value);
        }

        public Visibility ServiceListVisibility
        {
            get => _serviceListVisibility;
            set => SetProperty(ref _serviceListVisibility, value);
        }

        #region Enumerating Services
        private async Task<bool> ClearBluetoothLEDeviceAsync()
        {
            if (subscribedForNotifications)
            {
                // Need to clear the CCCD from the remote device so we stop receiving notifications
                GattCommunicationStatus result = await registeredCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
                if (result != GattCommunicationStatus.Success)
                {
                    return false;
                }
                else
                {
                    selectedCharacteristic.ValueChanged -= Characteristic_ValueChanged;
                    subscribedForNotifications = false;
                }
            }
            bluetoothLeDevice?.Dispose();
            bluetoothLeDevice = null;
            return true;
        }

        public ICommand ConnectButtonCommand { get; }

        private async void ConnectButton_Click()
        {
            ConnectButtonEnabled = false;

            if (!await ClearBluetoothLEDeviceAsync())
            {
                //rootPage.NotifyUser("Error: Unable to reset state, try again.", NotifyType.ErrorMessage);
                ConnectButtonEnabled = false;
                return;
            }

            try
            {
                // BT_Code: BluetoothLEDevice.FromIdAsync must be called from a UI thread because it may prompt for consent.
                bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(SelectedBleDeviceId);

                if (bluetoothLeDevice == null)
                {
                    //rootPage.NotifyUser("Failed to connect to device.", NotifyType.ErrorMessage);
                }
            }
            catch (Exception ex) when (ex.HResult == E_DEVICE_NOT_AVAILABLE)
            {
                //rootPage.NotifyUser("Bluetooth radio is not on.", NotifyType.ErrorMessage);
            }

            if (bluetoothLeDevice != null)
            {
                // Note: BluetoothLEDevice.GattServices property will return an empty list for unpaired devices. For all uses we recommend using the GetGattServicesAsync method.
                // BT_Code: GetGattServicesAsync returns a list of all the supported services of the device (even if it's not paired to the system).
                // If the services supported by the device are expected to change during BT usage, subscribe to the GattServicesChanged event.
                GattDeviceServicesResult result = await bluetoothLeDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);

                if (result.Status == GattCommunicationStatus.Success)
                {
                    IReadOnlyList<GattDeviceService> services = result.Services;
                    //rootPage.NotifyUser(String.Format("Found {0} services", services.Count), NotifyType.StatusMessage);
                    foreach (GattDeviceService service in services)
                    {
                        ServiceCollection.Add(new BluetoothLEAttributeDisplay(service));
                    }
                    ConnectButtonVisibility = Visibility.Collapsed;
                    ServiceListVisibility = Visibility.Visible;
                }
                else
                {
                    //rootPage.NotifyUser("Device unreachable", NotifyType.ErrorMessage);
                }
            }
            ConnectButtonEnabled = true;
        }
        #endregion

        #region Enumerating Characteristics

        public BluetoothLEAttributeDisplay ServiceListSelectedItem
        {
            get => _serviceListSelectedItem;
            set => SetProperty(ref _serviceListSelectedItem, value);
        }

        public Visibility CharacteristicListVisibility
        {
            get => _characteristicListVisibility;
            set => SetProperty(ref _characteristicListVisibility, value);
        }

        public ICommand ServiceListSelectionChangedCommand { get; }

        private async void ServiceList_SelectionChanged(object changeItem)
        {
            BluetoothLEAttributeDisplay attributeInfoDisp = ServiceListSelectedItem;

            CharacteristicCollection.Clear();
            RemoveValueChangedHandler();

            IReadOnlyList<GattCharacteristic> characteristics = null;
            try
            {
                // Ensure we have access to the device.
                DeviceAccessStatus accessStatus = await attributeInfoDisp.service.RequestAccessAsync();
                if (accessStatus == DeviceAccessStatus.Allowed)
                {
                    // BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characterstics only 
                    // and the new Async functions to get the characteristics of unpaired devices as well. 
                    GattCharacteristicsResult result = await attributeInfoDisp.service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        characteristics = result.Characteristics;
                    }
                    else
                    {
                        //rootPage.NotifyUser("Error accessing service.", NotifyType.ErrorMessage);

                        // On error, act as if there are no characteristics.
                        characteristics = new List<GattCharacteristic>();
                    }
                }
                else
                {
                    // Not granted access
                    //rootPage.NotifyUser("Error accessing service.", NotifyType.ErrorMessage);

                    // On error, act as if there are no characteristics.
                    characteristics = new List<GattCharacteristic>();

                }
            }
            catch (Exception ex)
            {
                //rootPage.NotifyUser("Restricted service. Can't read characteristics: " + ex.Message,NotifyType.ErrorMessage);
                // On error, act as if there are no characteristics.
                characteristics = new List<GattCharacteristic>();
            }

            foreach (GattCharacteristic c in characteristics)
            {
                CharacteristicCollection.Add(new BluetoothLEAttributeDisplay(c));
            }
            CharacteristicListVisibility = Visibility.Visible;
        }
        #endregion

        public string ValueChangedSubscribeToggleContent
        {
            get => _valueChangedSubscribeToggleContent;
            set => SetProperty(ref _valueChangedSubscribeToggleContent, value);
        }

        private void AddValueChangedHandler()
        {
            ValueChangedSubscribeToggleContent = "Unsubscribe from value changes";
            if (!subscribedForNotifications)
            {
                registeredCharacteristic = selectedCharacteristic;
                registeredCharacteristic.ValueChanged += Characteristic_ValueChanged;
                subscribedForNotifications = true;
            }
        }

        private void RemoveValueChangedHandler()
        {
            ValueChangedSubscribeToggleContent = "Subscribe to value changes";
            if (subscribedForNotifications)
            {
                registeredCharacteristic.ValueChanged -= Characteristic_ValueChanged;
                registeredCharacteristic = null;
                subscribedForNotifications = false;
            }
        }

        public BluetoothLEAttributeDisplay CharacteristicListSelectedItem
        {
            get => _characteristicListSelectedItem;
            set => SetProperty(ref _characteristicListSelectedItem, value);
        }

        public ICommand CharacteristicListSelectionChangedCommand { get; }

        private async void CharacteristicList_SelectionChanged(object selectedItem)
        {
            selectedCharacteristic = null;

            BluetoothLEAttributeDisplay attributeInfoDisp = CharacteristicListSelectedItem;
            if (attributeInfoDisp == null)
            {
                EnableCharacteristicPanels(GattCharacteristicProperties.None);
                return;
            }

            selectedCharacteristic = attributeInfoDisp.characteristic;
            if (selectedCharacteristic == null)
            {
                //rootPage.NotifyUser("No characteristic selected", NotifyType.ErrorMessage);
                return;
            }

            // Get all the child descriptors of a characteristics. Use the cache mode to specify uncached descriptors only 
            // and the new Async functions to get the descriptors of unpaired devices as well. 
            GattDescriptorsResult result = await selectedCharacteristic.GetDescriptorsAsync(BluetoothCacheMode.Uncached);
            if (result.Status != GattCommunicationStatus.Success)
            {
                //rootPage.NotifyUser("Descriptor read failure: " + result.Status.ToString(), NotifyType.ErrorMessage);
            }

            // BT_Code: There's no need to access presentation format unless there's at least one. 
            presentationFormat = null;
            if (selectedCharacteristic.PresentationFormats.Count > 0)
            {

                if (selectedCharacteristic.PresentationFormats.Count.Equals(1))
                {
                    // Get the presentation format since there's only one way of presenting it
                    presentationFormat = selectedCharacteristic.PresentationFormats[0];
                }
                else
                {
                    // It's difficult to figure out how to split up a characteristic and encode its different parts properly.
                    // In this case, we'll just encode the whole thing to a string to make it easy to print out.
                }
            }

            // Enable/disable operations based on the GattCharacteristicProperties.
            EnableCharacteristicPanels(selectedCharacteristic.CharacteristicProperties);
        }

        public Visibility CharacteristicReadButtonVisibility
        {
            get => _characteristicReadButtonVisibility;
            set => SetProperty(ref _characteristicReadButtonVisibility, value);
        }

        public Visibility CharacteristicWritePanelVisibility
        {
            get => _characteristicWritePanelVisibility;
            set => SetProperty(ref _characteristicWritePanelVisibility, value);
        }

        public string CharacteristicWriteValueText
        {
            get => _characteristicWriteValueText;
            set => SetProperty(ref _characteristicWriteValueText, value);
        }

        public string SelectedBleDeviceId
        {
            get => _selectedBleDeviceId;
            set => SetProperty(ref _selectedBleDeviceId, value);
        }

        public string SelectedBleDeviceName
        {
            get => _selectedBleDeviceName;
            set => SetProperty(ref _selectedBleDeviceName, value);
        }

        public Visibility ValueChangedSubscribeToggleVisibility
        {
            get => _valueChangedSubscribeToggleVisibility;
            set => SetProperty(ref _valueChangedSubscribeToggleVisibility, value);
        }

        private void EnableCharacteristicPanels(GattCharacteristicProperties properties)
        {
            // BT_Code: Hide the controls which do not apply to this characteristic.
            CharacteristicReadButtonVisibility = ConvertToVisibility(properties.HasFlag(GattCharacteristicProperties.Read));

            CharacteristicWritePanelVisibility = ConvertToVisibility(
                properties.HasFlag(GattCharacteristicProperties.Write) ||
                properties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse));
            CharacteristicWriteValueText = "";

            ValueChangedSubscribeToggleVisibility = ConvertToVisibility(properties.HasFlag(GattCharacteristicProperties.Indicate) ||
                                                       properties.HasFlag(GattCharacteristicProperties.Notify));

        }

        private Visibility ConvertToVisibility(bool visible)
        {
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public ICommand CharacteristicReadButtonClickCommand { get; }

        private async void CharacteristicReadButton_Click(object selectedItem)
        {
            // BT_Code: Read the actual value from the device by using Uncached.
            GattReadResult result = await selectedCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (result.Status == GattCommunicationStatus.Success)
            {
                string formattedResult = FormatValueByPresentation(result.Value, presentationFormat);
                //rootPage.NotifyUser($"Read result: {formattedResult}", NotifyType.StatusMessage);
            }
            else
            {
                //rootPage.NotifyUser($"Read failed: {result.Status}", NotifyType.ErrorMessage);
            }
        }

        public ICommand CharacteristicWriteButtonClickCommand { get; }

        private async void CharacteristicWriteButton_Click(object selectedItem)
        {
            if (!String.IsNullOrEmpty(CharacteristicWriteValueText))
            {
                IBuffer writeBuffer = CryptographicBuffer.ConvertStringToBinary(CharacteristicWriteValueText,
                    BinaryStringEncoding.Utf8);

                bool writeSuccessful = await WriteBufferToSelectedCharacteristicAsync(writeBuffer);
            }
            else
            {
                //rootPage.NotifyUser("No data to write to device", NotifyType.ErrorMessage);
            }
        }

        public ICommand CharacteristicWriteButtonIntClickCommand { get; }

        private async void CharacteristicWriteButtonInt_Click(object selectedItem)
        {
            if (!String.IsNullOrEmpty(CharacteristicWriteValueText))
            {
                bool isValidValue = Int32.TryParse(CharacteristicWriteValueText, out int readValue);
                if (isValidValue)
                {
                    DataWriter writer = new DataWriter
                    {
                        ByteOrder = ByteOrder.LittleEndian
                    };
                    writer.WriteInt32(readValue);

                    bool writeSuccessful = await WriteBufferToSelectedCharacteristicAsync(writer.DetachBuffer());
                }
                else
                {
                    //rootPage.NotifyUser("Data to write has to be an int32", NotifyType.ErrorMessage);
                }
            }
            else
            {
                //rootPage.NotifyUser("No data to write to device", NotifyType.ErrorMessage);
            }
        }

        private async Task<bool> WriteBufferToSelectedCharacteristicAsync(IBuffer buffer)
        {
            try
            {
                // BT_Code: Writes the value from the buffer to the characteristic.
                GattWriteResult result = await selectedCharacteristic.WriteValueWithResultAsync(buffer);

                if (result.Status == GattCommunicationStatus.Success)
                {
                    //rootPage.NotifyUser("Successfully wrote value to device", NotifyType.StatusMessage);
                    return true;
                }
                else
                {
                    //rootPage.NotifyUser($"Write failed: {result.Status}", NotifyType.ErrorMessage);
                    return false;
                }
            }
            catch (Exception ex) when (ex.HResult == E_BLUETOOTH_ATT_INVALID_PDU)
            {
                //rootPage.NotifyUser(ex.Message, NotifyType.ErrorMessage);
                return false;
            }
            catch (Exception ex) when (ex.HResult == E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED || ex.HResult == E_ACCESSDENIED)
            {
                // This usually happens when a device reports that it support writing, but it actually doesn't.
                //rootPage.NotifyUser(ex.Message, NotifyType.ErrorMessage);
                return false;
            }
        }

        private bool subscribedForNotifications = false;
        private bool _connectButtonEnabled = true;
        private Visibility _connectButtonVisibility;
        private Visibility _serviceListVisibility = Visibility.Collapsed;
        private string _valueChangedSubscribeToggleContent = "Subscribe to value changes";
        private Visibility _characteristicReadButtonVisibility = Visibility.Collapsed;
        private Visibility _characteristicWritePanelVisibility;
        private string _characteristicWriteValueText;
        private Visibility _valueChangedSubscribeToggleVisibility = Visibility.Collapsed;
        private string _characteristicLatestValueText;
        private BluetoothLEAttributeDisplay _serviceListSelectedItem;
        private Visibility _characteristicListVisibility = Visibility.Collapsed;
        private BluetoothLEAttributeDisplay _characteristicListSelectedItem;
        private string _selectedBleDeviceId;
        private string _selectedBleDeviceName;

        public ICommand ValueChangedSubscribeToggleClickCommand { get; }

        private async void ValueChangedSubscribeToggle_Click(object selectedItem)
        {
            if (!subscribedForNotifications)
            {
                // initialize status
                GattCommunicationStatus status = GattCommunicationStatus.Unreachable;
                GattClientCharacteristicConfigurationDescriptorValue cccdValue = GattClientCharacteristicConfigurationDescriptorValue.None;
                if (selectedCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
                {
                    cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Indicate;
                }

                else if (selectedCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
                {
                    cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Notify;
                }

                try
                {
                    // BT_Code: Must write the CCCD in order for server to send indications.
                    // We receive them in the ValueChanged event handler.
                    status = await selectedCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);

                    if (status == GattCommunicationStatus.Success)
                    {
                        AddValueChangedHandler();
                        //rootPage.NotifyUser("Successfully subscribed for value changes", NotifyType.StatusMessage);
                    }
                    else
                    {
                        //rootPage.NotifyUser($"Error registering for value changes: {status}", NotifyType.ErrorMessage);
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support indicate, but it actually doesn't.
                    //rootPage.NotifyUser(ex.Message, NotifyType.ErrorMessage);
                }
            }
            else
            {
                try
                {
                    // BT_Code: Must write the CCCD in order for server to send notifications.
                    // We receive them in the ValueChanged event handler.
                    // Note that this sample configures either Indicate or Notify, but not both.
                    GattCommunicationStatus result = await
                            selectedCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                                GattClientCharacteristicConfigurationDescriptorValue.None);
                    if (result == GattCommunicationStatus.Success)
                    {
                        subscribedForNotifications = false;
                        RemoveValueChangedHandler();
                        //rootPage.NotifyUser("Successfully un-registered for notifications", NotifyType.StatusMessage);
                    }
                    else
                    {
                        //rootPage.NotifyUser($"Error un-registering for notifications: {result}", NotifyType.ErrorMessage);
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support notify, but it actually doesn't.
                    //rootPage.NotifyUser(ex.Message, NotifyType.ErrorMessage);
                }
            }
        }

        public string CharacteristicLatestValueText
        {
            get => _characteristicLatestValueText;
            set => SetProperty(ref _characteristicLatestValueText, value);
        }

        private async void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // BT_Code: An Indicate or Notify reported that the value has changed.
            // Display the new value with a timestamp.
            string newValue = FormatValueByPresentation(args.CharacteristicValue, presentationFormat);
            string message = $"Value at {DateTime.Now:hh:mm:ss.FFF}: {newValue}";
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () => CharacteristicLatestValueText = message);
        }

        private string FormatValueByPresentation(IBuffer buffer, GattPresentationFormat format)
        {
            // BT_Code: For the purpose of this sample, this function converts only UInt32 and
            // UTF-8 buffers to readable text. It can be extended to support other formats if your app needs them.
            CryptographicBuffer.CopyToByteArray(buffer, out byte[] data);
            if (format != null)
            {
                if (format.FormatType == GattPresentationFormatTypes.UInt32 && data.Length >= 4)
                {
                    return BitConverter.ToInt32(data, 0).ToString();
                }
                else if (format.FormatType == GattPresentationFormatTypes.Utf8)
                {
                    try
                    {
                        return Encoding.UTF8.GetString(data);
                    }
                    catch (ArgumentException)
                    {
                        return "(error: Invalid UTF-8 string)";
                    }
                }
                else
                {
                    // Add support for other format types as needed.
                    return "Unsupported format: " + CryptographicBuffer.EncodeToHexString(buffer);
                }
            }
            else if (data != null)
            {
                // We don't know what format to use. Let's try some well-known profiles, or default back to UTF-8.
                if (selectedCharacteristic.Uuid.Equals(GattCharacteristicUuids.HeartRateMeasurement))
                {
                    try
                    {
                        return "Heart Rate: " + ParseHeartRateValue(data).ToString();
                    }
                    catch (ArgumentException)
                    {
                        return "Heart Rate: (unable to parse)";
                    }
                }
                else if (selectedCharacteristic.Uuid.Equals(GattCharacteristicUuids.BatteryLevel))
                {
                    try
                    {
                        // battery level is encoded as a percentage value in the first byte according to
                        // https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.characteristic.battery_level.xml
                        return "Battery Level: " + data[0].ToString() + "%";
                    }
                    catch (ArgumentException)
                    {
                        return "Battery Level: (unable to parse)";
                    }
                }
                // This is our custom calc service Result UUID. Format it like an Int
                else if (selectedCharacteristic.Uuid.Equals(Constants.ResultCharacteristicUuid))
                {
                    return BitConverter.ToInt32(data, 0).ToString();
                }
                // No guarantees on if a characteristic is registered for notifications.
                else if (registeredCharacteristic != null)
                {
                    // This is our custom calc service Result UUID. Format it like an Int
                    if (registeredCharacteristic.Uuid.Equals(Constants.ResultCharacteristicUuid))
                    {
                        return BitConverter.ToInt32(data, 0).ToString();
                    }
                }
                else
                {
                    try
                    {
                        return "Unknown format: " + Encoding.UTF8.GetString(data);
                    }
                    catch (ArgumentException)
                    {
                        return "Unknown format";
                    }
                }
            }
            else
            {
                return "Empty data received";
            }
            return "Unknown format";
        }

        /// <summary>
        /// Process the raw data received from the device into application usable data,
        /// according the the Bluetooth Heart Rate Profile.
        /// https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.characteristic.heart_rate_measurement.xml&u=org.bluetooth.characteristic.heart_rate_measurement.xml
        /// This function throws an exception if the data cannot be parsed.
        /// </summary>
        /// <param name="data">Raw data received from the heart rate monitor.</param>
        /// <returns>The heart rate measurement value.</returns>
        private static ushort ParseHeartRateValue(byte[] data)
        {
            // Heart Rate profile defined flag values
            const byte heartRateValueFormat = 0x01;

            byte flags = data[0];
            bool isHeartRateValueSizeLong = ((flags & heartRateValueFormat) != 0);

            if (isHeartRateValueSizeLong)
            {
                return BitConverter.ToUInt16(data, 1);
            }
            else
            {
                return data[1];
            }
        }
    }
}
