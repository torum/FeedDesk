using FeedDesk.Common;
using FeedDesk.Services;
using System.Collections.Generic;

namespace FeedDesk.Models;

public abstract class LinkItem : ViewModelBase
{
    public Dictionary<string, string> IconPathStrings { get; set; } = new()
    {
        { "AtomFeed", "M6.18,15.64A2.18,2.18 0 0,1 8.36,17.82C8.36,19 7.38,20 6.18,20C5,20 4,19 4,17.82A2.18,2.18 0 0,1 6.18,15.64M4,4.44A15.56,15.56 0 0,1 19.56,20H16.73A12.73,12.73 0 0,0 4,7.27V4.44M4,10.1A9.9,9.9 0 0,1 13.9,20H11.07A7.07,7.07 0 0,0 4,12.93V10.1Z" },
        { "RssFeed", "M6.18,15.64A2.18,2.18 0 0,1 8.36,17.82C8.36,19 7.38,20 6.18,20C5,20 4,19 4,17.82A2.18,2.18 0 0,1 6.18,15.64M4,4.44A15.56,15.56 0 0,1 19.56,20H16.73A12.73,12.73 0 0,0 4,7.27V4.44M4,10.1A9.9,9.9 0 0,1 13.9,20H11.07A7.07,7.07 0 0,0 4,12.93V10.1Z" },
        { "AtomPub", "M12,11A1,1 0 0,1 13,12A1,1 0 0,1 12,13A1,1 0 0,1 11,12A1,1 0 0,1 12,11M4.22,4.22C5.65,2.79 8.75,3.43 12,5.56C15.25,3.43 18.35,2.79 19.78,4.22C21.21,5.65 20.57,8.75 18.44,12C20.57,15.25 21.21,18.35 19.78,19.78C18.35,21.21 15.25,20.57 12,18.44C8.75,20.57 5.65,21.21 4.22,19.78C2.79,18.35 3.43,15.25 5.56,12C3.43,8.75 2.79,5.65 4.22,4.22M15.54,8.46C16.15,9.08 16.71,9.71 17.23,10.34C18.61,8.21 19.11,6.38 18.36,5.64C17.62,4.89 15.79,5.39 13.66,6.77C14.29,7.29 14.92,7.85 15.54,8.46M8.46,15.54C7.85,14.92 7.29,14.29 6.77,13.66C5.39,15.79 4.89,17.62 5.64,18.36C6.38,19.11 8.21,18.61 10.34,17.23C9.71,16.71 9.08,16.15 8.46,15.54M5.64,5.64C4.89,6.38 5.39,8.21 6.77,10.34C7.29,9.71 7.85,9.08 8.46,8.46C9.08,7.85 9.71,7.29 10.34,6.77C8.21,5.39 6.38,4.89 5.64,5.64M9.88,14.12C10.58,14.82 11.3,15.46 12,16.03C12.7,15.46 13.42,14.82 14.12,14.12C14.82,13.42 15.46,12.7 16.03,12C15.46,11.3 14.82,10.58 14.12,9.88C13.42,9.18 12.7,8.54 12,7.97C11.3,8.54 10.58,9.18 9.88,9.88C9.18,10.58 8.54,11.3 7.97,12C8.54,12.7 9.18,13.42 9.88,14.12M18.36,18.36C19.11,17.62 18.61,15.79 17.23,13.66C16.71,14.29 16.15,14.92 15.54,15.54C14.92,16.15 14.29,16.71 13.66,17.23C15.79,18.61 17.62,19.11 18.36,18.36Z" },
        { "XML-RPC", "M6,20A6,6 0 0,1 0,14C0,10.91 2.34,8.36 5.35,8.04C6.6,5.64 9.11,4 12,4C15.63,4 18.66,6.58 19.35,10C21.95,10.19 24,12.36 24,15A5,5 0 0,1 19,20H6M9.09,8.4L4.5,13L9.09,17.6L10.5,16.18L7.32,13L10.5,9.82L9.09,8.4M14.91,8.4L13.5,9.82L16.68,13L13.5,16.18L14.91,17.6L19.5,13L14.91,8.4Z" },
    };

    private string? _iconPath;
    public string? IconPath
    {
        get => _iconPath;
        set
        {
            if (_iconPath == value)
                return;

            _iconPath = value;

            NotifyPropertyChanged(nameof(IconPath));
        }
    }

    private string? _typeText;
    public string? TypeText
    {
        get => _typeText;
        set
        {
            if (_typeText == value)
                return;

            _typeText = value;

            NotifyPropertyChanged(nameof(TypeText));
        }
    }

    private string _title = "";
    public string Title
    {
        get => _title;
        set
        {
            if (_title == value)
                return;

            _title = value;

            NotifyPropertyChanged(nameof(Title));
        }
    }

    private string? _displayUrl;
    public string? DisplayUrl
    {
        get => _displayUrl;
        set
        {
            if (_displayUrl == value)
                return;

            _displayUrl = value;

            NotifyPropertyChanged(nameof(DisplayUrl));
        }
    }

}

public partial class FeedLinkItem : LinkItem
{
    public FeedLink FeedLinkData { get; set; }

    public FeedLinkItem(FeedLink fd)
    {
        FeedLinkData = fd;
        DisplayUrl = fd.FeedUri.AbsoluteUri;
        Title = fd.Title;

        if (fd.FeedKind == FeedLink.FeedKinds.Atom)
        {
            IconPath = IconPathStrings["AtomFeed"];
            TypeText = "Atom Feed";
        }
        else if (fd.FeedKind == FeedLink.FeedKinds.Rss)
        {
            IconPath = IconPathStrings["RssFeed"];
            TypeText = "RSS Feed";
        }
    }
}

public partial class ServiceDocumentLinkItem : LinkItem
{
    // TODO: Not really used?
    public bool IsSupported { get; set; }

    public SearviceDocumentLinkBase SearviceDocumentLinkData { get; set; }

    public ServiceDocumentLinkItem(SearviceDocumentLinkBase searviceDocumentLink)
    {
        SearviceDocumentLinkData = searviceDocumentLink;

        if (searviceDocumentLink is RsdLink rd)
        {
            Title = rd.EngineName ?? "";

            foreach (var hoge in rd.Apis)
            {
                if ((hoge.Name?.ToLower() == "wordpress") && hoge.Preferred)
                {
                    TypeText = "XML-RPC (WP)";
                    IconPath = IconPathStrings["XML-RPC"];

                    if (hoge.ApiLink != null)
                    {
                        DisplayUrl = hoge.ApiLink.AbsoluteUri;

                        // for now. because this is FeedDesk
                        IsSupported = false;
                    }
                }
                else if ((hoge.Name?.ToLower() == "movable type") && hoge.Preferred)
                {
                    TypeText = "XML-RPC (MT)";
                    IconPath = IconPathStrings["XML-RPC"];

                    if (hoge.ApiLink != null)
                    {
                        DisplayUrl = hoge.ApiLink.AbsoluteUri;

                        // for now. because this is FeedDesk
                        IsSupported = false;
                    }
                }
            }
        }
        else if (searviceDocumentLink is AppLink al)
        {
            Title = al.NodeService?.Name ?? "";

            TypeText = "Atom Publishing Protocol";
            IconPath = IconPathStrings["AtomPub"];

            // for now. because this is FeedDesk
            // TODO: 
            IsSupported = false;
        }
        else if (searviceDocumentLink is SearviceDocumentLinkErr sde)
        {
            // TODO:
        }
    }
}
