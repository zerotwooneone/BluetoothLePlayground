﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ListenerGui.Main;

public class DataViewmodel : INotifyPropertyChanged
{
    public DateTimeOffset TimeStamp { get; }
    public string? Base64Data { get; }
    public string? Utf8Data { get; }
    public string? HexData { get; }

    public DataViewmodel(DateTimeOffset timeStamp,
        string? base64Data,
        string? utf8Data, 
        string? hexData)
    {
        TimeStamp = timeStamp;
        Base64Data = base64Data;
        Utf8Data = utf8Data;
        HexData = hexData;
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