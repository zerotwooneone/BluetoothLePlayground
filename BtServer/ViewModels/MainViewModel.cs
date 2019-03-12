using Prism.Commands;
using Prism.Windows.Mvvm;
using Prism.Windows.Navigation;
using SDKTemplate;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
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


        public override void OnNavigatingFrom(
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
        }

        public const string InitialPublishContent = "Start Service";
        public MainViewModel()
        {
            PublishCommand = new DelegateCommand(PublishButton_ClickAsync);
            PublishContent = InitialPublishContent;
        }

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
    }
}

// Define the characteristics and other properties of our custom service.
public class Constants

{
    // BT_Code: Initializes custom local parameters w/ properties, protection levels as well as common descriptors like User Description. 

    public static readonly GattLocalCharacteristicParameters gattOperandParameters =
        new GattLocalCharacteristicParameters

        {
            CharacteristicProperties = GattCharacteristicProperties.Write |
                                       GattCharacteristicProperties.WriteWithoutResponse,

            WriteProtectionLevel = GattProtectionLevel.Plain,

            UserDescription = "Operand Characteristic"
        };


    public static readonly GattLocalCharacteristicParameters gattOperatorParameters =
        new GattLocalCharacteristicParameters

        {
            CharacteristicProperties = GattCharacteristicProperties.Write |
                                       GattCharacteristicProperties.WriteWithoutResponse,

            WriteProtectionLevel = GattProtectionLevel.Plain,

            UserDescription = "Operator Characteristic"
        };


    public static readonly GattLocalCharacteristicParameters gattResultParameters =
        new GattLocalCharacteristicParameters

        {
            CharacteristicProperties = GattCharacteristicProperties.Read |
                                       GattCharacteristicProperties.Notify,

            WriteProtectionLevel = GattProtectionLevel.Plain,

            UserDescription = "Result Characteristic"
        };


    public static readonly Guid CalcServiceUuid = Guid.Parse("caecface-e1d9-11e6-bf01-fe55135034f0");


    public static readonly Guid Op1CharacteristicUuid = Guid.Parse("caec2ebc-e1d9-11e6-bf01-fe55135034f1");

    public static readonly Guid Op2CharacteristicUuid = Guid.Parse("caec2ebc-e1d9-11e6-bf01-fe55135034f2");

    public static readonly Guid OperatorCharacteristicUuid = Guid.Parse("caec2ebc-e1d9-11e6-bf01-fe55135034f3");

    public static readonly Guid ResultCharacteristicUuid = Guid.Parse("caec2ebc-e1d9-11e6-bf01-fe55135034f4");
}

public enum CalculatorCharacteristics

{

    Operand1 = 1,

    Operand2 = 2,

    Operator = 3

}



public enum CalculatorOperators

{

    Add = 1,

    Subtract = 2,

    Multiply = 3,

    Divide = 4

}

