using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.ObjectModel;

namespace XmlClients.Core.Models.Clients;

public class XmlRpcClient : BlogClient
{
    /// <summary>
    /// XmlRpcClient 
    /// Implements: 
    ///   XML-RPC Movable Type API, MetaWeblog API, Blogger API 
    /// Uses following methods:
    ///  metaWeblog.getUsersBlogs
    ///  metaWeblog.getRecentPosts
    ///  metaWeblog.getPost
    ///  metaWeblog.newPost
    ///  metaWeblog.editPost
    ///  metaWeblog.deletePost
    ///  metaWeblog.getCategories
    ///  metaWeblog.newMediaObject
    ///  mt.getRecentPostTitles
    ///  mt.supportedMethods
    ///  mt.supportedTextFilters
    ///  mt.publishPost
    ///  mt.getCategoryList
    ///  mt.getPostCategories
    ///  mt.setPostCategories
    /// </summary>
    /// 
    public XmlRpcClient(string userName, string userPassword, Uri endpoint) : base(userName, userPassword, endpoint)
    {

    }

    public async override Task<NodeService> GetAccount(string accountName)
    {
        var account = new NodeService(accountName, _userName, _userPassword, _endpoint, ApiTypes.atXMLRPC_MovableType, ServiceTypes.XmlRpc);

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

        var xdoc = new XmlDocument();
        var xdec = xdoc.CreateXmlDeclaration("1.0", "UTF-8", null);
        xdoc.AppendChild(xdec);

        XmlElement objRootNode, objMethodNode, objParamsNode, objParamNode, objValueNode, objTypeNode;
        XmlText xt;

        objRootNode = xdoc.CreateElement(string.Empty, "methodCall", string.Empty);
        xdoc.AppendChild(objRootNode);

            objMethodNode = xdoc.CreateElement(string.Empty, "methodName", string.Empty);
                xt = xdoc.CreateTextNode("metaWeblog.getUsersBlogs"); // blogger.getUsersBlogs
                objMethodNode.AppendChild(xt);
                objRootNode.AppendChild(objMethodNode);

            objParamsNode = xdoc.CreateElement(string.Empty, "params", string.Empty);

                objParamNode = xdoc.CreateElement(string.Empty, "param", string.Empty);
                objParamsNode.AppendChild(objParamNode);

                    objValueNode = xdoc.CreateElement(string.Empty, "value", string.Empty);
                    objParamNode.AppendChild(objValueNode);

                        objTypeNode = xdoc.CreateElement(string.Empty, "string", string.Empty);
                        objValueNode.AppendChild(objTypeNode);

                objParamNode = xdoc.CreateElement(string.Empty, "param", string.Empty);
                objParamsNode.AppendChild(objParamNode);

                    objValueNode = xdoc.CreateElement(string.Empty, "value", string.Empty);
                    objParamNode.AppendChild(objValueNode);

                        objTypeNode = xdoc.CreateElement(string.Empty, "string", string.Empty);
                        xt = xdoc.CreateTextNode(_userName); 
                        objTypeNode.AppendChild(xt);
                        objValueNode.AppendChild(objTypeNode);

                objParamNode = xdoc.CreateElement(string.Empty, "param", string.Empty);
                objParamsNode.AppendChild(objParamNode);

                    objValueNode = xdoc.CreateElement(string.Empty, "value", string.Empty);
                    objParamNode.AppendChild(objValueNode);

                        objTypeNode = xdoc.CreateElement(string.Empty, "string", string.Empty);
                        xt = xdoc.CreateTextNode(_userPassword); 
                        objTypeNode.AppendChild(xt);
                        objValueNode.AppendChild(objTypeNode);

        objRootNode.AppendChild(objParamsNode);

        //System.Diagnostics.Debug.WriteLine("GET blogs(getUsersBlogs): " + AsXml(xdoc));

        ToDebugWindow(">> HTTP Request POST "
            //+ Environment.NewLine
            + _endpoint.AbsoluteUri
            + Environment.NewLine
            + AsUTF16Xml(xdoc)
            + Environment.NewLine);

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = _endpoint,
            Content = new StringContent(AsUTF8Xml(xdoc), Encoding.UTF8, "text/xml")
        };

        // TODO try exception
        var response = await Client.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            var contenTypeString = response.Content.Headers.GetValues("Content-Type").FirstOrDefault();

            if (!contenTypeString.StartsWith("text/xml"))
            {
                System.Diagnostics.Debug.WriteLine("Content-Type is invalid: " + contenTypeString);

                ToDebugWindow("<< HTTP Response Content-Type is invalid: " + contenTypeString
                    + Environment.NewLine
                    + "expecting " + "text/xml"
                    + Environment.NewLine);

                return blogs;
            }

            var s = await response.Content.ReadAsStringAsync();

            ToDebugWindow("<< HTTP Response " + response.StatusCode.ToString()
            + Environment.NewLine
            + s 
            + Environment.NewLine);

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


