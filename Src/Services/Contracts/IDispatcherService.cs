using System;
using System.Threading.Tasks;

namespace FeedDesk.Services.Contracts
{
    public interface IDispatcherService
    {
        bool TryEnqueue(Action action);
        Task EnqueueAsync(Action action);
        Task<T> EnqueueAsync<T>(Func<T> func);
        Task EnqueueAsync(Func<Task> func);
    }
}
