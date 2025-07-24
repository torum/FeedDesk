using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FeedDesk.Models.Clients;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FeedDesk.Models;

public enum ServiceTypes
{
    Feed,
    XmlRpc,
    AtomPub,
    AtomApi,
    Unknown
}

public enum ApiTypes
{
    atFeed,
    atAtomPub,
    //AtomPub_Hatena,
    atXMLRPC_MovableType,
    atXMLRPC_WordPress,
    //atWPJson,
    atAtomApi,
    atUnknown
}

public enum ViewTypes
{
    vtCards,
    vtMagazine,
    vtThreePanes
}

// HTTP REST Auth Types enum.
public enum AuthTypes
{
    Wsse, Basic
}

public abstract class NodeTree : Node
{
    private string _pathData = "M20,18H4V8H20M20,6H12L10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6Z";
    public string PathIcon
    {
        get => _pathData;
        protected set
        {
            if (_pathData == value)
                {
                return;
            }

            _pathData = value;

            NotifyPropertyChanged(nameof(PathIcon));
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                {
                return;
            }

            _isSelected = value;

            NotifyPropertyChanged(nameof(IsSelected));
        }
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
                {
                return;
            }

            _isExpanded = value;

            NotifyPropertyChanged(nameof(IsExpanded));
        }
    }

    private string _subNodeText = "";
    public string SubNodeText
    {
        get => _subNodeText;
        set
        {
            if (_subNodeText == value)
                {
                return;
            }

            _subNodeText = value;
            NotifyPropertyChanged(nameof(SubNodeText));
        }
    }

    private int _entryNewCount = 0;
    public int EntryNewCount
    {
        get => _entryNewCount;
        set
        {
            if (_entryNewCount == value)
                {
                return;
            }

            _entryNewCount = value;

            NotifyPropertyChanged(nameof(EntryNewCount));

            if (_entryNewCount > 0)
            {
                IsEntryCountMoreThanZero = true;
                if (_entryNewCount > 99)
                {
                    //SubNodeText = "99+";
                }
                else
                {
                    //SubNodeText = _entryNewCount.ToString();
                }
            }
            else
            {
                IsEntryCountMoreThanZero = false;
                //SubNodeText = "";
            }
        }
    }

    private bool _isEntryCountMoreThanZero;
    public bool IsEntryCountMoreThanZero
    {
        get => _isEntryCountMoreThanZero;
        set
        {
            if (_isEntryCountMoreThanZero == value)
                {
                return;
            }

            _isEntryCountMoreThanZero = value;

            NotifyPropertyChanged(nameof(IsEntryCountMoreThanZero));
        }
    }

    private ViewTypes _viewType;
    public ViewTypes ViewType
    {
        get => _viewType;
        set
        {
            if (_viewType == value)
                {
                return;
            }

            _viewType = value;

            NotifyPropertyChanged(nameof(ViewType));
        }
    }

    public ErrorObject? ErrorDatabase
    {
        get; set;
    }

    /*
    private bool _isDragOver;
    public bool IsDragOver
    {
        get => _isDragOver;
        set
        {
            if (_isDragOver == value)
                return;

            _isDragOver = value;

            NotifyPropertyChanged(nameof(IsDragOver));
        }
    }

    private bool _isBeforeDragSeparator;
    public bool IsBeforeDragSeparator
    {
        get => _isBeforeDragSeparator;
        set
        {
            if (_isBeforeDragSeparator == value)
                return;

            _isBeforeDragSeparator = value;

            NotifyPropertyChanged(nameof(IsBeforeDragSeparator));
        }
    }

    private bool _isAfterDragSeparator;
    public bool IsAfterDragSeparator
    {
        get => _isAfterDragSeparator;
        set
        {
            if (_isAfterDragSeparator == value)
                return;

            _isAfterDragSeparator = value;

            NotifyPropertyChanged(nameof(IsAfterDragSeparator));
        }
    }
    */

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value)
                {
                return;
            }

            _isBusy = value;

            if (value)
            {
                if (Parent != null)
                {
                    IncIsBusyCount(Parent);
                }
            }
            else
            {
                if (Parent != null)
                {
                    DecIsBusyCount(Parent);
                }
            }

            // if this has busy childrenn, make it busy anyway.
            if (_isBusyChildrenCount > 0)
            {
                _isBusy = true;
            }
            
            NotifyPropertyChanged(nameof(IsBusy));
        }
    }

    private static void IncIsBusyCount(NodeTree parentNode)
    {
        if (parentNode != null)
        {
            if ((parentNode is NodeFolder) || (parentNode is NodeRoot) || (parentNode is FeedTreeBuilder))
            {
                parentNode.IsBusyChildrenCount += 1;

                if (parentNode.Parent != null)
                {
                    IncIsBusyCount(parentNode.Parent);
                }
            }
        }
    }

    private static void DecIsBusyCount(NodeTree parentNode)
    {
        if (parentNode != null)
        {
            if ((parentNode is NodeFolder) || (parentNode is NodeRoot) || (parentNode is FeedTreeBuilder))
            {
                if (parentNode.IsBusyChildrenCount > 0)
                    {
                    parentNode.IsBusyChildrenCount -= 1;
                }

                if (parentNode.Parent != null)
                {
                    DecIsBusyCount(parentNode.Parent);
                }
            }
        }
    }

    protected int _isBusyChildrenCount = 0;
    public int IsBusyChildrenCount
    {
        get => _isBusyChildrenCount;
        protected set
        {

            if (_isBusyChildrenCount == value)
                {
                return;
            }

            _isBusyChildrenCount = value;
            NotifyPropertyChanged(nameof(IsBusyChildrenCount));

            if (_isBusyChildrenCount > 0)
            {
                SubNodeText = $"({_isBusyChildrenCount})";
            }
            else
            {
                SubNodeText = "";
            }
        }
    }

    private bool _isDisplayUnarchivedOnly = true;
    public bool IsDisplayUnarchivedOnly
    {
        get => _isDisplayUnarchivedOnly;
        set
        {
            if (_isDisplayUnarchivedOnly == value)
                {
                return;
            }

            _isDisplayUnarchivedOnly = value;

            NotifyPropertyChanged(nameof(IsDisplayUnarchivedOnly));
        }
    }

    private NodeTree? _parent;
    public NodeTree? Parent
    {
        get => _parent;
        set
        {
            if (_parent == value)
                {
                return;
            }

            _parent = value;

            NotifyPropertyChanged(nameof(Parent));
        }
    }

    private ObservableCollection<NodeTree> _children = [];
    public ObservableCollection<NodeTree> Children
    {
        get => _children;
        set
        {
            _children = value;

            NotifyPropertyChanged(nameof(Children));
        }
    }

    protected NodeTree(){}

    // TODO: Parent should be ....
    protected NodeTree(string name): base(name)
    {
        //BindingOperations.EnableCollectionSynchronization(_children, new object());
    }

    public bool ContainsChild(NodeTree nt)
    {
        if (ContainsChildLoop(Children, nt))
            {
            return true;
        }
        else
        {
            return false;
        }
    }

    private static bool ContainsChildLoop(ObservableCollection<NodeTree> childList, NodeTree ntc)
    {
        var hasChild = false;

        foreach (var c in childList)
        {
            if (c == ntc)
                {
                return true;
            }

            if (c.Children.Count > 0)
            {
                if (ContainsChildLoop(c.Children, ntc))
                    {
                    return true;
                }
            }
        }

        return hasChild;
    }

}

