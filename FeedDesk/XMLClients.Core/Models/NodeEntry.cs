using System;
using System.Text;
using System.Xml;
using XmlClients.Core.Helpers;
using XmlClients.Core.Models.Clients;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using static XmlClients.Core.Helpers.HtmlProperties;

namespace XmlClients.Core.Models;

// Entry class for listview 
public abstract class EntryItem : Node
{
    // System-wide unique id used for file name or unique id for table in db. 
    public string Id =>
            // Please don't change this.
            ServiceId + ":" + EntryId;

    // Service' ID need this for system-wide unique Entry Id.
    public string ServiceId { get; protected set; }

    // Entry' ID provided by services. In XML-RPC, this is the "postid" 
    public string? EntryId { get; set; }

    // Pointer to the NodeEntryCollection. Used in an Editor window to post entry etc.
    public NodeEntryCollection? NodeEntry { get; set; }

    // A link to Entry's HTML webpage.
    public Uri? AltHtmlUri { get; set; }

    private string _pathIcon = "M12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z";
    public string PathIcon
    {
        get => _pathIcon;
        set
        {
            if (_pathIcon == value)
                return;

            _pathIcon = value;
            NotifyPropertyChanged(nameof(PathIcon));
        }
    }

    public string Title
    {
        get => Name;
        set
        {
            if (Name == value)
                return;

            Name = value;
            NotifyPropertyChanged(nameof(Title));
        }
    }

    private string _summary = "";
    public string Summary
    {
        get => _summary;
        set
        {
            if (_summary == value)
                return;

            _summary = value;
            NotifyPropertyChanged(nameof(Summary));
            NotifyPropertyChanged(nameof(SummaryPlainText));
        }
    }

    public string SummaryPlainText => Windows.Data.Html.HtmlUtilities.ConvertToText(_summary);

    public enum ContentTypes
    {
        none,
        text,
        textHtml,
        markdown,
        hatena,
        unknown

        //type="text/html"
        //type="text/x-hatena-syntax"
        //type = "text/x-markdown"
    }

    public ContentTypes ContentType { get; set; }

    protected string _content = "";
    public string Content
    {
        get => _content;
        set
        {
            if (value == _content)
                return;

            _content = value;
            /*
            if ((_content != null) && (ContentBaseUri != null))
            {
                ContentHtmlWithBaseUri = new HtmlWithBaseUri(_content, ContentBaseUri);
            }
            */

            NotifyPropertyChanged(nameof(Content));
        }
    }

    // TODO:
    public Uri? ContentBaseUri
    {
        get; set;
    }

    /*
    // TODO:
    public HtmlWithBaseUri? ContentHtmlWithBaseUri
    {
        get; set; 
    }
    */

    // UTC
    private DateTime _published = default;
    public DateTime Published
    {
        get => _published;
        set
        {
            if (_published == value)
                return;

            _published = value;
            NotifyPropertyChanged(nameof(Published));
        }
    }

    public string PublishedDateTimeFormatedAbout
    {
        get
        {
            if (_published != default)
            {
                return TimeAgo(_published.ToLocalTime());//_published.ToString(System.Globalization.CultureInfo.CurrentUICulture);
            }
            else
            {
                if (_updated != default)
                {
                    return TimeAgo(_updated.ToLocalTime());//_published.ToString(System.Globalization.CultureInfo.CurrentUICulture);
                }
                else
                {
                    return "-";
                }
            }
        }
    }

    public string PublishedDateTimeFormated
    {
        get
        {
            if (_published != default)
            {
                return _published.ToLocalTime().ToString(System.Globalization.CultureInfo.CurrentUICulture);
            }
            else
            {
                if (_updated != default)
                {
                    return _updated.ToLocalTime().ToString(System.Globalization.CultureInfo.CurrentUICulture);
                }
                else
                {
                    return "-";
                }
            }
        }
    }

    // UTC
    private DateTime _updated = default;
    public DateTime Updated
    {
        get => _updated;
        set
        {
            if (_updated == value)
                return;

            _updated = value;
            NotifyPropertyChanged(nameof(Updated));
        }
    }

    private string _author = "";
    public string Author
    {
        get
        {
            if (string.IsNullOrEmpty(_author))
            {
                return "";
            }
            else
            {
                return _author;
            }
        }
        set
        {
            if (_author == value)
                return;

            _author = value;
            NotifyPropertyChanged(nameof(Author));
        }
    }

