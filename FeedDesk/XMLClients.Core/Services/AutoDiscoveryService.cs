using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using XmlClients.Core.Models;
using XmlClients.Core.Contracts.Services;
using HtmlAgilityPack;

namespace XmlClients.Core.Services;

#region == Result Classes for Service Discovery ==

public class FeedLink
{
    public enum FeedKinds
    {
        Atom,
        Rss,
        Unknown
    }

    public Uri FeedUri { get; set; }

    public string Title { get; set; }

    public Uri? SiteUri { get; set; }

    public string SiteTitle { get; set; }

    public FeedKinds FeedKind { get; set; }

    public FeedLink(Uri feedUri, FeedKinds feedKind, string title, Uri? siteUri, string siteTitle)
    {
        FeedUri = feedUri;
        FeedKind = feedKind;
        Title = title;
        SiteUri = siteUri;
        SiteTitle = siteTitle;
    }
}

public abstract class SearviceDocumentLinkBase
{
    public SearviceDocumentLinkBase()
    {

    }
}

public class SearviceDocumentLinkErr : SearviceDocumentLinkBase
{
    public string ErrTitle
    {
        get; set;
    }
    public string ErrDescription
    {
        get; set;
    }

    public SearviceDocumentLinkErr(string errorTitle, string errorDescription)
    {
        ErrTitle = errorTitle;
        ErrDescription = errorDescription;
    }
}

public class AppLink : SearviceDocumentLinkBase
{
    public NodeService? NodeService
    {
        get; set;
    }

    public AppLink()
    {

    }
}

public class RsdApi
{
    public string? Name
    {
        get; set;
    }

    public string? BlogID
    {
        get; set;
    }

    public bool Preferred { get; set; } = false;

    public Uri? ApiLink
    {
        get; set;
    }
}

public class RsdLink : SearviceDocumentLinkBase
{
    public string? Title
    {
        get; set;
    }

    public string? EngineName
    {
        get; set;
    }

    public Uri? HomePageLink
    {
        get; set;
    }

    public List<RsdApi> Apis { get; set; } = new();

    public RsdLink()
    {

    }
}

// Base class for Result.
public abstract class ServiceResultBase
{

}

// Error Class that Holds ErrorInfo (BasedOn ServiceResultBase)
public class ServiceResultErr : ServiceResultBase
{
    public string ErrTitle { get; set; }
    public string ErrDescription { get; set; }

    public ServiceResultErr(string et, string ed)
    {
        ErrTitle = et;
        ErrDescription = ed;
    }
}

// AuthRequired Class (BasedOn ServiceResultBase)
public class ServiceResultAuthRequired : ServiceResultBase
{
    public Uri Addr { get; set; }

    public ServiceResultAuthRequired(Uri addr)
    {
        Addr = addr;
    }
}

// HTML Result Class that Holds Feeds and Service Links embedded in HTML. (BasedOn ServiceResultBase)
public class ServiceResultHtmlPage : ServiceResultBase
{
    // eg, err while getting rsd document.
    public bool HasError;
    public string ErrTitle { get; set; } = "";
    public string ErrDescription {get; set; } = "";

    private ObservableCollection<FeedLink> _feeds = new();
    public ObservableCollection<FeedLink> Feeds
    {
        get => _feeds;
        set
        {
            if (_feeds == value)
            {
                return;
            }

            _feeds = value;
        }
    }

    private ObservableCollection<SearviceDocumentLinkBase> _services = new();
    public ObservableCollection<SearviceDocumentLinkBase> Services
    {
        get => _services;
        set
        {
            if (_services == value)
            {
                return;
            }

            _services = value;
        }
    }

    public ServiceResultHtmlPage()
    {

    }
}

// Feed Result Class That Holds Feed link info. (BasedOn ServiceResultBase)
public class ServiceResultFeed : ServiceResultBase
{
    public FeedLink? FeedlinkInfo;

    public ServiceResultFeed()
    {

    }
}

public class ServiceResultRsd : ServiceResultBase
{
    public RsdLink? Rsd;

    public ServiceResultRsd()
    {

    }
}

// Base Class for Service Result That Holds Feed link info. (BasedOn ServiceResultBase)
public abstract class ServiceResult : ServiceResultBase
{
    public ServiceTypes ServiceType { get; set; }

    public Uri EndpointUri;

    public AuthTypes AuthType { get; set; } = AuthTypes.Wsse;

    public ServiceResult(ServiceTypes serviceType, Uri endpointUri, AuthTypes authType)
    {
        ServiceType = serviceType;
        EndpointUri = endpointUri;
        AuthType = authType;
    }
}

// AtomPub Service Result Class That Holds NodeService (SearviceDocumentLink info). (BasedOn ServiceResult)
public class ServiceResultAtomPub : ServiceResult
{
    public NodeService AtomService { get; set; }

    public ServiceResultAtomPub(Uri endpointUri, AuthTypes authType, NodeService nodeService) : base(ServiceTypes.AtomPub, endpointUri, authType)
    {
        //Service = ServiceTypes.AtomPub;
        //EndpointUri = endpointUri;
        AtomService = nodeService;
    }
}

public class ServiceResultAtomAPI : ServiceResult
{
    public ServiceResultAtomAPI(Uri endpointUri, AuthTypes authType) : base(ServiceTypes.AtomApi, endpointUri, authType)
    {
        //ServiceType = ServiceTypes.AtomApi;
        //EndpointUri = endpointUri;
    }
}

public class ServiceResultXmlRpc : ServiceResult
{
    // XML-RPC specific blogid. 
    public string? BlogID { get; set; }

    public ServiceResultXmlRpc(Uri endpointUri, AuthTypes authType) : base(ServiceTypes.XmlRpc, endpointUri, authType)
    {
        ServiceType = ServiceTypes.XmlRpc;
        EndpointUri = endpointUri;
    }
}

#endregion

// Service Discovery class.
public class AutoDiscoveryService : IAutoDiscoveryService
{
    private readonly HttpClient _httpClient;

    #region == Events ==

    //public delegate void ServiceDiscoveryStatusUpdate(ServiceDiscovery sender, string data);

    public event AutoDiscoveryStatusUpdateEventHandler? StatusUpdate;

    #endregion

    public AutoDiscoveryService()
    {
        _httpClient = new HttpClient();

        // Accept HTML 
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        // Atom service document
        //_httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atomsvc+xml"));
        // Atom category document
        //_httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atomcat+xml"));

        // RSD file
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rsd+xml"));

        // Generic XML
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

        // Feed
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atom+xml"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rss+xml"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rdf+xml"));

        // This is a little hack for wordpress.com. Without this, wordpress.com returns HTTP status Forbidden. @GetAndParseRsdAsync
        //_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/110.0");
    }

    #region == Methods ==

