using XmlClients.Core.Models;
using XmlClients.Core.Models.Clients;

namespace XmlClients.Core.Contracts.Services;

public interface IFeedClientService
{
    BaseClient BaseClient
    {
        get;
    }

    Task<HttpClientEntryItemCollectionResultWrapper> GetEntries(Uri entriesUrl, string feedId);
}
