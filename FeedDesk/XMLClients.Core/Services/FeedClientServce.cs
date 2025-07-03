using System.Net;
using System.Net.Http.Headers;
using System.Xml;
using XmlClients.Core.Contracts.Services;
using XmlClients.Core.Helpers;
using XmlClients.Core.Models;
using XmlClients.Core.Models.Clients;

namespace XmlClients.Core.Services;

public class FeedClientService : BaseClient, IFeedClientService
{
    public BaseClient BaseClient => this;

    public FeedClientService()
    {
        //Client.BaseAddress = ;
        Client.DefaultRequestHeaders.Clear();
        //Client.DefaultRequestHeaders.ConnectionClose = false;
        Client.DefaultRequestHeaders.ConnectionClose = true;
        //Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
        Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atom+xml"));
        Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rss+xml"));
        Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rdf+xml"));
    }

    public async override Task<HttpClientEntryItemCollectionResultWrapper> GetEntries(Uri entriesUrl, string feedId)
    {
        var res = new HttpClientEntryItemCollectionResultWrapper();

        var list = new List<EntryItem>();
        res.Entries = list;

        if (!(entriesUrl.Scheme.Equals("http") || entriesUrl.Scheme.Equals("https")))
        {
            ToDebugWindow("<< Invalid URI scheme:"
                + Environment.NewLine
                + entriesUrl.Scheme
                + Environment.NewLine);

            InvalidUriScheme(res.Error, entriesUrl.Scheme, "FeedHttpClient: GetEntries");
            res.IsError = true;
            return res;
        }

        try
        {
            var HTTPResponseMessage = await Client.GetAsync(entriesUrl);

            if (HTTPResponseMessage.IsSuccessStatusCode)
            {
                /*
                var s = await HTTPResponseMessage.Content.ReadAsStringAsync();
                ToDebugWindow(">> HTTP Request: GET "
                    + entriesUrl.AbsoluteUri
                    + Environment.NewLine
                    + "<< HTTP Response " + HTTPResponseMessage.StatusCode.ToString()
                    + Environment.NewLine + s + Environment.NewLine
                    + Environment.NewLine);
                */
                /*
                var str = await HTTPResponseMessage.Content.ReadAsStringAsync();
                Debug.WriteLine(str);
                */

                ToDebugWindow(">> HTTP Request: GET "
                    + entriesUrl.AbsoluteUri
                    + Environment.NewLine
                    + "<< HTTP Response " + HTTPResponseMessage.StatusCode.ToString()
                    + Environment.NewLine);

                try
                {
                    var source = await HTTPResponseMessage.Content.ReadAsStreamAsync();

                    // Load XML
                    var xdoc = new XmlDocument();
                    try
                    {
                        XmlReaderSettings settings = new XmlReaderSettings();
                        settings.DtdProcessing = DtdProcessing.Parse;

                        XmlReader reader = XmlReader.Create(source, settings);

                        xdoc.Load(reader);
                    }
                    catch (Exception e)
                    {
                        ToDebugWindow("<< Invalid XML document returned from: " + entriesUrl.AbsoluteUri
                            + Environment.NewLine
                            + e.Message
                            + Environment.NewLine);

                        InvalidXml(res.Error, e.Message, "XmlDocument.Load", "FeedHttpClient: GetEntries");
                        res.IsError = true;

                        return res;
                    }

                    if (xdoc.DocumentElement == null)
                    {
                        return res;
                    }

                    // RSS 2.0
                    if (xdoc.DocumentElement.LocalName.Equals("rss"))
                    {
                        XmlNamespaceManager NsMgr = new XmlNamespaceManager(xdoc.NameTable);
                        NsMgr.AddNamespace("dc", "http://purl.org/dc/elements/1.1/");
                        NsMgr.AddNamespace("itunes", "http://www.itunes.com/dtds/podcast-1.0.dtd");
                        NsMgr.AddNamespace("content", "http://purl.org/rss/1.0/modules/content/");
                        NsMgr.AddNamespace("media", "http://search.yahoo.com/mrss/");

                        XmlNode? feedTitle = xdoc.DocumentElement.SelectSingleNode("channel/title");
                        res.Title = (feedTitle != null) ? feedTitle.InnerText : "";

                        XmlNode? feedDesc = xdoc.DocumentElement.SelectSingleNode("channel/description");
                        res.Description = (feedDesc != null) ? feedDesc.InnerText : "";

                        XmlNode? feedLinkUri = xdoc.DocumentElement.SelectSingleNode("channel/link");
                        try
                        {
                            if (feedLinkUri != null)
                                if (!string.IsNullOrEmpty(feedLinkUri.InnerText))
                                    if (feedLinkUri.InnerText.StartsWith("http"))
                                        res.HtmlUri = new Uri(feedLinkUri.InnerText);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(">> Exception @RSS 2.0 new Uri(feedLinkUri): " + res.Title);

                            ToDebugWindow(">> Exception @RSS 2.0 new Uri(feedLinkUri)"
                                + Environment.NewLine +
                                "RSS feed (" + res.Title + ") contain invalid entry Uri: " + e.Message +
                                Environment.NewLine);
                        }

                        XmlNode? feedPudDate = xdoc.DocumentElement.SelectSingleNode("channel/pubDate");
                        if (feedPudDate != null)
                        {
                            var s = feedPudDate.InnerText;
                            if (!string.IsNullOrEmpty(s))
                            {
                                try
                                {
                                    DateTimeOffset dtf = DateTimeParser.ParseDateTimeRFC822(s);
                                    res.Published = dtf.ToUniversalTime().DateTime;
                                }
                                catch (Exception e)
                                {
                                    Debug.WriteLine("Exception @ParseDateTimeRFC822 in the RSS 2.0 feed " + "(" + res.Title + ")" + " : " + e.Message);

                                    ToDebugWindow(">> Exception @FeedClient@ParseDateTimeRFC822()"
                                        + Environment.NewLine +
                                        "RSS feed entry(" + res.Title + ") contain invalid feed pubDate (DateTimeRFC822 expected): " + e.Message +
                                        Environment.NewLine);

                                    // TODO: really shouldn't to cover these invalid format, but...for the usability stand point...
                                    try
                                    {
                                        DateTime tmp;
                                        if (DateTime.TryParse(s, out tmp))
                                        {
                                            res.Published = tmp.ToUniversalTime();
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }

                        XmlNode? feedLastBuildDate = xdoc.DocumentElement.SelectSingleNode("channel/lastBuildDate");
                        if (feedLastBuildDate != null)
                        {
                            var s = feedLastBuildDate.InnerText;
                            if (!string.IsNullOrEmpty(s))
                            {
                                try
                                {
                                    DateTimeOffset dtf = DateTimeParser.ParseDateTimeRFC822(s);

                                    res.Updated = dtf.ToUniversalTime().DateTime;
                                }
                                catch (Exception e)
                                {
                                    Debug.WriteLine("Exception @ParseDateTimeRFC822 in the RSS 2.0 feed " + "(" + res.Title + ")" + " : " + e.Message);

                                    ToDebugWindow(">> Exception @FeedClient@ParseDateTimeRFC822()"
                                        + Environment.NewLine +
                                        "RSS feed entry(" + res.Title + ") contain invalid feed lastBuildDate (DateTimeRFC822 expected): " + e.Message +
                                        Environment.NewLine);

                                    // TODO: really shouldn't to cover these invalid format, but...for the usability stand point...
                                    try
                                    {
                                        DateTime tmp;
                                        if (DateTime.TryParse(s, out tmp))
                                        {
                                            res.Updated = tmp.ToUniversalTime();
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }

                        XmlNodeList? entryList;
                        entryList = xdoc.SelectNodes("//rss/channel/item");
                        if (entryList == null)
                        {
                            res.Entries = list;
                            return res;
                        }

                        var i = 0;
                        foreach (XmlNode l in entryList)
                        {
                            if (i >= 1000)
                                continue;
                            i++;

                            FeedEntryItem ent = new FeedEntryItem("", feedId, this);

                            FillEntryItemFromXmlRss(ent, l, NsMgr, entriesUrl);

                            if (!string.IsNullOrEmpty(ent.EntryId))
                                list.Add(ent);
                        }

                        // 
                        //await GetImages(list);
                    }
                    // RSS 1.0
                    else if (xdoc.DocumentElement.LocalName.Equals("RDF"))
                    {
                        XmlNamespaceManager NsMgr = new XmlNamespaceManager(xdoc.NameTable);
                        NsMgr.AddNamespace("rdf", "http://www.w3.org/1999/02/22-rdf-syntax-ns#");
                        NsMgr.AddNamespace("rss", "http://purl.org/rss/1.0/");
                        NsMgr.AddNamespace("dc", "http://purl.org/dc/elements/1.1/");
                        NsMgr.AddNamespace("hatena", "http://www.hatena.ne.jp/info/xmlns#");
                        NsMgr.AddNamespace("content", "http://purl.org/rss/1.0/modules/content/");
                        NsMgr.AddNamespace("media", "http://search.yahoo.com/mrss/");

                        XmlNode? feedTitle = xdoc.DocumentElement.SelectSingleNode("rss:channel/rss:title", NsMgr);
                        res.Title = (feedTitle != null) ? feedTitle.InnerText : "";

                        XmlNode? feedDesc = xdoc.DocumentElement.SelectSingleNode("rss:channel/rss:description", NsMgr);
                        res.Description = (feedDesc != null) ? feedDesc.InnerText : "";

                        XmlNode? feedLinkUri = xdoc.DocumentElement.SelectSingleNode("rss:channel/rss:link", NsMgr);
                        try
                        {
                            if (feedLinkUri != null)
                                if (!string.IsNullOrEmpty(feedLinkUri.InnerText))
                                    res.HtmlUri = new Uri(feedLinkUri.InnerText);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(">> Exception @RSS 1.0 new Uri(feedLinkUri)");

                            ToDebugWindow(">> Exception @RSS 1.0 new Uri(feedLinkUri)"
                                + Environment.NewLine +
                                "RSS feed (" + res.Title + ") contain invalid entry Uri: " + e.Message +
                                Environment.NewLine);
                        }

                        XmlNode? feedLastBuildDate = xdoc.DocumentElement.SelectSingleNode("rss:channel/dc:date", NsMgr);
                        if (feedLastBuildDate != null)
                        {
                            var s = feedLastBuildDate.InnerText;
                            if (!string.IsNullOrEmpty(s))
                            {
                                try
                                {
                                    var date = DateTimeOffset.Parse(s);

                                    res.Updated = date.UtcDateTime;
                                }
                                catch { }
                            }
                        }

                        XmlNodeList? entryList = xdoc.SelectNodes("//rdf:RDF/rss:item", NsMgr);
                        if (entryList == null)
                        {
                            res.Entries = list;
                            return res;
                        }

                        var i = 0;
                        foreach (XmlNode l in entryList)
                        {
                            if (i >= 1000)
                                continue;
                            i++;
                            FeedEntryItem ent = new FeedEntryItem("", feedId, this);

                            FillEntryItemFromXmlRdf(ent, l, NsMgr, entriesUrl);

                            if (!string.IsNullOrEmpty(ent.EntryId))
                                list.Add(ent);
                        }

                        // 
                        //await GetImages(list);
                    }
                    // Atom 0.3 or 1.0
                    else if (xdoc.DocumentElement.LocalName.Equals("feed"))
                    {
                        // Atom 1.0
                        if (xdoc.DocumentElement.NamespaceURI.Equals("http://www.w3.org/2005/Atom"))
                        {
                            XmlNamespaceManager atomNsMgr = new XmlNamespaceManager(xdoc.NameTable);
                            atomNsMgr.AddNamespace("atom", "http://www.w3.org/2005/Atom");
                            atomNsMgr.AddNamespace("app", "http://www.w3.org/2007/app");
                            atomNsMgr.AddNamespace("media", "http://search.yahoo.com/mrss/");

                            XmlNode? feedTitle = xdoc.DocumentElement.SelectSingleNode("atom:title", atomNsMgr);
                            res.Title = (feedTitle != null) ? feedTitle.InnerText : "";

                            XmlNode? feedDesc = xdoc.DocumentElement.SelectSingleNode("atom:subtitle", atomNsMgr);
                            res.Description = (feedDesc != null) ? feedDesc.InnerText : "";

                            XmlNodeList? feedLinkUris = xdoc.DocumentElement.SelectNodes("atom:link", atomNsMgr);
                            string relAttr;
                            string hrefAttr;
                            string typeAttr;
                            Uri? altUri = null;
                            if (feedLinkUris != null)
                            {
                                foreach (XmlNode u in feedLinkUris)
                                {
                                    if (u.Attributes != null)
                                    {
                                        relAttr = (u.Attributes["rel"] != null) ? u.Attributes["rel"]!.Value : "";
                                        hrefAttr = (u.Attributes["href"] != null) ? u.Attributes["href"]!.Value : "";
                                        typeAttr = (u.Attributes["type"] != null) ? u.Attributes["type"]!.Value : "";

                                        if (!string.IsNullOrEmpty(hrefAttr))
                                        {
                                            if (relAttr.Equals("alternate") || relAttr == "")
                                            {
                                                if ((typeAttr == "text/html") || typeAttr == "")
                                                {
                                                    try
                                                    {
                                                        //altUri = new Uri(hrefAttr);
                                                        if (hrefAttr.StartsWith("http"))
                                                        {
                                                            // Absolute uri.
                                                            altUri = new Uri(hrefAttr);
                                                        }
                                                        else
                                                        {
                                                            // Relative uri (probably...)
                                                            // Uri(baseUri, relativeUriString)
                                                            altUri = new Uri(entriesUrl, hrefAttr);
                                                        }
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        Debug.WriteLine("Exception @new Uri(altUri) @ FeedClient Atom1.0" + "(" + res.Title + ")" + " : " + e.Message);

                                                        ToDebugWindow(">> Exception @FeedClient@new Uri(altUri)"
                                                            + Environment.NewLine +
                                                            "Atom feed (" + res.Title + ") contain invalid atom:altUri: " + e.Message +
                                                            Environment.NewLine);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            res.HtmlUri = altUri;

                            XmlNode? feedUpdated = xdoc.DocumentElement.SelectSingleNode("atom:updated", atomNsMgr);
                            if (feedUpdated != null)
                            {
                                if (!string.IsNullOrEmpty(feedUpdated.InnerText))
                                {
                                    try
                                    {
                                        res.Updated = XmlConvert.ToDateTime(feedUpdated.InnerText, XmlDateTimeSerializationMode.Utc);
                                    }
                                    catch (Exception e)
                                    {
                                        //Debug.WriteLine("Exception @XmlConvert.ToDateTime in the Atom 1.0 feed " + "(" + entry.Name + ")" + " : " + e.Message);

                                        ToDebugWindow(">> Exception @FeedClient@ XmlConvert.ToDateTime()"
                                            + Environment.NewLine +
                                            "Atom feed(" + res.Title + ") contain invalid feed atom:updated: " + e.Message +
                                            Environment.NewLine);
                                    }
                                }
                            }

                            XmlNodeList? entryList;
                            entryList = xdoc.SelectNodes("//atom:feed/atom:entry", atomNsMgr);
                            if (entryList == null)
                            {
                                res.Entries = list;
                                return res;
                            }

                            var i = 0;
                            foreach (XmlNode l in entryList)
                            {
                                if (i >= 1000)
                                    continue;
                                i++;

                                FeedEntryItem ent = new FeedEntryItem("", feedId, this);
                                //ent.Status = EditEntryItem.EditStatus.esNormal;

                                FillEntryItemFromXmlAtom10(ent, l, atomNsMgr, entriesUrl);

                                if (!string.IsNullOrEmpty(ent.EntryId))
                                    list.Add(ent);
                            }

                            // 
                            //await GetImages(list);
                        }
                        // Old Atom 0.3
                        else if (xdoc.DocumentElement.NamespaceURI.Equals("http://purl.org/atom/ns#"))
                        {
                            XmlNamespaceManager atomNsMgr = new XmlNamespaceManager(xdoc.NameTable);
                            atomNsMgr.AddNamespace("atom", "http://purl.org/atom/ns#");
                            atomNsMgr.AddNamespace("dc", "http://purl.org/dc/elements/1.1/");

                            XmlNode? feedTitle = xdoc.DocumentElement.SelectSingleNode("atom:title", atomNsMgr);
                            res.Title = (feedTitle != null) ? feedTitle.InnerText : "";

                            XmlNode? feedDesc = xdoc.DocumentElement.SelectSingleNode("atom:tagline", atomNsMgr);
                            res.Description = (feedDesc != null) ? feedDesc.InnerText : "";

                            XmlNodeList? feedLinkUris = xdoc.DocumentElement.SelectNodes("atom:link", atomNsMgr);
                            string relAttr;
                            string hrefAttr;
                            string typeAttr;
                            Uri? altUri = null;
                            if (feedLinkUris != null)
                            {
                                foreach (XmlNode u in feedLinkUris)
                                {
                                    if (u.Attributes != null)
                                    {
                                        relAttr = (u.Attributes["rel"] != null) ? u.Attributes["rel"]!.Value : "";
                                        hrefAttr = (u.Attributes["href"] != null) ? u.Attributes["href"]!.Value : "";
                                        typeAttr = (u.Attributes["type"] != null) ? u.Attributes["type"]!.Value : "";

                                        if (!string.IsNullOrEmpty(hrefAttr))
                                        {
                                            if (relAttr.Equals("alternate") || relAttr == "")
                                            {
                                                if ((typeAttr == "text/html") || typeAttr == "")
                                                {
                                                    try
                                                    {
                                                        //altUri = new Uri(hrefAttr);
                                                        if (hrefAttr.StartsWith("http"))
                                                        {
                                                            // Absolute uri.
                                                            altUri = new Uri(hrefAttr);
                                                        }
                                                        else
                                                        {
                                                            // Relative uri (probably...)
                                                            // Uri(baseUri, relativeUriString)
                                                            altUri = new Uri(entriesUrl, hrefAttr);
                                                        }
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        Debug.WriteLine("Exception @new Uri(altUri) @ FeedClient Atom0.3" + "(" + res.Title + ")" + " : " + e.Message);

                                                        ToDebugWindow(">> Exception @FeedClient@new Uri(altUri)"
                                                            + Environment.NewLine +
                                                            "Atom feed(" + res.Title + ") contain invalid atom:altUri: " + e.Message +
                                                            Environment.NewLine);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            res.HtmlUri = altUri;

                            XmlNode? feedUpdated = xdoc.DocumentElement.SelectSingleNode("atom:modified", atomNsMgr);
                            if (feedUpdated != null)
                            {
                                if (!string.IsNullOrEmpty(feedUpdated.InnerText))
                                {
                                    try
                                    {
                                        res.Updated = XmlConvert.ToDateTime(feedUpdated.InnerText, XmlDateTimeSerializationMode.Utc);
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.WriteLine("Exception @XmlConvert.ToDateTime in the Atom 1.0 feed " + "(" + res.Title + ")" + " : " + e.Message);

                                        ToDebugWindow(">> Exception @FeedClient@ XmlConvert.ToDateTime()"
                                            + Environment.NewLine +
                                            "Atom feed(" + res.Title + ") contain invalid feed atom:published: " + e.Message +
                                            Environment.NewLine);
                                    }
                                }
                            }

                            XmlNodeList? entryList;
                            entryList = xdoc.SelectNodes("//atom:feed/atom:entry", atomNsMgr);
                            if (entryList == null)
                            {
                                res.Entries = list;
                                return res;
                            }

                            var i = 0;
                            foreach (XmlNode l in entryList)
                            {
                                if (i >= 1000)
                                    continue;
                                i++;

                                FeedEntryItem ent = new FeedEntryItem("", feedId, this);
                                //ent.Status = EditEntryItem.EditStatus.esNormal;

                                FillEntryItemFromXmlAtom03(ent, l, atomNsMgr, entriesUrl);

                                if (!string.IsNullOrEmpty(ent.EntryId))
                                    list.Add(ent);
                            }

                            // 
                            //await GetImages(list);

                            ToDebugWindow("<< Old version of Atom feed format detected. Update recommended: " + entriesUrl.AbsoluteUri
                                + Environment.NewLine);
                        }
                        else
                        {
                            ToDebugWindow("<< FormatUndetermined @FeedClient:GetEntries@xdoc.DocumentElement.NamespaceURI.Equals"
                                + Environment.NewLine);

                            FormatUndetermined(res.Error, "FeedHttpClient: GetEntries");
                            res.IsError = true;

                            return res;
                        }
                    }
                    else
                    {
                        ToDebugWindow("<< FormatUndetermined @FeedClient:GetEntries:xdoc.DocumentElement.LocalName/NamespaceURI"
                            + Environment.NewLine);

                        FormatUndetermined(res.Error, "FeedHttpClient: GetEntries");
                        res.IsError = true;

                        return res;
                    }

                }
                catch (Exception e)
                {
                    ToDebugWindow("<< Exception ReadAsStreamAsyncm: " + entriesUrl.AbsoluteUri
                        + Environment.NewLine
                        + e.Message
                        + Environment.NewLine);

                    Debug.WriteLine("<< Exception: " + e.Message);

                    HttpReqException(res.Error, e.Message, "HTTPResponseMessage.Content.ReadAsStreamAsync()", "FeedHttpClient:GetEntries");
                    res.IsError = true;
                }
            }
            // HTTP non 200 status code.
            else
            {
                var contents = await HTTPResponseMessage.Content.ReadAsStringAsync();

                if (contents != null)
                {
                    ToDebugWindow(">> HTTP Request: GET "
                        + entriesUrl.AbsoluteUri
                        + Environment.NewLine
                        + "<< HTTP Response: " + HTTPResponseMessage.StatusCode.ToString()
                        + Environment.NewLine);
                    //+ contents + Environment.NewLine);
                }
                else
                {
                    ToDebugWindow(">> HTTP Request: GET "
                        + entriesUrl.AbsoluteUri
                        + Environment.NewLine
                        + "<< HTTP Response: " + HTTPResponseMessage.StatusCode.ToString()
                        + Environment.NewLine);
                    //+ contents + Environment.NewLine);
                }

                NonSuccessStatusCode(res.Error, HTTPResponseMessage.StatusCode.ToString(), "Client.GetAsync", "FeedHttpClient.GetEntries");
                res.IsError = true;

                return res;
            }

        }
        // Internet connection errors
        catch (System.Net.Http.HttpRequestException e)
        {
            Debug.WriteLine("<< HttpRequestException: " + e.Message);

            ToDebugWindow(" << HttpRequestException: "
                + Environment.NewLine
                + e.Message
                + Environment.NewLine);

            HttpReqException(res.Error, e.Message, "Client.GetAsync", "FeedHttpClient:GetEntries");
            res.IsError = true;

            return res;
        }
        // The 'Domain'='.cnet.com' part of the cookie is invalid.
        catch (System.Net.CookieException e)
        {
            Debug.WriteLine("<< CookieException: " + e.Message);

            ToDebugWindow(" << CookieException: "
                + Environment.NewLine
                + e.Message
                + Environment.NewLine);

            HttpReqException(res.Error, e.Message, "Client.GetAsync", "FeedHttpClient:GetEntries");
            res.IsError = true;
        }
        catch (Exception e) when (e.InnerException is TimeoutException)
        {
            Debug.WriteLine("TimeoutException: " + e.Message);

            ToDebugWindow("<< TimeoutException:"
                + Environment.NewLine
                + e.Message
                + Environment.NewLine);

            HttpTimeoutException(res.Error, e.Message, "Client.GetAsync", "FeedHttpClient.GetEntries");
            res.IsError = true;

            return res;
        }
        catch (Exception e)
        {
            Debug.WriteLine("HTTP error: " + e.Message);

            ToDebugWindow("<< HTTP error:"
                + Environment.NewLine
                + e.Message
                + Environment.NewLine);

            GenericException(res.Error, "", ErrorObject.ErrTypes.HTTP, "HTTP request error (Exception)", e.Message, "Client.GetAsync", "FeedHttpClient.GetEntries");
            res.IsError = true;

            return res;
        }

        return res;
    }

    private void FillEntryItemFromXmlRss(FeedEntryItem entItem, XmlNode entryNode, XmlNamespaceManager NsMgr, Uri baseUri)
    {
        // Title (//rss/channel/item/title)
        XmlNode? entryTitle = entryNode.SelectSingleNode("title");
        entItem.Name = (entryTitle != null) ? WebUtility.HtmlDecode(entryTitle.InnerText) : "";

        // GUID (//rss/channel/item/guid)
        XmlNode? entryId = entryNode.SelectSingleNode("guid");
        entItem.EntryId = (entryId != null) ? entryId.InnerText : "";

        // Link (//rss/channel/item/link)
        XmlNode? entryLinkUri = entryNode.SelectSingleNode("link");
        try
        {
            /*
            if (entryLinkUri != null)
                if (!string.IsNullOrEmpty(entryLinkUri.InnerText))
                    entItem.AltHtmlUri = new Uri(entryLinkUri.InnerText);
            */
            if (entryLinkUri != null)
            {
                var link = entryLinkUri.InnerText;
                if (!string.IsNullOrEmpty(link))
                {
                    if (link.StartsWith("http"))
                    {
                        entItem.AltHtmlUri = new Uri(link);
                    }
                    else
                    {
                        entItem.AltHtmlUri = new Uri(baseUri, link);
                    }
                }
            }
        }
        catch (Exception e)
        {
            ToDebugWindow(">> Exception @FeedClient@FillEntryItemFromXmlRss:new Uri()"
                + Environment.NewLine +
                "RSS feed entry (" + entItem.Name + ") contain invalid entry Uri: " + e.Message +
                Environment.NewLine);
        }

        if (string.IsNullOrEmpty(entItem.EntryId))
            if (entItem.AltHtmlUri != null)
                entItem.EntryId = entItem.AltHtmlUri.AbsoluteUri;

        // pubDate (//rss/channel/item/pubDate)
        XmlNode? entryPudDate = entryNode.SelectSingleNode("pubDate");
        if (entryPudDate != null)
        {
            var s = entryPudDate.InnerText;
            if (!string.IsNullOrEmpty(s))
            {
                try
                {
                    DateTimeOffset dtf = DateTimeParser.ParseDateTimeRFC822(s);
                    entItem.Published = dtf.ToUniversalTime().DateTime;
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Exception @ParseDateTimeRFC822 in the RSS 2.0 feed " + "(" + entItem.Name + ")" + " : " + e.Message);

                    ToDebugWindow(">> Exception @FeedClient@FillEntryItemFromXmlRss:ParseDateTimeRFC822()"
                        + Environment.NewLine +
                        "RSS feed entry(" + entItem.Name + ") contain invalid entry pubDate (DateTimeRFC822 expected): " + e.Message +
                        Environment.NewLine);

                    // TODO: really shouldn't to cover these invalid format, but...for the usability stand point...
                    try
                    {
                        DateTime tmp;
                        if (DateTime.TryParse(s, out tmp))
                        {
                            entItem.Published = tmp.ToUniversalTime();
                        }
                    }
                    catch { }
                }
            }
        }

        // author (//rss/channel/item/author)
        var entryAuthor = "";
        XmlNodeList? entryAuthors = entryNode.SelectNodes("author");
        if (entryAuthors != null)
        {
            foreach (XmlNode auth in entryAuthors)
            {
                if (string.IsNullOrEmpty(entryAuthor))
                    entryAuthor = auth.InnerText;
                else
                    entryAuthor += "/" + auth.InnerText;
            }
            entItem.Author = entryAuthor;
        }

        // dc:creator (//rss/channel/item/dc:creator)
        if (string.IsNullOrEmpty(entItem.Author))
        {
            var entryDcAuthor = "";
            XmlNodeList? entryDcAuthors = entryNode.SelectNodes("dc:creator", NsMgr);
            if (entryDcAuthors != null)
            {
                foreach (XmlNode auth in entryDcAuthors)
                {
                    if (string.IsNullOrEmpty(entryDcAuthor))
                        entryDcAuthor = auth.InnerText;
                    else
                        entryDcAuthor += "/" + auth.InnerText;
                }
                entItem.Author = entryDcAuthor;
            }
        }

        // <itunes:author></itunes:author>
        if (string.IsNullOrEmpty(entItem.Author))
        {
            var entryItunesAuthor = "";
            XmlNodeList? entryItunesAuthors = entryNode.SelectNodes("itunes:author", NsMgr);
            if (entryItunesAuthors != null)
            {
                foreach (XmlNode auth in entryItunesAuthors)
                {
                    if (string.IsNullOrEmpty(entryItunesAuthor))
                        entryItunesAuthor = auth.InnerText;
                    else
                        entryItunesAuthor += "/" + auth.InnerText;
                }
                entItem.Author = entryItunesAuthor;
            }
        }

        // 
        if (string.IsNullOrEmpty(entItem.Author))
        {
            entItem.Author = "-";
        }

        // source (//rss/channel/item/source)
        var entrySource = "";
        XmlNode? entrySourceNode = entryNode.SelectSingleNode("source");
        if (entrySourceNode != null)
        {
            var s = entrySourceNode.InnerText;
            if (!string.IsNullOrEmpty(s))
            {
                entrySource = s;
            }

            if (string.IsNullOrEmpty(entrySource))
            {
                if (entrySourceNode.Attributes != null)
                {
                    if (entrySourceNode.Attributes["url"] != null)
                    {
                        var urlSource = entrySourceNode.Attributes["url"]!.Value;
                        try
                        {
                            entItem.SourceUri = new Uri(urlSource);
                        }
                        catch { }
                    }
                }
            }
            entItem.Source = entrySource;
        }

        // category (//rss/channel/item/category)
        var entryCategory = "";
        XmlNodeList? entryCategories = entryNode.SelectNodes("category");
        if (entryCategories != null)
        {
            foreach (XmlNode cat in entryCategories)
            {
                if (string.IsNullOrEmpty(entryCategory))
                    entryCategory = cat.InnerText;
                else
                    entryCategory += "/" + cat.InnerText;
            }
            entItem.Category = entryCategory;
        }

        if (string.IsNullOrEmpty(entItem.Category))
        {
            entItem.Category = "-";
        }

        // enclosure (//rss/channel/item/enclosure)
        XmlNode? enclosureNode = entryNode.SelectSingleNode("enclosure");
        if (enclosureNode != null)
        {
            if (enclosureNode.Attributes != null)
            {
                if (enclosureNode.Attributes["url"] != null)
                {
                    var url = enclosureNode.Attributes["url"]!.Value;

                    if (enclosureNode.Attributes["type"] != null)
                    {
                        var imageType = enclosureNode.Attributes["type"]!.Value;

                        if ((imageType == "image/jpg") || (imageType == "image/jpeg") || (imageType == "image/png") || (imageType == "image/gif"))
                        {
                            try
                            {
                                entItem.ImageUri = new Uri(url);
                            }
                            catch (Exception e)
                            {
                                ToDebugWindow(">> Exception @FeedClient@FillEntryItemFromXmlRss:new Uri()"
                                    + Environment.NewLine +
                                    "RSS feed entry (" + entItem.Name + ") contain invalid entry > enclosure@link Uri: " + e.Message +
                                    Environment.NewLine);
                            }
                        }
                        else if (imageType == "audio/mpeg")
                        {
                            try
                            {
                                entItem.AudioUri = new Uri(url);
                            }
                            catch (Exception e)
                            {
                                ToDebugWindow(">> Exception @FeedClient@FillEntryItemFromXmlRss:new Uri()"
                                    + Environment.NewLine +
                                    "RSS feed entry (" + entItem.Name + ") contain invalid entry > enclosure@link Uri: " + e.Message +
                                    Environment.NewLine);
                            }
                        }
                    }
                }
            }
        }

        // <itunes:image href=""/>
        if (entItem.ImageUri is null)
        {
            XmlNode? itunesImageNode = entryNode.SelectSingleNode("itunes:image", NsMgr);
            if (itunesImageNode != null)
            {
                if (itunesImageNode.Attributes != null)
                {
                    if (itunesImageNode.Attributes["href"] != null)
                    {
                        var url = itunesImageNode.Attributes["href"]!.Value;
                        try
                        {
                            entItem.ImageUri = new Uri(url);
                        }
                        catch (Exception e)
                        {
                            ToDebugWindow(">> Exception @FeedClient@FillEntryItemFromXmlRss:new Uri()"
                                + Environment.NewLine +
                                "RSS feed entry (" + entItem.Name + ") contain invalid entry > itunes:image@href Uri: " + e.Message +
                                Environment.NewLine);
                        }
                    }
                }
            }
        }

        if (entItem.ImageUri == null)
        {
            XmlNode? mediaThumbnailNode = entryNode.SelectSingleNode("media:thumbnail", NsMgr);
            if (mediaThumbnailNode != null)
            {
                if (mediaThumbnailNode.Attributes != null)
                {
                    if (mediaThumbnailNode.Attributes["url"] != null)
                    {
                        var url = mediaThumbnailNode.Attributes["url"]!.Value;
                        if (!string.IsNullOrEmpty(url))
                        {
                            try
                            {
                                entItem.ImageUri = new Uri(url);
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        // comments (//rss/channel/item/comments)
        XmlNode? entryCommentsNode = entryNode.SelectSingleNode("comments");
        if (entryCommentsNode != null)
        {
            var s = entryCommentsNode.InnerText;
            if (!string.IsNullOrEmpty(s))
            {
                try
                {
                    entItem.CommentUri = new Uri(s);
                }
                catch { }
            }
        }

        // description (//rss/channel/item/description)
        entItem.ContentType = EntryItem.ContentTypes.none;
        XmlNode? sum = entryNode.SelectSingleNode("description");
        if (sum != null)
        {
            var s = sum.InnerText;
            if (!string.IsNullOrEmpty(s))
            {
                entItem.ContentType = EntryItem.ContentTypes.unknown;

                entItem.Summary = s;
                if (!string.IsNullOrEmpty(s))
                {
                    // Summary
                    //entItem.Summary = await StripHtmlTags(entItem.Content);
                    //entItem.Summary = Truncate(entItem.Summary, 230);

                    //entItem.SummaryPlainText = Truncate(entItem.Summary, 78);

                    // gets image Uri
                    //if (entItem.ImageUri == null)
                    //    entItem.ImageUri = await GetImageUriFromHtml(s);
                }
            }
        }

        XmlNode? con = entryNode.SelectSingleNode("content:encoded", NsMgr);
        if (con != null)
        {
            var s = con.InnerText;
            if (!string.IsNullOrEmpty(s))
            {
                entItem.ContentType = EntryItem.ContentTypes.textHtml;

                // It wasn't a good idea to put in "Content" because the same thing show up in details page. Should just override"Summary".
                entItem.Content = s;
                //entItem.Summary = s;

                // gets image Uri
                //if (entItem.ImageUri == null)
                //    entItem.ImageUri = await GetImageUriFromHtml(s);
            }
        }

        if ((entItem.ContentType == EntryItem.ContentTypes.textHtml) || (entItem.ContentType == EntryItem.ContentTypes.unknown) || entItem.ContentType == EntryItem.ContentTypes.markdown)
        {
            entItem.ContentBaseUri = baseUri;
        }

        entItem.Status = FeedEntryItem.ReadStatus.rsNew;
    }

    private void FillEntryItemFromXmlRdf(FeedEntryItem entItem, XmlNode entryNode, XmlNamespaceManager NsMgr, Uri baseUri)
    {
        XmlNode? entryTitle = entryNode.SelectSingleNode("rss:title", NsMgr);
        entItem.Name = (entryTitle != null) ? WebUtility.HtmlDecode(entryTitle.InnerText) : "";

        XmlNode? entryLinkUri = entryNode.SelectSingleNode("rss:link", NsMgr);
        try
        {
            /*
            if (entryLinkUri != null)
                if (!string.IsNullOrEmpty(entryLinkUri.InnerText))
                    entItem.AltHtmlUri = new Uri(entryLinkUri.InnerText);
            */
            if (entryLinkUri != null)
            {
                var link = entryLinkUri.InnerText;
                if (!string.IsNullOrEmpty(link))
                {
                    if (link.StartsWith("http"))
                    {
                        entItem.AltHtmlUri = new Uri(link);
                    }
                    else
                    {
                        entItem.AltHtmlUri = new Uri(baseUri, link);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine("Exception @new Uri(entryLinkUri.InnerText)" + "(" + entItem.Name + ")" + " : " + e.Message);

            ToDebugWindow(">> Exception @RssFeedClient@FillEntryItemFromXmlRdf:new Uri()"
                + Environment.NewLine +
                "RSS feed entry (" + entItem.Name + ") contain invalid entry Uri: " + e.Message +
                Environment.NewLine);
        }

        if (string.IsNullOrEmpty(entItem.EntryId))
            if (entItem.AltHtmlUri != null)
                entItem.EntryId = entItem.AltHtmlUri.AbsoluteUri;

        XmlNode? entryPudDate = entryNode.SelectSingleNode("dc:date", NsMgr);
        if (entryPudDate != null)
        {
            var s = entryPudDate.InnerText;
            if (!string.IsNullOrEmpty(s))
            {
                try
                {
                    var date = DateTimeOffset.Parse(s);
                    entItem.Published = date.UtcDateTime;
                }
                catch (Exception e)
                {
                    //Debug.WriteLine("Exception @DateTime.Parse in the RSS 1.0 feed " + "(" + entItem.Name + ")" + " : " + e.Message);

                    ToDebugWindow(">> Exception @FeedClient@FillEntryItemFromXmlRdf: DateTime.Parse()"
                        + Environment.NewLine +
                        "RSS feed entry(" + entItem.Name + ") contain invalid entry dc:date: " + e.Message +
                        Environment.NewLine);
                }
            }
        }

        var entryAuthor = "";
        XmlNodeList? entryAuthors = entryNode.SelectNodes("dc:creator", NsMgr);
        if (entryAuthors != null)
        {
            foreach (XmlNode auth in entryAuthors)
            {
                if (string.IsNullOrEmpty(entryAuthor))
                    entryAuthor = auth.InnerText;
                else
                    entryAuthor += "/" + auth.InnerText;
            }
            entItem.Author = entryAuthor;
        }
        if (string.IsNullOrEmpty(entItem.Author))
        {
            //if (entItem.AltHtmlUri != null)
            //    entryAuthor = entItem.AltHtmlUri.Host;

            entItem.Author = "-";
        }

        var entryCategory = "";
        XmlNodeList? entryCategories = entryNode.SelectNodes("dc:subject", NsMgr);
        if (entryCategories != null)
        {
            foreach (XmlNode cat in entryCategories)
            {
                if (string.IsNullOrEmpty(entryCategory))
                    entryCategory = cat.InnerText;
                else
                    entryCategory += "/" + cat.InnerText;
            }
            entItem.Category = entryCategory;
        }

        if (string.IsNullOrEmpty(entItem.Category))
        {
            entItem.Category = "-";
        }

        XmlNode? mediaThumbnailNode = entryNode.SelectSingleNode("media:thumbnail", NsMgr);
        if (mediaThumbnailNode != null)
        {
            if (mediaThumbnailNode.Attributes != null)
            {
                if (mediaThumbnailNode.Attributes["url"] != null)
                {
                    var url = mediaThumbnailNode.Attributes["url"]!.Value;
                    if (!string.IsNullOrEmpty(url))
                    {
                        try
                        {
                            entItem.ImageUri = new Uri(url);
                        }
                        catch { }
                    }
                }
            }
        }

        if (entItem.ImageUri == null)
        {
            // hatena imageurl
            XmlNode? hatenaImgUri = entryNode.SelectSingleNode("hatena:imageurl", NsMgr);
            try
            {
                if (hatenaImgUri != null)
                    if (!string.IsNullOrEmpty(hatenaImgUri.InnerText))
                        entItem.ImageUri = new Uri(hatenaImgUri.InnerText);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception @new Uri(entryLinkUri.InnerText)" + "(" + entItem.Name + ")" + " : " + e.Message);

                ToDebugWindow(">> Exception @RssFeedClient@FillEntryItemFromXmlRdf:new Uri()"
                    + Environment.NewLine +
                    "RSS feed entry (" + entItem.Name + ") contain invalid image Uri: " + e.Message +
                    Environment.NewLine);
            }
        }

        // hatena commenturl
        XmlNode? hatenaCommentUri = entryNode.SelectSingleNode("hatena:bookmarkCommentListPageUrl", NsMgr);
        try
        {
            if (hatenaCommentUri != null)
                if (!string.IsNullOrEmpty(hatenaCommentUri.InnerText))
                    entItem.CommentUri = new Uri(hatenaCommentUri.InnerText);
        }
        catch (Exception e)
        {
            Debug.WriteLine("Exception @new Uri(entryLinkUri.InnerText)" + "(" + entItem.Name + ")" + " : " + e.Message);

            ToDebugWindow(">> Exception @RssFeedClient@FillEntryItemFromXmlRdf:new Uri()"
                + Environment.NewLine +
                "RSS feed entry (" + entItem.Name + ") contain invalid image Uri: " + e.Message +
                Environment.NewLine);
        }

        entItem.ContentType = EntryItem.ContentTypes.none;
        XmlNode? sum = entryNode.SelectSingleNode("rss:description", NsMgr);
        if (sum != null)
        {
            var s = sum.InnerText;
            if (!string.IsNullOrEmpty(s))
            {
                entItem.ContentType = EntryItem.ContentTypes.unknown;

                entItem.Summary = sum.InnerText;
                if (!string.IsNullOrEmpty(s))
                {
                    // Summary
                    //entItem.Summary = await StripHtmlTags(entItem.Content);

                    //entItem.SummaryPlainText = Truncate(entItem.SummaryPlainText, 78);
                }

                // gets image Uri
                //if (entItem.ImageUri == null)
                //    entItem.ImageUri = await GetImageUriFromHtml(s);
            }
        }

        XmlNode? con = entryNode.SelectSingleNode("content:encoded", NsMgr);
        if (con != null)
        {
            var s = con.InnerText;
            if (!string.IsNullOrEmpty(s))
            {
                entItem.ContentType = EntryItem.ContentTypes.textHtml;
                // It wasn't a good idea to put in "Content" because the same thing show up in details page. Should just override"Summary".
                entItem.Content = s;
                //entItem.Summary = s;
                //entItem.Content = s;

                // gets image Uri
                //if (entItem.ImageUri == null)
                //    entItem.ImageUri = await GetImageUriFromHtml(s);
            }
        }

        if ((entItem.ContentType == EntryItem.ContentTypes.textHtml) || (entItem.ContentType == EntryItem.ContentTypes.unknown) || entItem.ContentType == EntryItem.ContentTypes.markdown)
        {
            entItem.ContentBaseUri = baseUri;
        }

        entItem.Status = FeedEntryItem.ReadStatus.rsNew;
    }

    private void FillEntryItemFromXmlAtom03(FeedEntryItem entItem, XmlNode entryNode, XmlNamespaceManager atomNsMgr, Uri baseUri)
    {
        XmlNode? entryTitle = entryNode.SelectSingleNode("atom:title", atomNsMgr);
        if (entryTitle != null)
        {
            entItem.Name = WebUtility.HtmlDecode(entryTitle.InnerText);
        }

        XmlNode? entryID = entryNode.SelectSingleNode("atom:id", atomNsMgr);
        if (entryID != null)
        {
            entItem.EntryId = entryID.InnerText;
        }

        XmlNodeList? entryLinkUris = entryNode.SelectNodes("atom:link", atomNsMgr);
        string relAttr;
        string hrefAttr;
        string typeAttr;

        Uri? altUri = null;

        if (entryLinkUris != null)
        {
            foreach (XmlNode u in entryLinkUris)
            {
                if (u.Attributes is null)
                {
                    continue;
                }

                relAttr = (u.Attributes["rel"] != null) ? u.Attributes["rel"]!.Value : "";
                hrefAttr = (u.Attributes["href"] != null) ? u.Attributes["href"]!.Value : "";
                typeAttr = (u.Attributes["type"] != null) ? u.Attributes["type"]!.Value : "";

                if (!string.IsNullOrEmpty(hrefAttr))
                {
                    switch (relAttr)
                    {
                        case "service.post":
                            try
                            {
                                //editUri = new Uri(hrefAttr);
                                break;
                            }
                            catch
                            {
                                break;
                            }
                        case "alternate":
                            try
                            {
                                if (!string.IsNullOrEmpty(typeAttr))
                                {
                                    if (typeAttr == "text/html")
                                    {
                                        try
                                        {
                                            //altUri = new Uri(hrefAttr);
                                            if (hrefAttr.StartsWith("http"))
                                            {
                                                altUri = new Uri(hrefAttr);
                                            }
                                            else
                                            {
                                                altUri = new Uri(baseUri, hrefAttr);
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Debug.WriteLine("Exception @new Uri(altUri) @ FeedClient Atom0.3" + "(" + entItem.Name + ")" + " : " + e.Message);
                                        }
                                    }
                                }
                                break;
                            }
                            catch
                            {
                                break;
                            }
                        case "": //same as alternate
                            try
                            {
                                if (!string.IsNullOrEmpty(typeAttr))
                                {
                                    if (typeAttr == "text/html")
                                    {
                                        try
                                        {
                                            //altUri = new Uri(hrefAttr);
                                            if (hrefAttr.StartsWith("http"))
                                            {
                                                altUri = new Uri(hrefAttr);
                                            }
                                            else
                                            {
                                                altUri = new Uri(baseUri, hrefAttr);
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Debug.WriteLine("Exception @new Uri(altUri) @ FeedClient Atom0.3" + "(" + entItem.Name + ")" + " : " + e.Message);
                                        }
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        // I am not happy but let's assume it is html.
                                        //altUri = new Uri(hrefAttr);
                                        if (hrefAttr.StartsWith("http"))
                                        {
                                            altUri = new Uri(hrefAttr);
                                        }
                                        else
                                        {
                                            altUri = new Uri(baseUri, hrefAttr);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.WriteLine("Exception @new Uri(altUri) @ FeedClient Atom0.3" + "(" + entItem.Name + ")" + " : " + e.Message);
                                    }
                                }
                                break;
                            }
                            catch
                            {
                                break;
                            }
                    }
                }
            }
        }

        //entItem.EditUri = editUri;
        entItem.AltHtmlUri = altUri;

        if (string.IsNullOrEmpty(entItem.EntryId))
            if (entItem.AltHtmlUri != null)
                entItem.EntryId = entItem.AltHtmlUri.AbsoluteUri;

        XmlNode? entryPublished = entryNode.SelectSingleNode("atom:issued", atomNsMgr);
        if (entryPublished != null)
        {
            if (!string.IsNullOrEmpty(entryPublished.InnerText))
            {
                try
                {
                    entItem.Published = XmlConvert.ToDateTime(entryPublished.InnerText, XmlDateTimeSerializationMode.Utc);
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Exception @XmlConvert.ToDateTime in the Atom 0.3 feed " + "(" + entItem.Name + ")" + " : " + e.Message);
                }
            }
        }

        var entryAuthor = "";
        XmlNodeList? entryAuthors = entryNode.SelectNodes("atom:author", atomNsMgr);
        if (entryAuthors != null)
        {
            foreach (XmlNode auth in entryAuthors)
            {
                XmlNode? authName = auth.SelectSingleNode("atom:name", atomNsMgr);
                if (authName != null)
                {
                    if (string.IsNullOrEmpty(entryAuthor))
                        entryAuthor = authName.InnerText;
                    else
                        entryAuthor += "/" + authName.InnerText;
                }
            }
            entItem.Author = entryAuthor;
        }

        if (string.IsNullOrEmpty(entItem.Author))
        {
            //if (altUri != null)
            //    entryAuthor = altUri.Host;
            entItem.Author = "-";
        }

        entItem.ContentType = EntryItem.ContentTypes.none;

        XmlNode? cont = entryNode.SelectSingleNode("atom:content", atomNsMgr);
        if (cont != null)
        {
            if (cont.Attributes != null)
            {
                if (cont.Attributes["type"] != null)
                {
                    var contype = cont.Attributes["type"]!.Value;
                    if (!string.IsNullOrEmpty(contype))
                    {
                        //entItem.ContentTypeString = contype;

                        switch (contype)
                        {
                            case "text":
                                entItem.ContentType = EntryItem.ContentTypes.text;
                                break;
                            case "html":
                                entItem.ContentType = EntryItem.ContentTypes.textHtml;
                                break;
                            case "xhtml":
                                entItem.ContentType = EntryItem.ContentTypes.textHtml;
                                break;
                            case "text/plain":
                                entItem.ContentType = EntryItem.ContentTypes.text;
                                break;
                            case "text/html":
                                entItem.ContentType = EntryItem.ContentTypes.textHtml;
                                break;
                            case "text/x-markdown":
                                entItem.ContentType = EntryItem.ContentTypes.markdown;
                                break;
                            case "text/x-hatena-syntax":
                                entItem.ContentType = EntryItem.ContentTypes.hatena;
                                break;
                            default:
                                entItem.ContentType = EntryItem.ContentTypes.unknown;
                                break;
                        }
                    }
                }
                else
                {
                    entItem.ContentType = EntryItem.ContentTypes.unknown;
                }
            }
            else
            {
                entItem.ContentType = EntryItem.ContentTypes.unknown;
            }

            entItem.Content = cont.InnerText;

            if ((entItem.ContentType == EntryItem.ContentTypes.textHtml) || (entItem.ContentType == EntryItem.ContentTypes.unknown) || entItem.ContentType == EntryItem.ContentTypes.markdown)
            {
                // TODO: if content element has a baseuri attribute, use it instead.
                entItem.ContentBaseUri = baseUri;
            }
        }

        XmlNode? sum = entryNode.SelectSingleNode("atom:summary", atomNsMgr);
        if (sum != null)
        {
            entItem.Summary = sum.InnerText;
            //entry.ContentType = EntryFull.ContentTypes.textHtml;

            if (!string.IsNullOrEmpty(entItem.Summary))
            {
                //entItem.Summary = await StripHtmlTags(entItem.Summary);
                //entItem.SummaryPlainText = Truncate(sum.InnerText, 78);
            }
        }
        else
        {
            var s = entItem.Content;

            if (!string.IsNullOrEmpty(s))
            {
                if (entItem.ContentType == EntryItem.ContentTypes.textHtml)
                {
                    //entItem.Summary = await StripHtmlTags(s);
                    //entItem.SummaryPlainText = Truncate(s, 78);
                }
                else if (entItem.ContentType == EntryItem.ContentTypes.text)
                {
                    //entItem.Summary = s;
                    //entItem.SummaryPlainText = Truncate(s, 78);
                }
            }
        }

        entItem.Status = FeedEntryItem.ReadStatus.rsNew;

        if (entItem.ContentType == EntryItem.ContentTypes.textHtml)
        {
            // gets image Uri
            //entItem.ImageUri ??= await GetImageUriFromHtml(entItem.Content);
        }
    }

    private void FillEntryItemFromXmlAtom10(FeedEntryItem entItem, XmlNode entryNode, XmlNamespaceManager atomNsMgr, Uri baseUri)
    {
        // title
        XmlNode? entryTitle = entryNode.SelectSingleNode("atom:title", atomNsMgr);
        if (entryTitle != null)
        {
            entItem.Name = WebUtility.HtmlDecode(entryTitle.InnerText);
        }

        // id
        XmlNode? entryID = entryNode.SelectSingleNode("atom:id", atomNsMgr);
        if (entryID != null)
        {
            entItem.EntryId = entryID.InnerText;
        }

        // link
        XmlNodeList? entryLinkUris = entryNode.SelectNodes("atom:link", atomNsMgr);
        string relAttr;
        string hrefAttr;
        string typeAttr;
        //Uri? editUri = null;
        Uri? altUri = null;
        if (entryLinkUris != null)
        {
            foreach (XmlNode u in entryLinkUris)
            {
                if (u.Attributes == null) { continue; }

                relAttr = (u.Attributes["rel"] != null) ? u.Attributes["rel"]!.Value : "";
                hrefAttr = (u.Attributes["href"] != null) ? u.Attributes["href"]!.Value : "";
                typeAttr = (u.Attributes["type"] != null) ? u.Attributes["type"]!.Value : "";

                if (!string.IsNullOrEmpty(hrefAttr))
                {
                    switch (relAttr)
                    {
                        case "edit":
                            try
                            {
                                //entry.EditUri = new Uri(hrefAttr);
                                break;
                            }
                            catch (Exception e)
                            {
                                Debug.WriteLine("Exception @new Uri(editUri) @ FeedClient Atom1.0" + "(" + entItem.Name + ")" + " : " + e.Message);

                                ToDebugWindow(">> Exception @FeedClient@CreateAtomEntryFromXmlAtom@ new Uri(editUri)"
                                    + Environment.NewLine +
                                    "Atom feed entry(" + entItem.Name + ") contain invalid entry atom:editUri: " + e.Message +
                                    Environment.NewLine);

                                break;
                            }
                        case "alternate":
                            try
                            {
                                if (!string.IsNullOrEmpty(typeAttr))
                                {
                                    if (typeAttr == "text/html")
                                    {
                                        try
                                        {
                                            //altUri = new Uri(hrefAttr);
                                            if (hrefAttr.StartsWith("http"))
                                            {
                                                // Absolute uri.
                                                altUri = new Uri(hrefAttr);
                                            }
                                            else
                                            {
                                                // Relative uri (probably...)
                                                // Uri(baseUri, relativeUriString)
                                                altUri = new Uri(baseUri, hrefAttr);
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Debug.WriteLine("Exception @new Uri(altUri) @ FeedClient Atom1.0" + "(" + entItem.Name + ")" + " : " + e.Message);

                                            ToDebugWindow(">> Exception @FeedClient@CreateAtomEntryFromXmlAtom@ new Uri(altUri)"
                                                + Environment.NewLine +
                                                "Atom feed entry(" + entItem.Name + ") contain invalid entry atom:altUri: " + e.Message +
                                                Environment.NewLine);
                                        }
                                    }
                                }
                                else if (string.IsNullOrEmpty(typeAttr))
                                {
                                    try
                                    {
                                        // let's assume it is html.
                                        //altUri = new Uri(hrefAttr);
                                        if (hrefAttr.StartsWith("http"))
                                        {
                                            // Absolute uri.
                                            altUri = new Uri(hrefAttr);
                                        }
                                        else
                                        {
                                            // Relative uri (probably...)
                                            // Uri(baseUri, relativeUriString)
                                            altUri = new Uri(baseUri, hrefAttr);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.WriteLine("Exception @new Uri(altUri) @ FeedClient Atom1.0" + "(" + entItem.Name + ")" + " : " + e.Message);

                                        ToDebugWindow(">> Exception @FeedClient@CreateAtomEntryFromXmlAtom@ new Uri(altUri)"
                                            + Environment.NewLine +
                                            "Atom feed entry(" + entItem.Name + ") contain invalid entry atom:altUri: " + e.Message +
                                            Environment.NewLine);
                                    }
                                }
                                break;
                            }
                            catch
                            {
                                break;
                            }
                        case "enclosure":
                            try
                            {
                                if (!string.IsNullOrEmpty(typeAttr))
                                {
                                    if ((typeAttr == "image/jpg") || (typeAttr == "image/jpeg") || (typeAttr == "image/png") || (typeAttr == "image/gif"))
                                    {
                                        try
                                        {
                                            //entItem.ImageUri = new Uri(hrefAttr);
                                            if (hrefAttr.StartsWith("http"))
                                            {
                                                // Absolute uri.
                                                entItem.ImageUri = new Uri(hrefAttr);
                                            }
                                            else
                                            {
                                                // Relative uri (probably...)
                                                // Uri(baseUri, relativeUriString)
                                                entItem.ImageUri = new Uri(baseUri, hrefAttr);
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            ToDebugWindow(">> Exception @FeedClient@CreateAtomEntryFromXmlAtom:new Uri()"
                                                + Environment.NewLine +
                                                "Atom feed entry (" + entItem.Name + ") contain invalid entry > enclosure@link Uri: " + e.Message +
                                                Environment.NewLine);
                                        }
                                    }
                                }
                                else if ((typeAttr == "audio/mpeg"))
                                {
                                    try
                                    {
                                        //entItem.AudioUri = new Uri(hrefAttr);
                                        if (hrefAttr.StartsWith("http"))
                                        {
                                            // Absolute uri.
                                            entItem.AudioUri = new Uri(hrefAttr);
                                        }
                                        else
                                        {
                                            // Relative uri (probably...)
                                            // Uri(baseUri, relativeUriString)
                                            entItem.AudioUri = new Uri(baseUri, hrefAttr);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        ToDebugWindow(">> Exception @FeedClient@CreateAtomEntryFromXmlAtom:new Uri()"
                                            + Environment.NewLine +
                                            "Atom feed entry (" + entItem.Name + ") contain invalid entry > enclosure@link Uri: " + e.Message +
                                            Environment.NewLine);
                                    }
                                }
                                break;
                            }
                            catch
                            {
                                break;
                            }
                        case "": //same as alternate
                            try
                            {
                                if (!string.IsNullOrEmpty(typeAttr))
                                {
                                    if (typeAttr == "text/html")
                                    {
                                        try
                                        {
                                            //altUri = new Uri(hrefAttr);
                                            if (hrefAttr.StartsWith("http"))
                                            {
                                                // Absolute uri.
                                                altUri = new Uri(hrefAttr);
                                            }
                                            else
                                            {
                                                // Relative uri (probably...)
                                                // Uri(baseUri, relativeUriString)
                                                altUri = new Uri(baseUri, hrefAttr);
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Debug.WriteLine("Exception @new Uri(altUri) @ FeedClient Atom1.0" + "(" + entItem.Name + ")" + " : " + e.Message);

                                            ToDebugWindow(">> Exception @FeedClient@CreateAtomEntryFromXmlAtom@ new Uri(altUri)"
                                                + Environment.NewLine +
                                                "Atom feed entry(" + entItem.Name + ") contain invalid entry atom:altUri: " + e.Message +
                                                Environment.NewLine);
                                        }
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        // I am not happy but let's assume it is html.
                                        //altUri = new Uri(hrefAttr);
                                        if (hrefAttr.StartsWith("http"))
                                        {
                                            // Absolute uri.
                                            altUri = new Uri(hrefAttr);
                                        }
                                        else
                                        {
                                            // Relative uri (probably...)
                                            // Uri(baseUri, relativeUriString)
                                            altUri = new Uri(baseUri, hrefAttr);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.WriteLine("Exception @new Uri(altUri) @ FeedClient Atom1.0" + "(" + entItem.Name + ")" + " : " + e.Message);

                                        ToDebugWindow(">> Exception @FeedClient@CreateAtomEntryFromXmlAtom@ new Uri(altUri)"
                                            + Environment.NewLine +
                                            "Atom feed entry(" + entItem.Name + ") contain invalid entry atom:altUri: " + e.Message +
                                            Environment.NewLine);
                                    }
                                }
                                break;
                            }
                            catch
                            {
                                break;
                            }
                    }
                }
            }
            entItem.AltHtmlUri = altUri;
        }

        if (string.IsNullOrEmpty(entItem.EntryId))
            if (entItem.AltHtmlUri != null)
                entItem.EntryId = entItem.AltHtmlUri.AbsoluteUri;

        // published
        XmlNode? entryPublished = entryNode.SelectSingleNode("atom:published", atomNsMgr);
        if (entryPublished != null)
        {
            if (!string.IsNullOrEmpty(entryPublished.InnerText))
            {
                try
                {
                    entItem.Published = XmlConvert.ToDateTime(entryPublished.InnerText, XmlDateTimeSerializationMode.Utc);
                }
                catch (Exception e)
                {
                    //Debug.WriteLine("Exception @XmlConvert.ToDateTime in the Atom 1.0 feed " + "(" + entry.Name + ")" + " : " + e.Message);

                    ToDebugWindow(">> Exception @FeedClient@CreateAtomEntryFromXmlAtom: XmlConvert.ToDateTime()"
                        + Environment.NewLine +
                        "Atom feed entry(" + entItem.Name + ") contain invalid entry atom:published: " + e.Message +
                        Environment.NewLine);
                }
            }
        }

        // updated
        XmlNode? entryUpdated = entryNode.SelectSingleNode("atom:updated", atomNsMgr);
        if (entryUpdated != null)
        {
            if (!string.IsNullOrEmpty(entryUpdated.InnerText))
            {
                try
                {
                    entItem.Updated = XmlConvert.ToDateTime(entryUpdated.InnerText, XmlDateTimeSerializationMode.Utc);
                }
                catch (Exception e)
                {
                    //Debug.WriteLine("Exception @XmlConvert.ToDateTime in the Atom 1.0 feed " + "(" + entry.Name + ")" + " : " + e.Message);

                    ToDebugWindow(">> Exception @FeedClient@CreateAtomEntryFromXmlAtom: XmlConvert.ToDateTime()"
                        + Environment.NewLine +
                        "Atom feed entry(" + entItem.Name + ") contain invalid entry atom:Updated: " + e.Message +
                        Environment.NewLine);
                }
            }
        }

        // author 
        var entryAuthor = "";
        XmlNodeList? entryAuthors = entryNode.SelectNodes("atom:author", atomNsMgr);
        if (entryAuthors != null)
        {
            foreach (XmlNode auth in entryAuthors)
            {
                XmlNode? authName = auth.SelectSingleNode("atom:name", atomNsMgr);
                if (authName != null)
                {
                    if (string.IsNullOrEmpty(entryAuthor))
                        entryAuthor = authName.InnerText;
                    else
                        entryAuthor += "/" + authName.InnerText;
                }
            }
            entItem.Author = entryAuthor;
        }

        if (string.IsNullOrEmpty(entItem.Author))
        {
            //if (altUri != null)
            //    entryAuthor = altUri.Host;
            entItem.Author = "-";
        }

        // category 
        var entryCategory = "";
        XmlNodeList? entryCategories = entryNode.SelectNodes("atom:category", atomNsMgr);
        if (entryCategories != null)
        {
            foreach (XmlNode cat in entryCategories)
            {
                if (cat.Attributes == null) { continue; }

                if (cat.Attributes["term"] != null)
                {
                    if (string.IsNullOrEmpty(entryCategory))
                        entryCategory = cat.Attributes["term"]!.Value;
                    else
                        entryCategory += "/" + cat.Attributes["term"]!.Value;
                }
            }
            entItem.Category = entryCategory;
        }

        if (string.IsNullOrEmpty(entItem.Category))
        {
            entItem.Category = "-";
        }

        // TODO: source 
        var entrySource = "";
        XmlNode? entrySourceNode = entryNode.SelectSingleNode("atom:source", atomNsMgr);
        if (entrySourceNode != null)
        {
            if (string.IsNullOrEmpty(entrySource))
            {
                if (entrySourceNode.Attributes != null)
                {
                    if (entrySourceNode.Attributes["title"] != null)
                    {
                        //entrySource = entrySourceNode.Attributes["title"].Value;

                    }
                }
            }
            //entry.Source = entrySource;
        }

        // content and ContentType
        entItem.ContentType = EntryItem.ContentTypes.none;
        XmlNode? cont = entryNode.SelectSingleNode("atom:content", atomNsMgr);
        if (cont != null)
        {
            if (cont.Attributes != null)
            {
                if (cont.Attributes["type"] != null)
                {
                    var contype = cont.Attributes["type"]!.Value; 
                    if (!string.IsNullOrEmpty(contype))
                    {
                        //entry.ContentTypeString = contype;

                        switch (contype)
                        {
                            case "text":
                                entItem.ContentType = EntryItem.ContentTypes.text;
                                break;
                            case "html":
                                entItem.ContentType = EntryItem.ContentTypes.textHtml;
                                break;
                            case "xhtml":
                                entItem.ContentType = EntryItem.ContentTypes.textHtml;
                                break;
                            case "text/plain":
                                entItem.ContentType = EntryItem.ContentTypes.text;
                                break;
                            case "text/html":
                                entItem.ContentType = EntryItem.ContentTypes.textHtml;
                                break;
                            case "text/x-markdown":
                                entItem.ContentType = EntryItem.ContentTypes.markdown;
                                break;
                            case "text/x-hatena-syntax":
                                entItem.ContentType = EntryItem.ContentTypes.hatena;
                                break;
                            default:
                                entItem.ContentType = EntryItem.ContentTypes.unknown;
                                break;
                        }
                    }
                }
                else
                {
                    entItem.ContentType = EntryItem.ContentTypes.unknown;
                }
            }
            else
            {
                entItem.ContentType = EntryItem.ContentTypes.unknown;
            }

            // TODO: if xhtml, use innerXML
            entItem.Content = cont.InnerText;

            if ((entItem.ContentType == EntryItem.ContentTypes.textHtml) || (entItem.ContentType == EntryItem.ContentTypes.unknown) || entItem.ContentType == EntryItem.ContentTypes.markdown)
            {
                // TODO: if content element has a baseuri attribute, use it instead.
                entItem.ContentBaseUri = baseUri;
            }
        }

        // summary
        XmlNode? sum = entryNode.SelectSingleNode("atom:summary", atomNsMgr);
        if (sum != null)
        {
            entItem.Summary = sum.InnerText;
            //entry.ContentType = EntryFull.ContentTypes.textHtml;

            if (!string.IsNullOrEmpty(entItem.Summary))
            {
                //entItem.Summary = await StripHtmlTags(entItem.Summary);
                //entItem.SummaryPlainText = Truncate(sum.InnerText, 78);
            }
        }
        else
        {
            var s = entItem.Content;

            if (!string.IsNullOrEmpty(s))
            {
                if (entItem.ContentType == EntryItem.ContentTypes.textHtml)
                {
                    //entItem.Summary = await StripHtmlTags(s);
                    //entItem.Summary = s;
                }
                else if (entItem.ContentType == EntryItem.ContentTypes.text)
                {
                    //entItem.Summary = s;
                    //entItem.SummaryPlainText = Truncate(s, 78);
                }
            }
        }

        // xmlns:media="http://search.yahoo.com/mrss/" eg YouTube
        XmlNode? mediaNode = entryNode.SelectSingleNode("media:group", atomNsMgr);
        if (mediaNode != null)
        {
            if (string.IsNullOrEmpty(entItem.Name))
            {
                XmlNode? mediaTitleNode = mediaNode.SelectSingleNode("media:title", atomNsMgr);
                if (mediaTitleNode != null)
                {
                    entItem.Name = mediaTitleNode.InnerText;
                }
            }

            XmlNode? mediaContentNode = mediaNode.SelectSingleNode("media:content", atomNsMgr);
            if (mediaContentNode != null)
            {
                if (mediaContentNode.Attributes != null)
                {
                    if (mediaContentNode.Attributes["url"] != null)
                    {
                        var url = mediaContentNode.Attributes["url"]!.Value;
                        if (!string.IsNullOrEmpty(url))
                        {
                            if (mediaContentNode.Attributes["type"] != null)
                            {
                                var type = mediaContentNode.Attributes["type"]!.Value;
                                if (!string.IsNullOrEmpty(type))
                                {
                                    if (type == "application/x-shockwave-flash")
                                    {
                                        // YouTube does this...
                                    }
                                    else if (type == "video/mp4")
                                    {
                                        //
                                    }
                                    else if (type == "audio/mp4")
                                    {
                                        //
                                    }
                                    else if (type == "audio/mpeg")
                                    {
                                        //mp3
                                        try
                                        {
                                            entItem.AudioUri = new Uri(url);
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            XmlNode? mediaThumbnailNode = mediaNode.SelectSingleNode("media:thumbnail", atomNsMgr);
            if (mediaThumbnailNode != null)
            {
                if (mediaThumbnailNode.Attributes != null)
                {
                    if (mediaThumbnailNode.Attributes["url"] != null)
                    {
                        var url = mediaThumbnailNode.Attributes["url"]!.Value;
                        if (!string.IsNullOrEmpty(url))
                        {
                            try
                            {
                                entItem.ImageUri = new Uri(url);
                            }
                            catch { }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(entItem.Summary))
            {
                XmlNode? mediaDescriptionNode = mediaNode.SelectSingleNode("media:description", atomNsMgr);
                if (mediaDescriptionNode != null)
                {
                    entItem.Summary = mediaDescriptionNode.InnerText;
                }
            }
        }


        if (entItem.ImageUri == null)
        {
            XmlNode? mediaThumbnailNode = entryNode.SelectSingleNode("media:thumbnail", atomNsMgr);
            if (mediaThumbnailNode != null)
            {
                if (mediaThumbnailNode.Attributes != null)
                {
                    if (mediaThumbnailNode.Attributes["url"] != null)
                    {
                        var url = mediaThumbnailNode.Attributes["url"]!.Value;
                        if (!string.IsNullOrEmpty(url))
                        {
                            try
                            {
                                entItem.ImageUri = new Uri(url);
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        // image if not 
        if (entItem.ContentType == EntryItem.ContentTypes.textHtml)
        {
            // gets image Uri
            //if (entItem.ImageUri == null)
            //    entItem.ImageUri = await GetImageUriFromHtml(entItem.Content);
        }

        entItem.Status = FeedEntryItem.ReadStatus.rsNew;

        /*
<?xml version="1.0" encoding="utf-8"?>
<feed xmlns="http://www.w3.org/2005/Atom">
<title type="text">dive into mark</title>
<subtitle type="html"> A &lt;em&gt;lot&lt;/em&gt; of effort went into making this effortless</subtitle>
<updated>2005-07-31T12:29:29Z</updated>
<id>tag:example.org,2003:3</id>
<link rel="alternate" type="text/html" hreflang="en" href="http://example.org/"/>
<link rel="self" type="application/atom+xml" href="http://example.org/feed.atom"/>
<rights>Copyright (c) 2003, Mark Pilgrim</rights>
<generator uri="http://www.example.com/" version="1.0">Example Toolkit</generator>
<entry>
<title>Atom draft-07 snapshot</title>
<link rel="alternate" type="text/html" href="http://example.org/2005/04/02/atom"/>
<link rel="enclosure" type="audio/mpeg" length="1337" href="http://example.org/audio/ph34r_my_podcast.mp3"/>
<id>tag:example.org,2003:3.2397</id>
<updated>2005-07-31T12:29:29Z</updated>
<published>2003-12-13T08:29:29-04:00</published>
<author>
<name>Mark Pilgrim</name>
 <uri>http://example.org/</uri>
<email>f8dy@example.com</email>
</author>
<contributor>
<name>Sam Ruby</name>
</contributor>
<contributor>
<name>Joe Gregorio</name>
</contributor>
<content type="xhtml" xml:lang="en" xml:base="http://diveintomark.org/">
<div xmlns="http://www.w3.org/1999/xhtml"><p><i>[Update: The Atom draft is finished.]</i></p></div>
</content>
</entry>
</feed>
*/

        // TODO:
        //app:edited

        /*
        //app:control/app:draft(yes/no)
        XmlNode entryDraft = entryNode.SelectSingleNode("app:control/app:draft", atomNsMgr);
        if (entryDraft == null) System.Diagnostics.Debug.WriteLine("app:draft: is null.");

        string draft = entryDraft?.InnerText;
        entry.IsDraft = (String.Compare(draft, "yes", true) == 0) ? true : false;
        */

        /*
        XmlNode entryUpdated = entryNode.SelectSingleNode("atom:updated", atomNsMgr);
        if (entryUpdated != null)
        {
            if (!string.IsNullOrEmpty(entryUpdated.InnerText))
            {
                 = XmlConvert.ToDateTime(entryUpdated.InnerText, XmlDateTimeSerializationMode.Utc);
            }
        }
        */

        //AtomEntryHatena
        /*
        // Hatena's formatted-content
        XmlNode formattedContent = entryNode.SelectSingleNode("hatena:formatted-content", atomNsMgr);
        if (formattedContent != null)
        {
            entry.FormattedContent = formattedContent.InnerText;
        }
        */
    }
}