    public async Task<ServiceResultBase> DiscoverService(Uri addr, bool isFeed)
    {
        UpdateStatus(string.Format(">> HTTP GET " + addr.AbsoluteUri));

        try
        {
            var HTTPResponse = await _httpClient.GetAsync(addr);

            UpdateStatus(string.Format("<< HTTP status {0} returned.", HTTPResponse.StatusCode.ToString()));

            if (HTTPResponse.IsSuccessStatusCode)
            {
                if (HTTPResponse.Content == null)
                {
                    UpdateStatus("<< Content is emptty.");
                    var re = new ServiceResultErr("Received no content.", "Content empty.");
                    return re;
                }

                var contenTypeString = HTTPResponse.Content.Headers.GetValues("Content-Type").FirstOrDefault();

                if (!string.IsNullOrEmpty(contenTypeString))
                {
                    UpdateStatus(string.Format("- Content-Type header is {0}", contenTypeString));

                    // HTML page.
                    if (contenTypeString.StartsWith("text/html"))
                    {
                        UpdateStatus("- Parsing the HTML document ...");

                        // HTML parse.
                        var res = await ParseHtml(HTTPResponse.Content, addr, isFeed);

                        if (res is ServiceResultHtmlPage srhp)
                        {
                            if (isFeed)
                            {
                                if (srhp.Feeds.Count == 0)
                                {
                                    UpdateStatus("No feed link found.");
                                }
                            }

                            if (!isFeed)
                            {
                                if (srhp.Services.Count == 0)
                                {
                                    UpdateStatus("No service link found.");
                                }
                            }
                        }

                        return res;
                    }
                    else if (contenTypeString.StartsWith("text/xml") || contenTypeString.StartsWith("application/xml"))
                    {
                        UpdateStatus(string.Format("- Ambiguous Content-Type {0} returned. ", contenTypeString));

                        UpdateStatus("- Parsing XML document to determine the what this is ...");

                        // XML parse.
                        var xml = await Task.Run(() => ParseXml(HTTPResponse.Content, addr));

                        return xml;
                    }
                    else if (contenTypeString.StartsWith("application/rss+xml"))
                    {
                        UpdateStatus("- Parsing RSS feed ...");

                        // XML parse.
                        var feed = await Task.Run(() => ParseXml(HTTPResponse.Content, addr));

                        return feed;
                    }
                    else if (contenTypeString.StartsWith("application/rdf+xml"))
                    {
                        UpdateStatus("- Parsing RSS/RDF feed ...");

                        // XML parse.
                        var feed = await Task.Run(() => ParseXml(HTTPResponse.Content, addr));

                        return feed;
                    }
                    //
                    else if (contenTypeString.StartsWith("application/atomsvc+xml"))
                    {
                        // This is the AtomPub endpoint.
                        /*
                        UpdateStatus("Found an Atom Publishing Protocol service document.");

                        ServiceResultAtomPub ap = new ServiceResultAtomPub();
                        ap.EndpointUri = addr;
                        ap.Service = ServiceTypes.AtomPub; ;
                        return ap;
                        */

                        UpdateStatus("- AtomPub endpoint (UNDERDEVELOPENT).");

                        var re = new ServiceResultErr("AtomPub endpoint (UNDERDEVELOPENT).", string.Format("{0} is AtomPub endpoint. (UNDERDEVELOPENT)", contenTypeString));
                        return re;
                    }
                    else if (contenTypeString.StartsWith("application/rsd+xml"))
                    {
                        UpdateStatus("- Parsing RSD document ...");

                        /*
                        var source = await HTTPResponse.Content.ReadAsStreamAsync();
                        var parser = new XmlParser();
                        var document = await parser.ParseDocumentAsync(source);

                        RsdLink rsd = ParseRsd(document);

                        var resRsd = new ServiceResultRsd();
                        resRsd.Rsd = rsd;
                        */
                        var resRsd = new ServiceResultRsd
                        {
                            Rsd = await ParseRsdAsync(HTTPResponse.Content)
                        };

                        return (resRsd as ServiceResultBase);
                    }
                    else if (contenTypeString.StartsWith("application/atom+xml"))
                    {
                        // TODO:
                        // Possibly AtomApi endopoint. Or Atom Feed...

                        UpdateStatus("- Parsing Atom feed ...");

                        // RSS parse.
                        var feed = await Task.Run(() => ParseXml(HTTPResponse.Content, addr));

                        return feed;
                        /*
                        UpdateStatus("- Atom (UNDERDEVELOPENT).");

                        ServiceResultErr re = new ServiceResultErr("Atom (UNDERDEVELOPENT).", string.Format("{0} is Atom. (UNDERDEVELOPENT)", contenTypeString));
                        return re;
                        */
                    }
                    else if (contenTypeString.StartsWith("atom;"))
                    {
                        // Ssme as "application/atom+xml", GitHub returns this...

                        UpdateStatus("- (Wrong Content-Type) Parsing Atom feed ...");

                        var feed = await Task.Run(() => ParseXml(HTTPResponse.Content, addr));

                        return feed;
                    }
                    else if (contenTypeString.StartsWith("application/x.atom+xml"))
                    {
                        /*
                        // TODO:
                        // Possibly AtomApi endopoint.
                        UpdateStatus("<< Old Atom format returned... ");

                        ServiceResultAtomAPI ap = new ServiceResultAtomAPI();
                        ap.EndpointUri = addr;
                        ap.Service = ServiceTypes.AtomApi;
                        return ap;
                        */
                        UpdateStatus("- AtomAPI (UNDERDEVELOPENT).");

                        var re = new ServiceResultErr("AtomAPI (UNDERDEVELOPENT).", string.Format("{0} is AtomAPI. (UNDERDEVELOPENT)", contenTypeString));
                        return re;
                    }
                    else if (contenTypeString.StartsWith("application/x.atom+xml"))
                    {
                        /*
                        // TODO:
                        // Possibly AtomApi endopoint.
                        UpdateStatus("<< Old Atom format returned... ");

                        ServiceResultAtomAPI ap = new ServiceResultAtomAPI();
                        ap.EndpointUri = addr;
                        ap.Service = ServiceTypes.AtomApi;
                        return ap;
                        */
                        UpdateStatus("- AtomAPI (UNDERDEVELOPENT).");

                        var re = new ServiceResultErr("AtomAPI (UNDERDEVELOPENT).", string.Format("{0} is AtomAPI. (UNDERDEVELOPENT)", contenTypeString));
                        return re;
                    }
                    else
                    {
                        UpdateStatus("- Unknown Content-Type returned.");

                        var re = new ServiceResultErr("Received unsupported Content-Type.", string.Format("{0} is not supported.", contenTypeString));
                        return re;
                    }
                }
                else
                {
                    UpdateStatus("- No Content-Type returned. ");
                    var re = new ServiceResultErr("Download failed", "No Content-Type received.");
                    return re;
                }
            }
            else
            {
                UpdateStatus("- Could not retrieve any document. ");

                //If 401 Unauthorized,
                // A user may or may not enter an AtomPub endpoint which require auth to get service document.
                if (HTTPResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    UpdateStatus("- Authorization is required. ");

                    var rea = new ServiceResultAuthRequired(addr);
                    return rea;
                }
                else
                {
                    var re = new ServiceResultErr("HTTP error.", "Could not retrieve any document.");
                    return re;
                }
            }
        }
        catch (System.Net.Http.HttpRequestException e)
        {
            UpdateStatus("<< HttpRequestException: " + e.Message);
            var re = new ServiceResultErr("HTTP request error.", e.Message);
            return re;
        }
        catch (Exception e)
        {
            UpdateStatus("<< HTTP error: " + e.Message);
            var re = new ServiceResultErr("HTTP error.", e.Message);
            return re;
        }
    }

