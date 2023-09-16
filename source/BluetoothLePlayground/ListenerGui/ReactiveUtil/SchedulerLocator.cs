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

    private SynchronizationContext? _synchronizationContext;
    public SynchronizationContext GuiContext
    {
        get
        {
            if (_synchronizationContext == null && SynchronizationContext.Current != null)
            {
                //this is a horrible hack, not sure why we need this
                _synchronizationContext = SynchronizationContext.Current;
            }
            return SynchronizationContext.Current ?? _synchronizationContext;
        }
    }
}