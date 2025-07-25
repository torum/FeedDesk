using System;
using System.Xml;
using static FeedDesk.Models.ErrorObject;

namespace FeedDesk.Models;

public partial class FeedTreeBuilder : NodeRoot
{
    public FeedTreeBuilder() {
        Name = "NodeRoot";
    }

    // Loads service tree.
    public void LoadXmlDoc(XmlDocument doc)
    {
        if (doc == null)
            return;
        if (doc.DocumentElement == null)
            return;

        var accountList = doc.SelectNodes("//Accounts");
        if (accountList == null) 
            return;

        foreach (XmlNode a in accountList)
        {
            // Loop through the top level childs.
            var serviceList = a.ChildNodes;
            if (serviceList == null)
                continue;

            foreach (XmlNode s in serviceList)
            {
                if (s.LocalName.Equals("Service"))
                {
                    var accountName = s.Attributes?["Name"]?.Value;

                    var userName = s.Attributes?["UserName"]?.Value;
                    var userPassword = s.Attributes?["UserPassword"]?.Value;
                    var endpoint = s.Attributes?["EndPoint"]?.Value;
                    var api = (s.Attributes?["Api"] != null) ? s.Attributes?["Api"]?.Value : "Unknown"; 
                    var tp = (s.Attributes?["Type"] != null) ? s.Attributes?["Type"]?.Value : "Unknown";

                    var selecteds = string.IsNullOrEmpty(s.Attributes?["Selected"]?.Value) ? "" : s.Attributes?["Selected"]?.Value;
                    var expandeds = string.IsNullOrEmpty(s.Attributes?["Expanded"]?.Value) ? "" : s.Attributes?["Expanded"]?.Value;
                    var isSelecteds = (selecteds == "true") ? true : false;
                    var isExpandeds = (expandeds == "true") ? true : false;

                    ServiceTypes stp;
                    switch (tp)
                    {
                        case "AtomPub":
                            stp = ServiceTypes.AtomPub;
                            break;
                        case "Feed":
                            stp = ServiceTypes.Feed;
                            break;
                        case "XML-RPC":
                            stp = ServiceTypes.XmlRpc;
                            break;
                        case "AtomAPI":
                            stp = ServiceTypes.AtomApi;
                            break;
                        default:
                            stp = ServiceTypes.Unknown;
                            break;
                    }

                    ApiTypes at;
                    switch (api)
                    {
                        case "AtomPub":
                            at = ApiTypes.atAtomPub;
                            break;
                        case "AtomFeed":
                            at = ApiTypes.atFeed;
                            break;
                        case "RssFeed":
                            at = ApiTypes.atFeed;
                            break;
                        case "Feed":
                            at = ApiTypes.atFeed;
                            break;
                        case "XML-RPC_MovableType":
                            at = ApiTypes.atXMLRPC_MovableType;
                            break;
                        case "XML-RPC_WordPress":
                            at = ApiTypes.atXMLRPC_WordPress;
                            break;
                        case "AtomAPI":
                            at = ApiTypes.atAtomApi;
                            break;
                        default:
                            at = ApiTypes.atUnknown;
                            break;
                    }

                    var viewType = (s.Attributes?["ViewType"] != null) ? s.Attributes?["ViewType"]?.Value : "Cards";
                    ViewTypes vt;
                    switch (viewType)
                    {
                        case "Cards":
                            vt = ViewTypes.vtCards;
                            break;
                        case "Magazine":
                            vt = ViewTypes.vtMagazine;
                            break;
                        case "ThreePanes":
                            vt = ViewTypes.vtThreePanes;
                            break;
                        default:
                            vt = ViewTypes.vtCards;
                            break;
                    }

                    if (stp == ServiceTypes.Feed)
                        continue;

                    if ((!string.IsNullOrEmpty(accountName)) && (!string.IsNullOrEmpty(userName)) && (!string.IsNullOrEmpty(userPassword)) && (!string.IsNullOrEmpty(endpoint)))
                    {
                        NodeService account = new NodeService(accountName, userName, userPassword, new Uri(endpoint), at, stp);
                        account.IsSelected = isSelecteds;
                        account.IsExpanded = isExpandeds;
                        account.Parent = this;

                        account.ServiceType = stp;
                        account.Api = at;

                        account.ViewType = vt;

                        var collectionList = s.SelectNodes("Collection");
                        if (collectionList != null)
                        {
                            foreach (XmlNode c in collectionList)
                            {
                                var collectionName = c.Attributes?["Name"]?.Value;
                                var selectedc = string.IsNullOrEmpty(c.Attributes?["Selected"]?.Value) ? "" : c.Attributes["Selected"]?.Value;
                                var expandedc = string.IsNullOrEmpty(c.Attributes?["Expanded"]?.Value) ? "" : c.Attributes["Expanded"]?.Value;
                                var isSelectedc = (selectedc == "true") ? true : false;
                                var isExpandedc = (expandedc == "true") ? true : false;

                                var collectionHref = (c.Attributes?["Href"] != null) ? c.Attributes["Href"]?.Value : "";

                                var collectionId = (c.Attributes?["Id"] != null) ? c.Attributes["Id"]?.Value : "";

                                if ((!string.IsNullOrEmpty(collectionName)) && (!string.IsNullOrEmpty(collectionHref)))
                                {
                                    NodeEntryCollection? entries = null;
                                    if (collectionId != null)
                                    {
                                        // TODO:
                                        switch (account.Api)
                                        {
                                            case ApiTypes.atAtomPub:
                                                entries = new NodeAtomPubEntryCollection(collectionName, new Uri(collectionHref), collectionId);
                                                break;
                                            case ApiTypes.atXMLRPC_MovableType:
                                                entries = new NodeXmlRpcEntryCollection(collectionName, new Uri(collectionHref), collectionId);
                                                break;
                                            case ApiTypes.atXMLRPC_WordPress:
                                                entries = new NodeXmlRpcEntryCollection(collectionName, new Uri(collectionHref), collectionId);
                                                break;
                                                //case ApiTypes.atAtomAPI:
                                                //    break;
                                        }
                                    }

                                    if (entries == null)
                                        continue;

                                    if (entries is NodeAtomPubEntryCollection napec)
                                    {
                                        if (c.Attributes?["CategoriesUri"] != null)
                                        {
                                            var catsUrl = c.Attributes["CategoriesUri"]?.Value;
                                            if (!string.IsNullOrEmpty(catsUrl))
                                            {
                                                try
                                                {
                                                    Uri catsUri = new(catsUrl);
                                                    napec.CategoriesUri = catsUri;
                                                }
                                                catch { }
                                            }
                                        }

                                        var catFixed = c.Attributes?["IsCategoryFixed"]?.Value;
                                        if (!string.IsNullOrEmpty(catFixed))
                                        {
                                            if (catFixed == "true")
                                            {
                                                napec.IsCategoryFixed = true;
                                            }
                                        }

                                        XmlNodeList? acceptList = c.SelectNodes("Accept");
                                        if (acceptList != null)
                                        {
                                            foreach (XmlNode act in acceptList)
                                            {
                                                napec.AcceptTypes.Add(act.InnerText);
                                            }
                                        }
                                    }

                                    XmlNodeList? categoryList = c.SelectNodes("Category");
                                    if (categoryList != null)
                                    {
                                        foreach (XmlNode t in categoryList)
                                        {
                                            var categoryName = t.Attributes?["Name"]?.Value;
                                            var selectedt = string.IsNullOrEmpty(t.Attributes?["Selected"]?.Value) ? "" : t.Attributes["Selected"]?.Value;
                                            var expandedt = string.IsNullOrEmpty(t.Attributes?["Expanded"]?.Value) ? "" : t.Attributes["Expanded"]?.Value;
                                            var isSelectedt = (selectedc == "true") ? true : false;
                                            var isExpandedt = (expandedc == "true") ? true : false;

                                            if (!string.IsNullOrEmpty(categoryName))
                                            {
                                                NodeCategory? category;

                                                switch (account.Api)
                                                {
                                                    case ApiTypes.atAtomPub:
                                                        category = new NodeAtomPubCategory(categoryName);
                                                        break;
                                                    case ApiTypes.atXMLRPC_MovableType:
                                                        category = new NodeXmlRpcMTCategory(categoryName);
                                                        break;
                                                    case ApiTypes.atXMLRPC_WordPress:
                                                        category = new NodeXmlRpcWPCategory(categoryName);
                                                        break;
                                                    //case ApiTypes.atAtomAPI:
                                                    //    break;
                                                    default: category = null; break;
                                                }

                                                if (category == null)
                                                    return;

                                                if (category is NodeAtomPubCategory napc)
                                                {
                                                    napc.Term = categoryName;

                                                    var categoryScheme = t.Attributes?["Scheme"]?.Value;
                                                    napc.Scheme = categoryScheme;
                                                }


                                                category.IsSelected = isSelectedc;
                                                category.IsExpanded = isExpandedc;
                                                category.Parent = entries;

                                                entries.Children.Add(category);
                                            }
                                        }
                                    }

                                    entries.IsSelected = isSelectedc;
                                    entries.IsExpanded = isExpandedc;
                                    entries.Parent = account;

                                    account.Children.Add(entries);
                                }
                            }

                        }
                        this.Children.Add(account);
                    }
                }
                else if (s.LocalName.Equals("Feed"))
                {
                    NodeFeed? feed = LoadXmlChildFeed(s);
                    if (feed == null)
                        continue;

                    feed.Parent = this;

                    if (feed != null)
                        this.Children.Add(feed);

                }
                else if (s.LocalName.Equals("Folder"))
                {
                    NodeFolder? folder = LoadXmlChildFolder(s);
                    if (folder == null)
                        continue;

                    folder.Parent = this;

                    if (folder != null)
                        this.Children.Add(folder);
                }
            }

            break;
        }
    }

