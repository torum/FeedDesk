using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeedDesk.Services.Contracts;

public interface IFeedRefreshService
{
    //Task<FeedRefreshResult> RefreshAsync(FeedRefreshRequest request, CancellationToken cancellationToken);
    Task<FeedRefreshResult> RefreshAsync(FeedRefreshRequest request, Func<FeedRefreshStage, Task> reportStageAsync, CancellationToken cancellationToken);
}