// Data tempalate selector for NodeTree
public partial class ExplorerItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? FolderTemplate
    {
        get; set;
    }
    public DataTemplate? FileTemplate
    {
        get; set;
    }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        //return base.SelectTemplateCore(item, null);

        if (item is null) {
            return base.SelectTemplateCore(item);
        }

        if (item is not NodeTree) {
            return base.SelectTemplateCore(item);
        }

        //var explorerItem = (NodeTree)item;
        /*
        if (explorerItem is NodeFeed)
        {
            return FileTemplate;
        }
        else
        {
            return FolderTemplate;
        }
        */

        return null;

    }
    /*
    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        var explorerItem = (NodeTree)item;
        
        if (explorerItem is NodeFeed)
        {
            return FileTemplate;
        }
        else
        {
            return FolderTemplate;
        }
    }
    */

}

public partial class NodeRoot : NodeTree
{

}

// NodeFolder for NodeFeeds (Node/NodeTree)
public partial class NodeFolder : NodeTree
{
    protected bool _isPendingReload;
    public bool IsPendingReload
    {
        get => _isPendingReload;
        set
        {
            if (_isPendingReload == value)
                {
                return;
            }

            _isPendingReload = value;
            NotifyPropertyChanged(nameof(IsPendingReload));
        }
    }

