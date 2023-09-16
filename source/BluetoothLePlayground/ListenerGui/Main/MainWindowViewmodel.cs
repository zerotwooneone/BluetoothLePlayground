using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Foundation;
using ListenerGui.ReactiveUtil;
using ListenerGui.WpfUtil;

namespace ListenerGui.Main;

public class MainWindowViewmodel: INotifyPropertyChanged
{
    private const int MaxDisplayedAdvertisements = 20;
    private readonly ISchedulerLocator _schedulerLocator;
    private CompositeDisposable _started;
    private bool IsStarted => _started.Count != 0;
    private readonly Lazy<BluetoothLEAdvertisementWatcher> _watcher;
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    private readonly BroadcastCache BroadcastCache;
    
    public ObservableCollection<CacheModel> Ads { get; }

    public MainWindowViewmodel(ISchedulerLocator schedulerLocator)
    {
        Ads = new ObservableCollection<CacheModel>();
        BroadcastCache = new BroadcastCache();
        _started = new CompositeDisposable();
        _schedulerLocator = schedulerLocator;
        _watcher = new Lazy<BluetoothLEAdvertisementWatcher>(()=>new BluetoothLEAdvertisementWatcher());
        StartCommand = new RelayCommand(OnStart, (_) => !IsStarted);
        StopCommand = new RelayCommand(OnStop, (_) => IsStarted);
    }

    private void OnStart(object? obj)
    {
        if (IsStarted)
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
        //watcher.Received += OnAdvertisementReceived;
        _started.Add(
        Observable.FromEventPattern<TypedEventHandler<BluetoothLEAdvertisementWatcher, BluetoothLEAdvertisementReceivedEventArgs>,BluetoothLEAdvertisementReceivedEventArgs>(
                h => watcher.Received += h,
                h => watcher.Received -= h)
            .ObserveOn(_schedulerLocator.Get("BluetoothLEAdvertisementReceived"))
            .Select(k=>k.EventArgs)
            .Subscribe(OnAdvertisementReceived)
        );
        
        _started.Add(
            BroadcastCache.Cache
                .ObserveOn(_schedulerLocator.GuiContext)
                .Subscribe(c=>
                {
                    Ads.Insert(0, c);
                    while (Ads.Count>MaxDisplayedAdvertisements)
                    {
                        Ads.RemoveAt(Ads.Count-1);
                    }
                })
        );

        watcher.Stopped += OnStopped;

        // Wait 5 seconds to make sure the device is really out of range
        watcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(5000);
        watcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(200);

        // Starting watching for advertisements
        watcher.Start();
    }

    private void OnAdvertisementReceived(BluetoothLEAdvertisementReceivedEventArgs args)
    {
        BroadcastCache.Add(args);
    }

    private void OnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        //TestText = $"Stopped. Reason:{args.Error}";
        _started.Clear();
    }

    private void OnStop(object? obj)
    {
        if (_watcher.IsValueCreated)
        {
            var watcher = _watcher.Value;
            watcher.Stop();
        }
        _started.Clear();
        Ads.Clear();
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