    private NodeFolder? LoadXmlChildFolder(XmlNode node)
    {
        var folderName = node.Attributes?["Name"]?.Value;

        if (!string.IsNullOrEmpty(folderName))
        {
            var selecteds = string.IsNullOrEmpty(node.Attributes?["Selected"]?.Value) ? "" : node.Attributes["Selected"]?.Value;
            var expandeds = string.IsNullOrEmpty(node.Attributes?["Expanded"]?.Value) ? "" : node.Attributes["Expanded"]?.Value;
            var isSelecteds = (selecteds == "true") ? true : false;
            var isExpandeds = (expandeds == "true") ? true : false;

            NodeFolder folder = new NodeFolder(folderName);
            folder.IsSelected = isSelecteds;
            folder.IsExpanded = isExpandeds;
            folder.Parent = this;

            var unreadCount = 0;
            var attr = node.Attributes?["UnreadCount"];
            if (attr != null)
            {
                var s = node.Attributes?["UnreadCount"]?.Value;
                if (!string.IsNullOrEmpty(s))
                    unreadCount = int.Parse(s);
            }
            folder.EntryNewCount = unreadCount;

            var viewType = (node.Attributes?["ViewType"] != null) ? node.Attributes["ViewType"]?.Value : "Cards";
            ViewTypes vt;
            switch (viewType)
            {
                case "Cards":
                    vt = ViewTypes.vtCards;
                    break;
                case "Magazine":
                    vt = ViewTypes.vtMagazine;
                    break;
                case "ThreePanes":
                    vt = ViewTypes.vtThreePanes;
                    break;
                default:
                    vt = ViewTypes.vtCards;
                    break;
            }
            folder.ViewType = vt;

            // This is going to "Sort" folder and feed unintentionally.
            /*
            XmlNodeList? folderList = node.SelectNodes("Folder");
            if (folderList != null)
            {
                foreach (XmlNode f in folderList)
                {
                    NodeFolder? fd = LoadXmlChildFolder(f);
                    if (fd == null)
                        continue;

                    fd.Parent = folder;

                    if (fd != null)
                        folder.Children.Add(fd);
                }
            }

            XmlNodeList? feedList = node.SelectNodes("Feed");
            if (feedList != null)
            {
                foreach (XmlNode f in feedList)
                {
                    NodeFeed? feed = LoadXmlChildFeed(f);
                    if (feed == null)
                        continue;

                    feed.Parent = folder;

                    if (feed != null)
                        folder.Children.Add(feed);
                }
            }
            */
            // Re-write.
            foreach (XmlNode hoge in node.ChildNodes)
            {
                if (hoge.LocalName.Equals("Folder"))
                {
                    NodeFolder? fuga = LoadXmlChildFolder(hoge);
                    if (fuga == null)
                        continue;

                    fuga.Parent = folder;

                    if (fuga != null)
                        folder.Children.Add(fuga);
                }
                else if (hoge.LocalName.Equals("Feed"))
                {
                    NodeFeed? fuga = LoadXmlChildFeed(hoge);
                    if (fuga == null)
                        continue;

                    fuga.Parent = folder;

                    if (fuga != null)
                        folder.Children.Add(fuga);
                }
            }

            return folder;
        }

        return null;
    }

