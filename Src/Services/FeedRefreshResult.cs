using FeedDesk.Models;
using System;

namespace FeedDesk.Services;

public sealed record FeedRefreshRequest(string FeedId, Uri EndPoint, string Name, string Etag);

public sealed record FeedRefreshResult(DateTime FetchedAt, HttpClientEntryItemCollectionResultWrapper FetchResult, SqliteDataAccessInsertResultWrapper? SaveResult);