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

    public BroadcastCache(int maxSize = DefaultMaxSize)
    {
        var maxSize1 = maxSize < 1 ? DefaultMaxSize: maxSize;
        _cacheSubject =
            new Subject<CacheModel>();

        _cacheSubject
            .AsObservable()
            .Take(maxSize1)
            .Subscribe(_ =>
            {
                Count++;
            });
        
        var cache = _cacheSubject
            .AsObservable()
            .Replay(maxSize1);
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

    private IEnumerable<ManufacturerDataModel> GetManuData(IList<BluetoothLEManufacturerData>? manufacturerData)
    {
        if (manufacturerData == null)
        {
            return Enumerable.Empty<ManufacturerDataModel>();
        }

        return manufacturerData.Select(d =>
        {
            var bytes = GetBytes(d.Data);
            var utf8Data = GetUtf(bytes);
            var hexData = GetHex(bytes);
            return new ManufacturerDataModel
            {
                CompanyId = d.CompanyId,
                Base64Data = GetBase64(bytes),
                Utf8Data = utf8Data,
                HexData = hexData
            };
        }).ToArray();
    }

    private string GetHex(byte[] bytes)
    {
        return Convert.ToHexString(bytes);
    }

    private string GetUtf(byte[] bytes)
    {
        var stringValue = System.Text.Encoding.Default.GetString(bytes);
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return string.Empty;
        }

        return stringValue;
    }

    public string GetBase64(byte[] bytes)
    {
        return Convert.ToBase64String(bytes);
    }

    private static byte[] GetBytes(IBuffer? buffer)
    {
        if (buffer == null)
        {
            return Array.Empty<byte>();
        }
        var reader = DataReader.FromBuffer(buffer);
        var bytes = new byte[buffer.Length];
        reader.ReadBytes(bytes);
        return bytes;
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