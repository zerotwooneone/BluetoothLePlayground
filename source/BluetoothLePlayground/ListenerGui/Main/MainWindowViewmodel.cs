using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Foundation;
using ListenerGui.ReactiveUtil;
using ListenerGui.WpfUtil;

namespace ListenerGui.Main;

public class MainWindowViewmodel: INotifyPropertyChanged
{
    private readonly ISchedulerLocator _schedulerLocator;
    private CompositeDisposable _started;
    private bool IsStarted => _started.Count != 0;
    private readonly Lazy<BluetoothLEAdvertisementWatcher> _watcher;
    private string _testText;
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    private readonly BroadcastCache BroadcastCache;

    public string TestText
    {
        get => _testText;
        private set => SetField(ref _testText, value);
    }

    public MainWindowViewmodel(ISchedulerLocator schedulerLocator)
    {
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
        var xpart = args.Advertisement.ManufacturerData.ToArray();
        var ypart = xpart.Length > 0 ? string.Join(";",xpart.Select(x=>x.CompanyId.ToString())) : string.Empty;
        TestText = $"ADDR:{args.BluetoothAddress} Str:{args.RawSignalStrengthInDBm} x:{ypart}";
    }

    private void OnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        TestText = $"Stopped. Reason:{args.Error}";
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

internal class BroadcastCache
{
    private const int DefaultMaxSize = 10000;
    public int Count => _collectionSubject.Value.Count();
    private readonly BehaviorSubject<IEnumerable<BluetoothLEAdvertisementReceivedEventArgs>> _collectionSubject;

    public IObservable<IEnumerable<BluetoothLEAdvertisementReceivedEventArgs>> Cache =>
        _collectionSubject.AsObservable();

    public IObservable<IReadOnlyDictionary<ulong, IEnumerable<BluetoothLEAdvertisementReceivedEventArgs>>>
        AdvertisementsByAddress =>
        Cache
            .Select(list => list.GroupBy(arg => arg.BluetoothAddress).ToDictionary(g => g.Key,
                g => (IEnumerable<BluetoothLEAdvertisementReceivedEventArgs>) g.ToArray()));

    private readonly int _maxSize;
    //private readonly ConcurrentQueue<BluetoothLEAdvertisementReceivedEventArgs> _queue;

    public BroadcastCache(int maxSize = DefaultMaxSize)
    {
        _maxSize = maxSize < 1 ? DefaultMaxSize: maxSize;
        //_queue = new ConcurrentQueue<BluetoothLEAdvertisementReceivedEventArgs>();
        _collectionSubject =
            new BehaviorSubject<IEnumerable<BluetoothLEAdvertisementReceivedEventArgs>>(
                Enumerable.Empty<BluetoothLEAdvertisementReceivedEventArgs>());
    }
    public void Add(BluetoothLEAdvertisementReceivedEventArgs args)
    {
        _collectionSubject.OnNext(_collectionSubject.Value.Append(args).TakeLast(_maxSize).ToArray());
    }
}