            // TODO: Parse error response.


            // //methodResponse/fault
            // or
            // //methodResponse/params

            /*

// fault response   
<methodResponse>
    <fault>
        <value>
            <struct>
                <member>
                    <name>faultCode</name>
                    <value>
                        <int>400</int>
                    </value>
                </member>
                <member>
                    <name>faultString</name>
                    <value>
                        <string>Insufficient arguments passed to this XML-RPC method.</string>
                    </value>
                </member>
            </struct>
        </value>
    </fault>
</methodResponse>

            */

            var blogList = xdoc.SelectNodes("//methodResponse/params/param/value/array/data/value");
            if (blogList == null)
            {
                return blogs;
            }

            foreach (XmlNode b in blogList)
            {
                var blog = new NodeWorkspace("Blog");

                var memberList = b.SelectNodes("struct/member");
                if (memberList == null)
                {
                    continue;
                }

                //bool isAdmin = false;
                //bool isPrimary = false;
                Uri? url = null;
                var blogid = "";
                var blogName = "";
                Uri xmlrpc = null;

                foreach (XmlNode m in memberList)
                {

                    var valueList = m.ChildNodes;
                    var name = "";
                    var value = "";
                    foreach (XmlNode v in valueList)
                    {
                        if (v.Name == "name")
                        {
                            name = v.InnerText;
                        }
                        if (v.Name == "value")
                        {
                            value = v.InnerText;
                        }
                    }
                    
                    if (name == "isAdmin")
                    {
                        if (value != "0")
                        {
                            //isAdmin = true;
                        }
                    }

                    if (name == "isPrimary")
                    {
                        if (value != "0")
                        {
                            //isPrimary = true;
                        }
                    }

                    if (name == "url")
                    {
                        try
                        {
                            url = new Uri(value);
                        }
                        catch { }
                    }

                    if (name == "blogid")
                    {
                        blogid = value;
                    }

                    if (name == "blogName")
                    {
                        blogName = value;
                    }

                    if (name == "xmlrpc")
                    {
                        try
                        {
                            xmlrpc = new Uri(value);
                        }
                        catch { }
                    }

                }

                //System.Diagnostics.Debug.WriteLine("blogName: " + blogName);

                if (!string.IsNullOrEmpty(blogName))
                {
                    if (xmlrpc == null)
                    {
                        xmlrpc = _endpoint;
                    }

                    var col = new NodeXmlRpcEntryCollection(blogName, xmlrpc, blogid);


                    // Categories
                    var cats = await GetCategiries(xmlrpc, blogid);

                    foreach (var c in cats)
                    {
                        col.Children.Add(c);
                    }

                    //blogid, blogName are required. 
                    //url is optional.

                    // wp has isAdmin,
                    // for multisite, xmlrpc, isPrimary.

                    //make sure compare them case insensitive.

                    col.Parent = blog;
                    blog.Children.Add(col);

                }

                blog.IsExpanded = true;

                blogs.Add(blog);
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("get blogs failed (HTTP error): " );

            var contents = await response.Content.ReadAsStringAsync();

            if (contents != null)
            {
                ToDebugWindow(">> HTTP Request POST (Failed)"
                    + Environment.NewLine
                    + _endpoint.AbsoluteUri
                    + Environment.NewLine + Environment.NewLine
                    + "<< HTTP Response " + response.StatusCode.ToString()
                    + Environment.NewLine
                    + contents + Environment.NewLine);
            }
        }