    public async Task<ServiceResultBase> DiscoverServiceWithAuth(Uri addr, string userName, string apiKey, AuthTypes authType)
    {
        try
        {
            var webreq = new HttpRequestMessage(HttpMethod.Get, addr);

            if (authType == AuthTypes.Wsse)
            {
                // WSSE Auth header
                webreq.Headers.Add("Authorization", @"WSSE profile = ""UsernameToken""");
                webreq.Headers.Add("X-WSSE", MakeWSSEHeader(userName, apiKey));
            }
            else
            {
                // BASIC Auth
                var s = userName + ":" + apiKey;
                var byteArray = Encoding.UTF8.GetBytes(s);
                webreq.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }


            UpdateStatus(">> HTTP GET " + addr.AbsoluteUri);

            var HTTPResponse = await _httpClient.SendAsync(webreq);

            //var HTTPResponse = await _httpClient.GetAsync(addr);

            UpdateStatus(string.Format("<< HTTP status {0} returned.", HTTPResponse.StatusCode.ToString()));

            if (HTTPResponse.IsSuccessStatusCode)
            {
                if (HTTPResponse.Content == null)
                {
                    UpdateStatus("<< Content is emptty.");
                    var re = new ServiceResultErr("Received no content.", "Content empty.");
                    return re;
                }

                var contenTypeString = HTTPResponse.Content.Headers.GetValues("Content-Type").FirstOrDefault();

                if (!string.IsNullOrEmpty(contenTypeString))
                {
                    UpdateStatus(string.Format("- Content-Type header is {0}", contenTypeString));

                    // HTML page.
                    if (contenTypeString.StartsWith("text/html"))
                    {
                        UpdateStatus("- HTML document returned. Something is wrong.");

                        // Error
                        var re = new ServiceResultErr("API/Protocol authentication failed", "A HTML page returned.");
                        return re;
                    }
                    else if (contenTypeString.StartsWith("text/xml") || contenTypeString.StartsWith("application/xml"))
                    {
                        UpdateStatus(string.Format("- Ambiguous Content-Type {0} returned. ", contenTypeString));

                        UpdateStatus("- Parsing XML document to determine the what this is ...");

                        // XML parse.
                        var xml = await Task.Run(() => ParseXml(HTTPResponse.Content, addr));

                        return xml;
                    }
                    else if (contenTypeString.StartsWith("application/rss+xml"))
                    {
                        UpdateStatus("- RSS feed returned. Something is wrong.");

                        // Error
                        var re = new ServiceResultErr("API/Protocol authentication failed", "A RSS feed returned.");
                        return re;
                    }
                    else if (contenTypeString.StartsWith("application/rdf+xml"))
                    {
                        UpdateStatus("- RSS/RDF feed returned. Something is wrong.");

                        // Error
                        var re = new ServiceResultErr("API/Protocol authentication failed", "A RSS/RDF feed returned.");
                        return re;
                    }
                    //
                    else if (contenTypeString.StartsWith("application/atomsvc+xml"))
                    {
                        // AtomPub endpoint.
                        UpdateStatus("- Atom Publishing Protocol Service document returned.");

                        var ap = await Task.Run(() => ParseAtomServiceDocument(HTTPResponse.Content, addr, userName, apiKey, authType));
                        return ap;
                    }
                    else if (contenTypeString.StartsWith("application/rsd+xml"))
                    {
                        //await ParseRsd(HTTPResponse.Content);

                        UpdateStatus("- RSD (No need to be authenticated). Something went wrong.");

                        // Error
                        var re = new ServiceResultErr("RSD (No need to be authenticated).", string.Format("{0} is RSD. (Something went wrong)", contenTypeString));
                        return re;
                    }
                    else if (contenTypeString.StartsWith("application/atom+xml"))
                    {
                        UpdateStatus("- Parsing Atom feed ...");

                        // XML parse.
                        var feed = await Task.Run(() => ParseXml(HTTPResponse.Content, addr));

                        return feed;
                    }
                    else if (contenTypeString.StartsWith("application/x.atom+xml"))
                    {
                        /*
                        // TODO:
                        // Possibly AtomApi endopoint.
                        UpdateStatus("<< Old Atom format returned... ");

                        ServiceResultAtomAPI ap = new ServiceResultAtomAPI();
                        ap.EndpointUri = addr;
                        ap.Service = ServiceTypes.AtomApi;
                        return ap;
                        */
                        UpdateStatus("- AtomAPI (UNDERDEVELOPENT).");

                        var re = new ServiceResultErr("AtomAPI (UNDERDEVELOPENT).", string.Format("{0} is AtomAPI. (UNDERDEVELOPENT)", contenTypeString));
                        return re;
                    }
                    else if (contenTypeString.StartsWith("application/x.atom+xml"))
                    {
                        /*
                        // TODO:
                        // Possibly AtomApi endopoint.
                        UpdateStatus("<< Old Atom format returned... ");

                        ServiceResultAtomAPI ap = new ServiceResultAtomAPI();
                        ap.EndpointUri = addr;
                        ap.Service = ServiceTypes.AtomApi;
                        return ap;
                        */
                        UpdateStatus("- AtomAPI (UNDERDEVELOPENT).");

                        var re = new ServiceResultErr("AtomAPI (UNDERDEVELOPENT).", string.Format("{0} is AtomAPI. (UNDERDEVELOPENT)", contenTypeString));
                        return re;
                    }
                    else
                    {
                        UpdateStatus("- Unknown Content-Type returned.");

                        var re = new ServiceResultErr("Received unsupported Content-Type.", string.Format("{0} is not supported.", contenTypeString));
                        return re;
                    }
                }
                else
                {
                    UpdateStatus("- No Content-Type returned. ");
                    var re = new ServiceResultErr("Download failed", "No Content-Type reveived.");
                    return re;
                }
            }
            else
            {
                UpdateStatus("- Could not retrieve any document. ");

                //If 401 Unauthorized,
                // A user may or may not enter an AtomPub endpoint which require auth to get service document.
                if (HTTPResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    UpdateStatus("- Authorization failed. ");

                    var rea = new ServiceResultAuthRequired(addr);
                    return rea;
                }
                else
                {
                    var re = new ServiceResultErr("HTTP error.", "Could not retrieve any document.");
                    return re;
                }
            }

        }
        catch (System.Net.Http.HttpRequestException e)
        {
            UpdateStatus("<< HttpRequestException: " + e.Message);
            var re = new ServiceResultErr("HTTP request error.", e.Message);
            return re;
        }
        catch (Exception e)
        {
            UpdateStatus("<< HTTP error: " + e.Message);
            var re = new ServiceResultErr("HTTP error.", e.Message);
            return re;
        }
    }

