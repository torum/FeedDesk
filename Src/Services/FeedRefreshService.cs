using FeedDesk.Models;
using FeedDesk.Services.Contracts;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeedDesk.Services;
public enum FeedRefreshStage
{
    Downloading,
    Saving
}

public sealed class FeedRefreshService : IFeedRefreshService
{
    private readonly IFeedClientService _feedClientService;
    private readonly IDataAccessService _dataAccessService;

    public FeedRefreshService(IFeedClientService feedClientService, IDataAccessService dataAccessService)
    {
        _feedClientService = feedClientService;
        _dataAccessService = dataAccessService;
    }


    //public async Task<FeedRefreshResult> RefreshAsync(FeedRefreshRequest request, CancellationToken cancellationToken)
    public async Task<FeedRefreshResult> RefreshAsync(FeedRefreshRequest request, Func<FeedRefreshStage, Task> reportStageAsync, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await reportStageAsync(FeedRefreshStage.Downloading);
        cancellationToken.ThrowIfCancellationRequested();

        var fetchResult = await _feedClientService.GetEntries(request.EndPoint, request.FeedId, request.Etag, cancellationToken);

        var fetchedAt = DateTime.Now;

        if (fetchResult.IsError || fetchResult.Entries.Count == 0)
        {
            return new FeedRefreshResult(fetchedAt, fetchResult, null);
        }
        cancellationToken.ThrowIfCancellationRequested();

        await reportStageAsync(FeedRefreshStage.Saving);
        cancellationToken.ThrowIfCancellationRequested();

        var saveResult = await Task.Run(
            () => _dataAccessService.InsertEntries(
                fetchResult.Entries,
                request.FeedId,
                request.Name,
                fetchResult.Title,
                fetchResult.Description,
                fetchResult.Updated,
                fetchResult.HtmlUri),
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        return new FeedRefreshResult(fetchedAt, fetchResult, saveResult);
    }
}