        return blogs;
    }

    public async Task<List<NodeCategory>> GetCategiries(Uri categoriesUrl, string blogid)
    {
        var cats = new List<NodeCategory>();

        var xdoc = new XmlDocument();
        var xdec = xdoc.CreateXmlDeclaration("1.0", "UTF-8", null);
        xdoc.AppendChild(xdec);

        XmlElement objRootNode, objMethodNode, objParamsNode, objParamNode, objValueNode, objTypeNode;
        XmlText xt;

        objRootNode = xdoc.CreateElement(string.Empty, "methodCall", string.Empty);
        xdoc.AppendChild(objRootNode);

        objMethodNode = xdoc.CreateElement(string.Empty, "methodName", string.Empty);
        xt = xdoc.CreateTextNode("metaWeblog.getCategories");
        objMethodNode.AppendChild(xt);
        objRootNode.AppendChild(objMethodNode);

        objParamsNode = xdoc.CreateElement(string.Empty, "params", string.Empty);

        objParamNode = xdoc.CreateElement(string.Empty, "param", string.Empty);
        objParamsNode.AppendChild(objParamNode);

        objValueNode = xdoc.CreateElement(string.Empty, "value", string.Empty);
        objParamNode.AppendChild(objValueNode);

        objTypeNode = xdoc.CreateElement(string.Empty, "int", string.Empty);
        xt = xdoc.CreateTextNode(blogid);
        objTypeNode.AppendChild(xt);
        objValueNode.AppendChild(objTypeNode);

        objParamNode = xdoc.CreateElement(string.Empty, "param", string.Empty);
        objParamsNode.AppendChild(objParamNode);

        objValueNode = xdoc.CreateElement(string.Empty, "value", string.Empty);
        objParamNode.AppendChild(objValueNode);

        objTypeNode = xdoc.CreateElement(string.Empty, "string", string.Empty);
        xt = xdoc.CreateTextNode(_userName);
        objTypeNode.AppendChild(xt);
        objValueNode.AppendChild(objTypeNode);

        objParamNode = xdoc.CreateElement(string.Empty, "param", string.Empty);
        objParamsNode.AppendChild(objParamNode);

        objValueNode = xdoc.CreateElement(string.Empty, "value", string.Empty);
        objParamNode.AppendChild(objValueNode);

        objTypeNode = xdoc.CreateElement(string.Empty, "string", string.Empty);
        xt = xdoc.CreateTextNode(_userPassword);
        objTypeNode.AppendChild(xt);
        objValueNode.AppendChild(objTypeNode);

        objRootNode.AppendChild(objParamsNode);

        //System.Diagnostics.Debug.WriteLine("GetCategiries: " + AsUTF16Xml(xdoc));

        ToDebugWindow(">> HTTP Request POST "
            //+ Environment.NewLine
            + categoriesUrl.AbsoluteUri
            + Environment.NewLine
            + AsUTF16Xml(xdoc)
            + Environment.NewLine);

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = categoriesUrl,
            Content = new StringContent(AsUTF8Xml(xdoc), Encoding.UTF8, "text/xml")
        };

        var response = await Client.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            var contenTypeString = response.Content.Headers.GetValues("Content-Type").FirstOrDefault();

            if (!contenTypeString.StartsWith("text/xml"))
            {
                System.Diagnostics.Debug.WriteLine("Content-Type is invalid: " + contenTypeString);

                ToDebugWindow("<< HTTP Response Content-Type is invalid: " + contenTypeString
                    + Environment.NewLine
                    + "expecting " + "text/xml"
                    + Environment.NewLine);

                return cats;
            }

            var s = await response.Content.ReadAsStringAsync();

            //System.Diagnostics.Debug.WriteLine("GetCategiries response: " + s);

            ToDebugWindow("<< HTTP Response " + response.StatusCode.ToString()
            + Environment.NewLine
            + s
            + Environment.NewLine);

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

            var catList = xdoc.SelectNodes("//methodResponse/params/param/value/array/data/value");
            if (catList == null)
            {
                return cats;
            }

            foreach (XmlNode cal in catList)
            {
                var memberList = cal.SelectNodes("struct/member");
                if (memberList == null)
                {
                    continue;
                }

                var categoryName = "";
                var categoryId = "";
                var parentId = "";
                var description = "";
                var categoryDescription = "";
                Uri? htmlUrl = null;
                Uri? rssUrl = null;
                
                foreach (XmlNode m in memberList)
                {

                    var valueList = m.ChildNodes;
                    var name = "";
                    var value = "";
                    foreach (XmlNode v in valueList)
                    {
                        if (v.Name == "name")
                        {
                            name = v.InnerText;
                        }
                        if (v.Name == "value")
                        {
                            value = v.InnerText;
                        }
                    }

                    if (name == "categoryName")
                    {
                        categoryName = value;
                    }

                    if (name == "categoryId")
                    {
                        categoryId = value;
                    }

                    if (name == "parentId")
                    {
                        parentId = value;
                    }

                    if (name == "description")
                    {
                        description = value;
                    }

                    if (name == "categoryDescription")
                    {
                        categoryDescription = value;
                    }

                    if (name == "htmlUrl")
                    {
                        try
                        {
                            htmlUrl = new Uri(value);
                        }
                        catch { }
                    }

                    if (name == "rssUrl")
                    {
                        try
                        {
                            rssUrl = new Uri(value);
                        }
                        catch { }
                    }


                }
                
                if (!string.IsNullOrEmpty(categoryName))
                {
                    var category = new NodeXmlRpcMTCategory(categoryName)
                    {
                        CategoryId = categoryId,
                        ParentId = parentId,
                        Description = description,
                        CategoryDescription = categoryDescription,
                        HtmlUrl = htmlUrl,
                        RssUrl = rssUrl
                    };

                    cats.Add(category);
                }

            }


        }
        else
        {
            var contents = await response.Content.ReadAsStringAsync();

            if (contents != null)
            {
                ToDebugWindow(">> HTTP Request POST (Failed)"
                    + Environment.NewLine
                    + categoriesUrl.AbsoluteUri
                    + Environment.NewLine + Environment.NewLine
                    + "<< HTTP Response " + response.StatusCode.ToString()
                    + Environment.NewLine
                    + contents + Environment.NewLine);
            }

            //return cats;
        }

        return cats;
    }

    public async override Task<HttpClientEntryItemCollectionResultWrapper> GetEntries(Uri entryUri, string serviceId)
    {
        var res = new HttpClientEntryItemCollectionResultWrapper();

        var list = new List<EntryItem>();
        res.Entries = list;

        var xdoc = new XmlDocument();
        var xdec = xdoc.CreateXmlDeclaration("1.0", "UTF-8", null);
        xdoc.AppendChild(xdec);

        XmlElement objRootNode, objMethodNode, objParamsNode, objParamNode, objValueNode, objTypeNode;
        XmlText xt;

        objRootNode = xdoc.CreateElement(string.Empty, "methodCall", string.Empty);
        xdoc.AppendChild(objRootNode);

        objMethodNode = xdoc.CreateElement(string.Empty, "methodName", string.Empty);
        xt = xdoc.CreateTextNode("metaWeblog.getRecentPosts");
        objMethodNode.AppendChild(xt);
        objRootNode.AppendChild(objMethodNode);

        objParamsNode = xdoc.CreateElement(string.Empty, "params", string.Empty);

        objParamNode = xdoc.CreateElement(string.Empty, "param", string.Empty);
        objParamsNode.AppendChild(objParamNode);

        objValueNode = xdoc.CreateElement(string.Empty, "value", string.Empty);
        objParamNode.AppendChild(objValueNode);

        objTypeNode = xdoc.CreateElement(string.Empty, "string", string.Empty);
        objValueNode.AppendChild(objTypeNode);

        objParamNode = xdoc.CreateElement(string.Empty, "param", string.Empty);
        objParamsNode.AppendChild(objParamNode);

        objValueNode = xdoc.CreateElement(string.Empty, "value", string.Empty);
        objParamNode.AppendChild(objValueNode);

        objTypeNode = xdoc.CreateElement(string.Empty, "string", string.Empty);
        xt = xdoc.CreateTextNode(_userName);
        objTypeNode.AppendChild(xt);
        objValueNode.AppendChild(objTypeNode);

        objParamNode = xdoc.CreateElement(string.Empty, "param", string.Empty);
        objParamsNode.AppendChild(objParamNode);

        objValueNode = xdoc.CreateElement(string.Empty, "value", string.Empty);
        objParamNode.AppendChild(objValueNode);

        objTypeNode = xdoc.CreateElement(string.Empty, "string", string.Empty);
        xt = xdoc.CreateTextNode(_userPassword);
        objTypeNode.AppendChild(xt);
        objValueNode.AppendChild(objTypeNode);

        objRootNode.AppendChild(objParamsNode);

        //System.Diagnostics.Debug.WriteLine("GET blogs(getUsersBlogs): " + AsXml(xdoc));

        ToDebugWindow(">> HTTP Request POST "
            + entryUri.AbsoluteUri
            + Environment.NewLine
            + AsUTF16Xml(xdoc)
            + Environment.NewLine);

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = entryUri,
            Content = new StringContent(AsUTF8Xml(xdoc), Encoding.UTF8, "text/xml")
        };

        try
        {
            var response = await Client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var contenTypeString = response.Content.Headers.GetValues("Content-Type").FirstOrDefault();

                if (!contenTypeString.StartsWith("text/xml"))
                {
                    ToDebugWindow("<< HTTP Response Content-Type is invalid: " + contenTypeString
                        + Environment.NewLine
                        + "expecting " + "text/xml"
                        + Environment.NewLine);

                    InvalidContentType(res.Error, "Content-Type is invalid", "HttpResponse.Content.Headers.GetValues", "XmlRpcMTClient: GetEntries");
                    res.IsError = true;

                    return res;
                }

                var s = await response.Content.ReadAsStringAsync();

                ToDebugWindow("<< HTTP Response " + response.StatusCode.ToString()
                + Environment.NewLine
                + s
                + Environment.NewLine);

                try
                {
                    xdoc.LoadXml(s);
                }
                catch (Exception e)
                {
                    ToDebugWindow("<< Invalid XML returned:"
                        + Environment.NewLine
                        + e.Message
                        + Environment.NewLine);

                    InvalidXml(res.Error, e.Message, "XmlDocument.Load", "XmlRpcMTClient: GetEntries");
                    res.IsError = true;

                    return res;
                }

                XmlNodeList entryList;
                entryList = xdoc.SelectNodes("//methodResponse/params/param/value/array/data/value");
                if (entryList == null)
                {
                    res.Entries = list;

                    return res;
                }

                foreach (XmlNode l in entryList)
                {
                    var ent = new MTEntry("", serviceId, this);

                    FillEntryItemFromXML(ent, l, entryUri, serviceId);

                    list.Add(ent);
                }
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

            HttpReqException(res.Error, e.Message, "_HTTPConn.Client.SendAsync", "XmlRpcMTClient:GetEntries");
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

            GenericException(res.Error, "", ErrorObject.ErrTypes.HTTP, "HTTP request error (Exception)", e.Message, "_HTTPConn.Client.SendAsync", "XmlRpcMTClient:GetEntries");
            res.IsError = true;

            return res;
        }

        return res;
    }

    private void FillEntryItemFromXML(MTEntry entItem, XmlNode entryNode, Uri xmlrpcUri, string serviceId)
    {

        var entry = CreateMTEntryFromXML(entryNode, serviceId);
        if (entry == null)
        {
            return;
        }

        // multisite has independent endpoint. So we set it here.
        entry.EditUri = xmlrpcUri;
        entry.PostUri = xmlrpcUri;

        entItem.Name = entry.Name;
        //entItem.ID = entry.ID;
        entItem.EntryId = entry.EntryId;
        entItem.EditUri = entry.EditUri;
        entItem.PostUri = entry.PostUri;
        entItem.AltHtmlUri = entry.AltHtmlUri;
        entItem.EntryBody = entry;

        entItem.Status = entry.Status;

    }

    public async  override Task<EntryFull> GetFullEntry(Uri entryUri, string serviceId, string postid) 
    {
        if (string.IsNullOrEmpty(postid))
        {
            throw new InvalidOperationException("XML-RPC requires postid");
        }

        var xdoc = new XmlDocument();
        var xdec = xdoc.CreateXmlDeclaration("1.0", "UTF-8", null);
        xdoc.AppendChild(xdec);

        XmlElement objRootNode, objMethodNode, objParamsNode, objParamNode, objValueNode, objTypeNode;
        XmlText xt;

        objRootNode = xdoc.CreateElement(string.Empty, "methodCall", string.Empty);
        xdoc.AppendChild(objRootNode);

        objMethodNode = xdoc.CreateElement(string.Empty, "methodName", string.Empty);
        xt = xdoc.CreateTextNode("metaWeblog.getPost");
        objMethodNode.AppendChild(xt);
        objRootNode.AppendChild(objMethodNode);

        objParamsNode = xdoc.CreateElement(string.Empty, "params", string.Empty);

        objParamNode = xdoc.CreateElement(string.Empty, "param", string.Empty);
        objParamsNode.AppendChild(objParamNode);

        objValueNode = xdoc.CreateElement(string.Empty, "value", string.Empty);
        objParamNode.AppendChild(objValueNode);

        objTypeNode = xdoc.CreateElement(string.Empty, "int", string.Empty);
        xt = xdoc.CreateTextNode(postid);
        objTypeNode.AppendChild(xt);
        objValueNode.AppendChild(objTypeNode);

        objParamNode = xdoc.CreateElement(string.Empty, "param", string.Empty);
        objParamsNode.AppendChild(objParamNode);

        objValueNode = xdoc.CreateElement(string.Empty, "value", string.Empty);
        objParamNode.AppendChild(objValueNode);

        objTypeNode = xdoc.CreateElement(string.Empty, "string", string.Empty);
        xt = xdoc.CreateTextNode(_userName);
        objTypeNode.AppendChild(xt);
        objValueNode.AppendChild(objTypeNode);

        objParamNode = xdoc.CreateElement(string.Empty, "param", string.Empty);
        objParamsNode.AppendChild(objParamNode);

        objValueNode = xdoc.CreateElement(string.Empty, "value", string.Empty);
        objParamNode.AppendChild(objValueNode);

        objTypeNode = xdoc.CreateElement(string.Empty, "string", string.Empty);
        xt = xdoc.CreateTextNode(_userPassword);
        objTypeNode.AppendChild(xt);
        objValueNode.AppendChild(objTypeNode);

        objRootNode.AppendChild(objParamsNode);

        //System.Diagnostics.Debug.WriteLine("GET GetFullEntry: " + AsXml(xdoc));

        ToDebugWindow(">> HTTP Request POST "
            //+ Environment.NewLine
            + entryUri.AbsoluteUri
            + Environment.NewLine
            + AsUTF16Xml(xdoc)
            + Environment.NewLine);

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = entryUri,
            Content = new StringContent(AsUTF8Xml(xdoc), Encoding.UTF8, "text/xml")
        };

        var response = await Client.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            var contenTypeString = response.Content.Headers.GetValues("Content-Type").FirstOrDefault();

            if (!contenTypeString.StartsWith("text/xml"))
            {
                System.Diagnostics.Debug.WriteLine("Content-Type is invalid: " + contenTypeString);

                ToDebugWindow("<< HTTP Response Content-Type is invalid: " + contenTypeString
                    + Environment.NewLine
                    + "expecting " + "text/xml"
                    + Environment.NewLine);

                return null;
            }

            var s = await response.Content.ReadAsStringAsync();

            ToDebugWindow("<< HTTP Response " + response.StatusCode.ToString()
            + Environment.NewLine
            + s
            + Environment.NewLine);

            //System.Diagnostics.Debug.WriteLine("GetFullEntry response: " + s);

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

        }

        var entryNode = xdoc.SelectSingleNode("//methodResponse/params/param/value");
        if (entryNode == null)
        {
            return null;
        }

        var entry = CreateMTEntryFromXML(entryNode, serviceId);

        return entry;
    }

    private MTEntry? CreateMTEntryFromXML(XmlNode entryNode, string serviceId)
    {

        var memberList = entryNode.SelectNodes("struct/member");
        if (memberList == null)
        {
            return null;
        }

        Uri? url = null;
        var postid = "";
        var title = "";
        var description = "";

        foreach (XmlNode m in memberList)
        {
            var valueList = m.ChildNodes;
            var name = "";
            var value = "";
            foreach (XmlNode v in valueList)
            {
                if (v.Name == "name")
                {
                    name = v.InnerText;
                }
                if (v.Name == "value")
                {
                    value = v.InnerText;
                }
            }

            //System.Diagnostics.Debug.WriteLine("name: " + name + " - value: " + value);

            if (name == "postid")
            {
                postid = value;
            }

            if (name == "title")
            {
                title = value;
            }

            if (name == "link")
            {
                try
                {
                    url = new Uri(value);
                }
                catch { }
            }
            if (name == "permaLink")
            {
                try
                {
                    url = new Uri(value);
                }
                catch { }
            }

            if (name == "description")
            {
                description = value;
            }


            //TODO:

            //categories
            /*
  <member>
    <name>categories</name>
    <value>
      <array>
        <data>
          <value><string>Blogroll</string></value>
          <value><string>Uncategorized</string></value>
         */

            /*
name: postid - value: 50
name: description - value: hogehoge
name: title - value: test2
name: link - value: http://hoge.jp/en/blog/2018/04/06/test2/
name: permaLink - value: http://hoge.jp/en/blog/2018/04/06/test2/
       
name: categories - value: BlogrollUncategorized 
        
name: dateCreated - value: 20180406T05:17:44
name: date_modified - value: 20180406T05:17:44
name: date_modified_gmt - value: 20180406T05:17:44

name: mt_allow_comments - value: 0
name: mt_allow_pings - value: 1
name: mt_keywords - value: 
name: mt_excerpt - value: 
name: mt_text_more - value: 
name: wp_more_text - value: 

name: userid - value: 1

name: wp_slug - value: test2
name: wp_password - value: 
name: wp_author_id - value: 1
name: wp_author_display_name - value: torum
name: date_created_gmt - value: 20180406T05:17:44
name: post_status - value: publish
name: custom_fields - value: id67key_edit_lastvalue1id66key_edit_lockvalue1522991724:1
name: wp_post_format - value: standard

name: sticky - value: 0
name: wp_post_thumbnail - value: 
         
         */


        }

        var entry = new MTEntry(title, serviceId, this)
        {
            AltHtmlUri = url,
            EntryId = postid,
            //entry.EditUri = _endpoint; No, don't. Multisite has multiple endpoints for each blog.

            Content = description,

            //TODO: MT doesn't have this flag? need to check.
            IsDraft = false,
            Status = EditEntryItem.EditStatus.esNormal
        };

        return entry;
    }

    public async override Task<bool> UpdateEntry(EntryFull entry)
    {
        var xdoc = new XmlDocument();
        var xdec = xdoc.CreateXmlDeclaration("1.0", "UTF-8", null);
        xdoc.AppendChild(xdec);

        XmlElement objRootNode, objMethodNode, objParamsNode, objParamNode, objValueNode, objTypeNode;
        XmlText xt;

        objRootNode = xdoc.CreateElement(string.Empty, "methodCall", string.Empty);
        xdoc.AppendChild(objRootNode);

        objMethodNode = xdoc.CreateElement(string.Empty, "methodName", string.Empty);
        xt = xdoc.CreateTextNode("metaWeblog.editPost");
        objMethodNode.AppendChild(xt);
        objRootNode.AppendChild(objMethodNode);

        objParamsNode = xdoc.CreateElement(string.Empty, "params", string.Empty);

        objParamNode = xdoc.CreateElement(string.Empty, "param", string.Empty);
        objParamsNode.AppendChild(objParamNode);

        objValueNode = xdoc.CreateElement(string.Empty, "value", string.Empty);
        objParamNode.AppendChild(objValueNode);

        objTypeNode = xdoc.CreateElement(string.Empty, "int", string.Empty);
        xt = xdoc.CreateTextNode(entry.EntryId);
        objTypeNode.AppendChild(xt);
        objValueNode.AppendChild(objTypeNode);

        objParamNode = xdoc.CreateElement(string.Empty, "param", string.Empty);
        objParamsNode.AppendChild(objParamNode);

        objValueNode = xdoc.CreateElement(string.Empty, "value", string.Empty);
        objParamNode.AppendChild(objValueNode);

        objTypeNode = xdoc.CreateElement(string.Empty, "string", string.Empty);
        xt = xdoc.CreateTextNode(_userName);
        objTypeNode.AppendChild(xt);
        objValueNode.AppendChild(objTypeNode);

        objParamNode = xdoc.CreateElement(string.Empty, "param", string.Empty);
        objParamsNode.AppendChild(objParamNode);

        objValueNode = xdoc.CreateElement(string.Empty, "value", string.Empty);
        objParamNode.AppendChild(objValueNode);

        objTypeNode = xdoc.CreateElement(string.Empty, "string", string.Empty);
        xt = xdoc.CreateTextNode(_userPassword);
        objTypeNode.AppendChild(xt);
        objValueNode.AppendChild(objTypeNode);

        //
        var structNode = xdoc.CreateElement(string.Empty, "struct", string.Empty);
        var memberNode = xdoc.CreateElement(string.Empty, "member", string.Empty);

        var nameNode = xdoc.CreateElement(string.Empty, "name", string.Empty);
        //XmlText ntn = xdoc.CreateTextNode();
        var valueNode = xdoc.CreateElement(string.Empty, "value", string.Empty);
        var stringNode = xdoc.CreateElement(string.Empty, "string", string.Empty);
        //XmlText vtn = xdoc.CreateTextNode();

        //struct/member/name
        //struct/member/value
        //struct/member/value/string

        objParamNode = xdoc.CreateElement(string.Empty, "param", string.Empty);
        objParamsNode.AppendChild(objParamNode);

        objValueNode = xdoc.CreateElement(string.Empty, "value", string.Empty);
        objParamNode.AppendChild(objValueNode);

        objTypeNode = xdoc.CreateElement(string.Empty, "bool", string.Empty);
        var isPublish = entry.IsDraft ? "0" : "1";
        xt = xdoc.CreateTextNode(isPublish);
        objTypeNode.AppendChild(xt);
        objValueNode.AppendChild(objTypeNode);

        objRootNode.AppendChild(objParamsNode);

        System.Diagnostics.Debug.WriteLine("metaWeblog.editPost: " + AsUTF16Xml(xdoc));

        ToDebugWindow(">> HTTP Request POST "
            //+ Environment.NewLine
            + entry.EditUri.AbsoluteUri
            + Environment.NewLine
            + AsUTF16Xml(xdoc)
            + Environment.NewLine);

        //return true;

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = entry.EditUri,
            Content = new StringContent(AsUTF8Xml(xdoc), Encoding.UTF8, "text/xml")
        };

        var response = await Client.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            var contenTypeString = response.Content.Headers.GetValues("Content-Type").FirstOrDefault();

            if (!contenTypeString.StartsWith("text/xml"))
            {
                System.Diagnostics.Debug.WriteLine("Content-Type is invalid: " + contenTypeString);

                ToDebugWindow("<< HTTP Response Content-Type is invalid: " + contenTypeString
                    + Environment.NewLine
                    + "expecting " + "text/xml"
                    + Environment.NewLine);

                return false;
            }

            var s = await response.Content.ReadAsStringAsync();

            //System.Diagnostics.Debug.WriteLine("metaWeblog.editPost response: " + s);

            ToDebugWindow("<< HTTP Response " + response.StatusCode.ToString()
            + Environment.NewLine
            + s
            + Environment.NewLine);

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

                return false;
            }



        }

        return true;

    }

    public async override Task<bool> PostEntry(EntryFull entry)
    {
        await Task.Delay(1);

        //metaWeblog.newPost

        return true;

    }

    public async override Task<bool> DeleteEntry(Uri editUri)
    {
        await Task.Delay(1);

        //metaWeblog.deletePost

        return true;

    }
}