    public NodeFolder(string name) : base(name)
    {
        PathIcon = "M5,3H19A2,2 0 0,1 21,5V19A2,2 0 0,1 19,21H5A2,2 0 0,1 3,19V5A2,2 0 0,1 5,3M7.5,15A1.5,1.5 0 0,0 6,16.5A1.5,1.5 0 0,0 7.5,18A1.5,1.5 0 0,0 9,16.5A1.5,1.5 0 0,0 7.5,15M6,10V12A6,6 0 0,1 12,18H14A8,8 0 0,0 6,10M6,6V8A10,10 0 0,1 16,18H18A12,12 0 0,0 6,6Z";
    }
}

// Web Searvices (for Feeds, AtomPub, XML-RPC) (Node/NodeTree)
public partial class NodeService : NodeTree
{
    // Id = endPoint.AbsoluteUri 
    public string Id { get; protected set; }

    // Service provider logos icon (svg based path data)

    // WordPress
    //private string _pathIconWordPress = "M3.42,12C3.42,10.76 3.69,9.58 4.16,8.5L8.26,19.72C5.39,18.33 3.42,15.4 3.42,12M17.79,11.57C17.79,12.3 17.5,13.15 17.14,14.34L16.28,17.2L13.18,8L14.16,7.9C14.63,7.84 14.57,7.16 14.11,7.19C14.11,7.19 12.72,7.3 11.82,7.3L9.56,7.19C9.1,7.16 9.05,7.87 9.5,7.9L10.41,8L11.75,11.64L9.87,17.27L6.74,8L7.73,7.9C8.19,7.84 8.13,7.16 7.67,7.19C7.67,7.19 6.28,7.3 5.38,7.3L4.83,7.29C6.37,4.96 9,3.42 12,3.42C14.23,3.42 16.27,4.28 17.79,5.67H17.68C16.84,5.67 16.24,6.4 16.24,7.19C16.24,7.9 16.65,8.5 17.08,9.2C17.41,9.77 17.79,10.5 17.79,11.57M12.15,12.75L14.79,19.97L14.85,20.09C13.96,20.41 13,20.58 12,20.58C11.16,20.58 10.35,20.46 9.58,20.23L12.15,12.75M19.53,7.88C20.2,9.11 20.58,10.5 20.58,12C20.58,15.16 18.86,17.93 16.31,19.41L18.93,11.84C19.42,10.62 19.59,9.64 19.59,8.77L19.53,7.88M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,21.54C17.26,21.54 21.54,17.26 21.54,12C21.54,6.74 17.26,2.46 12,2.46C6.74,2.46 2.46,6.74 2.46,12C2.46,17.26 6.74,21.54 12,21.54Z";
    // Hatena
    //private string _pathIconHatena = "M 50.099609 0 C 22.499609 4.7369516e-015 0 22.499609 0 50.099609 C 4.7369516e-015 77.699609 22.499609 100.19922 50.099609 100.19922 C 77.699609 100.19922 100.09922 77.699609 100.19922 50.099609 C 100.19922 22.499609 77.699609 0 50.099609 0 z M 50.099609 6.4003906 C 74.199609 6.4003906 93.800781 25.999609 93.800781 50.099609 C 93.800781 74.199609 74.199609 93.800781 50.099609 93.800781 C 25.999609 93.800781 6.4003906 74.199609 6.4003906 50.099609 C 6.4003906 25.999609 25.999609 6.4003906 50.099609 6.4003906 z M 49 12.699219 C 48.2 15.499219 46.800781 20.300781 44.300781 25.300781 C 40.500781 33.100781 35.699219 39.900391 35.699219 39.900391 L 38.800781 81.699219 C 38.800781 81.699219 41.699609 84.900391 50.099609 84.900391 C 58.499609 84.900391 61.400391 81.699219 61.400391 81.699219 L 64.5 39.900391 C 64.5 39.900391 59.700391 33.100781 55.900391 25.300781 C 53.500391 20.300781 51.999219 15.499219 51.199219 12.699219 L 51.199219 48.199219 C 52.399219 48.699219 53.300781 49.799219 53.300781 51.199219 C 53.300781 52.999219 51.8 54.5 50 54.5 C 48.2 54.5 46.699219 52.999219 46.699219 51.199219 C 46.699219 49.699219 47.7 48.499609 49 48.099609 L 49 12.699219 z";
    // AtomPub
    //private string _pathIconAtomPub = "M12,11A1,1 0 0,1 13,12A1,1 0 0,1 12,13A1,1 0 0,1 11,12A1,1 0 0,1 12,11M4.22,4.22C5.65,2.79 8.75,3.43 12,5.56C15.25,3.43 18.35,2.79 19.78,4.22C21.21,5.65 20.57,8.75 18.44,12C20.57,15.25 21.21,18.35 19.78,19.78C18.35,21.21 15.25,20.57 12,18.44C8.75,20.57 5.65,21.21 4.22,19.78C2.79,18.35 3.43,15.25 5.56,12C3.43,8.75 2.79,5.65 4.22,4.22M15.54,8.46C16.15,9.08 16.71,9.71 17.23,10.34C18.61,8.21 19.11,6.38 18.36,5.64C17.62,4.89 15.79,5.39 13.66,6.77C14.29,7.29 14.92,7.85 15.54,8.46M8.46,15.54C7.85,14.92 7.29,14.29 6.77,13.66C5.39,15.79 4.89,17.62 5.64,18.36C6.38,19.11 8.21,18.61 10.34,17.23C9.71,16.71 9.08,16.15 8.46,15.54M5.64,5.64C4.89,6.38 5.39,8.21 6.77,10.34C7.29,9.71 7.85,9.08 8.46,8.46C9.08,7.85 9.71,7.29 10.34,6.77C8.21,5.39 6.38,4.89 5.64,5.64M9.88,14.12C10.58,14.82 11.3,15.46 12,16.03C12.7,15.46 13.42,14.82 14.12,14.12C14.82,13.42 15.46,12.7 16.03,12C15.46,11.3 14.82,10.58 14.12,9.88C13.42,9.18 12.7,8.54 12,7.97C11.3,8.54 10.58,9.18 9.88,9.88C9.18,10.58 8.54,11.3 7.97,12C8.54,12.7 9.18,13.42 9.88,14.12M18.36,18.36C19.11,17.62 18.61,15.79 17.23,13.66C16.71,14.29 16.15,14.92 15.54,15.54C14.92,16.15 14.29,16.71 13.66,17.23C15.79,18.61 17.62,19.11 18.36,18.36Z";
    // AtomFeed
    //private string _pathIconAtomFeed = "M6.18,15.64A2.18,2.18 0 0,1 8.36,17.82C8.36,19 7.38,20 6.18,20C5,20 4,19 4,17.82A2.18,2.18 0 0,1 6.18,15.64M4,4.44A15.56,15.56 0 0,1 19.56,20H16.73A12.73,12.73 0 0,0 4,7.27V4.44M4,10.1A9.9,9.9 0 0,1 13.9,20H11.07A7.07,7.07 0 0,0 4,12.93V10.1Z";
    // Blogger
    //private string _pathIconBlogger = "M14,13H9.95A1,1 0 0,0 8.95,14A1,1 0 0,0 9.95,15H14A1,1 0 0,0 15,14A1,1 0 0,0 14,13M9.95,10H12.55A1,1 0 0,0 13.55,9A1,1 0 0,0 12.55,8H9.95A1,1 0 0,0 8.95,9A1,1 0 0,0 9.95,10M16,9V10A1,1 0 0,0 17,11A1,1 0 0,1 18,12V15A3,3 0 0,1 15,18H9A3,3 0 0,1 6,15V8A3,3 0 0,1 9,5H13A3,3 0 0,1 16,8M20,2H4C2.89,2 2,2.89 2,4V20A2,2 0 0,0 4,22H20A2,2 0 0,0 22,20V4C22,2.89 21.1,2 20,2Z";