    private NodeFeed? LoadXmlChildFeed(XmlNode node)
    {
        var feedName = node.Attributes?["Name"]?.Value;

        var selecteds = string.IsNullOrEmpty(node.Attributes?["Selected"]?.Value) ? "" : node.Attributes?["Selected"]?.Value;
        //var expandeds = string.IsNullOrEmpty(node.Attributes["Expanded"].Value) ? "" : node.Attributes["Expanded"].Value;
        var isSelectedf = (selecteds == "true") ? true : false;
        //bool isExpandedf = (expandeds == "true") ? true : false;

        var endpoint = node.Attributes?["EndPoint"]?.Value;
        var api = (node.Attributes?["Api"] != null) ? node.Attributes?["Api"]?.Value : "Unknown";

        ApiTypes at;
        switch (api)
        {
            case "Feed":
                at = ApiTypes.atFeed;
                break;
            default:
                at = ApiTypes.atUnknown;
                break;
        }

        var viewType = (node.Attributes?["ViewType"] != null) ? node.Attributes?["ViewType"]?.Value : "Cards";
        ViewTypes vt;
        switch (viewType)
        {
            case "Cards":
                vt = ViewTypes.vtCards;
                break;
            case "Magazine":
                vt = ViewTypes.vtMagazine;
                break;
            case "ThreePanes":
                vt = ViewTypes.vtThreePanes;
                break;
            default:
                vt = ViewTypes.vtCards;
                break;
        }

        var siteTitle = "";
        var attr = node.Attributes?["SiteTitle"];
        if (attr != null)
        {
            siteTitle = string.IsNullOrEmpty(node.Attributes?["SiteTitle"]?.Value) ? "" : node.Attributes?["SiteTitle"]?.Value;
        }

        var siteSubTitle = "";
        attr = node.Attributes?["SiteSubTitle"];
        if (attr != null)
        {
            siteSubTitle = string.IsNullOrEmpty(node.Attributes?["SiteSubTitle"]?.Value) ? "" : node.Attributes?["SiteSubTitle"]?.Value;
        }

        Uri? siteUri = null;
        attr = node.Attributes?["SiteUri"];
        if (attr != null)
        {
            var siteLink = string.IsNullOrEmpty(node.Attributes?["SiteUri"]?.Value) ? "" : node.Attributes?["SiteUri"]?.Value;

            if (!string.IsNullOrEmpty(siteLink))
            {
                try
                {
                    siteUri = new Uri(siteLink);
                }
                catch { }
            }
        }

        DateTime Updated = default;
        attr = node.Attributes?["Updated"];
        if (attr != null)
        {
            var s = node.Attributes?["Updated"]?.Value;
            if (!string.IsNullOrEmpty(s))
                Updated = DateTime.Parse(s);
        }

        var unreadCount = 0;
        attr = node.Attributes?["UnreadCount"];
        if (attr != null)
        {
            var s = node.Attributes?["UnreadCount"]?.Value;
            if (!string.IsNullOrEmpty(s))
                unreadCount = int.Parse(s);
        }

        DateTime lastUpdate = default;
        attr = node.Attributes?["LastUpdate"];
        if (attr != null)
        {
            var s = node.Attributes?["LastUpdate"]?.Value;
            if (!string.IsNullOrEmpty(s))
                lastUpdate = DateTime.Parse(s);
        }

        ErrorObject? errHttpObj = null;
        XmlNodeList? ErrorList = node.SelectNodes("ErrorHTTP");
        if (ErrorList != null)
        {
            if (ErrorList.Count > 0)
            {
                XmlNode? errNode = ErrorList[0];
                errHttpObj = new ErrorObject();

                XmlNode? errAttr = errNode?.Attributes?["ErrCode"];
                if (errAttr != null)
                {
                    var s = errNode?.Attributes?["ErrCode"]?.Value;
                    errHttpObj.ErrCode = string.IsNullOrEmpty(s) ? "" : s;
                }

                var eType = (errNode?.Attributes?["ErrType"] != null) ? errNode.Attributes?["ErrType"]?.Value : "HTTP";
                ErrTypes et;
                switch (eType)
                {
                    case "DB":
                        et = ErrTypes.DB;
                        break;
                    case "API":
                        et = ErrTypes.API;
                        break;
                    case "HTTP":
                        et = ErrTypes.HTTP;
                        break;
                    case "XML":
                        et = ErrTypes.XML;
                        break;
                    default:
                        et = ErrTypes.Other;
                        break;
                }
                errHttpObj.ErrType = et;

                errAttr = errNode?.Attributes?["ErrDescription"];
                if (errAttr != null)
                {
                    var s = errNode?.Attributes?["ErrDescription"]?.Value;
                    errHttpObj.ErrDescription = string.IsNullOrEmpty(s) ? "" : s;
                }

                errAttr = errNode?.Attributes?["ErrText"];
                if (errAttr != null)
                {
                    var s = errNode?.Attributes?["ErrText"]?.Value;
                    errHttpObj.ErrText = string.IsNullOrEmpty(s) ? "" : s;
                }

                errAttr = errNode?.Attributes?["ErrPlace"];
                if (errAttr != null)
                {
                    var s = errNode?.Attributes?["ErrPlace"]?.Value;
                    errHttpObj.ErrPlace = string.IsNullOrEmpty(s) ? "" : s;
                }

                errAttr = errNode?.Attributes?["ErrPlaceParent"];
                if (errAttr != null)
                {
                    var s = errNode?.Attributes?["ErrPlaceParent"]?.Value;
                    errHttpObj.ErrPlaceParent = string.IsNullOrEmpty(s) ? "" : s;
                }

                errAttr = errNode?.Attributes?["ErrDatetime"];
                if (errAttr != null)
                {
                    var s = errNode?.Attributes?["ErrDatetime"]?.Value;
                    if (!string.IsNullOrEmpty(s))
                        errHttpObj.ErrDatetime = DateTime.Parse(s);
                }

            }
        }

        ErrorObject? errDbObj = null;
        XmlNodeList? ErrorDbList = node.SelectNodes("ErrorDatabase");
        if (ErrorDbList != null)
        {
            if (ErrorDbList.Count > 0)
            {
                XmlNode? err = ErrorDbList[0];
                errDbObj = new ErrorObject();

                var errAttr = err?.Attributes?["ErrCode"];
                if (errAttr != null)
                {
                    var s = err?.Attributes?["ErrCode"]?.Value;
                    errDbObj.ErrCode = string.IsNullOrEmpty(s) ? "" : s;
                }

                var eType = (err?.Attributes?["ErrType"] != null) ? err.Attributes?["ErrType"]?.Value : "DB";
                ErrTypes et;
                switch (eType)
                {
                    case "DB":
                        et = ErrTypes.DB;
                        break;
                    case "API":
                        et = ErrTypes.API;
                        break;
                    case "HTTP":
                        et = ErrTypes.HTTP;
                        break;
                    case "XML":
                        et = ErrTypes.XML;
                        break;
                    default:
                        et = ErrTypes.Other;
                        break;
                }
                errDbObj.ErrType = et;

                errAttr = err?.Attributes?["ErrDescription"];
                if (errAttr != null)
                {
                    var s = err?.Attributes?["ErrDescription"]?.Value;
                    errDbObj.ErrDescription = string.IsNullOrEmpty(s) ? "" : s;
                }

                errAttr = err?.Attributes?["ErrText"];
                if (errAttr != null)
                {
                    var s = err?.Attributes?["ErrText"]?.Value;
                    errDbObj.ErrText = string.IsNullOrEmpty(s) ? "" : s;
                }

                errAttr = err?.Attributes?["ErrPlace"];
                if (errAttr != null)
                {
                    var s = err?.Attributes?["ErrPlace"]?.Value;
                    errDbObj.ErrPlace = string.IsNullOrEmpty(s) ? "" : s;
                }

                errAttr = err?.Attributes?["ErrPlaceParent"];
                if (errAttr != null)
                {
                    var s = err?.Attributes?["ErrPlaceParent"]?.Value;
                    errDbObj.ErrPlaceParent = string.IsNullOrEmpty(s) ? "" : s;
                }

                errAttr = err?.Attributes?["ErrDatetime"];
                if (errAttr != null)
                {
                    var s = err?.Attributes?["ErrDatetime"]?.Value;
                    if (!string.IsNullOrEmpty(s))
                        errDbObj.ErrDatetime = DateTime.Parse(s);
                }

            }
        }

        if (!string.IsNullOrEmpty(endpoint))
        {
            feedName ??= "no name";
            NodeFeed feed = new NodeFeed(feedName, new Uri(endpoint));
            feed.IsSelected = isSelectedf;
            //feed.IsExpanded = isExpandedf;
            feed.Parent = this;

            feed.Title = siteTitle ?? "no title";
            feed.Description = siteSubTitle ?? ""; ;
            feed.HtmlUri = siteUri;
            feed.Updated = Updated;

            feed.EntryNewCount = unreadCount;
            feed.ViewType = vt;
            feed.LastFetched = lastUpdate;

            feed.Api = at;

            if (errHttpObj != null)
            {
                feed.Status = NodeFeed.DownloadStatus.error;
                feed.ErrorHttp = errHttpObj;
            }

            if (errDbObj != null)
            {
                feed.Status = NodeFeed.DownloadStatus.error;
                feed.ErrorDatabase = errDbObj;
            }

            return feed;
        }

        return null;
    }

