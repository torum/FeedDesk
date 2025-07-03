using XmlClients.Core.Models;
using XmlClients.Core.Services;

namespace XmlClients.Core.Contracts.Services;

public delegate void AutoDiscoveryStatusUpdateEventHandler(AutoDiscoveryService sender, string data);

public interface IAutoDiscoveryService
{
    event AutoDiscoveryStatusUpdateEventHandler? StatusUpdate;

    Task<ServiceResultBase> DiscoverService(Uri addr, bool isFeed);

    Task<ServiceResultBase> DiscoverServiceWithAuth(Uri addr, string userName, string apiKey, AuthTypes authType);

}