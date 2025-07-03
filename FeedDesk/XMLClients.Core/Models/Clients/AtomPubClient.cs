using System.Text;
using System.Xml;

namespace XmlClients.Core.Models.Clients;

// AtomPubClient - Implements Atom Publishing protocol 
public class AtomPubClient : BlogClient
{
    public AtomPubClient(string userName, string userPassword, Uri endpoint) : base(userName, userPassword, endpoint)
    {
        //TODO:
        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", userName, userPassword))));
    }

    public async override Task<NodeService> GetAccount(string accountName)
    {
        var account = new NodeService(accountName, _userName, _userPassword, _endpoint, ApiTypes.atAtomPub, ServiceTypes.AtomPub);

        var blogs = await GetBlogs();
        foreach (var item in blogs)
        {
            item.Parent = account;
            account.Children.Add(item);
        }

        account.IsExpanded = true;
        return account;
    }

    public async override Task<List<NodeWorkspace>> GetBlogs()
    {
        List<NodeWorkspace> blogs = new();

        // TODO try exception
        var HTTPResponseMessage = await Client.GetAsync(_endpoint);

        if (HTTPResponseMessage.IsSuccessStatusCode)
        {
             var s = await HTTPResponseMessage.Content.ReadAsStringAsync();

            //System.Diagnostics.Debug.WriteLine("GET blogs(collection): " + s);

            ToDebugWindow(">> HTTP Request GET "
                //+ Environment.NewLine
                + _endpoint.AbsoluteUri
                + Environment.NewLine + Environment.NewLine
                + "<< HTTP Response " + HTTPResponseMessage.StatusCode.ToString()
                + Environment.NewLine
                + s + Environment.NewLine);

            var contenTypeString = HTTPResponseMessage.Content.Headers.GetValues("Content-Type").FirstOrDefault();

            if (!contenTypeString.StartsWith("application/atomsvc+xml"))
            {
                Debug.WriteLine("Content-Type is invalid: " + contenTypeString);

                ToDebugWindow("<< Content-Type is invalid: " + contenTypeString
                    + Environment.NewLine
                    + "expecting " + "application/atomsvc+xml"
                    + Environment.NewLine);

                return blogs;
            }

            var xdoc = new XmlDocument();
            try
            {
                xdoc.LoadXml(s);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("LoadXml failed: " + e.Message);

                ToDebugWindow("<< Invalid XML returned:"
                    + Environment.NewLine
                    + e.Message
                    + Environment.NewLine);

                return blogs;
            }

            var atomNsMgr = new XmlNamespaceManager(xdoc.NameTable);
            atomNsMgr.AddNamespace("atom", "http://www.w3.org/2005/Atom");
            atomNsMgr.AddNamespace("app", "http://www.w3.org/2007/app");

            XmlNodeList workspaceList;
            workspaceList = xdoc.SelectNodes("//app:service/app:workspace", atomNsMgr);
            if (workspaceList == null)
            {
                return blogs;
            }

            foreach (XmlNode n in workspaceList)
            {
                var accountTitle = n.SelectSingleNode("atom:title", atomNsMgr);
                if (accountTitle == null)
                {
                    continue;
                }

                var blog = new NodeWorkspace(accountTitle.InnerText);

                var entries = await GetEntryNodesFromXML(n, atomNsMgr);
                foreach (var item in entries)
                {
                    item.Parent = blog;
                    blog.Children.Add(item);
                }

                blog.IsExpanded = true;

                blogs.Add(blog);
            }

        }
        else
        {
            System.Diagnostics.Debug.WriteLine("GET Error blogs(collection): " + HTTPResponseMessage.StatusCode.ToString());

            var contents = await HTTPResponseMessage.Content.ReadAsStringAsync();

            if (contents != null)
            {
                ToDebugWindow(">> HTTP Request GET (Failed)"
                    + Environment.NewLine
                    + _endpoint.AbsoluteUri
                    + Environment.NewLine + Environment.NewLine
                    + "<< HTTP Response " + HTTPResponseMessage.StatusCode.ToString()
                    + Environment.NewLine
                    + contents + Environment.NewLine);
            }
        }

        return blogs;
    }