    public Uri EndPoint { get; set; }

    public string UserName { get; set; } = "";

    public string UserPassword { get; set; } = "";

    public AuthTypes AuthType { get; set; }

    public ApiTypes Api { get; set; }

    public ServiceTypes ServiceType { get; set; }

    public BaseClient? Client
    {
        get; set;
    }

    public ErrorObject? ErrorHttp { get; set; }


    // The DateTime of the last time checking new feed.
    public DateTime LastFetched { get; set; }

    public NodeService(string name, string username, string password, Uri endPoint, ApiTypes api, ServiceTypes serviceType) : base(name)
    {
        // Default account icon
        PathIcon = "M12,19.2C9.5,19.2 7.29,17.92 6,16C6.03,14 10,12.9 12,12.9C14,12.9 17.97,14 18,16C16.71,17.92 14.5,19.2 12,19.2M12,5A3,3 0 0,1 15,8A3,3 0 0,1 12,11A3,3 0 0,1 9,8A3,3 0 0,1 12,5M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12C22,6.47 17.5,2 12,2Z";

        EndPoint = endPoint;

        Api = api;

        ServiceType = serviceType;

        //Id = Guid.NewGuid().ToString();
        Id = endPoint.AbsoluteUri;

        UserName = username;

        UserPassword = password;
        /*
        switch (api)
        {
            case ApiTypes.atAtomPub:
                Client = new AtomPubClient(UserName, UserPassword, EndPoint);
                break;
            case ApiTypes.atXMLRPC_MovableType:
                Client = new XmlRpcClient(UserName, UserPassword, EndPoint);
                break;
            case ApiTypes.atXMLRPC_WordPress:
                Client = new XmlRpcClient(UserName, UserPassword, EndPoint);
                break;
            case ApiTypes.atFeed:
                Client = new FeedClient();
                break;
                //TODO: WP, AtomAPI
        }
        */
    }