namespace SDKTemplate
{
    /// <summary>
    /// Class containing the details of Gatt characteristics presentation formats
    /// </summary>
    public class PresentationFormats
    {
        /// <summary>
        /// Units are established international standards for the measurement of physical quantities.
        /// </summary>
        /// <remarks>Please refer https://www.bluetooth.com/specifications/assigned-numbers/units </remarks>
        public enum Units
        {
            Unitless = 0x2700,
            LengthMetre = 0x2701,
            MassKilogram = 0x2702,
            TimeSecond = 0x2703,
            ElectricCurrentAmpere = 0x2704,
            ThermodynamicTemperatureKelvin = 0x2705,
            AmountOfSubstanceMole = 0x2706,
            LuminousIntensityCandela = 0x2707,
            AreaSquareMetres = 0x2710,
            VolumeCubicMetres = 0x2711,
            VelocityMetresPerSecond = 0x2712,
            AccelerationMetresPerSecondSquared = 0x2713,
            WaveNumberReciprocalMetre = 0x2714,
            DensityKilogramperCubicMetre = 0x2715,
            SurfaceDensityKilogramPerSquareMetre = 0x2716,
            SpecificVolumeCubicMetrePerKilogram = 0x2717,
            CurrentDensityAmperePerSquareMetre = 0x2718,
            MagneticFieldStrengthAmperePerMetre = 0x2719,
            AmountConcentrationMolePerCubicMetre = 0x271A,
            MassConcentrationKilogramPerCubicMetre = 0x271B,
            LuminanceCandelaPerSquareMetre = 0x271C,
            RefractiveIndex = 0x271D,
            RelativePermeability = 0x271E,
            PlaneAngleRadian = 0x2720,
            SolidAngleSteradian = 0x2721,
            FrequencyHertz = 0x2722,
            ForceNewton = 0x2723,
            PressurePascal = 0x2724,
            EnergyJoule = 0x2725,
            PowerWatt = 0x2726,
            ElectricChargeCoulomb = 0x2727,
            ElectricPotentialDifferenceVolt = 0x2728,
            CapacitanceFarad = 0x2729,
            ElectricResistanceOhm = 0x272A,
            ElectricConductanceSiemens = 0x272B,
            MagneticFluxWeber = 0x272C,
            MagneticFluxDensityTesla = 0x272D,
            InductanceHenry = 0x272E,
            CelsiusTemperatureDegreeCelsius = 0x272F,
            LuminousFluxLumen = 0x2730,
            IlluminanceLux = 0x2731,
            ActivityReferredToARadioNuclideBecquerel = 0x2732,
            AbsorbedDoseGray = 0x2733,
            DoseEquivalentSievert = 0x2734,
            CatalyticActivityKatal = 0x2735,
            DynamicViscosityPascalSecond = 0x2740,
            MomentOfForceNewtonMetre = 0x2741,
            SurfaceTensionNewtonPerMetre = 0x2742,
            AngularVelocityRadianPerSecond = 0x2743,
            AngularAccelerationRadianPerSecondSquared = 0x2744,
            HeatFluxDensityWattPerSquareMetre = 0x2745,
            HeatCapacityJoulePerKelvin = 0x2746,
            SpecificHeatCapacityJoulePerKilogramKelvin = 0x2747,
            SpecificEnergyJoulePerKilogram = 0x2748,
            ThermalConductivityWattPerMetreKelvin = 0x2749,
            EnergyDensityJoulePerCubicMetre = 0x274A,
            ElectricfieldstrengthVoltPerMetre = 0x274B,
            ElectricchargeDensityCoulombPerCubicMetre = 0x274C,
            SurfacechargeDensityCoulombPerSquareMetre = 0x274D,
            ElectricFluxDensityCoulombPerSquareMetre = 0x274E,
            PermittivityFaradPerMetre = 0x274F,
            PermeabilityHenryPerMetre = 0x2750,
            MolarEnergyJoulePermole = 0x2751,
            MolarentropyJoulePermoleKelvin = 0x2752,
            ExposureCoulombPerKilogram = 0x2753,
            AbsorbeddoserateGrayPerSecond = 0x2754,
            RadiantintensityWattPerSteradian = 0x2755,
            RadianceWattPerSquareMetreSteradian = 0x2756,
            CatalyticActivityConcentrationKatalPerCubicMetre = 0x2757,
            TimeMinute = 0x2760,
            TimeHour = 0x2761,
            TimeDay = 0x2762,
            PlaneAngleDegree = 0x2763,
            PlaneAngleMinute = 0x2764,
            PlaneAngleSecond = 0x2765,
            AreaHectare = 0x2766,
            VolumeLitre = 0x2767,
            MassTonne = 0x2768,
            PressureBar = 0x2780,
            PressureMilliMetreofmercury = 0x2781,
            LengthAngstrom = 0x2782,
            LengthNauticalMile = 0x2783,
            AreaBarn = 0x2784,
            VelocityKnot = 0x2785,
            LogarithmicRadioQuantityNeper = 0x2786,
            LogarithmicRadioQuantityBel = 0x2787,
            LengthYard = 0x27A0,
            LengthParsec = 0x27A1,
            LengthInch = 0x27A2,
            LengthFoot = 0x27A3,
            LengthMile = 0x27A4,
            PressurePoundForcePerSquareinch = 0x27A5,
            VelocityKiloMetrePerHour = 0x27A6,
            VelocityMilePerHour = 0x27A7,
            AngularVelocityRevolutionPerminute = 0x27A8,
            EnergyGramcalorie = 0x27A9,
            EnergyKilogramcalorie = 0x27AA,
            EnergyKiloWattHour = 0x27AB,
            ThermodynamicTemperatureDegreeFahrenheit = 0x27AC,
            Percentage = 0x27AD,
            PerMille = 0x27AE,
            PeriodBeatsPerMinute = 0x27AF,
            ElectricchargeAmpereHours = 0x27B0,
            MassDensityMilligramPerdeciLitre = 0x27B1,
            MassDensityMillimolePerLitre = 0x27B2,
            TimeYear = 0x27B3,
            TimeMonth = 0x27B4,
            ConcentrationCountPerCubicMetre = 0x27B5,
            IrradianceWattPerSquareMetre = 0x27B6,
            MilliliterPerKilogramPerminute = 0x27B7,
            MassPound = 0x27B8
        }

        /// <summary>
        /// The Name Space field is used to identify the organization that is responsible for defining the enumerations for the description field.
        /// </summary>
        /// <remarks>
        /// Please refer https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.descriptor.gatt.characteristic_presentation_format.xml
        /// </remarks>
        public enum NamespaceId
        {
            BluetoothSigAssignedNumber = 1,
            ReservedForFutureUse
        }

        /// <summary>
        /// The Description is an enumerated value from the organization identified by the Name Space field.
        /// </summary>
        /// <remarks>
        /// Please refer https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.descriptor.gatt.characteristic_presentation_format.xml
        /// </remarks>
        public const ushort Description = 0x0000;

        /// <summary>
        /// Exponent value for the characteristics
        /// </summary>
        public const int Exponent = 0;
    }
}
