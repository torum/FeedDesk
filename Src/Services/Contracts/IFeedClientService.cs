using FeedDesk.Models;
using FeedDesk.Models.Clients;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeedDesk.Services.Contracts;

public interface IFeedClientService
{
    Task<HttpClientEntryItemCollectionResultWrapper> GetEntries(Uri entriesUrl, string feedId, string etag, CancellationToken token);
}
