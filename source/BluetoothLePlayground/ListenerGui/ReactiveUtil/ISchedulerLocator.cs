using System.Reactive.Concurrency;

namespace ListenerGui.ReactiveUtil;

public interface ISchedulerLocator
{
    IScheduler Get(string name);
}