#region == Sample XML ==

/*
<?xml version="1.0" encoding="UTF-8"?>
<methodResponse>
  <params>
    <param>
      <value>

        <array>
          <data>

  <value>
    <struct>
      <member>
        <name>dateCreated</name>
        <value><dateTime.iso8601>20180406T05:17:44</dateTime.iso8601></value>
      </member>
      <member><name>userid</name><value><string>1</string></value></member>
      <member><name>postid</name><value><string>50</string></value></member>
      <member><name>description</name><value><string>hogehoge</string></value></member>
      <member><name>title</name><value><string>test2</string></value></member>
      <member><name>link</name><value><string>http://hoge.jp/en/blog/2018/04/06/test2/</string></value></member>
      <member><name>permaLink</name><value><string>http://hoge.jp/en/blog/2018/04/06/test2/</string></value></member>
      <member><name>categories</name>
        <value>
          <array>
            <data>
              <value><string>Blogroll</string></value>
              <value><string>Uncategorized</string></value>
            </data>
          </array>
        </value>
      </member>
      <member><name>mt_excerpt</name><value><string></string></value></member>
      <member><name>mt_text_more</name><value><string></string></value></member>
      <member><name>wp_more_text</name><value><string></string></value></member>
      <member><name>mt_allow_comments</name><value><int>0</int></value></member>
      <member><name>mt_allow_pings</name><value><int>1</int></value></member>
      <member><name>mt_keywords</name><value><string></string></value></member>
      <member><name>wp_slug</name><value><string>test2</string></value></member>
      <member><name>wp_password</name><value><string></string></value></member>
      <member><name>wp_author_id</name><value><string>1</string></value></member>
      <member><name>wp_author_display_name</name><value><string>torum</string></value></member>
      <member><name>date_created_gmt</name><value><dateTime.iso8601>20180406T05:17:44</dateTime.iso8601></value></member>
      <member><name>post_status</name><value><string>publish</string></value></member>
      <member><name>custom_fields</name>
        <value>
          <array>
            <data>
              <value>
                <struct>
                  <member><name>id</name><value><string>67</string></value></member>
                  <member><name>key</name><value><string>_edit_last</string></value></member>
                  <member><name>value</name><value><string>1</string></value></member>
                </struct>
              </value>
              <value>
                <struct>
                  <member><name>id</name><value><string>66</string></value></member>
                  <member><name>key</name><value><string>_edit_lock</string></value></member>
                  <member><name>value</name><value><string>1522991724:1</string></value></member>
                </struct>
              </value>
            </data>
          </array>
        </value>
      </member>
      <member><name>wp_post_format</name><value><string>standard</string></value></member>
      <member><name>date_modified</name><value><dateTime.iso8601>20180406T05:17:44</dateTime.iso8601></value></member>
      <member><name>date_modified_gmt</name><value><dateTime.iso8601>20180406T05:17:44</dateTime.iso8601></value></member>
      <member><name>sticky</name><value><boolean>0</boolean></value></member>
      <member><name>wp_post_thumbnail</name><value><string></string></value></member>
    </struct>
  </value>

  <value><struct>
  <member><name>dateCreated</name><value><dateTime.iso8601>20180405T02:08:25</dateTime.iso8601></value></member>
  <member><name>userid</name><value><string>1</string></value></member>
  <member><name>postid</name><value><string>47</string></value></member>
  <member><name>description</name><value><string>test body</string></value></member>
  <member><name>title</name><value><string>This is a test</string></value></member>
  <member><name>link</name><value><string>http://hoge.jp/en/blog/2018/04/05/this-is-a-test/</string></value></member>
  <member><name>permaLink</name><value><string>http://hoge.jp/en/blog/2018/04/05/this-is-a-test/</string></value></member>
  <member><name>categories</name><value><array><data>
  <value><string>Software</string></value>
  <value><string>testCat</string></value>
</data></array></value></member>
  <member><name>mt_excerpt</name><value><string></string></value></member>
  <member><name>mt_text_more</name><value><string></string></value></member>
  <member><name>wp_more_text</name><value><string></string></value></member>
  <member><name>mt_allow_comments</name><value><int>0</int></value></member>
  <member><name>mt_allow_pings</name><value><int>1</int></value></member>
  <member><name>mt_keywords</name><value><string></string></value></member>
  <member><name>wp_slug</name><value><string>this-is-a-test</string></value></member>
  <member><name>wp_password</name><value><string></string></value></member>
  <member><name>wp_author_id</name><value><string>1</string></value></member>
  <member><name>wp_author_display_name</name><value><string>torum</string></value></member>
  <member><name>date_created_gmt</name><value><dateTime.iso8601>20180405T02:08:25</dateTime.iso8601></value></member>
  <member><name>post_status</name><value><string>publish</string></value></member>
  <member><name>custom_fields</name><value><array><data>
  <value><struct>
  <member><name>id</name><value><string>62</string></value></member>
  <member><name>key</name><value><string>_edit_last</string></value></member>
  <member><name>value</name><value><string>1</string></value></member>
</struct></value>
  <value><struct>
  <member><name>id</name><value><string>63</string></value></member>
  <member><name>key</name><value><string>_edit_lock</string></value></member>
  <member><name>value</name><value><string>1522893995:1</string></value></member>
</struct></value>
</data></array></value></member>
  <member><name>wp_post_format</name><value><string>standard</string></value></member>
  <member><name>date_modified</name><value><dateTime.iso8601>20180405T02:08:25</dateTime.iso8601></value></member>
  <member><name>date_modified_gmt</name><value><dateTime.iso8601>20180405T02:08:25</dateTime.iso8601></value></member>
  <member><name>sticky</name><value><boolean>0</boolean></value></member>
  <member><name>wp_post_thumbnail</name><value><string></string></value></member>
</struct></value>

          </data>
        </array>

      </value>
    </param>
  </params>
</methodResponse>


*/

#endregion