    private async Task<List<NodeAtomPubEntryCollection>> GetEntryNodesFromXML(XmlNode w, XmlNamespaceManager atomNsMgr)
    {
        var cols = new List<NodeAtomPubEntryCollection>();

        /*
<?xml version="1.0" encoding="utf-8"?>
<service xmlns="http://www.w3.org/2007/app">
<workspace>
<atom:title xmlns:atom="http://www.w3.org/2005/Atom">hoge</atom:title>
<collection href="https://127.0.0.1/atom/entry">
    <atom:title xmlns:atom="http://www.w3.org/2005/Atom">fuga</atom:title>
    <accept>application/atom+xml;type=entry</accept>
</collection>
</workspace>
</service>
        */

        /*
<?xml version="1.0" encoding="utf-8"?>
<service xmlns="http://www.w3.org/2007/app" xmlns:atom="http://www.w3.org/2005/Atom">
<workspace>
<atom:title>hoge Workspace</atom:title>
<collection href="http://torum.jp/en/wp-app.php/service/posts">
    <atom:title>hoge Posts</atom:title>
    <accept>application/atom+xml;type=entry</accept>
<categories href="http://hoge.jp/wp-app.php/service/categories" />
</collection>
<collection href="http://hoge.jp/wp-app.php/service/attachments">
    <atom:title>hoge Media</atom:title>
    <accept>image/*</accept><accept>audio/*</accept><accept>video/*</accept>
</collection>
</workspace>
</service>
        */

        /*
        <?xml version="1.0" encoding="UTF-8"?>
        <service xmlns="http://www.w3.org/2007/app" xmlns:atom="http://www.w3.org/2005/Atom">
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

        var collectionList = w.SelectNodes("app:collection", atomNsMgr);
        if (collectionList == null)
        {
            return cols;
        }

        foreach (XmlNode n in collectionList)
        {
            var hrefAttr = n.Attributes["href"].Value;
            if (hrefAttr == null)
            {
                continue;
            }

            var title = n.SelectSingleNode("atom:title", atomNsMgr);
            if (title == null)
            {
                continue;
            }

            var entries = new NodeAtomPubEntryCollection(title.InnerText, new Uri(hrefAttr), hrefAttr);

            var acceptList = n.SelectNodes("app:accept", atomNsMgr);
            if (acceptList != null)
            {
                foreach (XmlNode a in acceptList)
                {
                    var acpt = a.InnerText;
                    if (!string.IsNullOrEmpty(acpt))
                    {
                        entries.AcceptTypes.Add(acpt);

                        if ((acpt == "application/atom+xml;type=entry")
                            || (acpt == "application/atom+xml"))
                        {
                            entries.IsAcceptEntry = true;
                        }
                    }
                }
            }
            else
            {
                // default entry
                entries.IsAcceptEntry = true;
            }

            /*
            XmlNode cats = n.SelectSingleNode("app:categories", atomNsMgr);
            if (cats != null)
            {
                // Look for category document.
                if (cats.Attributes["href"] != null)
                {
                    var hrefCat = cats.Attributes["href"];
                    try
                    {
                        entries.CategoriesUri = new Uri(hrefCat.Value);
                    }
                    catch { }
                }

                // Inline categories
                XmlNodeList catList = cats.SelectNodes("atom:category", atomNsMgr);
                foreach (XmlNode c in catList)
                {
                    if (c.Attributes["term"] != null)
                    {
                        NodeCategory category = new NodeCategory(c.Attributes["term"].Value);
                        entries.Children.Add(category);
                    }
                }
            }
            */
            var categoriesList = n.SelectNodes("app:categories", atomNsMgr);
            if (categoriesList != null)
            {
                foreach (XmlNode cats in categoriesList)
                {
                    var categories = new NodeAtomPubCatetories("Categories");
                    categories.IsExpanded = true;
                    categories.Parent = entries;

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

                    if (cats.Attributes["scheme"] != null)
                    {
                        var catScheme = cats.Attributes["scheme"].Value;
                        if (!string.IsNullOrEmpty(catScheme))
                        {
                            categories.Scheme = catScheme;
                        }
                    }
                    // scheme

                    var categoryList = cats.SelectNodes("atom:category", atomNsMgr);
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

                            category.Name = category.Term;

                            if (string.IsNullOrEmpty(category.Scheme))
                            {
                                category.Scheme = categories.Scheme;
                            }

                            categories.Children.Add(category);
                        }
                    }

                    entries.CategoriesUri = catHrefUri;

                    entries.IsCategoryFixed = categories.IsCategoryFixed;

                    if (categories.Children.Count > 0)
                    {
                        //entries.Children.Add(categories);

                        foreach (NodeAtomPubCategory c in categories.Children)
                        {
                            c.Parent = entries;
                            entries.Children.Add(c);
                        }
                    }
                }
            }

            // Get category document.
            if (entries.CategoriesUri != null)
            {
                var nc = await GetCategiries(entries.CategoriesUri);

                entries.IsCategoryFixed = nc.IsCategoryFixed;

                foreach (NodeAtomPubCategory c in nc.Children)
                {
                    bool isExists = false;

                    // check if exists
                    foreach (var hoge in entries.Children)
                    {
                        if (hoge is NodeAtomPubCategory)
                        {
                            if ((hoge as NodeAtomPubCategory).Term.Equals(c.Term))
                            {
                                isExists = true;
                                
                                break;
                            }
                        }
                    }

                    if (!isExists)
                    {
                        c.Parent = entries;
                        entries.Children.Add(c);
                    }
                }
            }

            cols.Add(entries);
        }

        return cols;
    }

    public async Task<NodeAtomPubCatetories> GetCategiries(Uri categoriesUrl)
    {
        var cats = new NodeAtomPubCatetories("AtomPub Categories");

        var HTTPResponseMessage = await Client.GetAsync(categoriesUrl);

        if (HTTPResponseMessage.IsSuccessStatusCode)
        {
            var s = await HTTPResponseMessage.Content.ReadAsStringAsync();

            ToDebugWindow(">> HTTP Request GET "
                + Environment.NewLine
                + categoriesUrl.AbsoluteUri
                + Environment.NewLine + Environment.NewLine
                + "<< HTTP Response " + HTTPResponseMessage.StatusCode.ToString()
                + Environment.NewLine
                + s + Environment.NewLine);

            /*
    <app:categories xmlns:app="http://www.w3.org/2007/app" xmlns="http://www.w3.org/2005/Atom" fixed="yes" scheme="http://torum.jp/en">
        <category term="Blogroll" />
        <category term="Software" />
        <category term="testCat" />
        <category term="Uncategorized" />
    </app:categories>
             */

            var contenTypeString = HTTPResponseMessage.Content.Headers.GetValues("Content-Type").FirstOrDefault();

            if (!contenTypeString.StartsWith("application/atomcat+xml"))
            {
                System.Diagnostics.Debug.WriteLine("Content-Type is invalid: " + contenTypeString);

                ToDebugWindow("<< Content-Type is invalid: " + contenTypeString
                    + Environment.NewLine
                    + "expecting " + "application/atomcat+xml"
                    + Environment.NewLine);

                return cats;
            }

            var xdoc = new XmlDocument();
            try
            {
                xdoc.LoadXml(s);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("LoadXml failed: " + e.Message);

                ToDebugWindow("<< Invalid XML returned:"
                    + Environment.NewLine
                    + e.Message
                    + Environment.NewLine);

                return cats;
            }

            var atomNsMgr = new XmlNamespaceManager(xdoc.NameTable);
            atomNsMgr.AddNamespace("atom", "http://www.w3.org/2005/Atom");
            atomNsMgr.AddNamespace("app", "http://www.w3.org/2007/app");

            if (xdoc.DocumentElement.Attributes["fixed"] != null)
            {
                string catFix = xdoc.DocumentElement.Attributes["fixed"].Value;
                if (!string.IsNullOrEmpty(catFix))
                {
                    if (catFix == "yes")
                    {
                        cats.IsCategoryFixed = true;
                    }
                    else
                    {
                        cats.IsCategoryFixed = false;
                    }
                }
            }

            if (xdoc.DocumentElement.Attributes["scheme"] != null)
            {
                var catScheme = xdoc.DocumentElement.Attributes["scheme"].Value;
                if (!string.IsNullOrEmpty(catScheme))
                {
                    cats.Scheme = catScheme;
                }
            }

            XmlNodeList categoryList;
            categoryList = xdoc.SelectNodes("//app:categories/atom:category", atomNsMgr);
            if (categoryList == null)
            {
                return cats;
            }

            foreach (XmlNode c in categoryList)
            {
                if (c.Attributes["term"] != null)
                {
                    var category = new NodeAtomPubCategory(c.Attributes["term"].Value);

                    if (c.Attributes["scheme"] != null)
                    {
                        category.Scheme = c.Attributes["scheme"].Value;
                    }

                    if (string.IsNullOrEmpty(category.Scheme))
                    {
                        category.Scheme = cats.Scheme;
                    }

                    cats.Children.Add(category);
                }
            }
        }
        else
        {
            var contents = await HTTPResponseMessage.Content.ReadAsStringAsync();

            if (contents != null)
            {
                ToDebugWindow(">> HTTP Request GET (Failed)"
                    + Environment.NewLine
                    + categoriesUrl.AbsoluteUri
                    + Environment.NewLine + Environment.NewLine
                    + "<< HTTP Response " + HTTPResponseMessage.StatusCode.ToString()
                    + Environment.NewLine
                    + contents + Environment.NewLine);
            }
        }

        return cats;
    }

    public async override Task<HttpClientEntryItemCollectionResultWrapper> GetEntries(Uri entriesUrl, string serviceId)
    {
        var res = new HttpClientEntryItemCollectionResultWrapper();

        var list = new List<EntryItem>();
        res.Entries = list;

        //System.Diagnostics.Debug.WriteLine("GetEntries Uri: " + entriesUrl.AbsoluteUri);
        var HTTPResponseMessage = await Client.GetAsync(entriesUrl);

        if (HTTPResponseMessage.IsSuccessStatusCode)
        {
            var s = await HTTPResponseMessage.Content.ReadAsStringAsync();

            ToDebugWindow(">> HTTP Request GET "
                + Environment.NewLine
                + entriesUrl.AbsoluteUri
                + Environment.NewLine + Environment.NewLine
                + "<< HTTP Response " + HTTPResponseMessage.StatusCode.ToString()
                + Environment.NewLine
                + s + Environment.NewLine);

            //System.Diagnostics.Debug.WriteLine("GET entries response: " + s);
            /*
   <?xml version="1.0" encoding="utf-8"?>
   <feed xmlns="http://www.w3.org/2005/Atom">
     <title type="text">dive into mark</title>
     <subtitle type="html">
       A &lt;em&gt;lot&lt;/em&gt; of effort
       went into making this effortless
     </subtitle>
     <updated>2005-07-31T12:29:29Z</updated>
     <id>tag:example.org,2003:3</id>
     <link rel="alternate" type="text/html"
      hreflang="en" href="http://example.org/"/>
     <link rel="self" type="application/atom+xml"
      href="http://example.org/feed.atom"/>
     <rights>Copyright (c) 2003, Mark Pilgrim</rights>
     <generator uri="http://www.example.com/" version="1.0">
       Example Toolkit
     </generator>
     <entry>
       <title>Atom draft-07 snapshot</title>
       <link rel="alternate" type="text/html"
        href="http://example.org/2005/04/02/atom"/>
       <link rel="enclosure" type="audio/mpeg" length="1337"
        href="http://example.org/audio/ph34r_my_podcast.mp3"/>
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
       <content type="xhtml" xml:lang="en"
        xml:base="http://diveintomark.org/">
         <div xmlns="http://www.w3.org/1999/xhtml">
           <p><i>[Update: The Atom draft is finished.]</i></p>
         </div>
       </content>
     </entry>
   </feed>
            */

            var contenTypeString = HTTPResponseMessage.Content.Headers.GetValues("Content-Type").FirstOrDefault();

            if (!contenTypeString.StartsWith("application/atom+xml"))
            {
                System.Diagnostics.Debug.WriteLine("Content-Type is invalid: " + contenTypeString);

                ToDebugWindow("<< Content-Type is invalid: " + contenTypeString
                    + Environment.NewLine
                    + "expecting " + "application/atom+xml"
                    + Environment.NewLine);

                InvalidContentType(res.Error, "Content-Type is invalid", "HttpResponse.Content.Headers.GetValues", "AtomPubClient: GetEntries");
                res.IsError = true;

                return (res as HttpClientEntryItemCollectionResultWrapper);
            }

            var xdoc = new XmlDocument();
            try
            {
                xdoc.LoadXml(s);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("LoadXml failed: " + e.Message);

                ToDebugWindow("<< Invalid XML returned:"
                    + Environment.NewLine
                    + e.Message
                    + Environment.NewLine);

                InvalidXml(res.Error, e.Message, "XmlDocument.Load", "AtomPubClient: GetEntries");
                res.IsError = true;

                return res;
            }

            var atomNsMgr = new XmlNamespaceManager(xdoc.NameTable);
            atomNsMgr.AddNamespace("atom", "http://www.w3.org/2005/Atom");
            atomNsMgr.AddNamespace("app", "http://www.w3.org/2007/app");

            XmlNodeList entryList;
            entryList = xdoc.SelectNodes("//atom:feed/atom:entry", atomNsMgr);
            if (entryList == null)
            {
                if (entryList == null)
                {
                    res.Entries = list;

                    return res;
                }
            }

            foreach (XmlNode l in entryList)
            {
                var ent = new AtomEntry("", serviceId, this);

                FillEntryItemFromXML(ent, l, atomNsMgr, serviceId);

                list.Add(ent);
            }
        }
        else
        {
            var contents = await HTTPResponseMessage.Content.ReadAsStringAsync();

            if (contents != null)
            {
                ToDebugWindow(">> HTTP Request GET (Failed)"
                    + Environment.NewLine
                    + entriesUrl.AbsoluteUri
                    + Environment.NewLine + Environment.NewLine
                    + "<< HTTP Response " + HTTPResponseMessage.StatusCode.ToString()
                    + Environment.NewLine
                    + contents + Environment.NewLine);
            }
        }

        return res;
    }

    private void FillEntryItemFromXML(AtomEntry entItem, XmlNode entryNode, XmlNamespaceManager atomNsMgr, string serviceId)
    {
        var entry = CreateAtomEntryFromXML(entryNode, atomNsMgr, serviceId);

        if (entry == null)
        {
            return;
        }

        entItem.Name = entry.Name;
        //entItem.ID = entry.ID;
        entItem.EntryId = entry.EntryId;
        entItem.EditUri = entry.EditUri;
        entItem.AltHtmlUri = entry.AltHtmlUri;
        entItem.EntryBody = entry;

        entItem.Status = entry.Status;
    }

    public async override Task<EntryFull?> GetFullEntry(Uri entryUri, string serviceId, string nil)
    {
        // TODO: 
        // HTTP Head, if_modified_since or If-None-Match etag or something... then  Get;

        var HTTPResponseMessage = await Client.GetAsync(entryUri);

        if (HTTPResponseMessage.IsSuccessStatusCode)
        {
            var s = await HTTPResponseMessage.Content.ReadAsStringAsync();

            ToDebugWindow(">> HTTP Request GET "
                + Environment.NewLine
                + entryUri.AbsoluteUri
                + Environment.NewLine + Environment.NewLine
                + "<< HTTP Response " + HTTPResponseMessage.StatusCode.ToString()
                + Environment.NewLine
                + s + Environment.NewLine);

            var contenTypeString = HTTPResponseMessage.Content.Headers.GetValues("Content-Type").FirstOrDefault();

            if (contenTypeString is null || !contenTypeString.StartsWith("application/atom+xml"))
            {
                System.Diagnostics.Debug.WriteLine("Content-Type is invalid: " + contenTypeString);

                ToDebugWindow("<< Content-Type is invalid: " + contenTypeString
                    + Environment.NewLine
                    + "expecting " + "application/atom+xml"
                    + Environment.NewLine);

                return null;
            }

            var xdoc = new XmlDocument();
            try
            {
                xdoc.LoadXml(s);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("LoadXml failed: " + e.Message);

                ToDebugWindow("<< Invalid XML returned:"
                    + Environment.NewLine
                    + e.Message
                    + Environment.NewLine);

                return null;
            }

            var atomNsMgr = new XmlNamespaceManager(xdoc.NameTable);
            atomNsMgr.AddNamespace("atom", "http://www.w3.org/2005/Atom");
            atomNsMgr.AddNamespace("app", "http://www.w3.org/2007/app");
            atomNsMgr.AddNamespace("hatena", "http://www.hatena.ne.jp/info/xmlns#");

            var entryNode = xdoc.SelectSingleNode("//atom:entry", atomNsMgr);
            if (entryNode == null)
            {
                System.Diagnostics.Debug.WriteLine("//atom:entry is null.");
                return null;
            }

            var cont = entryNode.SelectSingleNode("atom:content", atomNsMgr);
            if (cont == null)
            {
                System.Diagnostics.Debug.WriteLine("//atom:content is null.");
                return null;
            }

            var entry = CreateAtomEntryFromXML(entryNode, atomNsMgr, serviceId);



            //TODO: Save ETag
            //HTTPResponseMessage.Content


            return entry;
        }
        else
        {
            var contents = await HTTPResponseMessage.Content.ReadAsStringAsync();

            if (contents != null)
            {
                ToDebugWindow(">> HTTP Request GET (Failed)"
                    + Environment.NewLine
                    + _endpoint.AbsoluteUri
                    + Environment.NewLine + Environment.NewLine
                    + "<< HTTP Response " + HTTPResponseMessage.StatusCode.ToString()
                    + Environment.NewLine
                    + contents + Environment.NewLine);
            }

            return null;
        }
    }

    private AtomEntry CreateAtomEntryFromXML(XmlNode entryNode, XmlNamespaceManager atomNsMgr, string serviceId)
    {
        /*
			<entry>
				<id>hoge</id>
				<link rel="edit" href="https://127.0.0.1/app/entry/17391345971628358314"/>
				<link rel="alternate" type="text/html" href="https://127.0.0.1/htm/entry/2018/03/22/221846"/>
				<author>
                <name>hoge</name>
            </author>
				<title>test title</title>
				<updated>2018-03-22T22:18:46+09:00</updated>
				<published>2018-03-22T22:18:46+09:00</published>
				<app:edited>2018-03-22T22:18:46+09:00</app:edited>
				<summary type="text">asdf</summary>
				<content type="text/html">asdf</content>
				<hatena:formatted-content type="text/html" xmlns:hatena="http://www.hatena.ne.jp/info/xmlns#">&lt;a class=&quot;keyword&quot; href=&quot;http://d.hatena.ne.jp/keyword/asdf&quot;&gt;asdf&lt;/a&gt;</hatena:formatted-content>
				<category term="test" />
				<app:control>
					<app:draft>yes</app:draft>
				</app:control>
			  </entry>
         */

        var entryTitle = entryNode.SelectSingleNode("atom:title", atomNsMgr);
        if (entryTitle == null)
        {
            System.Diagnostics.Debug.WriteLine("atom:title: is null. ");
            //return;
        }

        var entryID = entryNode.SelectSingleNode("atom:id", atomNsMgr);
        if (entryID == null)
        {
            System.Diagnostics.Debug.WriteLine("atom:id: is null. ");
            //return;
        }

        var entryLinkUris = entryNode.SelectNodes("atom:link", atomNsMgr);
        string relAttr;
        string hrefAttr;
        string typeAttr;
        Uri editUri = null;
        Uri altUri = null;
        if (entryLinkUris == null)
        {
            System.Diagnostics.Debug.WriteLine("atom:link: is null. ");
            //continue;
        }
        else
        {
            foreach (XmlNode u in entryLinkUris)
            {
                relAttr = (u.Attributes["rel"] != null) ? u.Attributes["rel"].Value : "";
                hrefAttr = (u.Attributes["href"] != null) ? u.Attributes["href"].Value : "";
                typeAttr = (u.Attributes["type"] != null) ? u.Attributes["type"].Value : "";

                if ((!string.IsNullOrEmpty(relAttr)) && (!string.IsNullOrEmpty(hrefAttr)))
                {

                    switch (relAttr)
                    {
                        case "edit":
                            try
                            {
                                editUri = new Uri(hrefAttr);
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
                                        altUri = new Uri(hrefAttr);
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
                                        altUri = new Uri(hrefAttr);
                                    }
                                }
                                else
                                {
                                    // I am not happy but let's assume it is html.
                                    altUri = new Uri(hrefAttr);
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

        // TODO:
        //updated
        //published
        //app:edited
        //summary
        //category

        var entry = new AtomEntry("", serviceId, this);
        // TODO:
        //AtomEntryHatena
        /*
        // Hatena's formatted-content
        XmlNode formattedContent = entryNode.SelectSingleNode("hatena:formatted-content", atomNsMgr);
        if (formattedContent != null)
        {
            entry.FormattedContent = formattedContent.InnerText;
        }
        */


        entry.Name = (entryTitle != null) ? entryTitle.InnerText : "";
        entry.EntryId = (entryID != null) ? entryID.InnerText : "";
        entry.EditUri = editUri;
        entry.AltHtmlUri = altUri;

        var cont = entryNode.SelectSingleNode("atom:content", atomNsMgr);
        if (cont == null)
        {
            System.Diagnostics.Debug.WriteLine("//atom:content is null.");
        }
        else
        {
            var contype = cont.Attributes["type"].Value;
            if (!string.IsNullOrEmpty(contype))
            {
                entry.ContentTypeString = contype;

                entry.ContentType = contype switch
                {
                    "text" => EntryFull.ContentTypes.text,
                    "html" => EntryFull.ContentTypes.textHtml,
                    "xhtml" => EntryFull.ContentTypes.textHtml,
                    "text/plain" => EntryFull.ContentTypes.text,
                    "text/html" => EntryFull.ContentTypes.textHtml,
                    "text/x-markdown" => EntryFull.ContentTypes.markdown,
                    "text/x-hatena-syntax" => EntryFull.ContentTypes.hatena,
                    _ => EntryFull.ContentTypes.text,
                };
            }

            entry.Content = cont.InnerText;
        }

        //app:control/app:draft(yes/no)
        var entryDraft = entryNode.SelectSingleNode("app:control/app:draft", atomNsMgr);
        if (entryDraft == null)
        {
            System.Diagnostics.Debug.WriteLine("app:draft: is null.");
        }

        var draft = entryDraft?.InnerText;
        entry.IsDraft = string.Equals(draft, "yes", StringComparison.CurrentCultureIgnoreCase);

        entry.Status = entry.IsDraft ? EditEntryItem.EditStatus.esDraft : EditEntryItem.EditStatus.esNormal;

        return entry;
    }

    public async override Task<bool> UpdateEntry(EntryFull entry)
    {
        if (entry is not AtomEntry)
        {
            return false;
        }

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Put,
            RequestUri = entry.EditUri,
            Content = new StringContent((entry as AtomEntry).AsUTF8Xml(), Encoding.UTF8, "application/atom+xml")
        };

        var response = await Client.SendAsync(request);

        ToDebugWindow(">> HTTP Request PUT "
            + Environment.NewLine
            + entry.EditUri.AbsoluteUri
            + Environment.NewLine
            + (entry as AtomEntry).AsUTF16Xml()
            + Environment.NewLine + Environment.NewLine
            + "<< HTTP Response " + response.StatusCode.ToString()
            + Environment.NewLine);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Put failed. Status code is " + response.StatusCode.ToString());

            var contents = await response.Content.ReadAsStringAsync();

            /* 
             * Hatena AtomPub returns this.
             BadRequest 400 Cannot Change into Draft
            */

            if (contents != null) {

                System.Diagnostics.Debug.WriteLine(contents);

                ToDebugWindow("<< HTTP Request PUT failed. HTTP Response content is:"
                + Environment.NewLine
                + contents
                + Environment.NewLine);
            }

            return false;
        }

    }

    public override async Task<bool> PostEntry(EntryFull entry)
    {
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = entry.PostUri,
            Content = new StringContent((entry as AtomEntry).AsUTF8Xml(), Encoding.UTF8, "application/atom+xml")
        };

        var response = await Client.SendAsync(request);

        ToDebugWindow(">> HTTP Request POST "
            + Environment.NewLine
            + entry.EditUri.AbsoluteUri
            + Environment.NewLine
            + (entry as AtomEntry).AsUTF16Xml()
            + Environment.NewLine + Environment.NewLine
            + "<< HTTP Response " + response.StatusCode.ToString()
            + Environment.NewLine);

        /*
        XNamespace atom = "http://www.w3.org/2005/Atom";
        XNamespace app = "http://www.w3.org/2007/app";

        XDocument doc = new XDocument(new XElement(atom + "entry",
                                                new XAttribute(XNamespace.Xmlns + "app", app.NamespaceName),
                                                new XElement(atom + "title", "test title"),
                                                new XElement(atom + "author",
                                                    new XElement(atom + "name", "me")),
                                                new XElement(atom + "content",
                                                    new XAttribute("type", "text/plain"),
                                                    new XText("asdf")),
                                                new XElement(atom + "category",
                                                    new XAttribute("term", "test categ")
                                                    ),
                                                new XElement(app + "control",
                                                    new XElement(app + "draft", "yes")
                                                    )
                                                )
                                     );

        System.Diagnostics.Debug.WriteLine("content xml: " + doc.ToString());
        */

        if (response.IsSuccessStatusCode)
        {
            var entryUrl = response.Headers.Location;

            if (entryUrl != null)
            {
                entry.EditUri = entryUrl;

                var contenTypeString = response.Content.Headers.GetValues("Content-Type").FirstOrDefault();

                if (!contenTypeString.StartsWith("application/atom+xml"))
                {
                    System.Diagnostics.Debug.WriteLine("Content-Type is invalid: " + contenTypeString);

                    ToDebugWindow("<< Content-Type is invalid: " + contenTypeString
                        + Environment.NewLine
                        + "expecting " + "application/atom+xml"
                        + Environment.NewLine);
                }
                else
                {
                    // TODO: load content body xml entry and get id and rel alt and such.
                }

                //System.Diagnostics.Debug.WriteLine("created: " + entryUrl);

                ToDebugWindow("<< HTTP Response Header - Location: " 
                    + Environment.NewLine
                    + entryUrl
                    + Environment.NewLine);

                return true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Post IsSuccess, but Location header is null. ");

                ToDebugWindow("POST is successfull, but Location header is missing. "
                    + Environment.NewLine
                    + Environment.NewLine);

                return false;
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Post failed. Status code is " + response.StatusCode.ToString());

            var contents = await response.Content.ReadAsStringAsync();

            if (contents != null)
            {
                System.Diagnostics.Debug.WriteLine(contents);

                ToDebugWindow("POST failed. HTTP Response content is:"
                + Environment.NewLine
                + contents
                + Environment.NewLine);
            }

            return false;
        }

    }

    public async override Task<bool> DeleteEntry(Uri editUri)
    {
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Delete,
            RequestUri = editUri
        };

        var response = await Client.SendAsync(request);

        ToDebugWindow(">> HTTP Request DELETE "
            + Environment.NewLine
            + editUri.AbsoluteUri
            + Environment.NewLine + Environment.NewLine
            + "<< HTTP Response " + response.StatusCode.ToString()
            + Environment.NewLine);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Delete failed. Status code is " + response.StatusCode.ToString());

            var contents = await response.Content.ReadAsStringAsync();

            if (contents != null)
            {
                System.Diagnostics.Debug.WriteLine(contents);

                ToDebugWindow("DELETE failed. HTTP Response content is:"
                + Environment.NewLine
                + contents
                + Environment.NewLine);
            }

            return false;
        }

    }

}
