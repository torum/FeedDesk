using System.Xml;
using FeedDesk.Models;

namespace FeedDesk.Services.Contracts;

public interface IOpmlService
{
    NodeFolder LoadOpml(XmlDocument xdoc);

    XmlDocument WriteOpml(NodeTree serviceRootNode);
}