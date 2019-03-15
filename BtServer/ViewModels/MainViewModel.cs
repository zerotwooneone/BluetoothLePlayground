using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
using Prism.Commands;
using Prism.Windows.Mvvm;
using Prism.Windows.Navigation;
using SDKTemplate;

namespace BtServer.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public const string InitialPublishContent = "Start Service";
        private string _characteristicLatestValueText;
        private BluetoothLEAttributeDisplay _characteristicListSelectedItem;
        private Visibility _characteristicListVisibility = Visibility.Collapsed;
        private Visibility _characteristicReadButtonVisibility = Visibility.Collapsed;
        private Visibility _characteristicWritePanelVisibility;
        private string _characteristicWriteValueText;
        private bool _connectButtonEnabled = true;
        private Visibility _connectButtonVisibility;
        private string _operand1Text;
        private string _operand2Text;
        private string _operationText;
        private Visibility _peripheralWarningPanelVisible;
        private string _publishContent;
        private string _resultText;
        private string _selectedBleDeviceId;
        private string _selectedBleDeviceName;
        private Visibility _serverPanelVisible;
        private BluetoothLEAttributeDisplay _serviceListSelectedItem;
        private Visibility _serviceListVisibility = Visibility.Collapsed;
        private string _valueChangedSubscribeToggleContent = "Subscribe to value changes";
        private Visibility _valueChangedSubscribeToggleVisibility = Visibility.Collapsed;

        private BluetoothLEDevice bluetoothLeDevice;
        private GattLocalCharacteristic op1Characteristic;
        private GattLocalCharacteristic op2Characteristic;
        private int operand1Received;
        private int operand2Received;
        private GattLocalCharacteristic operatorCharacteristic;
        private CalculatorOperators operatorReceived = 0;


        private bool peripheralSupported;
        private GattPresentationFormat presentationFormat;

        // Only one registered characteristic at a time.
        private GattCharacteristic registeredCharacteristic;


        private GattLocalCharacteristic resultCharacteristic;

        private int resultVal;
        private GattCharacteristic selectedCharacteristic;
        private GattServiceProvider serviceProvider;

        private bool subscribedForNotifications;

        public MainViewModel()
        {
            PublishCommand = new DelegateCommand(PublishButton_ClickAsync);
            PublishContent = InitialPublishContent;
            ConnectButtonCommand = new DelegateCommand(ConnectButton_Click, ()=>!string.IsNullOrEmpty(SelectedBleDeviceId) && _connectAvailible);
            ServiceListSelectionChangedCommand = new DelegateCommand<object>(ServiceList_SelectionChanged);
            CharacteristicListSelectionChangedCommand =
                new DelegateCommand<object>(CharacteristicList_SelectionChanged);
            CharacteristicReadButtonClickCommand = new DelegateCommand<object>(CharacteristicReadButton_Click);
            CharacteristicWriteButtonClickCommand = new DelegateCommand<object>(CharacteristicWriteButton_Click);
            CharacteristicWriteButtonIntClickCommand = new DelegateCommand<object>(CharacteristicWriteButtonInt_Click);
            ValueChangedSubscribeToggleClickCommand = new DelegateCommand<object>(ValueChangedSubscribeToggle_Click);
            EnumerateButtonClickCommand = new DelegateCommand(EnumerateButton_Click);
            PairButtonClickCommand = new DelegateCommand(PairButton_Click, ()=>!(ResultsListViewSelectedItem?.IsPaired ?? false));
        }

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


        public ICommand PublishCommand { get; }

        public string PublishContent
        {
            get => _publishContent;
            protected set => SetProperty(ref _publishContent, value);
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

        public ObservableCollection<BluetoothLEAttributeDisplay> ServiceCollection { get; } =
            new ObservableCollection<BluetoothLEAttributeDisplay>();

        public ObservableCollection<BluetoothLEAttributeDisplay> CharacteristicCollection { get; }
            = new ObservableCollection<BluetoothLEAttributeDisplay>();
        
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

        public string ValueChangedSubscribeToggleContent
        {
            get => _valueChangedSubscribeToggleContent;
            set => SetProperty(ref _valueChangedSubscribeToggleContent, value);
        }

        public BluetoothLEAttributeDisplay CharacteristicListSelectedItem
        {
            get => _characteristicListSelectedItem;
            set => SetProperty(ref _characteristicListSelectedItem, value);
        }

        public ICommand CharacteristicListSelectionChangedCommand { get; }

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

        public ICommand CharacteristicReadButtonClickCommand { get; }

        public ICommand CharacteristicWriteButtonClickCommand { get; }

        public ICommand CharacteristicWriteButtonIntClickCommand { get; }

        public ICommand ValueChangedSubscribeToggleClickCommand { get; }

        public string CharacteristicLatestValueText
        {
            get => _characteristicLatestValueText;
            set => SetProperty(ref _characteristicLatestValueText, value);
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

            var success = await ClearBluetoothLEDeviceAsync();
            if (!success)
            {
                rootPageNotifyUser("Error: Unable to reset app state", NotifyType.ErrorMessage);
            }

            StopBleDeviceWatcher();
        }

        public async void PublishButton_ClickAsync()

        {
            // Server not initialized yet - initialize it and start publishing

            if (serviceProvider == null)

            {
                var serviceStarted = await ServiceProviderInitAsync();

                if (serviceStarted) PublishContent = "Stop Service";
            }

            else

            {
                // BT_Code: Stops advertising support for custom GATT Service 

                serviceProvider.StopAdvertising();

                serviceProvider = null;

                PublishContent = InitialPublishContent;
            }
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

            var localAdapter = await BluetoothAdapter.GetDefaultAsync();


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

            var serviceResult = await GattServiceProvider.CreateAsync(Constants.CalcServiceUuid);

            if (serviceResult.Error == BluetoothError.Success)
                serviceProvider = serviceResult.ServiceProvider;

            else
                return false;


            var result = await serviceProvider.Service.CreateCharacteristicAsync(Constants.Op1CharacteristicUuid,
                Constants.gattOperandParameters);

            if (result.Error == BluetoothError.Success)
                op1Characteristic = result.Characteristic;

            else
                return false;

            op1Characteristic.WriteRequested += Op1Characteristic_WriteRequestedAsync;


            result = await serviceProvider.Service.CreateCharacteristicAsync(Constants.Op2CharacteristicUuid,
                Constants.gattOperandParameters);

            if (result.Error == BluetoothError.Success)
                op2Characteristic = result.Characteristic;

            else
                return false;


            op2Characteristic.WriteRequested += Op2Characteristic_WriteRequestedAsync;


            result = await serviceProvider.Service.CreateCharacteristicAsync(Constants.OperatorCharacteristicUuid,
                Constants.gattOperatorParameters);

            if (result.Error == BluetoothError.Success)
                operatorCharacteristic = result.Characteristic;

            else
                return false;


            operatorCharacteristic.WriteRequested += OperatorCharacteristic_WriteRequestedAsync;


            // Add presentation format - 32-bit unsigned integer, with exponent 0, the unit is unitless, with no company description

            var intFormat = GattPresentationFormat.FromParts(
                GattPresentationFormatTypes.UInt32,
                PresentationFormats.Exponent,
                Convert.ToUInt16(PresentationFormats.Units.Unitless),
                Convert.ToByte(PresentationFormats.NamespaceId.BluetoothSigAssignedNumber),
                PresentationFormats.Description);


            Constants.gattResultParameters.PresentationFormats.Add(intFormat);


            result = await serviceProvider.Service.CreateCharacteristicAsync(Constants.ResultCharacteristicUuid,
                Constants.gattResultParameters);

            if (result.Error == BluetoothError.Success)
                resultCharacteristic = result.Characteristic;

            else
                return false;

            resultCharacteristic.ReadRequested += ResultCharacteristic_ReadRequestedAsync;

            resultCharacteristic.SubscribedClientsChanged += ResultCharacteristic_SubscribedClientsChanged;


            // BT_Code: Indicate if your sever advertises as connectable and discoverable.

            var advParameters = new GattServiceProviderAdvertisingParameters

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
            rootPageNotifyUser($"New device subscribed. New subscribed count: {sender.SubscribedClients.Count}",NotifyType.StatusMessage);
        }


        private void ServiceProvider_AdvertisementStatusChanged(GattServiceProvider sender,
            GattServiceProviderAdvertisementStatusChangedEventArgs args)

        {
            // Created - The default state of the advertisement, before the service is published for the first time.

            // Stopped - Indicates that the application has canceled the service publication and its advertisement.

            // Started - Indicates that the system was successfully able to issue the advertisement request.

            // Aborted - Indicates that the system was unable to submit the advertisement request, or it was canceled due to resource contention.


            rootPageNotifyUser($"New Advertisement Status: {sender.AdvertisementStatus}", NotifyType.StatusMessage);
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

                    var request = await args.GetRequestAsync();

                    if (request == null) return;


                    var writer = new DataWriter
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
            var computedValue = 0;

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
                        rootPageNotifyUser("Division overflow", NotifyType.ErrorMessage);
                    }

                    else
                    {
                        computedValue = operand1Received / operand2Received;
                    }

                    break;
            }

            NotifyClientDevices(computedValue);

            return computedValue;
        }


        private async void NotifyClientDevices(int computedValue)

        {
            var writer = new DataWriter
            {
                ByteOrder = ByteOrder.LittleEndian
            };

            writer.WriteInt32(computedValue);


            // BT_Code: Returns a collection of all clients that the notification was attempted and the result.

            var results = await resultCharacteristic.NotifyValueAsync(writer.DetachBuffer());


            rootPageNotifyUser($"Sent value {computedValue} to clients.", NotifyType.StatusMessage);

            foreach (var result in results)

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

                var request = await args.GetRequestAsync();

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

                var request = await args.GetRequestAsync();

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

                var request = await args.GetRequestAsync();

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


            var reader = DataReader.FromBuffer(request.Value);

            reader.ByteOrder = ByteOrder.LittleEndian;

            var val = reader.ReadInt32();


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

                    operatorReceived = (CalculatorOperators) val;

                    break;
            }

            // Complete the request if needed

            if (request.Option == GattWriteOption.WriteWithResponse) request.Respond();


            UpdateUX();
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

        private async void CharacteristicList_SelectionChanged(object selectedItem)
        {
            selectedCharacteristic = null;

            var attributeInfoDisp = CharacteristicListSelectedItem;
            if (attributeInfoDisp == null)
            {
                EnableCharacteristicPanels(GattCharacteristicProperties.None);
                return;
            }

            selectedCharacteristic = attributeInfoDisp.characteristic;
            if (selectedCharacteristic == null) return;

            // Get all the child descriptors of a characteristics. Use the cache mode to specify uncached descriptors only 
            // and the new Async functions to get the descriptors of unpaired devices as well. 
            var result = await selectedCharacteristic.GetDescriptorsAsync(BluetoothCacheMode.Uncached);
            if (result.Status != GattCommunicationStatus.Success)
            {
                rootPageNotifyUser("Descriptor read failure: " + result.Status.ToString(), NotifyType.ErrorMessage);
            }

            // BT_Code: There's no need to access presentation format unless there's at least one. 
            presentationFormat = null;
            if (selectedCharacteristic.PresentationFormats.Count > 0)
                if (selectedCharacteristic.PresentationFormats.Count.Equals(1))
                    presentationFormat = selectedCharacteristic.PresentationFormats[0];

            // Enable/disable operations based on the GattCharacteristicProperties.
            EnableCharacteristicPanels(selectedCharacteristic.CharacteristicProperties);
        }

        private void EnableCharacteristicPanels(GattCharacteristicProperties properties)
        {
            // BT_Code: Hide the controls which do not apply to this characteristic.
            CharacteristicReadButtonVisibility =
                ConvertToVisibility(properties.HasFlag(GattCharacteristicProperties.Read));

            CharacteristicWritePanelVisibility = ConvertToVisibility(
                properties.HasFlag(GattCharacteristicProperties.Write) ||
                properties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse));
            CharacteristicWriteValueText = "";

            ValueChangedSubscribeToggleVisibility = ConvertToVisibility(
                properties.HasFlag(GattCharacteristicProperties.Indicate) ||
                properties.HasFlag(GattCharacteristicProperties.Notify));
        }

        private Visibility ConvertToVisibility(bool visible)
        {
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void CharacteristicReadButton_Click(object selectedItem)
        {
            // BT_Code: Read the actual value from the device by using Uncached.
            var result = await selectedCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (result.Status == GattCommunicationStatus.Success)
            {
                var formattedResult = FormatValueByPresentation(result.Value, presentationFormat);
                rootPageNotifyUser($"Read result: {formattedResult}", NotifyType.StatusMessage);
            }
        }

        private async void CharacteristicWriteButton_Click(object selectedItem)
        {
            if (!string.IsNullOrEmpty(CharacteristicWriteValueText))
            {
                var writeBuffer = CryptographicBuffer.ConvertStringToBinary(CharacteristicWriteValueText,
                    BinaryStringEncoding.Utf8);

                var writeSuccessful = await WriteBufferToSelectedCharacteristicAsync(writeBuffer);
            }
        }

        private async void CharacteristicWriteButtonInt_Click(object selectedItem)
        {
            if (!string.IsNullOrEmpty(CharacteristicWriteValueText))
            {
                var isValidValue = int.TryParse(CharacteristicWriteValueText, out var readValue);
                if (isValidValue)
                {
                    var writer = new DataWriter
                    {
                        ByteOrder = ByteOrder.LittleEndian
                    };
                    writer.WriteInt32(readValue);

                    var writeSuccessful = await WriteBufferToSelectedCharacteristicAsync(writer.DetachBuffer());
                }
            }
        }

        private async Task<bool> WriteBufferToSelectedCharacteristicAsync(IBuffer buffer)
        {
            try
            {
                // BT_Code: Writes the value from the buffer to the characteristic.
                var result = await selectedCharacteristic.WriteValueWithResultAsync(buffer);

                if (result.Status == GattCommunicationStatus.Success)
                    return true;
                return false;
            }
            catch (Exception ex) when (ex.HResult == E_BLUETOOTH_ATT_INVALID_PDU)
            {
                rootPageNotifyUser(ex.Message, NotifyType.ErrorMessage);
                return false;
            }
            catch (Exception ex) when (ex.HResult == E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED ||
                                       ex.HResult == E_ACCESSDENIED)
            {
                // This usually happens when a device reports that it support writing, but it actually doesn't.
                rootPageNotifyUser(ex.Message, NotifyType.ErrorMessage);
                return false;
            }
        }

        private async void ValueChangedSubscribeToggle_Click(object selectedItem)
        {
            if (!subscribedForNotifications)
            {
                // initialize status
                var status = GattCommunicationStatus.Unreachable;
                var cccdValue = GattClientCharacteristicConfigurationDescriptorValue.None;
                if (selectedCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
                    cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Indicate;

                else if (selectedCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
                    cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Notify;

                try
                {
                    // BT_Code: Must write the CCCD in order for server to send indications.
                    // We receive them in the ValueChanged event handler.
                    status =
                        await selectedCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);

                    if (status == GattCommunicationStatus.Success) AddValueChangedHandler();
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support indicate, but it actually doesn't.
                    rootPageNotifyUser(ex.Message, NotifyType.ErrorMessage);
                }
            }
            else
            {
                try
                {
                    // BT_Code: Must write the CCCD in order for server to send notifications.
                    // We receive them in the ValueChanged event handler.
                    // Note that this sample configures either Indicate or Notify, but not both.
                    var result = await
                        selectedCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                            GattClientCharacteristicConfigurationDescriptorValue.None);
                    if (result == GattCommunicationStatus.Success)
                    {
                        subscribedForNotifications = false;
                        RemoveValueChangedHandler();
                        rootPageNotifyUser("Successfully un-registered for notifications", NotifyType.StatusMessage);
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support notify, but it actually doesn't.
                    rootPageNotifyUser(ex.Message, NotifyType.ErrorMessage);
                }
            }
        }

        private async void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // BT_Code: An Indicate or Notify reported that the value has changed.
            // Display the new value with a timestamp.
            var newValue = FormatValueByPresentation(args.CharacteristicValue, presentationFormat);
            var message = $"Value at {DateTime.Now:hh:mm:ss.FFF}: {newValue}";
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () => CharacteristicLatestValueText = message);
        }

        private string FormatValueByPresentation(IBuffer buffer, GattPresentationFormat format)
        {
            // BT_Code: For the purpose of this sample, this function converts only UInt32 and
            // UTF-8 buffers to readable text. It can be extended to support other formats if your app needs them.
            CryptographicBuffer.CopyToByteArray(buffer, out var data);
            if (format != null)
            {
                if (format.FormatType == GattPresentationFormatTypes.UInt32 && data.Length >= 4)
                    return BitConverter.ToInt32(data, 0).ToString();
                if (format.FormatType == GattPresentationFormatTypes.Utf8)
                    try
                    {
                        return Encoding.UTF8.GetString(data);
                    }
                    catch (ArgumentException)
                    {
                        return "(error: Invalid UTF-8 string)";
                    }

                return "Unsupported format: " + CryptographicBuffer.EncodeToHexString(buffer);
            }

            if (data != null)
            {
                // We don't know what format to use. Let's try some well-known profiles, or default back to UTF-8.
                if (selectedCharacteristic.Uuid.Equals(GattCharacteristicUuids.HeartRateMeasurement))
                    try
                    {
                        return "Heart Rate: " + ParseHeartRateValue(data);
                    }
                    catch (ArgumentException)
                    {
                        return "Heart Rate: (unable to parse)";
                    }

                if (selectedCharacteristic.Uuid.Equals(GattCharacteristicUuids.BatteryLevel))
                    try
                    {
                        // battery level is encoded as a percentage value in the first byte according to
                        // https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.characteristic.battery_level.xml
                        return "Battery Level: " + data[0] + "%";
                    }
                    catch (ArgumentException)
                    {
                        return "Battery Level: (unable to parse)";
                    }
                // This is our custom calc service Result UUID. Format it like an Int

                if (selectedCharacteristic.Uuid.Equals(Constants.ResultCharacteristicUuid))
                    return BitConverter.ToInt32(data, 0).ToString();
                // No guarantees on if a characteristic is registered for notifications.

                if (registeredCharacteristic != null)
                {
                    // This is our custom calc service Result UUID. Format it like an Int
                    if (registeredCharacteristic.Uuid.Equals(Constants.ResultCharacteristicUuid))
                        return BitConverter.ToInt32(data, 0).ToString();
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
        ///     Process the raw data received from the device into application usable data,
        ///     according the the Bluetooth Heart Rate Profile.
        ///     https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.characteristic.heart_rate_measurement.xml
        ///     &u=org.bluetooth.characteristic.heart_rate_measurement.xml
        ///     This function throws an exception if the data cannot be parsed.
        /// </summary>
        /// <param name="data">Raw data received from the heart rate monitor.</param>
        /// <returns>The heart rate measurement value.</returns>
        private static ushort ParseHeartRateValue(byte[] data)
        {
            // Heart Rate profile defined flag values
            const byte heartRateValueFormat = 0x01;

            var flags = data[0];
            var isHeartRateValueSizeLong = (flags & heartRateValueFormat) != 0;

            if (isHeartRateValueSizeLong)
                return BitConverter.ToUInt16(data, 1);
            return data[1];
        }

        #region Error Codes

        private readonly int E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED = unchecked((int) 0x80650003);
        private readonly int E_BLUETOOTH_ATT_INVALID_PDU = unchecked((int) 0x80650004);
        private readonly int E_ACCESSDENIED = unchecked((int) 0x80070005);

        private readonly int
            E_DEVICE_NOT_AVAILABLE = unchecked((int) 0x800710df); // HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE)

        #endregion

        #region Enumerating Services

        private async Task<bool> ClearBluetoothLEDeviceAsync()
        {
            if (subscribedForNotifications)
            {
                // Need to clear the CCCD from the remote device so we stop receiving notifications
                var result =
                    await registeredCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.None);
                if (result != GattCommunicationStatus.Success) return false;

                selectedCharacteristic.ValueChanged -= Characteristic_ValueChanged;
                subscribedForNotifications = false;
            }

            bluetoothLeDevice?.Dispose();
            bluetoothLeDevice = null;
            return true;
        }

        public ICommand ConnectButtonCommand { get; }
        private bool _connectAvailible = true;
        
        private async void ConnectButton_Click()
        {
            _connectAvailible = false;

            if (!await ClearBluetoothLEDeviceAsync())
            {
                rootPageNotifyUser("Error: Unable to reset state, try again.", NotifyType.ErrorMessage);
                _connectAvailible = false;
                return;
            }

            try
            {
                // BT_Code: BluetoothLEDevice.FromIdAsync must be called from a UI thread because it may prompt for consent.
                bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(SelectedBleDeviceId);

                if (bluetoothLeDevice == null)
                {
                    rootPageNotifyUser("Failed to connect to device.", NotifyType.ErrorMessage);
                }
            }
            catch (Exception ex) when (ex.HResult == E_DEVICE_NOT_AVAILABLE)
            {
                rootPageNotifyUser("Bluetooth radio is not on.", NotifyType.ErrorMessage);
            }

            if (bluetoothLeDevice != null)
            {
                // Note: BluetoothLEDevice.GattServices property will return an empty list for unpaired devices. For all uses we recommend using the GetGattServicesAsync method.
                // BT_Code: GetGattServicesAsync returns a list of all the supported services of the device (even if it's not paired to the system).
                // If the services supported by the device are expected to change during BT usage, subscribe to the GattServicesChanged event.
                var result = await bluetoothLeDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);

                if (result.Status == GattCommunicationStatus.Success)
                {
                    var services = result.Services;
                    rootPageNotifyUser(String.Format("Found {0} services", services.Count), NotifyType.StatusMessage);
                    foreach (var service in services) ServiceCollection.Add(new BluetoothLEAttributeDisplay(service));
                    ConnectButtonVisibility = Visibility.Collapsed;
                    ServiceListVisibility = Visibility.Visible;
                }
            }

            _connectAvailible = true;
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
            var attributeInfoDisp = ServiceListSelectedItem;

            CharacteristicCollection.Clear();
            RemoveValueChangedHandler();

            IReadOnlyList<GattCharacteristic> characteristics = null;
            try
            {
                // Ensure we have access to the device.
                var accessStatus = await attributeInfoDisp.service.RequestAccessAsync();
                if (accessStatus == DeviceAccessStatus.Allowed)
                {
                    // BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characterstics only 
                    // and the new Async functions to get the characteristics of unpaired devices as well. 
                    var result = await attributeInfoDisp.service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    if (result.Status == GattCommunicationStatus.Success)
                        characteristics = result.Characteristics;
                    else
                        characteristics = new List<GattCharacteristic>();
                }
                else
                {
                    // Not granted access
                    rootPageNotifyUser("Error accessing service.", NotifyType.ErrorMessage);

                    // On error, act as if there are no characteristics.
                    characteristics = new List<GattCharacteristic>();
                }
            }
            catch (Exception ex)
            {
                rootPageNotifyUser("Restricted service. Can't read characteristics: " + ex.Message,NotifyType.ErrorMessage);
                // On error, act as if there are no characteristics.
                characteristics = new List<GattCharacteristic>();
            }

            foreach (var c in characteristics) CharacteristicCollection.Add(new BluetoothLEAttributeDisplay(c));
            CharacteristicListVisibility = Visibility.Visible;
        }

        #endregion

        public ObservableCollection<BluetoothLEDeviceDisplay> KnownDevices { get; } = new ObservableCollection<BluetoothLEDeviceDisplay>();
        private List<DeviceInformation> UnknownDevices = new List<DeviceInformation>();
        private BluetoothLEDeviceDisplay _resultsListViewSelectedItem;
        private DeviceWatcher deviceWatcher;
        private string _enumerateButtonContent = InitialEnumerateButtonContent;
        private int _test;
        private const string InitialEnumerateButtonContent = "Start enumerating";

        public ICommand EnumerateButtonClickCommand { get; }
        public ICommand PairButtonClickCommand { get; }

        private void EnumerateButton_Click()
        {
            if (deviceWatcher == null)
            {
                StartBleDeviceWatcher();
                EnumerateButtonContent = "Stop enumerating";
                rootPageNotifyUser($"Device watcher started.", NotifyType.StatusMessage);
            }
            else
            {
                StopBleDeviceWatcher();
                EnumerateButtonContent = InitialEnumerateButtonContent;
                rootPageNotifyUser($"Device watcher stopped.", NotifyType.StatusMessage);
            }
        }    

        public string EnumerateButtonContent
        {
            get => _enumerateButtonContent;
            set => SetProperty(ref _enumerateButtonContent, value);
        }

        public BluetoothLEDeviceDisplay ResultsListViewSelectedItem
        {
            get => _resultsListViewSelectedItem;
            set
            {
                SetProperty(ref _resultsListViewSelectedItem, value);
                if (_resultsListViewSelectedItem != null)
                {
                    SelectedBleDeviceId = _resultsListViewSelectedItem.Id;
                    SelectedBleDeviceName = _resultsListViewSelectedItem.Name;
                    ((DelegateCommand)ConnectButtonCommand).RaiseCanExecuteChanged();
                }
            }
        }

        #region Device discovery

        /// <summary>
        /// Starts a device watcher that looks for all nearby Bluetooth devices (paired or unpaired). 
        /// Attaches event handlers to populate the device collection.
        /// </summary>
        private void StartBleDeviceWatcher()
        {
            // Additional properties we would like about the device.
            // Property strings are documented here https://msdn.microsoft.com/en-us/library/windows/desktop/ff521659(v=vs.85).aspx
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" };

            // BT_Code: Example showing paired and non-paired in a single query.
            string aqsAllBluetoothLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

            deviceWatcher =
                    DeviceInformation.CreateWatcher(
                        aqsAllBluetoothLEDevices,
                        requestedProperties,
                        DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            deviceWatcher.Removed += DeviceWatcher_Removed;
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcher.Stopped += DeviceWatcher_Stopped;

            // Start over with an empty collection.
            KnownDevices.Clear();

            // Start the watcher. Active enumeration is limited to approximately 30 seconds.
            // This limits power usage and reduces interference with other Bluetooth activities.
            // To monitor for the presence of Bluetooth LE devices for an extended period,
            // use the BluetoothLEAdvertisementWatcher runtime class. See the BluetoothAdvertisement
            // sample for an example.
            deviceWatcher.Start();
        }

        /// <summary>
        /// Stops watching for all nearby Bluetooth devices.
        /// </summary>
        private void StopBleDeviceWatcher()
        {
            if (deviceWatcher != null)
            {
                // Unregister the event handlers.
                deviceWatcher.Added -= DeviceWatcher_Added;
                deviceWatcher.Updated -= DeviceWatcher_Updated;
                deviceWatcher.Removed -= DeviceWatcher_Removed;
                deviceWatcher.EnumerationCompleted -= DeviceWatcher_EnumerationCompleted;
                deviceWatcher.Stopped -= DeviceWatcher_Stopped;

                // Stop the watcher.
                deviceWatcher.Stop();
                deviceWatcher = null;
            }
        }

        private BluetoothLEDeviceDisplay FindBluetoothLEDeviceDisplay(string id)
        {
            foreach (BluetoothLEDeviceDisplay bleDeviceDisplay in KnownDevices)
            {
                if (bleDeviceDisplay.Id == id)
                {
                    return bleDeviceDisplay;
                }
            }
            return null;
        }

        private DeviceInformation FindUnknownDevices(string id)
        {
            foreach (DeviceInformation bleDeviceInfo in UnknownDevices)
            {
                if (bleDeviceInfo.Id == id)
                {
                    return bleDeviceInfo;
                }
            }
            return null;
        }

        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    Debug.WriteLine(String.Format("Added {0}{1}", deviceInfo.Id, deviceInfo.Name));

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        // Make sure device isn't already present in the list.
                        if (FindBluetoothLEDeviceDisplay(deviceInfo.Id) == null)
                        {
                            if (deviceInfo.Name != string.Empty)
                            {
                                // If device has a friendly name display it immediately.
                                KnownDevices.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                            }
                            else
                            {
                                // Add it to a list in case the name gets updated later. 
                                UnknownDevices.Add(deviceInfo);
                            }
                        }

                    }
                }
            });
        }

        private async void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    Debug.WriteLine(String.Format("Updated {0}{1}", deviceInfoUpdate.Id, ""));

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                        if (bleDeviceDisplay != null)
                        {
                            // Device is already being displayed - update UX.
                            bleDeviceDisplay.Update(deviceInfoUpdate);
                            return;
                        }

                        DeviceInformation deviceInfo = FindUnknownDevices(deviceInfoUpdate.Id);
                        if (deviceInfo != null)
                        {
                            deviceInfo.Update(deviceInfoUpdate);
                            // If device has been updated with a friendly name it's no longer unknown.
                            if (deviceInfo.Name != String.Empty)
                            {
                                KnownDevices.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                                UnknownDevices.Remove(deviceInfo);
                            }
                        }
                    }
                }
            });
        }

        private async void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    Debug.WriteLine(String.Format("Removed {0}{1}", deviceInfoUpdate.Id,""));

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        // Find the corresponding DeviceInformation in the collection and remove it.
                        BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                        if (bleDeviceDisplay != null)
                        {
                            KnownDevices.Remove(bleDeviceDisplay);
                        }

                        DeviceInformation deviceInfo = FindUnknownDevices(deviceInfoUpdate.Id);
                        if (deviceInfo != null)
                        {
                            UnknownDevices.Remove(deviceInfo);
                        }
                    }
                }
            });
        }

        private async void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object e)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcher)
                {
                    rootPageNotifyUser($"{KnownDevices.Count} devices found. Enumeration completed.",NotifyType.StatusMessage);
                }
            });
        }

        private async void DeviceWatcher_Stopped(DeviceWatcher sender, object e)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcher)
                {
                    rootPageNotifyUser($"No longer watching for devices.",sender.Status == DeviceWatcherStatus.Aborted ? NotifyType.ErrorMessage : NotifyType.StatusMessage);
                }
            });
        }
        #endregion

        #region Pairing

        private bool isBusy = false;

        private async void PairButton_Click()
        {
            // Do not allow a new Pair operation to start if an existing one is in progress.
            if (isBusy)
            {
                return;
            }

            isBusy = true;

            rootPageNotifyUser("Pairing started. Please wait...", NotifyType.StatusMessage);

            // For more information about device pairing, including examples of
            // customizing the pairing process, see the DeviceEnumerationAndPairing sample.

            // Capture the current selected item in case the user changes it while we are pairing.
            
            // BT_Code: Pair the currently selected device.
            DevicePairingResult result = await ResultsListViewSelectedItem.DeviceInformation.Pairing.PairAsync();
            rootPageNotifyUser($"Pairing result = {result.Status}",
                result.Status == DevicePairingResultStatus.Paired || result.Status == DevicePairingResultStatus.AlreadyPaired
                    ? NotifyType.StatusMessage
                    : NotifyType.ErrorMessage);

            isBusy = false;
        }

        #endregion

        private void rootPageNotifyUser(string message, NotifyType notifyType)
        {
            Debug.WriteLine($"{notifyType} : {message}");
        }
    }
}
