namespace ListenerGui.Main;

public class AdvertisementViewmodel
{
    public string BluetoothAddress { get; }
    public short RawSignalStrengthInDBm { get; }
    public DataViewmodel Data { get; }

    public AdvertisementViewmodel(string bluetoothAddress, short rawSignalStrengthInDBm, DataViewmodel data)
    {
        BluetoothAddress = bluetoothAddress;
        RawSignalStrengthInDBm = rawSignalStrengthInDBm;
        Data = data;
    }
}