    public NodeService(string name, Uri endPoint, ApiTypes api, ServiceTypes serviceType) : base(name)
    {
        // Default account icon
        PathIcon = "M12,19.2C9.5,19.2 7.29,17.92 6,16C6.03,14 10,12.9 12,12.9C14,12.9 17.97,14 18,16C16.71,17.92 14.5,19.2 12,19.2M12,5A3,3 0 0,1 15,8A3,3 0 0,1 12,11A3,3 0 0,1 9,8A3,3 0 0,1 12,5M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12C22,6.47 17.5,2 12,2Z";

        EndPoint = endPoint;

        Api = api;

        ServiceType = serviceType;

        //Id = Guid.NewGuid().ToString();
        Id = endPoint.AbsoluteUri;
        /*
        switch (api)
        {
            case ApiTypes.atAtomPub:
                _client = new AtomPubClient(UserName, UserPassword, EndPoint);
                break;
            case ApiTypes.atXMLRPC_MovableType:
                _client = new XmlRpcClient(UserName, UserPassword, EndPoint);
                break;
            case ApiTypes.atXMLRPC_WordPress:
                _client = new XmlRpcClient(UserName, UserPassword, EndPoint);
                break;
            case ApiTypes.atFeed:
                _client = new FeedClient();
                break;

                //TODO: WP, AtomAPI
        }
        */
    }
}

