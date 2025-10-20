using System.Xml;
using FeedDesk.Services.Contracts;
using FeedDesk.Models;
using System;

namespace FeedDesk.Services;

public class OpmlService : IOpmlService
{
    // Import OPML
    public NodeFolder LoadOpml(XmlDocument xdoc)
    {
        var opmlTitle = "OPML imported feeds";

        // Dummy Root Folder
        var dummyFolder = new NodeFolder(opmlTitle)
        {
            IsSelected = true,
            IsExpanded = true,
            Parent = null // for now
        };

        if (xdoc is null)
            return dummyFolder;

        var opmlTitleNode = xdoc.SelectSingleNode("//opml/head/title");
        if (opmlTitleNode is not null)
        {
            dummyFolder.Name = opmlTitleNode.InnerText;
        }

        var bodyNode = xdoc.SelectSingleNode("//opml/body");

        if (bodyNode is null)
            return dummyFolder;

        if (bodyNode.ChildNodes.Count > 0)
        {
            ProcessOutlineChild(bodyNode.ChildNodes, dummyFolder);
        }

        return dummyFolder;
    }

    private static void ProcessOutlineChild(XmlNodeList childs, NodeFolder parent)
    {
        foreach (XmlNode outline in childs)
        {
            if (!outline.LocalName.Equals("outline"))
                continue;

            var title = "empty";

            if (outline.Attributes != null)
            {
                if (outline.Attributes["text"] is not null)
                {
                    var s = outline.Attributes["text"]?.InnerText;
                    if (!string.IsNullOrEmpty(s))
                    {
                        title = s;
                    }
                }


                if (outline.Attributes["xmlUrl"] is not null)
                {
                    var xmlUrl = outline.Attributes["xmlUrl"]?.Value;

                    if (!string.IsNullOrEmpty(xmlUrl))
                    {
                        try
                        {
                            NodeFeed feed = new(title, new Uri(xmlUrl));

                            if (outline.Attributes["htmlUrl"] is not null)
                            {
                                var htmlUrl = outline.Attributes["htmlUrl"]?.Value;

                                if (htmlUrl != null)
                                {
                                    feed.HtmlUri = new Uri(htmlUrl);
                                }
                            }

                            feed.Parent = parent;

                            parent.Children.Add(feed);

                            continue;
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
            }

            if (outline.ChildNodes.Count > 0)
            {
                NodeFolder folder = new(title)
                {
                    IsSelected = true,
                    IsExpanded = true,

                    Parent = parent
                };

                parent.Children.Add(folder);

                ProcessOutlineChild(outline.ChildNodes, folder);
            }
        }
    }

    // Export OPML
    public XmlDocument WriteOpml(NodeTree serviceRootNode)
    {
        XmlDocument xdoc = new();
        XmlDeclaration xdec = xdoc.CreateXmlDeclaration("1.0", "UTF-8", null);
        xdoc.AppendChild(xdec);

        XmlElement eleRoot = xdoc.CreateElement(string.Empty, "opml", string.Empty);
        xdoc.AppendChild(eleRoot);

        XmlAttribute verAttr = xdoc.CreateAttribute("version");
        verAttr.Value = "1.0";
        eleRoot.SetAttributeNode(verAttr);

        XmlElement eleHead = xdoc.CreateElement(string.Empty, "head", string.Empty);
        XmlElement eleTitle = xdoc.CreateElement(string.Empty, "title", string.Empty);
        eleTitle.InnerText = "Exported from XmlClients at " + DateTime.Now.ToString();
        eleHead.AppendChild(eleTitle);
        eleRoot.AppendChild(eleHead);

        XmlElement eleBody = xdoc.CreateElement(string.Empty, "body", string.Empty);
        eleRoot.AppendChild(eleBody);

        foreach (NodeTree nt in serviceRootNode.Children)
        {
            if ((nt is NodeFeed) || (nt is NodeFolder))
            {
                ProcessNodeChild(nt, xdoc, eleBody);
            }
        }

        return xdoc;
    }

    private static void ProcessNodeChild(NodeTree nt, XmlDocument xdoc, XmlElement parent)
    {
        if (nt is NodeFeed feed)
        {
            XmlElement eleOutline = xdoc.CreateElement(string.Empty, "outline", string.Empty);
            parent.AppendChild(eleOutline);

            XmlAttribute typeAttr = xdoc.CreateAttribute("type");
            typeAttr.Value = "rss";
            eleOutline.SetAttributeNode(typeAttr);

            XmlAttribute textAttr = xdoc.CreateAttribute("text");
            textAttr.Value = nt.Name;
            eleOutline.SetAttributeNode(textAttr);

            XmlAttribute titleAttr = xdoc.CreateAttribute("title");
            titleAttr.Value = nt.Name;
            eleOutline.SetAttributeNode(titleAttr);

            XmlAttribute xmlUriAttr = xdoc.CreateAttribute("xmlUrl");
            xmlUriAttr.Value = feed.EndPoint.AbsoluteUri;
            eleOutline.SetAttributeNode(xmlUriAttr);

            if (feed.HtmlUri is not null)
            {
                XmlAttribute htmlUriAttr = xdoc.CreateAttribute("htmlUrl");
                htmlUriAttr.Value = feed.HtmlUri.AbsoluteUri;
                eleOutline.SetAttributeNode(htmlUriAttr);
            }
        }
        else if (nt is NodeFolder folder)
        {
            XmlElement eleOutline = xdoc.CreateElement(string.Empty, "outline", string.Empty);
            parent.AppendChild(eleOutline);

            XmlAttribute textAttr = xdoc.CreateAttribute("text");
            textAttr.Value = nt.Name;
            eleOutline.SetAttributeNode(textAttr);

            XmlAttribute titleAttr = xdoc.CreateAttribute("title");
            titleAttr.Value = nt.Name;
            eleOutline.SetAttributeNode(titleAttr);

            if (folder.Children.Count > 0)
            {
                foreach (NodeTree ntc in nt.Children)
                {
                    ProcessNodeChild(ntc, xdoc, eleOutline);
                }
            }
        }
    }
}