    private string _category = "";
    public string Category
    {
        get
        {
            if (string.IsNullOrEmpty(_category))
            {
                return "";
            }
            else
            {
                return _category;
            }
        }
        set
        {
            if (_category == value)
                return;

            _category = value;
            NotifyPropertyChanged(nameof(Category));
        }
    }

    private Uri? _imageUri;
    public Uri? ImageUri
    {
        get => _imageUri;
        set
        {
            if (_imageUri == value)
                return;

            _imageUri = value;
            NotifyPropertyChanged(nameof(ImageUri));
        }
    }

    private Uri? _audioUri;
    public Uri? AudioUri
    {
        get => _audioUri;
        set
        {
            if (_audioUri == value)
                return;

            _audioUri = value;
            NotifyPropertyChanged(nameof(AudioUri));
        }
    }

    // TODO: stil using?
    // For FeedEntryItem, "IsArchived" is used so far.
    private string _commonStatus = "";
    public string CommonStatus
    {
        get => _commonStatus;
        protected set
        {
            if (_commonStatus == value)
                return;
            _commonStatus = value;
            NotifyPropertyChanged(nameof(CommonStatus));
        }
    }

    // rss item source@url (news source site info)
    private string _source = "";
    public string Source
    {
        get => _source;
        set
        {
            if (_source == value)
                return;

            _source = value;
            NotifyPropertyChanged(nameof(Source));
        }
    }

    private Uri? _sourceUri;
    public Uri? SourceUri
    {
        get => _sourceUri;
        set
        {
            if (_sourceUri == value)
                return;

            _sourceUri = value;
            NotifyPropertyChanged(nameof(SourceUri));
        }
    }


    // comment uri for hatena and hacker news.
    private Uri? _commentUri;
    public Uri? CommentUri
    {
        get => _commentUri;
        set
        {
            if (_commentUri == value)
                return;

            _commentUri = value;
            NotifyPropertyChanged(nameof(CommentUri));
        }
    }

    /*
    private bool _isImageDownloaded = false;
    public bool IsImageDownloaded
    {
        get => _isImageDownloaded;
        set
        {
            if (_isImageDownloaded == value)
                return;
            _isImageDownloaded = value;
            NotifyPropertyChanged(nameof(IsImageDownloaded));
        }
    }
    */

    /*
    private string _imageId = "";
    public string ImageId
    {
        get => _imageId;
        set
        {
            if (_imageId == value)
                return;
            _imageId = value;
            NotifyPropertyChanged(nameof(ImageId));
        }
    }
    */

    /*
    private ImageSource _image;
    public ImageSource Image
    {
        get => _image;
        set
        {
            if (_image == value)
                return;

            _image = value;
            NotifyPropertyChanged(nameof(Image));
        }
    }
    */

    /*
    private byte[] _imageByteArray = Array.Empty<byte>();
    public byte[] ImageByteArray
    {
        get => _imageByteArray;
        set
        {
            if (_imageByteArray == value)
                return;

            _imageByteArray = value;
            NotifyPropertyChanged(nameof(ImageByteArray));
        }
    }
    */

    public BaseClient? Client { get; } = null;

    public EntryItem(string title, string serviceId, BaseClient? bc) : base(title)
    {
        Client = bc;
        ServiceId = serviceId;
        ContentType = ContentTypes.none;
    }

    public static string TimeAgo(DateTime dateTime)
    {
        string result;
        var timeSpan = DateTime.Now.Subtract(dateTime);

        if (timeSpan <= TimeSpan.FromSeconds(60))
        {
            //result = string.Format("{0}s", timeSpan.Seconds);
            result = $"{timeSpan.Minutes}{"FeedEntryItem_Published_Second".GetLocalized()}";
        }
        else if (timeSpan <= TimeSpan.FromMinutes(60))
        {
            //result = timeSpan.Minutes > 1 ?　String.Format("{0}min", timeSpan.Minutes) :　"1min";//"about a minute ago";
            result = $"{timeSpan.Minutes}{"FeedEntryItem_Published_Minute".GetLocalized()}";
        }
        else if (timeSpan <= TimeSpan.FromHours(24))
        {
            //result = timeSpan.Hours > 1 ? String.Format("{0}h", timeSpan.Hours) : "1h";//"about an hour ago";
            result = $"{timeSpan.Hours}{"FeedEntryItem_Published_Hour".GetLocalized()}";
        }
        else if (timeSpan <= TimeSpan.FromDays(30))
        {
            //result = timeSpan.Days > 1 ?　String.Format("{0}d", timeSpan.Days) :　"1d";//"yesterday";
            result = $"{timeSpan.Days}{"FeedEntryItem_Published_Day".GetLocalized()}";
        }
        else if (timeSpan <= TimeSpan.FromDays(365))
        {
            //result = timeSpan.Days > 30 ?　String.Format("{0}mo", timeSpan.Days / 30) : "1mo";//"about a month ago";
            result = timeSpan.Days > 30 ? $"{timeSpan.Days / 30}{"FeedEntryItem_Published_Month".GetLocalized()}" : $"1{"FeedEntryItem_Published_Month".GetLocalized()}";
        }
        else
        {
            //result = timeSpan.Days > 365 ?　String.Format("{0}y", timeSpan.Days / 365) :　"1y";//"about a year ago";
            result = timeSpan.Days > 365 ? $"{timeSpan.Days / 365}{"FeedEntryItem_Published_Year".GetLocalized()}" : $"1{"FeedEntryItem_Published_Year".GetLocalized()}";
        }

        return result;
    }
}

