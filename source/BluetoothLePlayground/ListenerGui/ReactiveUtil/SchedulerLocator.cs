using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace ListenerGui.ReactiveUtil;

public class SchedulerLocator : ISchedulerLocator
{
    private readonly TaskPoolScheduler _scheduler;
    
    /// <summary>
    /// this will cancel ALL tasks from this scheduler
    /// </summary>
    private readonly CancellationTokenSource _cts;

    public SchedulerLocator()
    {
        _cts = new CancellationTokenSource();
        _scheduler = new TaskPoolScheduler(new TaskFactory(_cts.Token));
    }

    public IScheduler Get(string name)
    {
        return _scheduler;
    }

    public SynchronizationContext GuiContext => SynchronizationContext.Current;
}