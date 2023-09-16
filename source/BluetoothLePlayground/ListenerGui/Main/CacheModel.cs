using System.Collections.Generic;
using Windows.Devices.Bluetooth.Advertisement;

namespace ListenerGui.Main;

public struct CacheModel
{
    public BluetoothLEAdvertisementReceivedEventArgs Args { get; init; }
    public string BluetoothAddress { get; init; }
    public short RawSignalStrengthInDBm { get; init; }
    public IEnumerable<ManufacturerDataModel> ManufacturerData { get; init; }
}