// Feed Entry Item
public class FeedEntryItem : EntryItem
{
    // Icon Path
    private static readonly string _rsNew = "M12 5C15.87 5 19 8.13 19 12C19 15.87 15.87 19 12 19C8.13 19 5 15.87 5 12C5 8.13 8.13 5 12 5M12 2C17.5 2 22 6.5 22 12C22 17.5 17.5 22 12 22C6.5 22 2 17.5 2 12C2 6.5 6.5 2 12 2M12 4C7.58 4 4 7.58 4 12C4 16.42 7.58 20 12 20C16.42 20 20 16.42 20 12C20 7.58 16.42 4 12 4Z";
    private static readonly string _rsNewVisited = "M10,17L5,12L6.41,10.58L10,14.17L17.59,6.58L19,8M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z";
    private static readonly string _rsNormal = "M12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z";
    private static readonly string _rsNormalVisited = "M12 2C6.5 2 2 6.5 2 12S6.5 22 12 22 22 17.5 22 12 17.5 2 12 2M12 20C7.59 20 4 16.41 4 12S7.59 4 12 4 20 7.59 20 12 16.41 20 12 20M16.59 7.58L10 14.17L7.41 11.59L6 13L10 17L18 9L16.59 7.58Z";

    // internal read state
    public enum ReadStatus
    {
        rsNew,
        rsNewVisited,
        rsNormal,
        rsNormalVisited
    }

    private ReadStatus _rs = ReadStatus.rsNormal;
    public ReadStatus Status
    {
        get => _rs;
        set
        {
            if (_rs == value)
                return;
            _rs = value;
            NotifyPropertyChanged(nameof(Status));

            PathIcon = _rs switch
            {
                ReadStatus.rsNew => _rsNew,
                ReadStatus.rsNewVisited => _rsNewVisited,
                ReadStatus.rsNormal => _rsNormal,
                ReadStatus.rsNormalVisited => _rsNormalVisited,
                _ => _rsNormal,
            };
        }
    }

    // internal flag
    private bool _isArchived = false;
    public bool IsArchived
    {
        get => _isArchived;
        set
        {
            if (_isArchived == value)
                return;
            _isArchived = value;
            NotifyPropertyChanged(nameof(IsArchived));

            if (IsArchived)
                CommonStatus = "IsArchived"; //?

        }
    }

    // not in spec. for internal/UI use.
    //private string _publisher = "";
    public string Publisher
    {
        get
        {
            if (string.IsNullOrEmpty(Source))
            {
                return _feedTitle;
            }
            else
            {
                return Source + " via " + _feedTitle;
                // not gonna work..
                //return _source + " " + "FeedEntryItem_Publisher_Via".GetLocalized() + " " + _feedTitle;
                //return $"{Source} {"FeedEntryItem_Publisher_Via".GetLocalized()} {_feedTitle}";
            }
        }
    }

    private string _feedTitle = "";
    public string FeedTitle
    {
        get => _feedTitle;
        set
        {
            if (_feedTitle == value)
                return;
            _feedTitle = value;
            NotifyPropertyChanged(nameof(FeedTitle));
        }
    }

    public string Host
    {
        get
        {
            if (AltHtmlUri != null)
            {
                return AltHtmlUri.Host;
            }
            else
            {
                return "-";
            }
        }
    }

    public FeedEntryItem(string title, string serviceId, BaseClient? bc) : base(title, serviceId, bc)
    {
        PathIcon = _rsNew;

        Status = ReadStatus.rsNew;

    }

