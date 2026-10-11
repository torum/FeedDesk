using System.Xml;

namespace FeedDesk.Services.Contracts;

public interface IFeedTreePersistenceService
{
    XmlDocument? Load();
    void Save(XmlDocument document);
}