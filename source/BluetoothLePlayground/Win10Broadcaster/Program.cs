using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            // Start the program
            var program = new Program();
        }

        public Program()
        {
            // Create Bluetooth Listener
            var publisher = new BluetoothLEAdvertisementPublisher();

            publisher.StatusChanged += OnStatusChanged;
            //publisher.Advertisement.LocalName = "test";
            
            // Add custom data to the advertisement
            var manufacturerData = new BluetoothLEManufacturerData();
            manufacturerData.CompanyId = 0xFFFE;

            var writer = new DataWriter();
            writer.WriteString("Hello World");

// Make sure that the buffer length can fit within an advertisement payload (~20 bytes). 
// Otherwise you will get an exception.
            manufacturerData.Data = writer.DetachBuffer();

// Add the manufacturer data to the advertisement publisher:
            publisher.Advertisement.ManufacturerData.Add(manufacturerData);
            
            publisher.Start();

            bool cancelled = false;
            void OnCancelKey(object? sender, ConsoleCancelEventArgs e)
            {
                Console.WriteLine("Cancel Pressed. Shutting down...");
                cancelled = true;
            }
            Console.CancelKeyPress += OnCancelKey;
            while (!cancelled)
            {
                Thread.Sleep(100);
            }
            publisher.Stop();
        }

        private void OnStatusChanged(BluetoothLEAdvertisementPublisher sender, BluetoothLEAdvertisementPublisherStatusChangedEventArgs args)
        {
            Console.WriteLine($"status changed Status:{args.Status} Error:{args.Error} TransmitPower:{args.SelectedTransmitPowerLevelInDBm}");
        }

        private void OnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
        {
            Console.WriteLine($"Stopped {args.Error}");
        }

        private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            // Tell the user we see an advertisement and print some properties
            Console.WriteLine("Advertisement:");
            Console.WriteLine($"  BT_ADDR: {eventArgs.BluetoothAddress} Str:{eventArgs.RawSignalStrengthInDBm}");
            if (!string.IsNullOrWhiteSpace(eventArgs.Advertisement.LocalName))
            {
                Console.WriteLine($"  FR_NAME: {eventArgs.Advertisement.LocalName}");    
            }
            Console.WriteLine();
        }
    }
}