    public ReadStatus StatusTextToType(string status)
    {
        if (status == ReadStatus.rsNew.ToString())
        {
            return ReadStatus.rsNew;
        }
        else if (status == ReadStatus.rsNormal.ToString())
        {
            return ReadStatus.rsNormal;
        }
        else if (status == ReadStatus.rsNewVisited.ToString())
        {
            return ReadStatus.rsNewVisited;
        }
        else if (status == ReadStatus.rsNormalVisited.ToString())
        {
            return ReadStatus.rsNormalVisited;
        }
        else
        {
            return ReadStatus.rsNew;
        }
    }
}

// Edit Entry Item
public class EditEntryItem : EntryItem
{
    // Icon Path
    private static readonly string _esNew = "M13,9V3.5L18.5,9M6,2C4.89,2 4,2.89 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2H6Z";
    private static readonly string _esDraft = "M6,2C4.89,2 4,2.9 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2H6M13,3.5L18.5,9H13V3.5M12,11A3,3 0 0,1 15,14V15H16V19H8V15H9V14C9,12.36 10.34,11 12,11M12,13A1,1 0 0,0 11,14V15H13V14C13,13.47 12.55,13 12,13Z";
    private static readonly string _esNormal = "M13,9H18.5L13,3.5V9M6,2H14L20,8V20A2,2 0 0,1 18,22H6C4.89,22 4,21.1 4,20V4C4,2.89 4.89,2 6,2M15,18V16H6V18H15M18,14V12H6V14H18Z";
    private static readonly string _esUpdating = "M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M12,18C9.95,18 8.19,16.76 7.42,15H9.13C9.76,15.9 10.81,16.5 12,16.5A3.5,3.5 0 0,0 15.5,13A3.5,3.5 0 0,0 12,9.5C10.65,9.5 9.5,10.28 8.9,11.4L10.5,13H6.5V9L7.8,10.3C8.69,8.92 10.23,8 12,8A5,5 0 0,1 17,13A5,5 0 0,1 12,18Z";
    private static readonly string _esQueueUpdate = "M14,2H6C4.89,2 4,2.89 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M12.54,19.37V17.37H8.54V15.38H12.54V13.38L15.54,16.38L12.54,19.37M13,9V3.5L18.5,9H13Z";
    private static readonly string _esQueuePost = "M13,9H18.5L13,3.5V9M6,2H14L20,8V20A2,2 0 0,1 18,22H6C4.89,22 4,21.1 4,20V4C4,2.89 4.89,2 6,2M11,15V12H9V15H6V17H9V20H11V17H14V15H11Z";

    // IsDraft flag. AtomPub and XML-PRC WP. MP doesn't have this?
    public bool IsDraft { get; set; }

    public enum EditStatus
    {
        esNew,
        esDraft,
        esNormal,
        esUpdating,
        esQueueUpdate,
        esQueuePost
    }

    // EditStatus. This is system's internal status.
    private EditStatus _es;
    public EditStatus Status
    {
        get => _es;
        set
        {
            if (_es == value)
                return;
            _es = value;

            // Update icon.
            NotifyPropertyChanged(nameof(Status));

            switch (_es)
            {
                case EditStatus.esNew:
                    PathIcon = _esNew;
                    break;
                case EditStatus.esDraft:
                    PathIcon = _esDraft;
                    break;
                case EditStatus.esNormal:
                    PathIcon = _esNormal;
                    break;
                case EditStatus.esUpdating:
                    PathIcon = _esUpdating;
                    break;
                case EditStatus.esQueueUpdate:
                    PathIcon = _esQueueUpdate;
                    break;
                case EditStatus.esQueuePost:
                    PathIcon = _esQueuePost;
                    break;
                default:
                    PathIcon = _esNew;
                    break;
            }
        }
    }

    public EditEntryItem(string title, string serviceId, BaseClient? bc) : base(title, serviceId, bc)
    {
        Status = EditStatus.esNew;
    }
}

// Base class for a Blog Entry class, which represents the whole Entry.
public abstract class EntryFull : EditEntryItem
{
    // Entry's PostUri. In XML-RPC, this is xmlrpcUri same as EditUri.
    public Uri? PostUri { get; set; }

    // Entry's EditUri. In XML-RPC, this is xmlrpcUri.
    public Uri? EditUri { get; set; }

    public EntryFull? EntryBody { get; set; }

    protected EntryFull(string title, string serviceId, BaseClient? bc) : base(title, serviceId, bc)
    {

    }
}

// Atom Blog Entry class, which represents Atom Entry.
public class AtomEntry : EntryFull
{
    public string? ContentTypeString { get; set; }
    public string? ETag { get; set; }

    public AtomEntry(string title, string serviceId, BaseClient? bc) : base(title, serviceId, bc)
    {

    }

