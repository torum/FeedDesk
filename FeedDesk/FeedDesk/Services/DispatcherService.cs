using CommunityToolkit.WinUI;
using FeedDesk.Services.Contracts;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeedDesk.Services;

public class DispatcherService : IDispatcherService
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _queue;

    public DispatcherService(Microsoft.UI.Dispatching.DispatcherQueue queue)
    {
        _queue = queue;
    }

    public bool TryEnqueue(Action action) => _queue.TryEnqueue(() => action());


    // Awaitable action
    public Task EnqueueAsync(Action action) => _queue.EnqueueAsync(action);

    // Awaitable function that returns a value (e.g., getting text from a TextBox)
    public Task<T> EnqueueAsync<T>(Func<T> function) => _queue.EnqueueAsync(function);

    // Awaitable async function (e.g., showing a ContentDialog)
    public Task EnqueueAsync(Func<Task> function) => _queue.EnqueueAsync(function);

}
