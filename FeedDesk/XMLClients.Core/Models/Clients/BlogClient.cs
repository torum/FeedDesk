using System.Text;
using System.Xml;

namespace XmlClients.Core.Models.Clients;

// Base HTTP Blog client 
public abstract class BlogClient : BaseClient
{
    protected string _userName = "";
    protected string _userPassword = "";
    protected Uri _endpoint;

    public BlogClient(string userName, string userPassword, Uri endpoint)
    {
        _userName = userName;
        _userPassword = userPassword;
        _endpoint = endpoint;
    }

    public abstract Task<NodeService> GetAccount(string accountName);

    public abstract Task<List<NodeWorkspace>> GetBlogs();

    public abstract Task<EntryFull> GetFullEntry(Uri entryUri, string serviceId, string postid = "");

    public abstract Task<bool> UpdateEntry(EntryFull entry);

    public abstract Task<bool> PostEntry(EntryFull entry);

    public abstract Task<bool> DeleteEntry(Uri editUri);

    public static string AsUTF8Xml(XmlDocument xdoc)
    {
        var sb = new StringBuilder();
        using var stringWriter = new StringWriterWithEncoding(sb, Encoding.UTF8);
        using var xmlTextWriter = XmlWriter.Create(stringWriter);
        xdoc.WriteTo(xmlTextWriter);
        xmlTextWriter.Flush();
        return stringWriter.GetStringBuilder().ToString();
    }

    public static string AsUTF16Xml(XmlDocument xdoc)
    {
        using var stringWriter = new System.IO.StringWriter();
        using var xmlTextWriter = XmlWriter.Create(stringWriter);
        xdoc.WriteTo(xmlTextWriter);
        xmlTextWriter.Flush();
        return stringWriter.GetStringBuilder().ToString();
    }
}