// NodeFeed class for Feed (Node/NodeTree/NodeService)
public partial class NodeFeed : NodeService
{
    private static readonly string _defaultPathIcon = "M6.18,15.64A2.18,2.18 0 0,1 8.36,17.82C8.36,19 7.38,20 6.18,20C5,20 4,19 4,17.82A2.18,2.18 0 0,1 6.18,15.64M4,4.44A15.56,15.56 0 0,1 19.56,20H16.73A12.73,12.73 0 0,0 4,7.27V4.44M4,10.1A9.9,9.9 0 0,1 13.9,20H11.07A7.07,7.07 0 0,0 4,12.93V10.1Z";
    private static readonly string _downloadingPathIcon = "M12 2A10 10 0 1 0 22 12A10 10 0 0 0 12 2M18 11H13L14.81 9.19A3.94 3.94 0 0 0 12 8A4 4 0 1 0 15.86 13H17.91A6 6 0 1 1 12 6A5.91 5.91 0 0 1 16.22 7.78L18 6Z";
    private static readonly string _savingPathIcon = "M13,2.03C17.73,2.5 21.5,6.25 21.95,11C22.5,16.5 18.5,21.38 13,21.93V19.93C16.64,19.5 19.5,16.61 19.96,12.97C20.5,8.58 17.39,4.59 13,4.05V2.05L13,2.03M11,2.06V4.06C9.57,4.26 8.22,4.84 7.1,5.74L5.67,4.26C7.19,3 9.05,2.25 11,2.06M4.26,5.67L5.69,7.1C4.8,8.23 4.24,9.58 4.05,11H2.05C2.25,9.04 3,7.19 4.26,5.67M2.06,13H4.06C4.24,14.42 4.81,15.77 5.69,16.9L4.27,18.33C3.03,16.81 2.26,14.96 2.06,13M7.1,18.37C8.23,19.25 9.58,19.82 11,20V22C9.04,21.79 7.18,21 5.67,19.74L7.1,18.37M12,16.5L7.5,12H11V8H13V12H16.5L12,16.5Z";
    private static readonly string _loadingPathIcon = "M17.65,6.35C16.2,4.9 14.21,4 12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20C15.73,20 18.84,17.45 19.73,14H17.65C16.83,16.33 14.61,18 12,18A6,6 0 0,1 6,12A6,6 0 0,1 12,6C13.66,6 15.14,6.69 16.22,7.78L13,11H20V4L17.65,6.35Z";
    private static readonly string _loadErrorPathIcon = "M2 12C2 17 6 21 11 21C13.4 21 15.7 20.1 17.4 18.4L15.9 16.9C14.6 18.3 12.9 19 11 19C4.8 19 1.6 11.5 6.1 7.1S18 5.8 18 12H15L19 16H19.1L23 12H20C20 7 16 3 11 3S2 7 2 12M10 15H12V17H10V15M10 7H12V13H10V7";

    // Atom //feed/title
    // RSS2.0 //rss/channel/title
    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set
        {
            if (_title == value)
                {
                return;
            }

            _title = value;
            NotifyPropertyChanged(nameof(Title));
        }
    }

    // Atom //feed/subtitle
    // RSS2.0  //rss/channel/description
    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set
        {
            if (_description == value)
                {
                return;
            }

            _description = value;
            NotifyPropertyChanged(nameof(Description));
        }
    }

    // not really used.
    // RSS2.0 //rss/channnel/copyright
    private string _copyright = string.Empty;
    public string Copyright
    {
        get => _copyright;
        set
        {
            if (_copyright == value)
                {
                return;
            }

            _copyright = value;
            NotifyPropertyChanged(nameof(Copyright));
        }
    }

    // Atom //feed/link, RSS2.0 //rss/channnel/link
    private Uri? _htmlUri;
    public Uri? HtmlUri
    {
        get => _htmlUri;
        set
        {
            if (_htmlUri == value)
                {
                return;
            }

            _htmlUri = value;
            NotifyPropertyChanged(nameof(HtmlUri));
        }
    }

    /*
    // not really used.
    // RSS2.0 //rss/channel/pubDate 
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
    */

    // Atom //feed/updated
    // RSS2.0 //rss/channel/lastBuildDate
    private DateTime _updated = default;
    public DateTime Updated
    {
        get => _updated;
        set
        {
            if (_updated == value)
                {
                return;
            }

            _updated = value;
            NotifyPropertyChanged(nameof(Updated));
            NotifyPropertyChanged(nameof(UpdatedDateTimeFormated));
        }
    }

    public string? UpdatedDateTimeFormated
    {
        get
        {
            if (Updated != default)
            {
                return Updated.ToLocalTime().ToString(System.Globalization.CultureInfo.CurrentUICulture);
            }
            else
            {
                return "-";
            }
        }
    }

    public enum DownloadStatus
    {
        normal,
        downloading,
        saving,
        loading,
        error
    }

    private DownloadStatus _status = DownloadStatus.normal;
    public DownloadStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
                {
                return;
            }

            _status = value;

            if (_status == DownloadStatus.normal)
            {
                PathIcon = _defaultPathIcon;
            }
            else if (_status == DownloadStatus.downloading)
            {
                PathIcon = _downloadingPathIcon;
            }
            else if (_status == DownloadStatus.saving)
            {
                PathIcon = _savingPathIcon;
            }
            else if (_status == DownloadStatus.loading)
            {
                PathIcon = _loadingPathIcon;
            }
            else if (_status == DownloadStatus.error)
            {
                PathIcon = _loadErrorPathIcon;
            }
            NotifyPropertyChanged(nameof(PathIcon));

            NotifyPropertyChanged(nameof(Status));
        }
    }

    public ObservableCollection<EntryItem> List { get; set; } = [];

    public NodeFeed(string name, Uri feedUrl) : base(name, feedUrl, ApiTypes.atFeed, ServiceTypes.Feed)
    {
        PathIcon = "M6.18,15.64A2.18,2.18 0 0,1 8.36,17.82C8.36,19 7.38,20 6.18,20C5,20 4,19 4,17.82A2.18,2.18 0 0,1 6.18,15.64M4,4.44A15.56,15.56 0 0,1 19.56,20H16.73A12.73,12.73 0 0,0 4,7.27V4.44M4,10.1A9.9,9.9 0 0,1 13.9,20H11.07A7.07,7.07 0 0,0 4,12.93V10.1Z";
    }
}

