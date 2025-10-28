using FeedDesk.Models;
using FeedDesk.Models.Clients;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FeedDesk.Services.Contracts;

public interface IFeedClientService
{
    BaseClient BaseClient {get;}

    Task<HttpClientEntryItemCollectionResultWrapper> GetEntries(Uri entriesUrl, string feedId, CancellationToken token);
}
