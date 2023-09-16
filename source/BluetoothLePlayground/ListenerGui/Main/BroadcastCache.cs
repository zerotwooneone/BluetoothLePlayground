using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace ListenerGui.Main;

internal class BroadcastCache
{
    private const int DefaultMaxSize = 10000;
    public int Count { get; private set; }
    private readonly Subject<CacheModel> _cacheSubject;

    public IObservable<CacheModel> Cache { get; private set; }
        
    private readonly int _maxSize;

    public BroadcastCache(int maxSize = DefaultMaxSize)
    {
        _maxSize = maxSize < 1 ? DefaultMaxSize: maxSize;
        _cacheSubject =
            new Subject<CacheModel>();

        _cacheSubject
            .AsObservable()
            .Take(_maxSize)
            .Subscribe(_ =>
            {
                Count++;
            });
        
        var cache = _cacheSubject
            .AsObservable()
            .Replay(_maxSize);
        cache.Connect();
        Cache = cache;
    }
    public void Add(BluetoothLEAdvertisementReceivedEventArgs args)
    {
        var newModel = new CacheModel
        {
            Args = args,
            BluetoothAddress = GetMac(args.BluetoothAddress),
            RawSignalStrengthInDBm = args.RawSignalStrengthInDBm,
            ManufacturerData = GetManuData(args.Advertisement.ManufacturerData)
        };
        _cacheSubject.OnNext(newModel);
    }

    private IEnumerable<ManufacturerDataModel> GetManuData(IList<BluetoothLEManufacturerData> manufacturerData)
    {
        if (manufacturerData == null)
        {
            return Enumerable.Empty<ManufacturerDataModel>();
        }

        return manufacturerData.Select(d =>
        {
            return new ManufacturerDataModel
            {
                CompanyId = d.CompanyId,
                Base64Data = GetBase64(d.Data)
            };
        }).ToArray();
    }
    
    public string GetBase64(IBuffer buffer)
    {
        if (buffer == null)
        {
            return string.Empty;
        }
        var reader = DataReader.FromBuffer(buffer);
        var bytes = new byte[buffer.Length];
        reader.ReadBytes(bytes);
        return System.Convert.ToBase64String(bytes);
    }

    private string GetMac(ulong bluetoothAddress)
    {
        // return string.Join(":",
        //     BitConverter.GetBytes(bluetoothAddress).Reverse()
        //         .Select(b => b.ToString("X2"))).Substring(6);
        var bytes = BitConverter
            .GetBytes(bluetoothAddress);
        var reversed = bytes
            .Reverse()
            .ToArray();
        var substring = BitConverter.ToString(reversed, 2,6);
        return substring.Replace('-',':');
    }
}