// Workspace Node (Node/NodeTree)
public partial class NodeWorkspace : NodeTree
{
    public NodeWorkspace(string name) : base(name)
    { }
}

// Base class for Entry Collection (Node/NodeTree) 
public abstract class NodeEntryCollection : NodeTree
{
    /// <summary>
    /// entries resource URI or xml-rpc URL for a blog.
    /// </summary>
    public Uri Uri { get; private set; }

    /// <summary>
    /// entries resource URI or xml-rpc blogId for a blog.
    /// </summary>
    public string Id { get; private set; }

    // Constructor.
    public NodeEntryCollection(string name, Uri uri, string id) : base(name)
    {
        Uri = uri;
        Id = id;
        PathIcon = "M4,5V7H21V5M4,11H21V9H4M4,19H21V17H4M4,15H21V13H4V15Z";
    }

    // TODO:
    public ObservableCollection<EntryItem> List { get; } = [];

    public BaseClient? Client
    {
        get
        {
            if (Parent == null)
                {
                return null;
            }

            if (Parent is NodeService nds)
            {
                return nds.Client;
            }

            if (Parent.Parent == null)
                {
                return null;
            }

            if (Parent.Parent is not NodeService ndsp)
                {
                return null;
            }

            return ndsp.Client;
        }
    }
}

// AtomPub Entry Collection (Node/NodeTree/NodeEntryCollection) 
public partial class NodeAtomPubEntryCollection : NodeEntryCollection
{
    public Uri? CategoriesUri { get; set; }

    public bool IsCategoryFixed { get; set; }

    public bool IsAcceptEntry { get; set; }

    //public string CategoryScheme { get; set; }

    //TODO: enum supported AcceptTypes
    // "application/atom+xml"
    // "application/atom+xml;type=entry"
    // "application/atomcat+xml"
    //image/png
    //image/jpeg
    //image/gif
    public Collection<string> AcceptTypes = [];

    public NodeAtomPubEntryCollection(string name, Uri uri, string id) : base(name, uri, id)
    {
        PathIcon = "M4,5V7H21V5M4,11H21V9H4M4,19H21V17H4M4,15H21V13H4V15Z";
    }
}

