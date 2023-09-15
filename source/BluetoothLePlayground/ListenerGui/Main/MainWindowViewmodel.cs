using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Windows.Devices.Bluetooth.Advertisement;
using ListenerGui.WpfUtil;

namespace ListenerGui.Main;

public class MainWindowViewmodel: INotifyPropertyChanged
{
    private bool _started;
    private readonly Lazy<BluetoothLEAdvertisementWatcher> _watcher;
    private string _testText;
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

    public string TestText
    {
        get => _testText;
        private set => SetField(ref _testText, value);
    }

    public MainWindowViewmodel()
    {
        _watcher = new Lazy<BluetoothLEAdvertisementWatcher>(()=>new BluetoothLEAdvertisementWatcher());
        StartCommand = new RelayCommand(OnStart, (_) => !_started);
        StopCommand = new RelayCommand(OnStop, (_) => _started);
    }

    private void OnStart(object? obj)
    {
        if (_started)
        {
            return;
        }
        var watcher = _watcher.Value;
        
        //use Active to get more data from advertisements
        watcher.ScanningMode = BluetoothLEScanningMode.Passive;

        // Only activate the watcher when we're recieving values >= -80
        //watcher.SignalStrengthFilter.InRangeThresholdInDBm = -80;

        // Stop watching if the value drops below -90 (user walked away)
        //watcher.SignalStrengthFilter.OutOfRangeThresholdInDBm = -90;

        // Register callback for when we see an advertisements
        watcher.Received += OnAdvertisementReceived;

        watcher.Stopped += OnStopped;

        // Wait 5 seconds to make sure the device is really out of range
        watcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(5000);
        watcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(200);

        // Starting watching for advertisements
        watcher.Start();
        
        _started = true;
    }

    private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        TestText = $"ADDR:{args.BluetoothAddress} Str:{args.RawSignalStrengthInDBm}";
    }

    private void OnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        TestText = $"Stopped. Reason:{args.Error}";
        _started = false;
    }

    private void OnStop(object? obj)
    {
        if (_watcher.IsValueCreated)
        {
            var watcher = _watcher.Value;
            watcher.Stop();
        }
        _started = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}