using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PCL.Core.IO.Download;

public class NDlScheduler
{
    private readonly Channel<NDlTask> _queue = Channel.CreateUnbounded<NDlTask>();
    private readonly List<Task> _workers = [];
    private readonly object _syncRoot = new();
    private int _workerCount;

    public NDlScheduler(int maxConcurrency = 4)
    {
        MaxConcurrency = Math.Max(1, maxConcurrency);
    }

    public int MaxConcurrency { get; }
    public ObservableCollection<NDlTask> Tasks { get; } = [];

    public event EventHandler<NDlProgress>? ProgressChanged;

    public NDlTask Enqueue(NDlRequest request)
    {
        var task = new NDlTask(request);
        task.ProgressChanged += (_, progress) => ProgressChanged?.Invoke(task, progress);
        lock (_syncRoot)
        {
            Tasks.Add(task);
            EnsureWorkers();
        }

        _queue.Writer.TryWrite(task);
        return task;
    }

    public IReadOnlyList<NDlTask> Snapshot()
    {
        lock (_syncRoot)
            return Tasks.ToArray();
    }

    public void CancelAll()
    {
        foreach (var task in Snapshot())
            task.Cancel();
    }

    public bool Cancel(string id)
    {
        var task = Snapshot().FirstOrDefault(task => string.Equals(task.Request.Id, id, StringComparison.OrdinalIgnoreCase));
        if (task is null)
            return false;
        task.Cancel();
        return true;
    }

    private void EnsureWorkers()
    {
        while (_workerCount < MaxConcurrency)
        {
            _workerCount++;
            _workers.Add(Task.Run(WorkerAsync));
        }
    }

    private async Task WorkerAsync()
    {
        await foreach (var task in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (task.State == NDlTaskState.Cancelled)
                continue;

            await task.RunAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
