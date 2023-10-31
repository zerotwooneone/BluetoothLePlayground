using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
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
    private const int MaxBroadcasters = 100;
    private readonly ISchedulerLocator _schedulerLocator;
    private CompositeDisposable _started;
    private CompositeDisposable _tab0Disposables;
    private CompositeDisposable _tab1Disposables;
    private bool IsStarted => _started.Count != 0;
    private readonly Lazy<BluetoothLEAdvertisementWatcher> _watcher;
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    private readonly BroadcastCache BroadcastCache;
    private int _selectedTabIndex;
    private IList _selectedBroadcasters;

    public ObservableCollection<BroadcasterViewmodel> Broadcasters { get; }

    public IList SelectedBroadcasters
    {
        get => _selectedBroadcasters;
        set
        {
            if (SetField(ref _selectedBroadcasters, value))
            {
                int x = 0;
            }
        }
    }

    private static readonly ManufacturerDataModel DefaultManufacturerDataModel = default;
    public int SelectedTabIndex
    {
        private get => _selectedTabIndex;
        set
        {
            var previousTabIndex = _selectedTabIndex;
            _selectedTabIndex = value;

            Advertisements.Clear();
            
            _tab0Disposables.Clear();

            if (previousTabIndex <= 0 || _selectedTabIndex <= 0)
            {
                Broadcasters.DisposeAll().Clear();
                _tab1Disposables.Clear();
            }

            switch (value)
            {
                case 1:
                case 2:
                    _tab1Disposables.Add(
                        BroadcastCache.Cache
                            .ObserveOn(_schedulerLocator.Get("broadcast cache"))
                            .GroupBy(c=>c.BluetoothAddress)
                            .Select(g=>new BroadcasterViewmodel(g.Key, _schedulerLocator, g,10, 10))
                            .ObserveOn(_schedulerLocator.GuiContext)
                            .Select(vm =>
                            {
                                Broadcasters.Add(vm);

                                //need ToArray to avoid exception changing the list size during the next operation
                                return Broadcasters.ToArray();
                            })
                            .ObserveOn(_schedulerLocator.Get("broadcaster overflow"))
                            .Select(array =>
                            {
                                var sorted = array.OrderByDescending(s => s.LastAdvert);
                                return sorted.Skip(MaxBroadcasters).ToArray();
                            })
                            .ObserveOn(_schedulerLocator.GuiContext)
                            .SelectMany(toRemove =>
                            {
                                foreach (var remove in toRemove)
                                {
                                    Broadcasters.Remove(remove);
                                }

                                return toRemove;
                            })
                            .ObserveOn(_schedulerLocator.Get("broadcaster dispose"))
                            .Subscribe(vm =>
                            {
                                vm.Dispose();
                            })
                        );
                    break;
                default:
                case 0:
                    _tab0Disposables.Add(
                        BroadcastCache.Cache
                            .ObserveOn(_schedulerLocator.Get("convert advertisement"))
                            .Select(c=>
                            {
                                var manufacturerData = c.ManufacturerData.ToArray();
                                var firstData = manufacturerData.FirstOrDefault(d => !DefaultManufacturerDataModel.Equals(d));
                                var dataViewmodel = new DataViewmodel(c.Args.Timestamp, firstData.Base64Data, firstData.Utf8Data, firstData.HexData);
                                return new AdvertisementViewmodel(c.BluetoothAddress, c.RawSignalStrengthInDBm, dataViewmodel);
                            })
                            .ObserveOn(_schedulerLocator.GuiContext)
                            .Select(c =>
                            {
                                Advertisements.Insert(0, c);
                                return Advertisements.ToArray();
                            })
                            .ObserveOn(_schedulerLocator.Get("find old ads"))
                            .Select(array => array.Skip(MaxDisplayedAdvertisements).ToArray())
                            .ObserveOn(_schedulerLocator.GuiContext)
                            .Subscribe(toRemove=>
                            {
                                foreach (var remove in toRemove)
                                {
                                    Advertisements.Remove(remove);
                                }
                            })
                    );
                    break;
            }
        }
    }

    public ObservableCollection<AdvertisementViewmodel> Advertisements { get; }

    public MainWindowViewmodel(ISchedulerLocator schedulerLocator)
    {
        _tab0Disposables = new CompositeDisposable();
        _tab1Disposables = new CompositeDisposable();
        Advertisements = new ObservableCollection<AdvertisementViewmodel>();
        Broadcasters = new ObservableCollection<BroadcasterViewmodel>();
        SelectedBroadcasters = new ObservableCollection<BroadcasterViewmodel>();
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