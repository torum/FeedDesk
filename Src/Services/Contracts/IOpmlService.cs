using FeedDesk.Models;
using System.Xml;

namespace FeedDesk.Services.Contracts;

public interface IOpmlService
{
    NodeFolder LoadOpml(XmlDocument xdoc);

    XmlDocument WriteOpml(NodeTree serviceRootNode);
}