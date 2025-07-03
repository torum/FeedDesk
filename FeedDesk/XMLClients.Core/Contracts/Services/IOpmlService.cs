using System.Xml;
using XmlClients.Core.Models;

namespace XmlClients.Core.Contracts.Services;

public interface IOpmlService
{
    NodeFolder LoadOpml(XmlDocument xdoc);

    XmlDocument WriteOpml(NodeTree serviceRootNode);
}