    // Saves service tree.
    public XmlDocument AsXmlDoc()
    {
        XmlDocument doc = new();
        XmlDeclaration xdec = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
        doc.AppendChild(xdec);

        XmlElement root = doc.CreateElement(string.Empty, "Accounts", string.Empty);
        doc.AppendChild(root);

        foreach (var s in this.Children)
        {
            if (s is NodeService)
            {
                if (s is NodeFeed nfd)
                {
                    XmlElement feed = AsXmlFeedElement(doc, nfd);

                    root.AppendChild(feed);
                }
                else 
                {
                    XmlElement service = doc.CreateElement(string.Empty, "Service", string.Empty);

                    XmlAttribute attrs = doc.CreateAttribute("Name");
                    attrs.Value = s.Name;
                    service.SetAttributeNode(attrs);

                    XmlAttribute attrsd = doc.CreateAttribute("Expanded");
                    attrsd.Value = s.IsExpanded ? "true" : "false";
                    service.SetAttributeNode(attrsd);

                    XmlAttribute attrss = doc.CreateAttribute("Selected");
                    attrss.Value = s.IsSelected ? "true" : "false";
                    service.SetAttributeNode(attrss);

                    XmlAttribute attrsn = doc.CreateAttribute("UserName");
                    attrsn.Value = ((NodeService)s).UserName;
                    service.SetAttributeNode(attrsn);

                    XmlAttribute attrsp = doc.CreateAttribute("UserPassword");
                    attrsp.Value = ((NodeService)s).UserPassword;
                    service.SetAttributeNode(attrsp);

                    XmlAttribute attrse = doc.CreateAttribute("EndPoint");
                    attrse.Value = ((NodeService)s).EndPoint.AbsoluteUri;
                    service.SetAttributeNode(attrse);

                    XmlAttribute attrv = doc.CreateAttribute("ViewType");
                    switch (s.ViewType)
                    {
                        case ViewTypes.vtCards:
                            attrv.Value = "Cards";
                            service.SetAttributeNode(attrv);
                            break;
                        case ViewTypes.vtMagazine:
                            attrv.Value = "Magazine";
                            service.SetAttributeNode(attrv);
                            break;
                        case ViewTypes.vtThreePanes:
                            attrv.Value = "ThreePanes";
                            service.SetAttributeNode(attrv);
                            break;
                    }

                    XmlAttribute atstp = doc.CreateAttribute("Type");
                    switch (((NodeService)s).ServiceType)
                    {
                        case ServiceTypes.AtomPub:
                            atstp.Value = "AtomPub";
                            service.SetAttributeNode(atstp);
                            break;
                        case ServiceTypes.Feed:
                            atstp.Value = "Feed";
                            service.SetAttributeNode(atstp);
                            break;
                        case ServiceTypes.XmlRpc:
                            atstp.Value = "XML-RPC";
                            service.SetAttributeNode(atstp);
                            break;
                        case ServiceTypes.AtomApi:
                            atstp.Value = "AtomAPI";
                            service.SetAttributeNode(atstp);
                            break;
                        case ServiceTypes.Unknown:
                            atstp.Value = "Unknown";
                            service.SetAttributeNode(atstp);
                            break;

                    }

                    XmlAttribute atapi = doc.CreateAttribute("Api");
                    switch (((NodeService)s).Api)
                    {
                        case ApiTypes.atAtomPub:
                            atapi.Value = "AtomPub";
                            service.SetAttributeNode(atapi);
                            break;
                        case ApiTypes.atFeed:
                            atapi.Value = "Feed";
                            service.SetAttributeNode(atapi);
                            break;
                        case ApiTypes.atXMLRPC_MovableType:
                            atapi.Value = "XML-RPC_MovableType";
                            service.SetAttributeNode(atapi);
                            break;
                        case ApiTypes.atXMLRPC_WordPress:
                            atapi.Value = "XML-RPC_WordPress";
                            service.SetAttributeNode(atapi);
                            break;
                        case ApiTypes.atAtomApi:
                            atapi.Value = "AtomAPI";
                            service.SetAttributeNode(atapi);
                            break;
                        case ApiTypes.atUnknown:
                            atapi.Value = "Unknown";
                            service.SetAttributeNode(atapi);
                            break;
                    }

                    root.AppendChild(service);


                    foreach (var c in s.Children)
                    {
                        if (!(c is NodeEntryCollection)) continue;

                        XmlElement collection = doc.CreateElement(string.Empty, "Collection", string.Empty);

                        XmlAttribute attrc = doc.CreateAttribute("Name");
                        attrc.Value = c.Name;
                        collection.SetAttributeNode(attrc);

                        XmlAttribute attrcd = doc.CreateAttribute("Expanded");
                        attrcd.Value = c.IsExpanded ? "true" : "false";
                        collection.SetAttributeNode(attrcd);

                        XmlAttribute attrcs = doc.CreateAttribute("Selected");
                        attrcs.Value = c.IsSelected ? "true" : "false";
                        collection.SetAttributeNode(attrcs);

                        XmlAttribute attrch = doc.CreateAttribute("Href");
                        attrch.Value = ((NodeEntryCollection)c).Uri.AbsoluteUri;
                        collection.SetAttributeNode(attrch);

                        XmlAttribute attrid = doc.CreateAttribute("Id");
                        attrid.Value = ((NodeEntryCollection)c).Id;
                        collection.SetAttributeNode(attrid);

                        service.AppendChild(collection);

                        if (c is NodeAtomPubEntryCollection napec)
                        {
                            foreach (var a in napec.AcceptTypes)
                            {
                                XmlElement acceptType = doc.CreateElement(string.Empty, "Accept", string.Empty);
                                XmlText xt = doc.CreateTextNode(a);
                                acceptType.AppendChild(xt);

                                collection.AppendChild(acceptType);
                            }

                            if (napec.CategoriesUri != null)
                            {
                                XmlAttribute attrcaturi = doc.CreateAttribute("CategoriesUri");
                                attrcaturi.Value = napec.CategoriesUri.AbsoluteUri;
                                collection.SetAttributeNode(attrcaturi);
                            }

                            XmlAttribute attrcatfixed = doc.CreateAttribute("IsCategoryFixed");
                            attrcatfixed.Value = napec.IsCategoryFixed ? "true" : "false";
                            collection.SetAttributeNode(attrcatfixed);
                        }

                        foreach (var t in ((NodeEntryCollection)c).Children)
                        {
                            if (!(t is NodeCategory)) continue;

                            XmlElement category = doc.CreateElement(string.Empty, "Category", string.Empty);

                            XmlAttribute attrt = doc.CreateAttribute("Name");
                            attrt.Value = (t).Name;
                            category.SetAttributeNode(attrt);

                            XmlAttribute attrtd = doc.CreateAttribute("Expanded");
                            attrtd.Value = (t).IsExpanded ? "true" : "false";
                            category.SetAttributeNode(attrtd);

                            XmlAttribute attrts = doc.CreateAttribute("Selected");
                            attrts.Value = (t).IsSelected ? "true" : "false";
                            category.SetAttributeNode(attrts);

                            collection.AppendChild(category);

                            if (t is NodeAtomPubCategory napc)
                            {
                                //
                                //XmlAttribute attrtt = doc.CreateAttribute("Term");
                                //attrtt.Value = ((NodeCategory)t).Term;
                                //category.SetAttributeNode(attrtt);

                                //
                                XmlAttribute attrschme = doc.CreateAttribute("Scheme");
                                attrschme.Value = napc.Scheme;
                                category.SetAttributeNode(attrschme);
                            }
                            else if (t is NodeXmlRpcMTCategory)
                            {
                                //

                            }
                            else if (t is NodeXmlRpcWPCategory)
                            {
                                //

                            }


                        }


                    }
                }
            }
            else if (s is NodeFolder n)
            {
                XmlElement folder = AsXmlFolderElement(doc, n);

                root.AppendChild(folder);
            }
        }

        return doc;
    }

