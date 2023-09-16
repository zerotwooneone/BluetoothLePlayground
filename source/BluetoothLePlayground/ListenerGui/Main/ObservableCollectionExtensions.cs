using System;
using System.Collections.ObjectModel;

namespace ListenerGui.Main;

public static class ObservableCollectionExtensions
{
    public static ObservableCollection<T> DisposeAll<T>(this ObservableCollection<T> disposables) where T:IDisposable
    {
        if (disposables == null)
        {
            return disposables;
        }

        foreach (var disposable in disposables)
        {
            try
            {
                disposable.Dispose();
            }
            catch 
            {
                //nothing
            }
        }

        return disposables;
    }
}