using System;

namespace FeedDesk.Models;

public enum FeedRefreshStage
{
    Downloading,
    Saving
}

public sealed record FeedRefreshRequest(string FeedId, Uri EndPoint, string Name, string Etag);

public sealed record FeedRefreshResult(DateTime FetchedAt, HttpClientEntryItemCollectionResultWrapper FetchResult, SqliteDataAccessInsertResultWrapper? SaveResult);