    private XmlElement AsXmlFolderElement(XmlDocument doc, NodeFolder fd)
    {
        XmlElement folder = doc.CreateElement(string.Empty, "Folder", string.Empty);

        XmlAttribute attrd = doc.CreateAttribute("Name");
        attrd.Value = fd.Name;
        folder.SetAttributeNode(attrd);

        attrd = doc.CreateAttribute("Expanded");
        attrd.Value = fd.IsExpanded ? "true" : "false";
        folder.SetAttributeNode(attrd);

        attrd = doc.CreateAttribute("Selected");
        attrd.Value = fd.IsSelected ? "true" : "false";
        folder.SetAttributeNode(attrd);

        attrd = doc.CreateAttribute("UnreadCount");
        attrd.Value = fd.EntryNewCount.ToString();
        folder.SetAttributeNode(attrd);

        attrd = doc.CreateAttribute("ViewType");
        switch (fd.ViewType)
        {
            case ViewTypes.vtCards:
                attrd.Value = "Cards";
                folder.SetAttributeNode(attrd);
                break;
            case ViewTypes.vtMagazine:
                attrd.Value = "Magazine";
                folder.SetAttributeNode(attrd);
                break;
            case ViewTypes.vtThreePanes:
                attrd.Value = "ThreePanes";
                folder.SetAttributeNode(attrd);
                break;
        }

        foreach (var hoge in fd.Children)
        {
            if (hoge is NodeFeed fuga)
            {
                XmlElement feed = AsXmlFeedElement(doc, fuga);

                folder.AppendChild(feed);
            }
            else if (hoge is NodeFolder fugafuga)
            {
                XmlElement folderChild = AsXmlFolderElement(doc, fugafuga);

                folder.AppendChild(folderChild);
            }
        }

        return folder;
    }