    private async Task<ServiceResultBase> ParseHtml(HttpContent content, Uri addr, bool isFeed)
    {
        ServiceResultHtmlPage res = new();

        //Use the default configuration for AngleSharp
        //var config = Configuration.Default;

        //Create a new context for evaluating webpages with the given config
        //var context = BrowsingContext.New(config);

        try
        {
            //Source
            var source = await content.ReadAsStreamAsync();

            //Create a virtual request to specify the document to load (here from our fixed string)
            //var document = await context.OpenAsync(req => req.Content(source));

            var document = new HtmlDocument();
            document.Load(source);

            if (document.DocumentNode != null)
            {
                var siteTitle = "";
                // gets page title
                //var elementTitle = document.QuerySelector("html > head > title");
                var elementTitle = document.DocumentNode.SelectSingleNode("//html/head/title");
                if (elementTitle != null)
                {
                    //siteTitle = elementTitle.TextContent;
                    siteTitle = elementTitle.InnerText;

                    UpdateStatus("- Webpage title found: " + siteTitle);
                }
                else
                {
                    UpdateStatus("- Webpage title NOT found.");
                }

                //Debug.WriteLine(document.DocumentNode.InnerHtml);

                //var elements = document.QuerySelectorAll("link");
                var elements = document.DocumentNode.SelectNodes("//html/head/link");
                if (elements != null)
                {
                    foreach (var e in elements)
                    {
                        //var re = e.GetAttribute("rel");
                        //var ty = e.GetAttribute("type");
                        //var hf = e.GetAttribute("href");
                        //var t = e.GetAttribute("title");

                        if (e.Attributes == null)
                        {
                            continue;
                        }

                        var re = e.Attributes["rel"]?.Value;
                        var ty = e.Attributes["type"]?.Value;
                        var hf = e.Attributes["href"]?.Value;
                        var t = e.Attributes["title"]?.Value;

                        if (!string.IsNullOrEmpty(re))
                        {
                            if (re.ToUpper() == "EDITURI")
                            {
                                if (isFeed)
                                {
                                    continue;
                                }

                                if (!string.IsNullOrEmpty(ty) && !string.IsNullOrEmpty(hf))
                                {
                                    if (ty == "application/rsd+xml")
                                    {
                                        UpdateStatus("- A link to RSD document found.");

                                        Uri? _rsdUrl = null;
                                        try
                                        {
                                            //_rsdUrl = new Uri(hf);
                                            if (hf.StartsWith("http"))
                                            {
                                                // Absolute uri.
                                                _rsdUrl = new Uri(hf);
                                            }
                                            else
                                            {
                                                // Relative uri (probably...)
                                                // Uri(baseUri, relativeUriString)
                                                _rsdUrl = new Uri(addr, hf);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine("Exception@ServiceDiscovery@ParseHTML on var _rsdUrl = new Uri(hf) : " + ex.Message);

                                            UpdateStatus(">> A link to RSD document invalid :" + ex.Message);
                                        }

                                        if (_rsdUrl != null)
                                        {
                                            var rsd = await GetAndParseRsdAsync(_rsdUrl);

                                            if (rsd is SearviceDocumentLinkErr rle)
                                            {
                                                res.HasError = true;
                                                res.ErrTitle = rle.ErrTitle;
                                                res.ErrDescription = rle.ErrDescription;
                                            }
                                            else if (rsd is RsdLink rl)
                                            {
                                                if (rl.Apis != null)
                                                {
                                                    if (rl.Apis.Count > 0)
                                                    {
                                                        res.Services.Add(rl);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (re.ToUpper() == "SERVICE")
                            {
                                if (isFeed)
                                    continue;

                                // TODO:
                            }

                            if (re.ToLower() == "https://api.w.org/")
                            {
                                if (isFeed)
                                    continue;

                                // TODO:
                            }

                            if (re.ToUpper() == "ALTERNATE")
                            {
                                if (!isFeed)
                                    continue;

                                if (!string.IsNullOrEmpty(ty) && !string.IsNullOrEmpty(hf))
                                {
                                    if (ty == "application/atom+xml")
                                    {
                                        try
                                        {
                                            Uri _atomFeedUrl;
                                            if (hf.StartsWith("http"))
                                            {
                                                // Absolute uri.
                                                _atomFeedUrl = new Uri(hf);
                                            }
                                            else
                                            {
                                                // Relative uri (probably...)
                                                // Uri(baseUri, relativeUriString)
                                                _atomFeedUrl = new Uri(addr, hf);
                                            }

                                            t ??= "";
                                            FeedLink fl = new(_atomFeedUrl, FeedLink.FeedKinds.Atom, t, addr, siteTitle);

                                            res.Feeds.Add(fl);

                                            UpdateStatus("Found a link to an Atom feed.");
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine("Exception@ServiceDiscovery@ParseHTML on var _atomFeedUrl = new Uri(hf) : " + ex.Message);
                                        }
                                    }
                                    else if (ty == "application/rss+xml")
                                    {
                                        try
                                        {
                                            Uri _rssFeedUrl;

                                            if (hf.StartsWith("http"))
                                            {
                                                // Absolute uri.
                                                _rssFeedUrl = new Uri(hf);
                                            }
                                            else
                                            {
                                                // Relative uri (probably...)
                                                // Uri(baseUri, relativeUriString)
                                                _rssFeedUrl = new Uri(addr, hf);
                                            }

                                            t ??= "";
                                            FeedLink fl = new(_rssFeedUrl, FeedLink.FeedKinds.Rss, t, addr, siteTitle);

                                            res.Feeds.Add(fl);

                                            UpdateStatus("Found a link to an RSS feed.");
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine("Exception@ServiceDiscovery@ParseHTML on var _rssFeedUrl = new Uri(hf) : " + ex.Message);
                                        }

                                    }
                                    else
                                    {
                                        //Debug.WriteLine("rel type: " + ty);
                                    }
                                }
                            }
                        }
                    }

                    if (elements.Count == 0)
                    {
                        // If webpage is hosted on a free host like Byethost, it returns a empty html with a Javascript to test whether it is a bot or not.
                        // <noscript>This site requires Javascript to work, please enable Javascript in your browser or use a browser with Javascript support</noscript>

                        UpdateStatus("- No link found. If the webpage is hosted on a free host like Byethost, it returns a empty html with a Javascript to test whether it is a browser or not. In this case, we can't access the page.");
                    }
                }
                else
                {
                    UpdateStatus("- No link found. If the webpage is hosted on a free host like Byethost, it returns a empty html with a Javascript to test whether it is a browser or not. In this case, we can't access the page.");
                }
            }
            else
            {
                UpdateStatus("- No link found. If the webpage is hosted on a free host like Byethost, it returns a empty html with a Javascript to test whether it is a browser or not. In this case, we can't access the page.");
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine("Exception@ServiceDiscovery@ParseHTML: " + e.Message);
            UpdateStatus("Exception@ServiceDiscovery@ParseHTML: " + e.Message);
        }

        return res;
    }

    private async Task<ServiceResultBase> ParseXml(HttpContent content, Uri addr)
    {
        try
        {
            var source = await content.ReadAsStreamAsync();

            //var parser = new XmlParser();
            //var document = await parser.ParseDocumentAsync(source);
            var document = new System.Xml.XmlDocument();
            document.Load(source);

            var isOK = false;
            string? feedTitle = "", siteLink = "";
            Uri? siteUri = null;

            if (document != null)
            {
                if (document.DocumentElement != null)
                {
                    // Possibly RSS 2.0
                    if (document.DocumentElement.LocalName.Equals("rss"))
                    {
                        var ver = document.DocumentElement.GetAttribute("version");
                        if (!string.IsNullOrEmpty(ver))
                        {
                            if (ver.Equals("2.0"))
                            {
                                UpdateStatus("RSS 2.0 feed detected.");
                                isOK = true;
                            }
                        }

                        // feed title
                        //var elementTitle = document.QuerySelector("rss > channel > title");
                        var elementTitle = document.SelectSingleNode("//rss/channel/title");
                        if (elementTitle != null)
                        {
                            //feedTitle = elementTitle.TextContent;
                            feedTitle = elementTitle.InnerText;

                            if (!string.IsNullOrEmpty(feedTitle))
                            {
                                UpdateStatus("Found a title for the RSS feed.");
                            }
                            else
                            {
                                feedTitle = "Empty RSS feed title";
                            }
                        }

                        //var sl = document.DocumentElement.QuerySelectorAll("channel > link");
                        var sl = document.DocumentElement.SelectNodes("channel/link");
                        if (sl != null)
                        {
                            foreach (XmlNode sle in sl)
                            {
                                /*
                                if (string.IsNullOrEmpty(sle.NamespaceUri))
                                {
                                    siteLink = sle.TextContent;
                                    if (!string.IsNullOrEmpty(siteLink))
                                    {
                                        try
                                        {
                                            siteUri = new Uri(siteLink);
                                        }
                                        catch { }
                                    }
                                    break;
                                }
                                */
                                siteLink = sle.InnerText;
                                if (!string.IsNullOrEmpty(siteLink))
                                {
                                    try
                                    {
                                        siteUri = new Uri(siteLink);
                                    }
                                    catch { }
                                }
                                break;
                            }
                        }

                        if (isOK)
                        {
                            ServiceResultFeed rss = new ServiceResultFeed();
                            rss.FeedlinkInfo = new(addr, FeedLink.FeedKinds.Rss, feedTitle, siteUri, "");

                            return (rss as ServiceResultBase);
                        }
                    }
                    // Possibly RSS 1.0
                    else if (document.DocumentElement.LocalName.Equals("RDF"))
                    {
                        var ns = document.DocumentElement.GetAttribute("xmlns");

                        if (!string.IsNullOrEmpty(ns))
                        {
                            if (ns.Equals("http://purl.org/rss/1.0/"))
                            {
                                ns = document.DocumentElement.GetAttribute("xmlns:rdf");
                                if (!string.IsNullOrEmpty(ns))
                                {
                                    if (ns.Equals("http://www.w3.org/1999/02/22-rdf-syntax-ns#"))
                                    {
                                        UpdateStatus("RSS 1.0 feed detected.");
                                        isOK = true;
                                    }
                                }
                            }
                        }

                        //feedTitle = document.DocumentElement.QuerySelector("channel > title").TextContent;
                        var elementTitle = document.DocumentElement.SelectSingleNode("channel/title");
                        if (elementTitle != null)
                        {
                            feedTitle = elementTitle.InnerText;
                        }

                        if (!string.IsNullOrEmpty(feedTitle))
                        {
                            UpdateStatus("Found a title for the RSS feed.");
                        }
                        else
                        {
                            feedTitle = "Empty RSS feed title";
                        }

                        //siteLink = document.DocumentElement.QuerySelector("channel > link").TextContent;
                        var sl = document.DocumentElement.SelectSingleNode("channel/link");
                        if (sl != null)
                        {
                            siteLink = sl.InnerText;
                        }

                        if (!string.IsNullOrEmpty(siteLink))
                        {
                            try
                            {
                                siteUri = new Uri(siteLink);
                            }
                            catch { }
                        }

                        if (isOK)
                        {
                            ServiceResultFeed rdf = new ServiceResultFeed();
                            rdf.FeedlinkInfo = new(addr, FeedLink.FeedKinds.Rss, feedTitle, siteUri, "");

                            return (rdf as ServiceResultBase);
                        }
                    }
                    // Possibly Atom 1.0 or 0.3 feed
                    else if (document.DocumentElement.LocalName.Equals("feed"))
                    {
                        var ns = document.DocumentElement.GetAttribute("xmlns");

                        if (!string.IsNullOrEmpty(ns))
                        {
                            if (ns.Equals("http://www.w3.org/2005/Atom") || ns.Equals("http://purl.org/atom/ns#"))
                            {
                                UpdateStatus("Atom feed detected.");
                                isOK = true;
                            }
                        }

                        // feed title
                        //var elementTitle = document.QuerySelector("feed > title");
                        var elementTitle = document.DocumentElement.SelectSingleNode("channel/title");
                        if (elementTitle != null)
                        {
                            //feedTitle = elementTitle.TextContent;
                            feedTitle = elementTitle.InnerText;
                            if (!string.IsNullOrEmpty(feedTitle))
                            {
                                UpdateStatus("Found a title for the Atom feed.");
                            }
                            else
                            {
                                feedTitle = "Empty Atom feed title";
                            }
                        }

                        //var sl = document.DocumentElement.QuerySelector("feed > link");
                        var sl = document.DocumentElement.SelectSingleNode("feed/link");
                        if (sl != null)
                        {
                            //siteLink = sl.GetAttribute("href");
                            if (sl.Attributes != null)
                            {
                                siteLink = sl.Attributes["href"]?.Value;

                                if (!string.IsNullOrEmpty(siteLink))
                                {
                                    try
                                    {
                                        siteUri = new Uri(siteLink);
                                    }
                                    catch { }
                                }
                            }
                        }

                        if (isOK)
                        {
                            ServiceResultFeed atom = new ServiceResultFeed();
                            atom.FeedlinkInfo = new(addr, FeedLink.FeedKinds.Atom, feedTitle, siteUri, "");

                            return (atom as ServiceResultBase);
                        }
                    }
                    else if (document.DocumentElement.LocalName.Equals("rsd"))
                    {
                        UpdateStatus("- Parsing RSD document ...");

                        //RsdLink rsd = ParseRsd(document);
                        //ServiceResultRsd resRsd = new ServiceResultRsd();
                        //resRsd.Rsd = rsd;

                        var resRsd = new ServiceResultRsd();
                        resRsd.Rsd = await ParseRsdAsync(content);

                        return (resRsd as ServiceResultBase);
                    }
                    else
                    {
                        UpdateStatus("- Unknown XML document ...");
                    }
                }
            }
        }

        catch (Exception ex)
        {
            Debug.WriteLine("Exception@ServiceDiscovery@ParseXML: " + ex.Message);
            UpdateStatus("Exception@ServiceDiscovery@ParseXML: " + ex.Message);
        }

        ServiceResultErr ret = new ServiceResultErr("XML parse error.", "Could not parse the document.");

        return (ret as ServiceResultBase);
    }

    private async Task<ServiceResultBase> ParseAtomServiceDocument(HttpContent content, Uri addr, string userName, string apiKey, AuthTypes authType)
    {
        var source = await content.ReadAsStreamAsync();

        var xdoc = new XmlDocument();
        try
        {
            XmlReader reader = XmlReader.Create(source);
            xdoc.Load(reader);
        }
        catch (Exception e)
        {
            UpdateStatus("<< XML parse error (Atom Service document): " + e.Message);

            ServiceResultErr xe = new ServiceResultErr("XML parse error.", "Could not parse the Atom Service document: " + e.Message);

            return (xe as ServiceResultBase);
        }

        var atomNsMgr = new XmlNamespaceManager(xdoc.NameTable);
        atomNsMgr.AddNamespace("atom", "http://www.w3.org/2005/Atom");
        atomNsMgr.AddNamespace("app", "http://www.w3.org/2007/app");

        if (xdoc.DocumentElement.Name is "app:service" or "service")
        {
            var account = new NodeService("New Service (Atom Publishing Protocol)", userName, apiKey, addr, ApiTypes.atAtomPub, ServiceTypes.AtomPub)
            {
                EndPoint = addr,
                ServiceType = ServiceTypes.AtomPub,
                UserName = userName,
                UserPassword = apiKey,
                Api = ApiTypes.atAtomPub,
                AuthType = authType
            };

            var workspaceList = xdoc.DocumentElement.SelectNodes("app:workspace", atomNsMgr);
            //XmlNodeList workspaceList = xdoc.SelectNodes("//service/app:workspace", atomNsMgr);

            if (workspaceList != null)
            {
                foreach (XmlNode ws in workspaceList)
                {
                    var workspace = new NodeWorkspace("Workspace Name");
                    workspace.IsExpanded = true;
                    workspace.Parent = account;

                    var wtl = ws.SelectSingleNode("atom:title", atomNsMgr);
                    if (wtl != null)
                    {
                        if (string.IsNullOrEmpty(wtl.InnerText))
                        {
                            workspace.Name = wtl.InnerText;
                        }
                    }

                    var collectionList = ws.SelectNodes("app:collection", atomNsMgr);
                    if (collectionList != null)
                    {
                        foreach (XmlNode col in collectionList)
                        {
                            Uri colHrefUri = null;
                            if (col.Attributes["href"] != null)
                            {
                                string href = col.Attributes["href"].Value;
                                if (!string.IsNullOrEmpty(href))
                                {
                                    try
                                    {
                                        colHrefUri = new Uri(href);
                                    }
                                    catch { }
                                }
                            }

                            NodeAtomPubEntryCollection collection = new NodeAtomPubEntryCollection("Collection Name", colHrefUri, colHrefUri.AbsoluteUri);
                            collection.IsExpanded = true;
                            collection.Parent = workspace;

                            var ctl = col.SelectSingleNode("atom:title", atomNsMgr);
                            if (ctl != null)
                            {
                                if (!string.IsNullOrEmpty(ctl.InnerText))
                                    collection.Name = ctl.InnerText;
                            }

                            var accepts = col.SelectNodes("app:accept", atomNsMgr);
                            if (accepts != null)
                            {
                                foreach (XmlNode acp in accepts)
                                {
                                    string acpt = acp.InnerText;
                                    if (!string.IsNullOrEmpty(acpt))
                                    {
                                        collection.AcceptTypes.Add(acpt);

                                        if ((acpt == "application/atom+xml;type=entry")
                                            || (acpt == "application/atom+xml"))
                                        {
                                            collection.IsAcceptEntry = true;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // default entry
                                collection.IsAcceptEntry = true;
                            }

                            var categoriesList = col.SelectNodes("app:categories", atomNsMgr);
                            if (categoriesList != null)
                            {
                                foreach (XmlNode cats in categoriesList)
                                {
                                    var categories = new NodeAtomPubCatetories("Categories");
                                    categories.IsExpanded = true;
                                    categories.Parent = collection;

                                    Uri catHrefUri = null;
                                    if (cats.Attributes["href"] != null)
                                    {
                                        var cathref = cats.Attributes["href"].Value;
                                        if (!string.IsNullOrEmpty(cathref))
                                        {
                                            try
                                            {
                                                catHrefUri = new Uri(cathref);

                                                categories.Href = catHrefUri;
                                            }
                                            catch { }
                                        }
                                    }

                                    if (cats.Attributes["fixed"] != null)
                                    {
                                        var catFix = cats.Attributes["fixed"].Value;
                                        if (!string.IsNullOrEmpty(catFix))
                                        {
                                            if (catFix == "yes")
                                            {
                                                categories.IsCategoryFixed = true;
                                            }
                                            else
                                            {
                                                categories.IsCategoryFixed = false;
                                            }
                                        }
                                    }

                                    XmlNodeList categoryList = cats.SelectNodes("atom:category", atomNsMgr);
                                    if (categoryList != null)
                                    {
                                        foreach (XmlNode cat in categoryList)
                                        {
                                            var category = new NodeAtomPubCategory("Category");
                                            category.IsExpanded = true;
                                            category.Parent = categories;

                                            if (cat.Attributes["term"] != null)
                                            {
                                                var term = cat.Attributes["term"].Value;
                                                if (!string.IsNullOrEmpty(term))
                                                {
                                                    category.Term = term;
                                                }
                                            }

                                            if (cat.Attributes["scheme"] != null)
                                            {
                                                var scheme = cat.Attributes["scheme"].Value;
                                                if (!string.IsNullOrEmpty(scheme))
                                                {
                                                    category.Scheme = scheme;
                                                }
                                            }

                                            categories.Children.Add(category);
                                        }
                                    }

                                    collection.Children.Add(categories);
                                }
                            }

                            workspace.Children.Add(collection);
                        }
                    }

                    account.Children.Add(workspace);
                }
            }

            var ap = new ServiceResultAtomPub(addr, authType, account);

            return (ap as ServiceResultBase);
        }

        var ret = new ServiceResultErr("Fail to read service document.", "No service element found in the Atom Service document:" + xdoc.OuterXml.ToString());

        return (ret as ServiceResultBase);
    }

    private async Task<SearviceDocumentLinkBase> GetAndParseRsdAsync(Uri addr)
    {
        UpdateStatus(string.Format(">> HTTP GET " + addr.AbsoluteUri));

        // This is a little hack for wordpress.com. Without this, wordpress.com returns HTTP status Forbidden 
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/110.0");

        try
        {
            var HTTPResponse = await _httpClient.GetAsync(addr);

            UpdateStatus(string.Format("<< HTTP status {0} returned.", HTTPResponse.StatusCode.ToString()));

            if (HTTPResponse.IsSuccessStatusCode)
            {
                if (HTTPResponse.Content != null)
                {
                    UpdateStatus(string.Format("- Parsing RSD document."));

                    return await ParseRsdAsync(HTTPResponse.Content);
                    /*
                    var source = await HTTPResponse.Content.ReadAsStreamAsync();
                    var parser = new XmlParser();
                    var document = await parser.ParseDocumentAsync(source);

                    var rsdDoc = ParseRsd(document);
                    return rsdDoc;
                    */
                }
                else
                {
                    UpdateStatus("Could not retrieve RSD document. The content is empty.");
                    var rle = new SearviceDocumentLinkErr("RSD content is empty", "Could not retrieve RSD document.");
                    return rle;
                }
            }
            else
            {
                UpdateStatus("Could not retrieve RSD document. ");
                var rle = new SearviceDocumentLinkErr("HTTP error while getting RSD document", $"{HTTPResponse.StatusCode}");
                return rle;
            }
        }
        catch (System.Net.Http.HttpRequestException e)
        {
            UpdateStatus("<< HttpRequestException: " + e.Message);
            var rle = new SearviceDocumentLinkErr("HTTP error while getting RSD document", $"HttpRequestException: {e.Message}");
            return rle;
        }
        catch (Exception e)
        {
            UpdateStatus("<< HTTP error: " + e.Message);
            var rle = new SearviceDocumentLinkErr("HTTP error while getting RSD document", $"Exception: {e.Message}");
            return rle;
        }
    }

    private async Task<RsdLink> ParseRsdAsync(HttpContent content)
    {
        RsdLink rsdDoc = new();

        try
        {
            var source = await content.ReadAsStreamAsync();
            XDocument xdoc = XDocument.Load(source);

            if (xdoc.Root != null)
            {
                // TODO: check.
                XNamespace aw = "http://archipelago.phrasewise.com/rsd";

                // TODO: check.
                if (xdoc.Root.Name.ToString().ToLower() == @"{http://archipelago.phrasewise.com/rsd}rsd")
                {
                    var serv = xdoc.Root.Element(aw + "service");
                    if (serv != null)
                    {
                        rsdDoc.EngineName = serv.Element(aw + "engineName")?.Value;
                        var homePageLink = serv.Element(aw + "homePageLink")?.Value;
                        if (!string.IsNullOrEmpty(homePageLink))
                        {
                            try
                            {
                                if (homePageLink.StartsWith("http"))
                                    rsdDoc.HomePageLink = new Uri(homePageLink);
                            }
                            catch { }
                        }

                        var apis = serv.Element(aw + "apis");
                        if (apis != null)
                        {
                            var apiList = apis.Elements(aw + "api");

                            if (apiList != null)
                            {
                                foreach (var api in apiList)
                                {
                                    var apiName = api.Attribute("name");
                                    var apiBlogId = api.Attribute("blogID");
                                    var apiPreferred = api.Attribute("preferred");
                                    var apiLink = api.Attribute("apiLink");

                                    if (string.IsNullOrEmpty(apiName?.Value) || string.IsNullOrEmpty(apiBlogId?.Value) || string.IsNullOrEmpty(apiLink?.Value))
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        RsdApi hoge = new()
                                        {
                                            Name = apiName?.Value ?? "",
                                            BlogID = apiBlogId?.Value ?? ""
                                        };
                                        if (!string.IsNullOrEmpty(apiPreferred?.Value))
                                        {
                                            if (apiPreferred?.Value.ToLower() == "true")
                                            {
                                                hoge.Preferred = true;
                                            }
                                        }
                                        if (!string.IsNullOrEmpty(apiLink?.Value))
                                        {
                                            try
                                            {
                                                hoge.ApiLink = new Uri(apiLink.Value);
                                            }
                                            catch { }
                                        }

                                        if (hoge.ApiLink != null)
                                        {
                                            rsdDoc.Apis.Add(hoge);
                                        }
                                    }
                                }
                            }
                        }

                        foreach (var fuga in rsdDoc.Apis)
                        {
                            if ((fuga.Name?.ToLower() == "wordpress") && (fuga.Preferred))
                            {
                                UpdateStatus(string.Format("-  WordPress found."));
                            }
                            else
                            {
                                // TODO:
                            }
                        }
                    }
                    else
                    {
                        UpdateStatus("Load XML/RSD failed. service element is missing: " + xdoc.Root.ToString());
                    }
                }
                else
                {
                    UpdateStatus("Load XML/RSD failed. Document root name or namespace is wrong: " + xdoc.Root.Name.ToString());
                }
            }
            else
            {
                UpdateStatus("Load XML/RSD failed.");
            }
        }
        catch (Exception e)
        {
            UpdateStatus("Load XML/RSD failed: " + e.Message);
        }

        return rsdDoc;
    }

    /*
    private RsdLink ParseRsd(AngleSharp.Xml.Dom.IXmlDocument document)
    {
        RsdLink rsdDoc = new();

        try
        {
            //var source = await content.ReadAsStreamAsync();
            //var parser = new XmlParser();
            //var document = await parser.ParseDocumentAsync(source);

            if (document != null)
            {
                if (document.DocumentElement != null)
                {
                    rsdDoc.EngineName = document.DocumentElement.QuerySelector("service > engineName").TextContent;

                    var homePageLink = document.DocumentElement.QuerySelector("service > homePageLink").TextContent;
                    if (!string.IsNullOrEmpty(homePageLink))
                    {
                        try
                        {
                            rsdDoc.HomePageLink = new Uri(homePageLink);
                        }
                        catch { }
                    }

                    var apis = document.DocumentElement.QuerySelectorAll("service > apis > api");
                    if (apis != null)
                    {
                        foreach (var api in apis)
                        {
                            var apiName = api.GetAttribute("name");
                            var apiBlogId = api.GetAttribute("blogID");
                            var apiPreferred = api.GetAttribute("preferred");
                            var apiLink = api.GetAttribute("apiLink");

                            RsdApi hoge = new();
                            hoge.Name = apiName;
                            hoge.BlogID = apiBlogId;
                            if (!string.IsNullOrEmpty(apiPreferred))
                            {
                                if (apiPreferred.ToLower() == "true")
                                {
                                    hoge.Preferred = true;
                                }
                            }
                            if (!string.IsNullOrEmpty(apiLink))
                            {
                                try
                                {
                                    hoge.ApiLink = new Uri(apiLink);
                                }
                                catch { }
                            }

                            rsdDoc.Apis.Add(hoge);
                        }
                    }

                    foreach (var fuga in rsdDoc.Apis)
                    {
                        if ((fuga.Name.ToLower() == "wordpress") && (fuga.Preferred))
                        {
                            UpdateStatus(string.Format("-  WordPress found."));
                        }
                        else
                        {
                            // TODO:
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            UpdateStatus("Load XML/RSD failed: " + e.Message);
        }

        return rsdDoc;
    }
    */

    private void UpdateStatus(string data)
    {
        //await Task.Run(() => { StatusUpdate?.Invoke(this, data); });

        StatusUpdate?.Invoke(this, data);
    }

    #region == WSSE == 

    private string MakeWSSEHeader(string userName, string password)
    {
        var nonce = GenNounce(40);
        var nonceBytes = Encoding.UTF8.GetBytes(nonce);
        var nonce64 = Convert.ToBase64String(nonceBytes);
        var createdString = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var digest = GenDigest(password, createdString, nonce);
        var header = string.Format(
            @"UsernameToken Username=""{0}"", PasswordDigest=""{1}"", Nonce=""{2}"", Created=""{3}""",
            userName, digest, nonce64, createdString);
        return header;
    }

    private static string GenDigest(string password, string created, string nonce)
    {
        byte[] digest;
        //using (SHA1Managed sha1 = new SHA1Managed())
        using (var sha1 = SHA1.Create())
        {
            var digestText = nonce + created + password;
            var digestBytes = Encoding.UTF8.GetBytes(digestText);
            digest = sha1.ComputeHash(digestBytes);
        }
        var digest64 = Convert.ToBase64String(digest);
        return digest64;
    }

    private static string GenNounce(int length)
    {
        using var rng = RandomNumberGenerator.Create();
        var buffer = new byte[length];
        rng.GetBytes(buffer);
        return Convert.ToBase64String(buffer);
        /*
        RNGCryptoServiceProvider rnd = new RNGCryptoServiceProvider();
        byte[] buffer = new byte[length];
        rnd.GetBytes(buffer);
        string nonce64 = Convert.ToBase64String(buffer);
        return nonce64;
        */
    }

    #endregion

    #endregion
}

#region == Sample XML Code ==

// RSD - Really Simple Discovery: XML-RPC or AtomAPI or WP json
// Content-Type: application/rsd+xml

// RSD - Really Simple Discovery
// <link rel="EditURI" type="application/rsd+xml" title="RSD" href="http://1270.0.0.1/xmlrpc.php" />
/*
<?xml version="1.0" encoding="UTF-8"?>
<rsd version="1.0" xmlns="http://archipelago.phrasewise.com/rsd">
  <service>
    <engineName>WordPress</engineName>
    <engineLink>https://wordpress.org/</engineLink>
    <homePageLink>http://1270.0.0.1</homePageLink>
    <apis>
      <api name="WordPress" blogID="1" preferred="true" apiLink="http://1270.0.0.1/xmlrpc.php" />
      <api name="Movable Type" blogID="1" preferred="false" apiLink="http://1270.0.0.1/xmlrpc.php" />
      <api name="MetaWeblog" blogID="1" preferred="false" apiLink="http://1270.0.0.1xmlrpc.php" />
      <api name="Blogger" blogID="1" preferred="false" apiLink="http://1270.0.0.1/xmlrpc.php" />
      <api name="WP-API" blogID="1" preferred="false" apiLink="http://1270.0.0.1/wp-json/" />
    </apis>
  </service>
</rsd>
*/


// AtomPub Service Document
// Content-Type: application/atomsvc+xml
/*
<?xml version="1.0" encoding="UTF-8"?>
<service
    xmlns="http://www.w3.org/2007/app"
    xmlns:atom="http://www.w3.org/2005/Atom">
    <workspace>
        <atom:title>BlogTitle</atom:title>
        <collection href="https://livedoor.blogcms.jp/atompub/userid/article">
            <atom:title>BlogTitle - Entries</atom:title>
            <accept>application/atom+xml;type=entry</accept>
        
            <categories fixed="no" scheme="https://livedoor.blogcms.jp/atompub/userid/category">
            </categories>
        </collection>
        <collection href="https://livedoor.blogcms.jp/atompub/userid/image">
            <atom:title>BlogTitle - Images</atom:title>
            <accept>image/png</accept>
            <accept>image/jpeg</accept>
            <accept>image/gif</accept>
        </collection>
    </workspace>
</service>
*/

// AtomAPI at vox
// Content-Type: application/atom+xml
/*
<?xml version="1.0" encoding="utf-8"?>
<feed xmlns="http://purl.org/atom/ns#">
    <link xmlns="http://purl.org/atom/ns#" rel="service.post" href="http://www.vox.com/services/atom/svc=post/collection_id=6a00c2251f52cd549d00c2251f5478604a" title="blog" type="application/x.atom+xml"/>
    <link xmlns="http://purl.org/atom/ns#" rel="alternate" href="http://marumoto.vox.com/" title="blog" type="text/html"/>
    <link xmlns="http://purl.org/atom/ns#" rel="service.feed" href="http://www.vox.com/services/atom/svc=asset/6p00c2251f52cd549d" title="blog" type="application/atom+xml"/>
    <link xmlns="http://purl.org/atom/ns#" rel="service.upload" href="http://www.vox.com/services/atom/svc=asset" title="blog" type="application/atom+xml"/>
    <link xmlns="http://purl.org/atom/ns#" rel="replies" href="http://www.vox.com/services/atom/svc=asset/6p00c2251f52cd549d/type=Comment" title="blog" type="application/atom+xml"/>
</feed>
*/

// AtomAPI at livedoor http://cms.blog.livedoor.com/atom
// Content-Type: application/x.atom+xml
/*
<?xml version="1.0" encoding="UTF-8"?>
<feed xmlns="http://purl.org/atom/ns#">
    <link xmlns="http://purl.org/atom/ns#" type="application/x.atom+xml" rel="service.post" href="http://cms.blog.livedoor.com/atom/blog_id=95864" title="hepcat de　ブログ"/>
    <link xmlns="http://purl.org/atom/ns#" type="application/x.atom+xml" rel="service.feed" href="http://cms.blog.livedoor.com/atom/blog_id=95864" title="hepcat de　ブログ"/>
    <link xmlns="http://purl.org/atom/ns#" type="application/x.atom+xml" rel="service.categories" href="http://cms.blog.livedoor.com/atom/blog_id=95864/svc=categories" title="hepcat de　ブログ"/>
    <link xmlns="http://purl.org/atom/ns#" type="application/x.atom+xml" rel="service.upload" href="http://cms.blog.livedoor.com/atom/blog_id=95864/svc=upload" title="hepcat de　ブログ"/>
</feed>
*/

#endregion