// XML-RPC Entry Collection (Node/NodeTree/NodeEntryCollection) 
public partial class NodeXmlRpcEntryCollection : NodeEntryCollection
{
    public NodeXmlRpcEntryCollection(string name, Uri uri, string id) : base(name, uri, id)
    {
        PathIcon = "M4,5V7H21V5M4,11H21V9H4M4,19H21V17H4M4,15H21V13H4V15Z";
    }
}
/*
public class NodeXmlRpcMTEntryCollection : NodeEntryCollection
{
    // Constructor.
    public NodeXmlRpcMTEntryCollection(string name, Uri uri) : base(name, uri)
    {
        PathIcon = "M4,5V7H21V5M4,11H21V9H4M4,19H21V17H4M4,15H21V13H4V15Z";
    }
}

public class NodeXmlRpcWPEntryCollection : NodeEntryCollection
{
    // Constructor.
    public NodeXmlRpcWPEntryCollection(string name, Uri uri) : base(name, uri)
    {
        PathIcon = "M4,5V7H21V5M4,11H21V9H4M4,19H21V17H4M4,15H21V13H4V15Z";
    }
}
*/

public partial class NodeAtomPubCatetories : NodeTree
{
    public Uri? Href { get; set; }

    public bool IsCategoryFixed { get; set; }

    public string? Scheme { get; set; }

    public List<NodeAtomPubCategory> CategoryList = [];

    public NodeAtomPubCatetories(string title) : base(title)
    {
        PathIcon = "M16,17H5V7H16L19.55,12M17.63,5.84C17.27,5.33 16.67,5 16,5H5A2,2 0 0,0 3,7V17A2,2 0 0,0 5,19H16C16.67,19 17.27,18.66 17.63,18.15L22,12L17.63,5.84Z";

        //ID = Guid.NewGuid().ToString();

    }
}

public partial class NodeCategory : NodeTree
{
    public NodeCategory(string title) : base(title)
    {
        PathIcon = "M16,17H5V7H16L19.55,12M17.63,5.84C17.27,5.33 16.67,5 16,5H5A2,2 0 0,0 3,7V17A2,2 0 0,0 5,19H16C16.67,19 17.27,18.66 17.63,18.15L22,12L17.63,5.84Z";

        //ID = Guid.NewGuid().ToString();

    }

}

public partial class NodeAtomPubCategory : NodeCategory
{
    public string Term { get; set; }
    public string? Scheme { get; set; }

    public NodeAtomPubCategory(string title) : base(title)
    {
        //PathIcon = "M16,17H5V7H16L19.55,12M17.63,5.84C17.27,5.33 16.67,5 16,5H5A2,2 0 0,0 3,7V17A2,2 0 0,0 5,19H16C16.67,19 17.27,18.66 17.63,18.15L22,12L17.63,5.84Z";

        //ID = Guid.NewGuid().ToString();

        Term = title;

    }

}

public partial class NodeXmlRpcMTCategory : NodeCategory
{
    public string CategoryName { get; set; }

    public string? CategoryId { get; set; }

    public string? ParentId { get; set; }

    public string? Description { get; set; }

    public string? CategoryDescription { get; set; }

    public Uri? HtmlUrl { get; set; }

    public Uri? RssUrl { get; set; }

    // Constructor.
    public NodeXmlRpcMTCategory(string title) : base(title)
    {
        //PathIcon = "M16,17H5V7H16L19.55,12M17.63,5.84C17.27,5.33 16.67,5 16,5H5A2,2 0 0,0 3,7V17A2,2 0 0,0 5,19H16C16.67,19 17.27,18.66 17.63,18.15L22,12L17.63,5.84Z";

        //ID = Guid.NewGuid().ToString();

        CategoryName = title;

    }

}

public partial class NodeXmlRpcWPCategory : NodeCategory
{

    //TODO:

    //public string Term { get; set; }

    // Constructor.
    public NodeXmlRpcWPCategory(string title) : base(title)
    {
        //PathIcon = "M16,17H5V7H16L19.55,12M17.63,5.84C17.27,5.33 16.67,5 16,5H5A2,2 0 0,0 3,7V17A2,2 0 0,0 5,19H16C16.67,19 17.27,18.66 17.63,18.15L22,12L17.63,5.84Z";

        //ID = Guid.NewGuid().ToString();

        //Term = title;

    }

}
