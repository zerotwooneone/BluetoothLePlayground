using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using ListenerGui.ReactiveUtil;

namespace ListenerGui.Main;

public class BroadcasterViewmodel : INotifyPropertyChanged, IDisposable
{
    public string Id { get; }
    public ObservableCollection<DataViewmodel> Data { get; }
    public ObservableCollection<CacheModel> Adverts { get; }
    private readonly CompositeDisposable _subscriptions;
    private DateTimeOffset _lastAdvert;
    private short _signalDbm;
    private readonly List<short> _signalStrengths;
    private short _signalAverage;

    public DateTimeOffset LastAdvert
    {
        get => _lastAdvert;
        private set => SetField(ref _lastAdvert, value);
    }

    public short SignalDbm
    {
        get => _signalDbm;
        private set => SetField(ref _signalDbm, value);
    }

    public short SignalAverage
    {
        get => _signalAverage;
        set => SetField(ref _signalAverage, value);
    }

    public BroadcasterViewmodel(
        string id,
        ISchedulerLocator schedulerLocator,
        IObservable<CacheModel> cache,
        int maxAdverts,
        int signalsToAverage)
    {
        _signalStrengths = new List<short>();
        _subscriptions = new CompositeDisposable();
        Id = id;
        Adverts = new ObservableCollection<CacheModel>();
        Data = new ObservableCollection<DataViewmodel>();
        LastAdvert = DateTimeOffset.Now;
        
        _subscriptions.Add(
            cache
                .ObserveOn(schedulerLocator.GuiContext)
                .Subscribe(m =>
                {
                    SignalDbm = m.RawSignalStrengthInDBm;
                    LastAdvert = m.Args.Timestamp;
                    Adverts.Insert(0, m);
                    while (Adverts.Count > maxAdverts)
                    {
                        Adverts.RemoveAt(Adverts.Count - 1);
                    }

                    var manufacturerDataModel = m.ManufacturerData.FirstOrDefault();
                    Data.Insert(0, new DataViewmodel(
                        m.Args.Timestamp,
                        manufacturerDataModel.Base64Data,
                        manufacturerDataModel.Utf8Data));
                    while (Data.Count>maxAdverts)
                    {   
                        Data.RemoveAt(Data.Count-1);
                    }
                })
        );
        _subscriptions.Add(
            cache
                .ObserveOn(schedulerLocator.Get("average broadcaster"))
                .Select(c =>
                {
                    _signalStrengths.Insert(0,c.RawSignalStrengthInDBm);
                    while (_signalStrengths.Count > signalsToAverage)
                    {
                        _signalStrengths.RemoveAt(_signalStrengths.Count-1);
                    }

                    return (short)_signalStrengths.Average(c=>c);
                })
                .ObserveOn(schedulerLocator.GuiContext)
                .Subscribe(avg =>
                {
                    SignalAverage = avg;
                }));
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

    public void Dispose()
    {
        _subscriptions.Dispose();
    }
}