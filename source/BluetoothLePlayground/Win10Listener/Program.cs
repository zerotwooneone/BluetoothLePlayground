﻿using Windows.Devices.Bluetooth.Advertisement;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            // Start the program
            var program = new Program();

            // Close on key press
            Console.ReadLine();
        }

        public Program()
        {
            // Create Bluetooth Listener
            var watcher = new BluetoothLEAdvertisementWatcher();

            watcher.ScanningMode = BluetoothLEScanningMode.Active;

            // Only activate the watcher when we're recieving values >= -80
            watcher.SignalStrengthFilter.InRangeThresholdInDBm = -80;

            // Stop watching if the value drops below -90 (user walked away)
            watcher.SignalStrengthFilter.OutOfRangeThresholdInDBm = -90;

            // Register callback for when we see an advertisements
            watcher.Received += OnAdvertisementReceived;

            watcher.Stopped += OnStopped;

            // Wait 5 seconds to make sure the device is really out of range
            watcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(5000);
            watcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(2000);

            // Starting watching for advertisements
            watcher.Start();

            bool cancelled = false;
            void OnCancelKey(object? sender, ConsoleCancelEventArgs e)
            {
                Console.WriteLine("Cancel Pressed. Shutting down...");
                watcher.Stop();
                cancelled = true;
            }
            Console.CancelKeyPress += OnCancelKey;
            while (!cancelled)
            {
                Thread.Sleep(100);
            }
        }

        private void OnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
        {
            Console.WriteLine($"Stopped {args.Error}");
        }

        private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            // Tell the user we see an advertisement and print some properties
            Console.WriteLine("Advertisement:");
            Console.WriteLine($"  BT_ADDR: {eventArgs.BluetoothAddress}");
            Console.WriteLine($"  FR_NAME: {eventArgs.Advertisement.LocalName}");
            Console.WriteLine();
        }
    }
}