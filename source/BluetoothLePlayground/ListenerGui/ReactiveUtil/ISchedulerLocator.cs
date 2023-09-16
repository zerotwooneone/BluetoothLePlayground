using System.Reactive.Concurrency;
using System.Threading;

namespace ListenerGui.ReactiveUtil;

public interface ISchedulerLocator
{
    IScheduler Get(string name);
    SynchronizationContext GuiContext { get; }
}