    private XmlElement AsXmlFeedElement(XmlDocument doc, NodeFeed fd)
    {
        XmlElement feed = doc.CreateElement(string.Empty, "Feed", string.Empty);

        XmlAttribute attrf = doc.CreateAttribute("Name");
        attrf.Value = fd.Name;
        feed.SetAttributeNode(attrf);

        /*
        attrf = doc.CreateAttribute("Expanded");
        attrf.Value = fd.IsExpanded ? "true" : "false";
        feed.SetAttributeNode(attrf);
        */

        attrf = doc.CreateAttribute("Selected");
        attrf.Value = fd.IsSelected ? "true" : "false";
        feed.SetAttributeNode(attrf);

        attrf = doc.CreateAttribute("EndPoint");
        attrf.Value = fd.EndPoint.AbsoluteUri;
        feed.SetAttributeNode(attrf);

        attrf = doc.CreateAttribute("Type");
        attrf.Value = "Feed";
        feed.SetAttributeNode(attrf);

        attrf = doc.CreateAttribute("Api");
        switch (fd.Api)
        {
            case ApiTypes.atFeed:
                attrf.Value = "Feed";
                feed.SetAttributeNode(attrf);
                break;
            case ApiTypes.atUnknown:
                attrf.Value = "Unknown";
                feed.SetAttributeNode(attrf);
                break;
        }

        attrf = doc.CreateAttribute("ViewType");
        switch (fd.ViewType)
        {
            case ViewTypes.vtCards:
                attrf.Value = "Cards";
                feed.SetAttributeNode(attrf);
                break;
            case ViewTypes.vtMagazine:
                attrf.Value = "Magazine";
                feed.SetAttributeNode(attrf);
                break;
            case ViewTypes.vtThreePanes:
                attrf.Value = "ThreePanes";
                feed.SetAttributeNode(attrf);
                break;
        }

        attrf = doc.CreateAttribute("SiteTitle");
        attrf.Value = fd.Title;
        feed.SetAttributeNode(attrf);

        attrf = doc.CreateAttribute("SiteSubTitle");
        attrf.Value = fd.Description;
        feed.SetAttributeNode(attrf);

        attrf = doc.CreateAttribute("SiteUri");
        if (fd.HtmlUri != null)
            attrf.Value = fd.HtmlUri.AbsoluteUri;
        feed.SetAttributeNode(attrf);

        attrf = doc.CreateAttribute("Updated");
        attrf.Value = fd.Updated.ToString("yyyy-MM-dd HH:mm:ss");
        feed.SetAttributeNode(attrf);

        attrf = doc.CreateAttribute("UnreadCount");
        attrf.Value = fd.EntryNewCount.ToString();
        feed.SetAttributeNode(attrf);

        attrf = doc.CreateAttribute("LastUpdate");
        attrf.Value = fd.LastFetched.ToString("yyyy-MM-dd HH:mm:ss");
        feed.SetAttributeNode(attrf);


        if (fd.ErrorHttp != null)
        {
            var httpError = doc.CreateElement(string.Empty, "ErrorHTTP", string.Empty);

            XmlAttribute attrErr = doc.CreateAttribute("ErrType");
            attrErr.Value = fd.ErrorHttp.ErrType.ToString();
            httpError.SetAttributeNode(attrErr);

            attrErr = doc.CreateAttribute("ErrCode");
            attrErr.Value = fd.ErrorHttp.ErrCode;
            httpError.SetAttributeNode(attrErr);

            attrErr = doc.CreateAttribute("ErrDescription");
            attrErr.Value = fd.ErrorHttp.ErrDescription;
            httpError.SetAttributeNode(attrErr);

            attrErr = doc.CreateAttribute("ErrText");
            attrErr.Value = fd.ErrorHttp.ErrText;
            httpError.SetAttributeNode(attrErr);

            attrErr = doc.CreateAttribute("ErrPlace");
            attrErr.Value = fd.ErrorHttp.ErrPlace;
            httpError.SetAttributeNode(attrErr);

            attrErr = doc.CreateAttribute("ErrPlaceParent");
            attrErr.Value = fd.ErrorHttp.ErrPlaceParent;
            httpError.SetAttributeNode(attrErr);

            attrErr = doc.CreateAttribute("ErrDatetime");
            attrErr.Value = fd.ErrorHttp.ErrDatetime.ToString("yyyy-MM-dd HH:mm:ss");
            httpError.SetAttributeNode(attrErr);

            feed.AppendChild(httpError);
        }

        if (fd.ErrorDatabase != null)
        {
            var DatabaseError = doc.CreateElement(string.Empty, "ErrorDatabase", string.Empty);

            XmlAttribute attrErr = doc.CreateAttribute("ErrType");
            attrErr.Value = fd.ErrorDatabase.ErrType.ToString();
            DatabaseError.SetAttributeNode(attrErr);

            attrErr = doc.CreateAttribute("ErrCode");
            attrErr.Value = fd.ErrorDatabase.ErrCode;
            DatabaseError.SetAttributeNode(attrErr);

            attrErr = doc.CreateAttribute("ErrDescription");
            attrErr.Value = fd.ErrorDatabase.ErrDescription;
            DatabaseError.SetAttributeNode(attrErr);

            attrErr = doc.CreateAttribute("ErrText");
            attrErr.Value = fd.ErrorDatabase.ErrText;
            DatabaseError.SetAttributeNode(attrErr);

            attrErr = doc.CreateAttribute("ErrPlace");
            attrErr.Value = fd.ErrorDatabase.ErrPlace;
            DatabaseError.SetAttributeNode(attrErr);

            attrErr = doc.CreateAttribute("ErrPlaceParent");
            attrErr.Value = fd.ErrorDatabase.ErrPlaceParent;
            DatabaseError.SetAttributeNode(attrErr);

            attrErr = doc.CreateAttribute("ErrDatetime");
            attrErr.Value = fd.ErrorDatabase.ErrDatetime.ToString("yyyy-MM-dd HH:mm:ss");
            DatabaseError.SetAttributeNode(attrErr);

            feed.AppendChild(DatabaseError);
        }

        return feed;
    }
}
