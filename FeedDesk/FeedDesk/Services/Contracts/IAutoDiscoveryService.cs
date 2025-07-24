using FeedDesk.Models;
using FeedDesk.Services;
using System;
using System.Threading.Tasks;

namespace FeedDesk.Services.Contracts;

public delegate void AutoDiscoveryStatusUpdateEventHandler(AutoDiscoveryService sender, string data);

public interface IAutoDiscoveryService
{
    event AutoDiscoveryStatusUpdateEventHandler? StatusUpdate;

    Task<ServiceResultBase> DiscoverService(Uri addr, bool isFeed);

    Task<ServiceResultBase> DiscoverServiceWithAuth(Uri addr, string userName, string apiKey, AuthTypes authType);

}