    public XmlDocument AsXmlDoc()
    {
        var xdoc = new XmlDocument();
        var xdec = xdoc.CreateXmlDeclaration("1.0", "UTF-8", null);
        xdoc.AppendChild(xdec);
        /*
			<entry xmlns="http://www.w3.org/2005/Atom" xmlns:app="http://www.w3.org/2007/app">
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

        var atomNsMgr = new XmlNamespaceManager(xdoc.NameTable);
        atomNsMgr.AddNamespace("atom", "http://www.w3.org/2005/Atom");
        atomNsMgr.AddNamespace("app", "http://www.w3.org/2007/app");

        var rootNode = xdoc.CreateElement(string.Empty, "entry", "http://www.w3.org/2005/Atom");
        //rootNode.SetAttribute("xmlns", "http://www.w3.org/2005/Atom");
        rootNode.SetAttribute("xmlns:app", "http://www.w3.org/2007/app");
        xdoc.AppendChild(rootNode);

        var idNode = xdoc.CreateElement(string.Empty, "id", "http://www.w3.org/2005/Atom");
        var idTextNode = xdoc.CreateTextNode(this.EntryId);
        idNode.AppendChild(idTextNode);
        rootNode.AppendChild(idNode);

        //link rel
        //link rel
        //author/name
        //updated
        //published
        //app:edited
        //summary

        //category

        var titleNode = xdoc.CreateElement(string.Empty, "title", "http://www.w3.org/2005/Atom");
        var titleTextNode = xdoc.CreateTextNode(this.Name);
        titleNode.AppendChild(titleTextNode);
        rootNode.AppendChild(titleNode);

        var contentNode = xdoc.CreateElement(string.Empty, "content", "http://www.w3.org/2005/Atom");
        contentNode.SetAttribute("type", this.ContentTypeString);
        var contentTextNode = xdoc.CreateTextNode(this.Content);
        contentNode.AppendChild(contentTextNode);
        rootNode.AppendChild(contentNode);

        var controlNode = xdoc.CreateElement("app", "control", "http://www.w3.org/2007/app");
        var draftNode = xdoc.CreateElement("app", "draft", "http://www.w3.org/2007/app");
        var yesOrNo = this.IsDraft ? "yes" : "no";
        var draftTextNode = xdoc.CreateTextNode(yesOrNo);
        draftNode.AppendChild(draftTextNode);
        controlNode.AppendChild(draftNode);
        rootNode.AppendChild(controlNode);

        return xdoc;

    }

    public string AsUTF8Xml()
    {
        var sb = new StringBuilder();
        using var stringWriter = new StringWriterWithEncoding(sb, Encoding.UTF8);
        using var xmlTextWriter = XmlWriter.Create(stringWriter);
        XmlDocument xdoc = AsXmlDoc();

        xdoc.WriteTo(xmlTextWriter);
        xmlTextWriter.Flush();
        return stringWriter.GetStringBuilder().ToString();
    }

    public string AsUTF16Xml()
    {
        using var stringWriter = new System.IO.StringWriter();
        using var xmlTextWriter = XmlWriter.Create(stringWriter);
        var xdoc = AsXmlDoc();

        xdoc.WriteTo(xmlTextWriter);
        xmlTextWriter.Flush();
        return stringWriter.GetStringBuilder().ToString();
    }

}

// Hatena Blog Atom Entry class.
public class AtomEntryHatena : AtomEntry
{
    protected string _formattedContent = "";

    public string FormattedContent
    {
        get => _formattedContent;
        set
        {
            if (value == _formattedContent)
                return;

            _formattedContent = value;

            NotifyPropertyChanged(nameof(FormattedContent));
        }
    }

    public AtomEntryHatena(string title, string serviceId, BlogClient bc) : base(title, serviceId, bc)
    {

    }

    //public XmlDocument AsXmlDoc(){ }

    //public string AsXml(){}

}

public class MTEntry : EntryFull
{
    public MTEntry(string title, string serviceId, BaseClient bc) : base(title, serviceId, bc)
    {
        //
    }
}

public class WPEntry : EntryFull
{
    public WPEntry(string title, string serviceId, BaseClient bc) : base(title, serviceId, bc)
    {
        //
    }
}

// StringWriter With Encoding.
public class StringWriterWithEncoding : StringWriter
{
    public StringWriterWithEncoding(StringBuilder sb, Encoding encoding)
        : base(sb)
    {
        this.m_Encoding = encoding;
    }
    private readonly Encoding m_Encoding;
    public override Encoding Encoding => this.m_Encoding;
}
