using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using FeedDesk.Helpers;
using FeedDesk.Models;
using FeedDesk.Models.Clients;
using FeedDesk.Services;
using FeedDesk.Services.Contracts;
using FeedDesk.Views;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Input;
using System.Xml;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Core;
using Windows.Storage;
using WinRT.Interop;

namespace FeedDesk.ViewModels;

public partial class MainViewModel : ObservableRecipient
{

    #region == FeedEdit ==

    private NodeFeed? _feedToEdit;
    public NodeFeed? FeedToEDit
    {
        get => _feedToEdit;
        set 
        { 
            if (SetProperty(ref _feedToEdit, value))
            {
                NameToEditFeed = _feedToEdit?.Name;
            }
        }
    }

    private string? _nameToEditFeed = string.Empty;
    public string? NameToEditFeed
    {
        get => _nameToEditFeed;
        set => SetProperty(ref _nameToEditFeed, value);
    }

    [RelayCommand]
    private void CheckIfFeedIsValidUsingValidator()
    {
        if (FeedToEDit is null)
            return;

        if (FeedToEDit.EndPoint is not null)
        {
            var hoge = new Uri("https://validator.w3.org/feed/check.cgi?url=" + HttpUtility.UrlEncode(FeedToEDit.EndPoint.AbsoluteUri));

            _ = Task.Run(() => Windows.System.Launcher.LaunchUriAsync(hoge), _cts.Token);
        }
    }

    [RelayCommand]
    private void UpdateFeedItemProperty()
    {
        if (string.IsNullOrEmpty(NameToEditFeed))
        {
            return;
            //_navigationService.NavigateTo(typeof(MainViewModel).FullName!, null);
        }

        if (FeedToEDit != null)
        {
            /* Not good when navigate go back.
            var hoge = new NodeTreePropertyChangedArgs();
            hoge.Name = Name;
            hoge.Node = Feed;
            _navigationService.NavigateTo(typeof(MainViewModel).FullName!, hoge);
            */

            _ = Task.Run(async () =>
            {
                await UpdateFeedAsync(FeedToEDit, NameToEditFeed);
            });

        }

        var shell = App.GetService<ShellPage>();
        _ = shell.NavFrame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    #endregion

    #region == FolderEdit ==

    private NodeFolder? _folderToEdit;
    public NodeFolder? FolderToEdit
    {
        get => _folderToEdit;
        set {
            
            if (SetProperty(ref _folderToEdit, value)) 
            {
                NameToEditFolder = _folderToEdit?.Name;
            }
        }
    }

    private string? _nameToEditFolder = string.Empty;
    public string? NameToEditFolder
    {
        get => _nameToEditFolder;
        set => SetProperty(ref _nameToEditFolder, value);
    }

    [RelayCommand]
    private void UpdateFolderItemProperty()
    {
        if (!string.IsNullOrEmpty(NameToEditFolder))
        {
            if (FolderToEdit != null)
            {
                FolderToEdit.Name = NameToEditFolder;
            }

            var shell = App.GetService<ShellPage>();
            _ = shell.NavFrame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
            //_navigationService.NavigateTo(typeof(MainViewModel).FullName!, null);
        }
    }

    #endregion

    #region == FolderAdd ==

    private NodeTree? _targetNodeToAddFolder;

    private string? _nameToAddFolder = string.Empty;
    public string? NameToAddFolder
    {
        get => _nameToAddFolder;
        set => SetProperty(ref _nameToAddFolder, value);
    }

    [RelayCommand]
    private void AddFolderItemProperty()
    {
        if (string.IsNullOrEmpty(NameToAddFolder))
        {
            return;
        }

        if (_targetNodeToAddFolder is null)
        {
            return;
        }

        NodeFolder folder = new(NameToAddFolder)
        {
            Parent = _targetNodeToAddFolder
        };

        _targetNodeToAddFolder.IsExpanded = true;
        _targetNodeToAddFolder.Children.Insert(0, folder);

        folder.IsSelected = true;

        var shell = App.GetService<ShellPage>();
        _ = shell.NavFrame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
    }


    #endregion

    #region == Setting ==

    private ElementTheme _theme = ElementTheme.Default;
    public ElementTheme Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    private SystemBackdropOption _material = SystemBackdropOption.Mica;
    public SystemBackdropOption Material
    {
        get => _material;
        set => SetProperty(ref _material, value);
    }

    private bool _isAcrylicSupported = false;
    public bool IsAcrylicSupported
    {
        get => _isAcrylicSupported;
        set => SetProperty(ref _isAcrylicSupported, value);
    }

    private bool _isBackdropEnabled = false;
    public bool IsBackdropEnabled
    {
        get => _isBackdropEnabled;
        set => SetProperty(ref _isBackdropEnabled, value);
    }

    private bool _isMicaSupported = false;
    public bool IsMicaSupported
    {
        get => _isMicaSupported;
        set => SetProperty(ref _isMicaSupported, value);
    }

#pragma warning disable IDE0079
#pragma warning disable CA1822
    public string VersionText
#pragma warning restore CA1822
#pragma warning restore IDE0079
    {
        get
        {
            Version version;

            if (RuntimeHelper.IsMSIX)
            {
                var packageVersion = Package.Current.Id.Version;

                version = new(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
            }
            else
            {
                version = Assembly.GetExecutingAssembly().GetName().Version!;
            }

            return $"{"Version".GetLocalized()} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
    }

    [RelayCommand]
    private void SwitchTheme(ElementTheme? param)
    {
        if (param is null)
        {
            return;
        }

        if (Theme == param)
        {
            return;
        }

        if (App.MainWnd == null)
        {
            return;
        }
        //var mainWin = App.GetService<MainWindow>();

        Theme = (ElementTheme)param;

        if (App.MainWnd?.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = Theme;

            //TitleBarHelper.UpdateTitleBar(Theme, App.MainWnd);
            App.MainWnd.SetCapitionButtonColorForWin11();
        }
    }

    [RelayCommand]
    private static void SwitchSystemBackdrop(string? backdrop)
    {
        if (backdrop == null)
        {
            return;
        }

        if (Enum.TryParse(backdrop, out SystemBackdropOption cacheBackdrop))
        {
            var mainWin = App.GetService<MainWindow>();
            mainWin.SwitchBackdrop(cacheBackdrop);
        }
    }

    #endregion

    #region == Service Treeview ==

    private readonly FeedTreeBuilder _services = new();

    public ObservableCollection<NodeTree> Services
    {
        get => _services.Children;
        set
        {
            _services.Children = value;
            OnPropertyChanged(nameof(Services));
        }
    }

    public FeedTreeBuilder Root => _services;

    private NodeTree? _selectedTreeViewItem;
    public NodeTree? SelectedTreeViewItem
    {
        get => _selectedTreeViewItem;
        set
        {
            if (_selectedTreeViewItem == value)
            {
                return;
            }

            try
            {
                _selectedTreeViewItem = value;

                OnPropertyChanged(nameof(SelectedTreeViewItem));

                // Clear Listview selected Item.
                SelectedListViewItem = null;

                // Clear error if shown.
                ErrorObj = null;
                IsShowFeedError = false;

                if (_selectedTreeViewItem == null)
                {
                    IsToggleInboxAppButtonEnabled = false;
                    Entries.Clear();
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                    return;
                }

                IsToggleInboxAppButtonEnabled = true;

                // Update Title bar info
                SelectedServiceName = _selectedTreeViewItem.Name;

                if (_selectedTreeViewItem is NodeService nds)
                {
                    if (nds.ErrorHttp != null)
                    {
                        ErrorObj = nds.ErrorHttp;
                        IsShowFeedError = true;
                    }
                    else if (nds.ErrorDatabase != null)
                    {
                        ErrorObj = nds.ErrorDatabase;
                        IsShowFeedError = true;
                    }

                    IsShowInboxEntries = nds.IsDisplayUnarchivedOnly;

                    // NodeFeed is selected
                    if (_selectedTreeViewItem is NodeFeed nfeed)
                    {
                        Entries = [];
                        
                        LoadEntries(nfeed);
                    }
                    else
                    {
                        // TODO: 
                        Entries = [];
                    }
                }
                else if (_selectedTreeViewItem is NodeFolder folder)
                {
                    IsShowInboxEntries = folder.IsDisplayUnarchivedOnly;

                    Entries = [];

                    LoadEntries(folder);
                    /*
                    if (!folder.IsPendingReload && !folder.IsBusy)
                    {
                        LoadEntriesAwaiter(folder);
                    }
                    else
                    {
                        if (Root.IsBusyChildrenCount <= 0)
                        {
                            folder.IsPendingReload = false;
                            LoadEntriesAwaiter(folder);
                        }
                    }
                    */
                    folder.IsPendingReload = false;
                }

                // notify at last.
                EntryArchiveAllCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SelectedTreeViewItem: {ex.Message}");
                (App.Current as App)?.AppendErrorLog("SelectedTreeViewItem", ex.Message);
            }

        }
    }

    private string _selectedServiceName = string.Empty;
    public string SelectedServiceName
    {
        get => _selectedServiceName;
        set => SetProperty(ref _selectedServiceName, value);
    }

    #endregion

    #region == Entry ListViews ==

    //[ObservableProperty]
    //[NotifyCanExecuteChangedFor(nameof(EntryArchiveAllCommand))]
    //private ObservableCollection<EntryItem> entries = new();

    private ObservableCollection<EntryItem> _entries = [];
    public ObservableCollection<EntryItem> Entries
    {
        get => _entries;
        set 
        {
            if (SetProperty(ref _entries, value))
            {
                EntryArchiveAllCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private FeedEntryItem? _selectedListViewItem = null;
    public FeedEntryItem? SelectedListViewItem
    {
        get => _selectedListViewItem;
        set
        {
            if (_selectedListViewItem == value)
            {
                return;
            }

            try
            {
                _selectedListViewItem = value;

                OnPropertyChanged(nameof(SelectedListViewItem));

                if (_selectedListViewItem == null)
                {
                    IsEntryDetailVisible = false;

                    return;
                }

                IsEntryDetailVisible = true;

                EntryViewExternalCommand.NotifyCanExecuteChanged();

                //
                if (string.IsNullOrEmpty(_selectedListViewItem.Summary.Trim()))
                {
                    IsSummaryExists = false;
                }
                else
                {
                    IsSummaryExists = true;
                }

                if ((_selectedListViewItem as EntryItem).ContentType == EntryItem.ContentTypes.text)
                {
                    IsContentText = true;

                    if (!string.IsNullOrEmpty(_selectedListViewItem.Content.Trim()))
                    {
                        IsSummaryExists = false;
                    }
                }
                else
                {
                    IsContentText = false;
                }

                if (((_selectedListViewItem as EntryItem).ContentType == EntryItem.ContentTypes.textHtml) ||
                    ((_selectedListViewItem as EntryItem).ContentType == EntryItem.ContentTypes.unknown))
                {
                    IsContentHTML = true;

                    if (!string.IsNullOrEmpty(_selectedListViewItem.Content.Trim()))
                    {
                        IsSummaryExists = false;
                    }
                }
                else
                {
                    IsContentHTML = false;
                }

                if ((_selectedListViewItem as EntryItem).AltHtmlUri != null)
                {
                    IsAltLinkExists = true;
                }
                else
                {
                    IsAltLinkExists = false;
                }

                if (_selectedListViewItem.ImageUri != null)
                {
                    IsImageLinkExists = true;
                }
                else
                {
                    IsImageLinkExists = false;
                }

                if (_selectedListViewItem.AudioUri != null)
                {
                    IsAudioLinkExists = true;
                }
                else
                {
                    IsAudioLinkExists = false;
                }

                if (_selectedListViewItem.CommentUri != null)
                {
                    IsCommentPageLinkExists = true;
                }
                else
                {
                    IsCommentPageLinkExists = false;
                }

                if ((_selectedListViewItem.Status != FeedEntryItem.ReadStatus.rsNewVisited) && (_selectedListViewItem.Status != FeedEntryItem.ReadStatus.rsNormalVisited))
                {
                    //Task.Run(() => UpdateEntryStatusAsReadAsync(SelectedTreeViewItem!, _selectedListViewItem));
                    UpdateEntryStatusAsRead(SelectedTreeViewItem!, _selectedListViewItem);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SelectedListViewItem: {ex.Message}");
                (App.Current as App)?.AppendErrorLog("SelectedListViewItem", ex.Message);
            }

        }
    }
    
    private bool _isSummaryExists;
    public bool IsSummaryExists
    {
        get => _isSummaryExists;
        set => SetProperty(ref _isSummaryExists, value);
    }

    private bool _isContentText;
    public bool IsContentText
    {
        get => _isContentText;
        set => SetProperty(ref _isContentText, value);
    }

    private bool _isContentHTML;
    public bool IsContentHTML
    {
        get => _isContentHTML;
        set => SetProperty(ref _isContentHTML, value);
    }

    private bool _isAltLinkExists;
    public bool IsAltLinkExists
    {
        get => _isAltLinkExists;
        set
        {
            SetProperty(ref _isAltLinkExists, value);
            IsNoAltLinkExists = !value;
        }
    }

    private bool _isNoAltLinkExists;
    public bool IsNoAltLinkExists
    {
        get => _isNoAltLinkExists;
        set => SetProperty(ref _isNoAltLinkExists, value);
    }

    private bool _isImageLinkExists;
    public bool IsImageLinkExists
    {
        get => _isImageLinkExists;
        set => SetProperty(ref _isImageLinkExists, value);
    }

    private bool _isAudioLinkExists;
    public bool IsAudioLinkExists
    {
        get => _isAudioLinkExists;
        set => SetProperty(ref _isAudioLinkExists, value);
    }

    private MediaSource? _mediaSource;
    public MediaSource? MediaSource
    {
        get => _mediaSource;
        set => SetProperty(ref _mediaSource, value);
    }

    private bool _isMediaPlayerVisible;
    public bool IsMediaPlayerVisible
    {
        get => _isMediaPlayerVisible;
        set
        {
            SetProperty(ref _isMediaPlayerVisible, value);
            IsNotMediaPlayerVisible = !value;
        }
    }

    private bool _isNotMediaPlayerVisible;
    public bool IsNotMediaPlayerVisible
    {
        get => _isNotMediaPlayerVisible;
        set => SetProperty(ref _isNotMediaPlayerVisible, value);
    }

    private bool _isCommentPageLinkExists;
    public bool IsCommentPageLinkExists
    {
        get => _isCommentPageLinkExists;
        set => SetProperty(ref _isCommentPageLinkExists, value);
    }

    /*
    private bool _isContentBrowserVisible;
    public bool IsContentBrowserVisible
    {
        get
        {
            return _isContentBrowserVisible;
        }
        set
        {
            if (_isContentBrowserVisible == value)
                return;

            _isContentBrowserVisible = value;
            NotifyPropertyChanged(nameof(IsContentBrowserVisible));
        }
    }
    */

    private bool _isToggleInboxAppButtonEnabled;
    public bool IsToggleInboxAppButtonEnabled
    {
        get => _isToggleInboxAppButtonEnabled;
        set => SetProperty(ref _isToggleInboxAppButtonEnabled, value);
    }

    private string? _inboxnboxAppButtonLabel = "Inbox".GetLocalized();
    public string? InboxAppButtonLabel
    {
        get => _inboxnboxAppButtonLabel ?? "Inbox";
        set => SetProperty(ref _inboxnboxAppButtonLabel, value);
    }

    private string _toggleInboxAppButtonIcon = "M19,15H15A3,3 0 0,1 12,18A3,3 0 0,1 9,15H5V5H19M19,3H5C3.89,3 3,3.9 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5A2,2 0 0,0 19,3Z";
    public string ToggleInboxAppButtonIcon
    {
        get => _toggleInboxAppButtonIcon;
        set => SetProperty(ref _toggleInboxAppButtonIcon, value);
    }

    private bool _isShowInboxEntries = true;
    public bool IsShowInboxEntries
    {
        get => _isShowInboxEntries;
        set
        {
            if (SetProperty(ref _isShowInboxEntries, value))
            {
                IsShowAllEntries = !value;
                ToggleInboxAppButtonLabel();
            }
        }
    }

    private bool _isShowAllEntries = false;
    public bool IsShowAllEntries
    {
        get => _isShowAllEntries;
        set 
        {
            if (SetProperty(ref _isShowAllEntries, value))
            {
                IsShowInboxEntries = !value;
                ToggleInboxAppButtonLabel();
            }
        }
    }

    private void ToggleInboxAppButtonLabel()
    {
        if (IsShowAllEntries)
        {
            InboxAppButtonLabel = "All".GetLocalized();
            ToggleInboxAppButtonIcon = "M14.5 11C14.78 11 15 11.22 15 11.5V13H9V11.5C9 11.22 9.22 11 9.5 11H14.5M20 13.55V10H18V13.06C18.69 13.14 19.36 13.31 20 13.55M21 9H3V3H21V9M19 5H5V7H19V5M8.85 19H6V10H4V21H9.78C9.54 20.61 9.32 20.19 9.14 19.75L8.85 19M17 18C16.44 18 16 18.44 16 19S16.44 20 17 20 18 19.56 18 19 17.56 18 17 18M23 19C22.06 21.34 19.73 23 17 23S11.94 21.34 11 19C11.94 16.66 14.27 15 17 15S22.06 16.66 23 19M19.5 19C19.5 17.62 18.38 16.5 17 16.5S14.5 17.62 14.5 19 15.62 21.5 17 21.5 19.5 20.38 19.5 19Z";
        }
        if (IsShowInboxEntries)
        {
            InboxAppButtonLabel = "Inbox".GetLocalized();
            ToggleInboxAppButtonIcon = "M19,15H15A3,3 0 0,1 12,18A3,3 0 0,1 9,15H5V5H19M19,3H5C3.89,3 3,3.9 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5A2,2 0 0,0 19,3Z";
        }
    }

    #endregion

    #region == Flags ==

    private bool _isBackEnabled;
    public bool IsBackEnabled
    {
        get => _isBackEnabled;
        set => SetProperty(ref _isBackEnabled, value);
    }

    private bool _isDebugWindowEnabled = false;
    public bool IsDebugWindowEnabled
    {
        get => _isDebugWindowEnabled;
        set => SetProperty(ref _isDebugWindowEnabled, value);
    }

    private bool _isEntryDetaileVisible = false;
    public bool IsEntryDetailVisible
    {
        get => _isEntryDetaileVisible;
        set => SetProperty(ref _isEntryDetaileVisible, value);
    }

    private bool _isFeedTreeLoaded = false;
    public bool IsFeedTreeLoaded => _isFeedTreeLoaded;

    #endregion

    #region == Errors ==

    // Feed node error obj
    private ErrorObject? _errorObj;
    public ErrorObject? ErrorObj
    {
        get => _errorObj;
        set => SetProperty(ref _errorObj, value);
    }

    private bool _isShowFeedError = false;
    public bool IsShowFeedError
    {
        get => _isShowFeedError;
        set
        {
            SetProperty(ref _isShowFeedError, value);
            IsNotShowFeedError = !value;
        }
    }

    private bool _isNotShowFeedError = true;
    public bool IsNotShowFeedError
    {
        get => _isNotShowFeedError;
        set => SetProperty(ref _isNotShowFeedError, value);
    }

    // Main error
    private ErrorObject? _errorMain;
    public ErrorObject? ErrorMain
    {
        get => _errorMain;
        set => SetProperty(ref _errorMain, value);
    }

    private string? _errorMainTitle;
    public string? ErrorMainTitle
    {
        get => _errorMainTitle;
        set => SetProperty(ref _errorMainTitle, value);
    }

    private string? _errorMainMessage;
    public string? ErrorMainMessage
    {
        get => _errorMainMessage;
        set => SetProperty(ref _errorMainMessage, value);
    }

    private bool _isMainErrorInfoBarVisible = false;
    public bool IsMainErrorInfoBarVisible
    {
        get => _isMainErrorInfoBarVisible;
        set
        {
            if ((value == true) && (ErrorMain != null))
            {
                ErrorMainTitle = ErrorMain.ErrDescription;
                ErrorMainMessage = ErrorMain.ErrText;
            }
            else if (value == false)
            {
                _errorMain = null;
            }

            SetProperty(ref _isMainErrorInfoBarVisible, value);
        }
    }

    #endregion

    #region == Warning ==

    private string? _warningMainTitle;
    public string? WarningMainTitle
    {
        get => _warningMainTitle;
        set => SetProperty(ref _warningMainTitle, value);
    }

    private string? _warningMainMessage;
    public string? WarningMainMessage
    {
        get => _warningMainMessage;
        set => SetProperty(ref _warningMainMessage, value);
    }

    private bool _isMainWarningInfoBarVisible = false;
    public bool IsMainWarningInfoBarVisible
    {
        get => _isMainWarningInfoBarVisible;
        set => SetProperty(ref _isMainWarningInfoBarVisible, value);
    }

    #endregion

    #region == Options ==

    private double _widthLeftPane = 256;
    public double WidthLeftPane
    {
        get => _widthLeftPane;
        set => SetProperty(ref _widthLeftPane, value);
    }

    private double _widthDetailPane = 256;
    public double WidthDetailPane
    {
        get => _widthDetailPane;
        set => SetProperty(ref _widthDetailPane, value);
    }

    #endregion

    #region == Events ==

    public event EventHandler<bool>? ShowWaitDialog;

    //public event EventHandler<string>? DebugOutput;

    #endregion

    #region == Debug Event Window ==

    private readonly StringBuilder DebugEventLogStringBuilder = new();

    private string? _debuEventLog;
    public string? DebugEventLog
    {
        get => _debuEventLog;
        set => SetProperty(ref _debuEventLog, value);
    }

    private readonly Queue<string> debugEvents = new(101);

    public void OnDebugOutput(BaseClient sender, string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return;
        }

        if (!IsDebugWindowEnabled)
        {
            return;
        }

        var que = App.MainWnd?.CurrentDispatcherQueue;
        if (que is not null)
        {
            App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
            {
                DebugEventLogStringBuilder.AppendLine(data);
                DebugEventLog = DebugEventLogStringBuilder.ToString();
            });
        }
    }

    #endregion

    #region == Services ==

    private readonly IFileDialogService _fileDialogService;

    private readonly IDataAccessService _dataAccessService;

    private readonly IFeedClientService _feedClientService;

    private readonly IOpmlService _opmlService;

    #endregion

    private readonly CancellationTokenSource _cts = new();

    public MainViewModel(IFileDialogService fileDialogService, IDataAccessService dataAccessService, IFeedClientService feedClientService, IOpmlService opmlService)
    {
        _fileDialogService = fileDialogService;
        _dataAccessService = dataAccessService;
        _feedClientService = feedClientService;
        _feedClientService.BaseClient.DebugOutput += OnDebugOutput;
        _opmlService = opmlService;

        InitializeFeedTree();
        InitializeDatabase();

        if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
        {
            IsAcrylicSupported = true;
            IsBackdropEnabled = true;
        }
        if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
        {
            IsMicaSupported = true;
            IsBackdropEnabled = true;
        }

#if DEBUG
        //IsDebugWindowEnabled = true;
#else
        IsDebugWindowEnabled = false;
#endif
    }

    #region == Initialization ==

    private void InitializeFeedTree()
    {
        var filePath = Path.Combine(App.AppDataFolder, "Searvies.xml");
        if (RuntimeHelper.IsMSIX)
        {
            filePath = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "Searvies.xml");
        }

        if (File.Exists(filePath))
        {
            var doc = new System.Xml.XmlDocument();

            try
            {
                doc.Load(filePath);

                _services.LoadXmlDoc(doc);

                _isFeedTreeLoaded = true;
            }
            catch (Exception ex)
            {
                ErrorMain = new ErrorObject
                {
                    ErrType = ErrorObject.ErrTypes.XML,
                    ErrCode = "",
                    ErrText = ex.Message,
                    ErrDescription = "Error loading \"Searvies.xml\"",
                    ErrDatetime = DateTime.Now,
                    ErrPlace = "MainViewModel::InitializeFeedTree",
                    ErrPlaceParent = "MainViewModel()"
                };
                IsMainErrorInfoBarVisible = true;

                Debug.WriteLine("Exception while loading service.xml:" + ex);
            }
        }
    }

    private async void InitializeDatabase()
    {
        var filePath = Path.Combine(App.AppDataFolder, "Feeds.db");
        if (RuntimeHelper.IsMSIX)
        {
            filePath = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "Feeds.db");
            //Debug.WriteLine(Windows.Storage.ApplicationData.Current.LocalFolder.Path);
        }

        var res = await Task.Run(()=>_dataAccessService.InitializeDatabase(filePath), _cts.Token);
        if (res.IsError)
        {
            ErrorMain = res.Error;
            IsMainErrorInfoBarVisible = true;

            Debug.WriteLine("SQLite DB init: " + res.Error.ErrText + ": " + res.Error.ErrDescription + " @" + res.Error.ErrPlace + "@" + res.Error.ErrPlaceParent);
        }
    }

    /*
    private void InitializeFeedClient()
    {
        // subscribe to DebugOutput event.
        //_feedClientService.BaseClient.DebugOutput += new BaseClient.ClientDebugOutput(OnDebugOutput);

        InitClientsRecursiveLoop(_services.Children);
    }
    */
    /*
    private void InitClientsRecursiveLoop(ObservableCollection<NodeTree> nt)
    {
        foreach (var c in nt)
        {
            if (c is NodeFeed nf)
            {
                nf.Client = _feedClientService.BaseClient;
                //nf.Client.DebugOutput += new BaseClient.ClientDebugOutput(OnDebugOutput);
            }

            if (c.Children.Count > 0)
            {
                InitClientsRecursiveLoop(c.Children);
            }
        }
    }
    */

    #endregion

    #region == Finalization ==

    public void CleanUp()
    {
        try
        {
            _cts?.Cancel();

            _feedClientService?.BaseClient?.Dispose();

            _cts?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error while Shutdown() : " + ex);
        }
    }

    public void SaveServiceXml()
    {
        // This may be a bad idea.
        if (!IsFeedTreeLoaded)
        {
            return;
        }

        var filePath = Path.Combine(App.AppDataFolder, "Searvies.xml");
        if (RuntimeHelper.IsMSIX)
        {
            filePath = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "Searvies.xml");
        }

        var xdoc = _services.AsXmlDoc();

        xdoc.Save(filePath);
    }

    #endregion

    #region == Entries Refreshing ==

    private async void LoadEntries(NodeTree nt)
    {
        await Task.Run(async () =>
        {
            try
            {
                await LoadEntriesAsync(nt);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadEntriesAwaiter: {ex.Message}");
                App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    (App.Current as App)?.AppendErrorLog("LoadEntriesAwaiter", ex.Message);
                });
            }
        }, _cts.Token);
    }

    // Loads node's all (including children) entries from database.
    private async Task<List<EntryItem>> LoadEntriesAsync(NodeTree nt)
    {
        if (nt == null)
        {
            return [];
        }

        var dispatcher = App.MainWnd?.CurrentDispatcherQueue;
        if (App.MainWnd?.CurrentDispatcherQueue is null)
        {
            return [];
        }

        // don't clear Entries here.

        if (nt is NodeFeed feed)
        {
            if (dispatcher is null)
            {
                return [];
            }
            await dispatcher.EnqueueAsync(() =>
            {
                feed.IsBusy = true;
                if (feed == _selectedTreeViewItem)
                {
                    //EntryArchiveAllCommand.NotifyCanExecuteChanged();
                }
            });
            await Task.Delay(100);

            var res = await Task.Run(()=>_dataAccessService.SelectEntriesByFeedId(feed.Id, feed.IsDisplayUnarchivedOnly), _cts.Token);

            if (res.IsError)
            {
                if (dispatcher is null)
                {
                    return [];
                }
                await dispatcher.EnqueueAsync(() =>
                {
                    // set's error
                    feed.ErrorDatabase = res.Error;
                    
                    if (feed == _selectedTreeViewItem)
                    {
                        // show error
                        ErrorObj = feed.ErrorDatabase;
                        IsShowFeedError = true;
                    }

                    feed.Status = NodeFeed.DownloadStatus.error;

                    Debug.WriteLine(feed.ErrorDatabase.ErrText + ", " + feed.ErrorDatabase.ErrDescription + ", " + feed.ErrorDatabase.ErrPlace);

                    feed.IsBusy = false;
                });

                return [];
            }
            else
            {
                if (dispatcher is null)
                {
                    return [];
                }
                var tmp = new List<EntryItem>();
                await dispatcher.EnqueueAsync(() =>
                {
                    //Debug.WriteLine("LoadEntries success: " + feed.Name);

                    // Clear error
                    //feed.ErrorDatabase = null;

                    // Update the count
                    feed.EntryNewCount = res.UnreadCount;

                    //if (feed.Status != NodeFeed.DownloadStatus.error)
                    //    feed.Status = NodeFeed.DownloadStatus.normal;

                    //feed.List = new ObservableCollection<EntryItem>(res.SelectedEntries);

                    feed.IsBusy = false;

                    // If this is selected Node.
                    if (feed == _selectedTreeViewItem)
                    {
                        // Hide error
                        //DatabaseError = null;
                        //IsShowDatabaseErrorMessage = false;

                        // Load entries.
                        //Entries = res.SelectedEntries;
                        // COPY!! 
                        Entries = new ObservableCollection<EntryItem>(res.SelectedEntries);

                        EntryArchiveAllCommand.NotifyCanExecuteChanged();
                    }
                });
                await Task.Delay(100);

                return res.SelectedEntries;
            }
        }
        else if (nt is NodeFolder folder)
        {
            List<string> tmpList = [];
            if (dispatcher is null)
            {
                return [];
            }
            await dispatcher.EnqueueAsync(() =>
            {
                folder.IsBusy = true;
                if (folder == _selectedTreeViewItem)
                {
                    //EntryArchiveAllCommand.NotifyCanExecuteChanged();
                }
                folder.IsPendingReload = false;
            });
            await Task.Delay(100);

            if (folder.Children.Count > 0)
            {
                tmpList = GetAllFeedIdsFromChildNodes(folder.Children);
            }

            if (tmpList.Count == 0)
            {
                if (dispatcher is null)
                {
                    return [];
                }
                await dispatcher.EnqueueAsync(() =>
                {
                    folder.IsBusy = false;
                });
                return [];
            }

            var res = await Task.Run(()=>_dataAccessService.SelectEntriesByFeedIds(tmpList, folder.IsDisplayUnarchivedOnly), _cts.Token);

            if (res.IsError)
            {
                if (dispatcher is null)
                {
                    return [];
                }
                await dispatcher.EnqueueAsync(() =>
                {
                    // show error
                    ErrorObj = res.Error;

                    if (folder == _selectedTreeViewItem)
                    {
                        IsShowFeedError = true;
                    }

                    folder.IsBusy = false;
                });

                return [];
            }
            else
            {
                if (dispatcher is null)
                {
                    return [];
                }
                await dispatcher.EnqueueAsync(() =>
                {
                    // Clear error
                    //folder.ErrorDatabase = null;

                    // Update the count
                    folder.EntryNewCount = res.UnreadCount;

                    //if (folder.Status != NodeFeed.DownloadStatus.error)
                    //    folder.Status = NodeFeed.DownloadStatus.normal;

                    folder.IsBusy = false;

                    if (folder == _selectedTreeViewItem)
                    {
                        // Load entries.  
                        //Entries = res.SelectedEntries;
                        // COPY!!
                        Entries = new ObservableCollection<EntryItem>(res.SelectedEntries);

                        EntryArchiveAllCommand.NotifyCanExecuteChanged();
                    }
                });
                await Task.Delay(100);

                return res.SelectedEntries;
            }
        }

        return [];
    }

    // gets all children's feed ids.
    private static List<string> GetAllFeedIdsFromChildNodes(ObservableCollection<NodeTree> list)
    {
        var res = new List<string>();

        foreach (var nt in list)
        {
            if (nt is NodeFeed feed)
            {
                res.Add(feed.Id);
            }
            else if (nt is NodeFolder folder)
            {
                res.AddRange(GetAllFeedIdsFromChildNodes(folder.Children));
            }
        }

        return res;
    }

    // update specific feed or feeds in a folder.
    private async Task RefreshFeedAsync()
    {
        if (_selectedTreeViewItem is null)
        {
            return;
        }

        if ((_selectedTreeViewItem is NodeFeed) || _selectedTreeViewItem is NodeFolder)
        {
            await GetEntriesAsync(_selectedTreeViewItem);
        }
    }

    // gets entries recursively and save to db.
    private async Task GetEntriesAsync(NodeTree nt)
    {
        if (nt == null)
        {
            return;
        }

        var dispatcher = App.MainWnd?.CurrentDispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }

        if (nt is NodeFeed feed)
        {
            /*
            // check some conditions.
            if ((feed.Api != ApiTypes.atFeed) || (feed.Client == null))
            {
                return;
            }
            */
            if (feed.Api != ApiTypes.atFeed)
            {
                return;
            }
            if (dispatcher is null)
            {
                return;
            }
            // Update Node Downloading Status
            await dispatcher.EnqueueAsync(() =>
            {
                feed.IsBusy = true;
                feed.Status = NodeFeed.DownloadStatus.downloading;

                // TODO: should I be doing this here? or after receiving the data...
                feed.LastFetched = DateTime.Now;

                if (feed == _selectedTreeViewItem)
                {
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                }
            });

            //
            await Task.Delay(30);

            Debug.WriteLine("Getting Entries from: " + feed.Name);

            // Get Entries from web.
            var resEntries = await _feedClientService.GetEntries(feed.EndPoint, feed.Id, _cts.Token);

            // Check Node exists. Could have been deleted.
            if (feed == null)
            {
                return;
            }

            // Result is HTTP Error
            if (resEntries.IsError)
            {
                if (dispatcher is null)
                {
                    return;
                }
                await dispatcher.EnqueueAsync(() =>
                {
                    // Sets Node Error.
                    feed.ErrorHttp = resEntries.Error;

                    // If Node is selected, show the Error.
                    if (feed == SelectedTreeViewItem)
                    {
                        ErrorObj = feed.ErrorHttp;
                        IsShowFeedError = true;
                    }

                    if (feed.Parent != null)
                    {
                        if (feed.Parent is NodeFolder parentFolder)
                        {
                            MinusAllParentEntryCount(parentFolder, feed.EntryNewCount);
                        }
                    }
                    feed.EntryNewCount = 0;

                    // Update Node Downloading Status
                    feed.Status = NodeFeed.DownloadStatus.error;
                    feed.IsBusy = false;
                });

                return;
            }
            else
            {
                if (dispatcher is null)
                {
                    return;
                }
                await dispatcher.EnqueueAsync(() =>
                {
                    // Clear Node Error
                    feed.ErrorHttp = null;
                    if (feed == SelectedTreeViewItem)
                    {
                        // Hide any Error Message
                        ErrorObj = null;
                        IsShowFeedError = false;
                    }

                    feed.Status = NodeFeed.DownloadStatus.normal;

                    feed.LastFetched = DateTime.Now;

                    feed.Title = resEntries.Title;
                    feed.Description = resEntries.Description;
                    feed.HtmlUri = resEntries.HtmlUri;
                    feed.Updated = resEntries.Updated;

                    feed.IsBusy = false;

                    if (feed == _selectedTreeViewItem)
                    {
                        EntryArchiveAllCommand.NotifyCanExecuteChanged();
                    }
                });

                if (resEntries.Entries.Count > 0)
                {
                    await SaveEntryListAsync(resEntries.Entries, feed);
                }
            }
        }
        else if (nt is NodeFolder folder)
        {
            if (dispatcher is null)
            {
                return;
            }
            await dispatcher.EnqueueAsync(async () =>
            {
                nt.IsBusy = true;
                await Task.Delay(100);
                EntryArchiveAllCommand.NotifyCanExecuteChanged();
            });

            var tasks = new List<Task>();
            
            await RefreshAllFeedsRecursiveLoopAsync(tasks, folder);

            await Task.WhenAll(tasks).ConfigureAwait(true);

            if (dispatcher is null)
            {
                return;
            }
            await dispatcher.EnqueueAsync(async () =>
            {
                nt.IsBusy = false;
                await Task.Delay(100);
                EntryArchiveAllCommand.NotifyCanExecuteChanged();
            });
        }
    }

    // update all feeds.
    private async Task RefreshAllFeedsAsync()
    {
        DebugEventLog = string.Empty;

        var tasks = new List<Task>();
        
        await RefreshAllFeedsRecursiveLoopAsync(tasks, _services);

        if (tasks.Count <= 0)
        {
            return;
        }

        await Task.WhenAll(tasks);

        if (_selectedTreeViewItem is NodeFolder folder)
        {
            if (folder.IsPendingReload)
            {
                await LoadEntriesAsync(folder);
                //folder.IsPendingReload = false;
            }
        }

        await Task.Delay(100);

        App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
        {
            EntryArchiveAllCommand.NotifyCanExecuteChanged();
        });
    }

    // update all feeds recursive loop.
    private async Task<List<Task>> RefreshAllFeedsRecursiveLoopAsync(List<Task> tasks, NodeTree nt)
    {
        if (nt.Children.Count > 0)
        {
            foreach (var c in nt.Children)
            {
                if ((c is NodeEntryCollection) || (c is NodeFeed))
                {
                    if (c is NodeFeed feed)
                    {
                        var now = DateTime.Now;
                        var last = feed.LastFetched;
                        if ((last > now.AddMinutes(-1)) && (last <= now))
                        {
                            Debug.WriteLine("Skipping " + feed.Name + ": " + last.ToString());
                        }
                        else
                        {
                            tasks.Add(Task.Run(async () =>
                            {
                                var list = await GetEntryListAsync(feed).ConfigureAwait(false);

                                if (list.Count > 0)
                                {
                                    var res = await SaveEntryListAsync(list, feed);

                                    await Task.Delay(100);

                                    if (res.Count > 0)
                                    {
                                        // reload entries if selected.
                                        await CheckParentSelectedAndLoadEntriesIfNotBusyAsync(feed); ;
                                    }
                                    else
                                    {
                                        ///
                                        await CheckParentSelectedAndNotifyAsync(feed); ;
                                    }
                                }
                                else
                                {
                                    //
                                    await CheckParentSelectedAndNotifyAsync(feed);
                                }

                                await Task.Delay(100);

                                await CheckParentSelectedAndLoadEntriesIfPendingAsync(feed);

                                App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
                                {
                                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                                });
                            }, _cts.Token));
                        }
                    }
                }

                if (c.Children.Count > 0)
                {
                    await RefreshAllFeedsRecursiveLoopAsync(tasks, c);
                }
            }
        }

        return tasks;
    }

    private async Task CheckParentSelectedAndLoadEntriesIfNotBusyAsync(NodeTree nt)
    {
        if (nt != null)
        {
            if (nt.Parent is NodeFolder parentFolder)
            {
                if (parentFolder == SelectedTreeViewItem)
                {
                    if (parentFolder.IsBusyChildrenCount <= 0)
                    {
                        await LoadEntriesAsync(parentFolder);

                        App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
                        {
                            parentFolder.IsPendingReload = false;
                            EntryArchiveAllCommand.NotifyCanExecuteChanged();
                        });
                    }
                    else
                    {
                        App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
                        {
                            parentFolder.IsPendingReload = true;
                            //EntryArchiveAllCommand.NotifyCanExecuteChanged();
                        });
                    }
                }
                else
                {
                    await CheckParentSelectedAndLoadEntriesIfNotBusyAsync(parentFolder);
                }
            }
        }
    }

    private async Task CheckParentSelectedAndLoadEntriesIfPendingAsync(NodeTree nt)
    {
        if (nt != null)
        {
            if (nt.Parent is NodeFolder parentFolder)
            {
                if (parentFolder == SelectedTreeViewItem)
                {
                    if ((parentFolder.IsPendingReload) && (parentFolder.IsBusyChildrenCount <= 0))
                    {
                        await LoadEntriesAsync(parentFolder);

                        App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
                        {
                            parentFolder.IsPendingReload = false;
                            //EntryArchiveAllCommand.NotifyCanExecuteChanged();
                        });
                    }
                }
                else
                {
                    /*
                    App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
                    {
                        //parentFolder.IsPendingReload = false;
                        EntryArchiveAllCommand.NotifyCanExecuteChanged();
                    });
                    */
                    await CheckParentSelectedAndLoadEntriesIfPendingAsync(parentFolder);
                }
                App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                });
            }
        }
    }

    private async Task CheckParentSelectedAndNotifyAsync(NodeTree nt)
    {
        if (nt != null)
        {
            if (nt.Parent is NodeFolder parentFolder)
            {
                if (parentFolder == SelectedTreeViewItem)
                {
                    if (parentFolder.IsBusyChildrenCount <= 0)
                    {
                    }
                }
                else
                {
                    await CheckParentSelectedAndNotifyAsync(parentFolder);
                }
                App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                });
            }
        }
    }

    // gets entries from web and return the list.
    private async Task<List<EntryItem>> GetEntryListAsync(NodeFeed feed)
    {
        var res = new List<EntryItem>();

        if (feed == null)
        {
            return res;
        }

        var dispatcher = App.MainWnd?.CurrentDispatcherQueue;
        if (dispatcher is null)
        {
            return res;
        }

        // check some conditions.
        if (feed.Api != ApiTypes.atFeed)
        {
            return res;
        }

        if (App.MainWnd is null) {  return res; }
        if (App.MainWnd?.CurrentDispatcherQueue is null) { return res; }

        // Update Node Downloading Status
        if (dispatcher is null)
        {
            return res;
        }
        await dispatcher.EnqueueAsync(() =>
        {
            feed.IsBusy = true;

            //Debug.WriteLine("Getting entries: " + feed.Name);

            feed.Status = NodeFeed.DownloadStatus.downloading;

            // TODO: should I be doing this here? or after receiving the data...
            feed.LastFetched = DateTime.Now;
            
            if (feed == SelectedTreeViewItem)
            {
                EntryArchiveAllCommand.NotifyCanExecuteChanged();
            }
            
        });


        await Task.Delay(30);
        //Debug.WriteLine("Getting Entries from: " + feed.Name);

        // Get Entries from web.
        //var resEntries = await feed.Client.GetEntries(feed.EndPoint, feed.Id);
        var resEntries = await _feedClientService.GetEntries(feed.EndPoint, feed.Id, _cts.Token);

        // Check Node exists. Could have been deleted... but unlikely...
        if (feed == null)
        {
            //feed.IsBusy = false;
            Debug.WriteLine("GetEntryListAsync: feed is null.");
            return res;
        }

        // Result is HTTP Error
        if (resEntries.IsError)
        {
            if (dispatcher is null)
            {
                return res;
            }
            await dispatcher.EnqueueAsync(() =>
            {
                // Sets Node Error.
                feed.ErrorHttp = resEntries.Error;

                // If Node is selected, show the Error.
                if (feed == SelectedTreeViewItem)
                {
                    ErrorObj = feed.ErrorHttp;
                    IsShowFeedError = true;
                }

                if (feed.Parent != null)
                {
                    if (feed.Parent is NodeFolder parentFolder)
                    {
                        MinusAllParentEntryCount(parentFolder, feed.EntryNewCount);
                    }
                }

                // TODO: should I ?
                feed.EntryNewCount = 0;

                // Update Node Downloading Status
                feed.Status = NodeFeed.DownloadStatus.error;

                feed.IsBusy = false;
            });

            return res;
        }
        else
        {
            if (dispatcher is null)
            {
                return res;
            }
            // Result is success.
            await dispatcher.EnqueueAsync(() =>
            {
                //Debug.WriteLine("Getting entries success: " + feed.Name);

                // Clear Node Error
                feed.ErrorHttp = null;

                //fnd.Status = NodeFeed.DownloadStatus.saving;
                feed.Status = NodeFeed.DownloadStatus.normal;

                feed.LastFetched = DateTime.Now;

                feed.Title = resEntries.Title;
                feed.Description = resEntries.Description;
                feed.HtmlUri = resEntries.HtmlUri;
                feed.Updated = resEntries.Updated;

                feed.IsBusy = false;

                if (feed == SelectedTreeViewItem)
                {
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                }
            });

            await Task.Delay(100);

            if (resEntries.Entries.Count > 0)
            {
#pragma warning disable IDE0028
#pragma warning disable IDE0306 
                return new List<EntryItem>(resEntries.Entries);
#pragma warning restore IDE0306 
#pragma warning restore IDE0028

                /*
                App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    feed.List = new ObservableCollection<EntryItem>(resEntries.Entries);

                    feed.EntryCount = feed.List.Count;

                    if (feed == SelectedTreeViewItem)
                        Entries = feed.List;
                });
                */
            }
            else
            {
                return res;
            }
        }
    }

    // save them to database.
    private async Task<List<EntryItem>> SaveEntryListAsync(List<EntryItem> list, NodeFeed feed)
    {
        var res = new List<EntryItem>();

        if (list.Count == 0)
        {
            return res;
        }

        var dispatcher = App.MainWnd?.CurrentDispatcherQueue;
        if (dispatcher is null)
        {
            return res;
        }

        // Update Node Downloading Status
        await dispatcher.EnqueueAsync(() =>
        {
            feed.IsBusy = true;

            // reset errors here.
            feed.ErrorDatabase = null;

            //Debug.WriteLine("Saving entries: " + feed.Name);
            feed.Status = NodeFeed.DownloadStatus.saving;

            if (feed == _selectedTreeViewItem)
            {
                EntryArchiveAllCommand.NotifyCanExecuteChanged();
            }
        });
        await Task.Delay(100);

        //var resInsert = await Task.FromResult(InsertEntriesLock(list));
        var resInsert = await Task.Run(()=>_dataAccessService.InsertEntries(list, feed.Id, feed.Name, feed.Title, feed.Description, feed.Updated, feed.HtmlUri!),_cts.Token);

        // Result is DB Error
        if (resInsert.IsError)
        {
            if (dispatcher is null)
            {
                return res;
            }
            await dispatcher.EnqueueAsync(() =>
            {
                // Sets Node Error.
                feed.ErrorDatabase = resInsert.Error;
                
                // If Node is selected, show the Error.
                if (feed == _selectedTreeViewItem)
                {
                    ErrorObj = feed.ErrorDatabase;
                    IsShowFeedError = true;
                }
                
                feed.Status = NodeFeed.DownloadStatus.error;

                Debug.WriteLine(feed.ErrorDatabase.ErrText + ", " + feed.ErrorDatabase.ErrDescription + ", " + feed.ErrorDatabase.ErrPlace);

                feed.IsBusy = false;
            });

            return res;
        }
        else
        {
            if (resInsert.AffectedCount > 0)
            {
                //var newItems = resInsert.InsertedEntries;
                if (dispatcher is null)
                {
                    return res;
                }
                // Update Node Downloading Status
                await dispatcher.EnqueueAsync(() =>
                {
                    //Debug.WriteLine("Saving entries success: " + feed.Name);

                    //feed.EntryNewCount += newItems.Count;
                    //UpdateNewEntryCount(feed, newItems.Count);
                    UpdateNewEntryCount(feed, resInsert.AffectedCount);

                    if (feed.Status != NodeFeed.DownloadStatus.error)
                    {
                        feed.Status = NodeFeed.DownloadStatus.normal;
                    }

                    feed.IsBusy = false;

                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                });

                await Task.Delay(100);

                if (feed == SelectedTreeViewItem)
                {
                    await LoadEntriesAsync(feed);
                }
            }
            else
            {
                if (dispatcher is null)
                {
                    return res;
                }
                // Update Node Downloading Status
                await dispatcher.EnqueueAsync(() =>
                {
                    //feed.EntryCount = newItems.Count;

                    if (feed.Status != NodeFeed.DownloadStatus.error)
                    {
                        feed.Status = NodeFeed.DownloadStatus.normal;
                    }

                    feed.IsBusy = false;
                    /* not good
                    if (feed == _selectedTreeViewItem)
                    {
                        EntryArchiveAllCommand.NotifyCanExecuteChanged();
                    }
                    */
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                });

                await Task.Delay(100);
            }

            return resInsert.InsertedEntries;
        }
    }

    private static void UpdateNewEntryCount(NodeFeed feed, int newCount)
    {
        if (feed != null)
        {
            if (newCount > 0)
            {
                feed.EntryNewCount += newCount;

                if (feed.Parent is NodeFolder folder)
                {
                    UpdateParentNewEntryCount(folder, newCount);
                }
            }
        }

        /*
        App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
        {

        });
        */
    }

    private static void UpdateParentNewEntryCount(NodeFolder folder, int newCount)
    {
        if (folder != null)
        {
            if (newCount > 0)
            {
                folder.EntryNewCount += newCount;

                if (folder.Parent is NodeFolder parentFolder)
                {
                    UpdateParentNewEntryCount(parentFolder, newCount);
                }
            }
        }

        /*
        App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
        {

        });
        */
    }

    #endregion

    #region == Entries Archiving ==

    private async Task ArchiveAllAsync(NodeTree nd)
    {
        if (nd == null)
        {
            return;
        }

        var dispatcher = App.MainWnd?.CurrentDispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }

        if (nd is NodeFeed feed)
        {
            await dispatcher.EnqueueAsync(() =>
            {
                feed.IsBusy = true;

                if (feed == SelectedTreeViewItem)
                {
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                }

                // TODO: not really saving
                //feed.Status = NodeFeed.DownloadStatus.saving;
            });

            List<string> list =
            [
                feed.Id
            ];

            var res = await Task.Run(() => _dataAccessService.UpdateAllEntriesAsArchived(list), _cts.Token);

            if (res.IsError)
            {
                Debug.WriteLine("ArchiveAllAsync(NodeFeed):" + res.Error.ErrText);
                if (dispatcher is null)
                {
                    return;
                }
                await dispatcher.EnqueueAsync(() =>
                {
                    feed.ErrorDatabase = res.Error;

                    if (feed == SelectedTreeViewItem)
                    {
                        //DatabaseError = (nd as NodeFeed).ErrorDatabase;
                        //IsShowDatabaseErrorMessage = true;
                    }

                    feed.Status = NodeFeed.DownloadStatus.error;

                    feed.IsBusy = false;
                });

                return;
            }
            else
            {
                if (dispatcher is null)
                {
                    return;
                }
                await dispatcher.EnqueueAsync(() =>
                {
                    // Clear error
                    feed.ErrorDatabase = null;
                    /*
                    if (feed.Status != NodeFeed.DownloadStatus.error)
                        feed.Status = NodeFeed.DownloadStatus.normal;
                    */
                    if (res.AffectedCount > 0)
                    {
                        // reset unread count.
                        if (feed.Parent != null)
                        {
                            if (feed.Parent is NodeFolder parentFolder)
                            {
                                MinusAllParentEntryCount(parentFolder, feed.EntryNewCount);
                            }
                        }
                        feed.EntryNewCount = 0;

                        if (feed == SelectedTreeViewItem)
                        {
                            Entries.Clear();
                            // nah
                            if (!feed.IsDisplayUnarchivedOnly)
                            {
                                //LoadEntriesAwaiter(feed);
                                //await LoadEntriesAsync(SelectedTreeViewItem);
                            }
                        }
                    }
                    feed.IsBusy = false;
                });

                if (res.AffectedCount > 0)
                {
                    if (feed == SelectedTreeViewItem)
                    {
                        if (!feed.IsDisplayUnarchivedOnly)
                        {
                            await LoadEntriesAsync(SelectedTreeViewItem);
                            //LoadEntriesAwaiter(feed);
                        }
                    }
                }
                if (dispatcher is null)
                {
                    return;
                }
                await dispatcher.EnqueueAsync(() =>
                {
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                });
            }
        }
        else if (nd is NodeFolder folder)
        {
            if (folder.Children.Count > 0)
            {
                if (dispatcher is null)
                {
                    return;
                }
                await dispatcher.EnqueueAsync(() =>
                {
                    folder.IsBusy = true;// test
                    if (folder == SelectedTreeViewItem)
                    {
                        //Entries.Clear();
                        EntryArchiveAllCommand.NotifyCanExecuteChanged();
                    }
                });

                List<string> tmpList = [];

                tmpList = GetAllFeedIdsFromChildNodes(folder.Children);

                var res = await Task.Run(() => _dataAccessService.UpdateAllEntriesAsArchived(tmpList), _cts.Token);

                if (res.IsError)
                {
                    // TODO:
                    Debug.WriteLine("ArchiveAllAsync(NodeFolder):" + res.Error.ErrText);
                }
                else
                {
                    if (res.AffectedCount > 0)
                    {
                        if (dispatcher is null)
                        {
                            return;
                        }
                        await dispatcher.EnqueueAsync(() =>
                        {
                            if (folder.Parent is NodeFolder parentFolder)
                            {
                                MinusAllParentEntryCount(parentFolder, folder.EntryNewCount);
                            }

                            folder.EntryNewCount = 0;
                            ResetAllEntryCountAtChildNodes(folder.Children);

                            if (folder == SelectedTreeViewItem)
                            {
                                Entries.Clear();
                            }
                            folder.IsBusy = false;// test
                        });

                        if (folder == SelectedTreeViewItem)
                        {
                            if (!folder.IsDisplayUnarchivedOnly)
                            {
                                await LoadEntriesAsync(folder); ;
                                //LoadEntriesAwaiter(folder);
                            }
                        }
                    }
                }
                if (dispatcher is null)
                {
                    return;
                }
                await dispatcher.EnqueueAsync(() =>
                {
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                });
            }
        }
    }

    private static void ResetAllEntryCountAtChildNodes(ObservableCollection<NodeTree> list)
    {
        foreach (var nt in list)
        {
            if (nt is NodeFeed feed)
            {
                feed.EntryNewCount = 0;
            }
            else if (nt is NodeFolder folder)
            {
                folder.EntryNewCount = 0;

                if (folder.Children.Count > 0)
                {
                    ResetAllEntryCountAtChildNodes(folder.Children);
                }
            }
        }
    }

    private static void MinusAllParentEntryCount(NodeFolder folder, int minusCount)
    {
        if (folder is not null)
        {
            if ((minusCount > 0) && (folder.EntryNewCount >= minusCount))
            {
                folder.EntryNewCount -= minusCount;
            }

            if (folder.Parent is not null)
            {
                if (folder.Parent is NodeFolder parentFolder)
                {
                    MinusAllParentEntryCount(parentFolder, minusCount);
                }
            }
        }
    }

    private async void UpdateEntryStatusAsRead(NodeTree nd, FeedEntryItem entry)
    {
        // This may freeze UI in certain situations.
        //await UpdateEntryStatusAsReadAsync(nd,entry);

        //Task.Run(() => UpdateEntryStatusAsReadAsync(nd, entry));
        await Task.Run(async () =>
        {
            try
            {
                await UpdateEntryStatusAsReadAsync(nd, entry);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateEntryStatusAsReadAwaiter: {ex.Message}");
                App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    (App.Current as App)?.AppendErrorLog("UpdateEntryStatusAsReadAwaiter", ex.Message);
                });
            }
        }, _cts.Token);
    }

    private async Task UpdateEntryStatusAsReadAsync(NodeTree nd, FeedEntryItem entry)
    {
        if ((nd == null) || (entry == null))
        {
            return;
        }

        if ((nd is not NodeFeed) && (nd is not NodeFolder))
        {
            return;
        }
        
        var dispatcher = App.MainWnd?.CurrentDispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }

        /*
        App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
        {
            nd.IsBusy = true;
        });
        */

        if ((entry.Status == FeedEntryItem.ReadStatus.rsNewVisited) || entry.Status == FeedEntryItem.ReadStatus.rsNormalVisited)
        {
            return;
        }

        var rs = FeedEntryItem.ReadStatus.rsNewVisited;
        if (entry.IsArchived)
        {
            rs = FeedEntryItem.ReadStatus.rsNormalVisited;
        }

        var res = await Task.Run(() => _dataAccessService.UpdateEntryReadStatus(entry.EntryId, rs), _cts.Token);

        if (res.IsError)
        {
            if (dispatcher is null)
            {
                return;
            }
            await dispatcher.EnqueueAsync(() =>
            {
                Debug.WriteLine("UpdateEntryStatusAsReadAsync:" + res.Error.ErrText);

                if ((nd == null) || (entry == null))
                {
                    return;
                }

                //nd.ErrorDatabase = res.Error;

                if (nd == SelectedTreeViewItem)
                {
                    //DatabaseError = (nd as NodeFeed).ErrorDatabase;
                    //IsShowDatabaseErrorMessage = true;

                    if (nd is NodeFeed feed)
                    {
                        feed.Status = NodeFeed.DownloadStatus.error;
                    }
                }
                //nd.IsBusy = false;
            });

            return;
        }
        else
        {
            if (dispatcher is null)
            {
                return;
            }
            await dispatcher.EnqueueAsync(() =>
            {
                if (entry != null)
                {
                    entry.Status = rs;
                }

                //if (nd != null)
                //    nd.IsBusy = false;
            });
        }
    }

    #endregion

    #region == Feed Treeview commands ==

    [RelayCommand]
    private static void FeedAdd()
    {    
        var shell = App.GetService<ShellPage>();
        shell.NavFrame.Navigate(typeof(FeedAddPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
    }//=> _navigationService.NavigateTo(typeof(FeedAddViewModel).FullName!, null);

    public async void AddFeed(FeedLink feedlink)
    {
        if (feedlink == null)
        {
            return;
        }

        if (IsFeedDupeCheck(feedlink.FeedUri.AbsoluteUri))
        {
            Debug.WriteLine("IsFeedDupeCheck == true:" + feedlink.FeedUri.AbsoluteUri);

            WarningMainTitle = "Specified feed already exists";
            WarningMainMessage = feedlink.FeedUri.AbsoluteUri;
            IsMainWarningInfoBarVisible = true;
            return;
        }

        var resInsert = await Task.Run(()=>_dataAccessService.InsertFeed(feedlink.FeedUri.AbsoluteUri, feedlink.FeedUri, feedlink.Title, feedlink.SiteTitle, "", new DateTime(), feedlink.SiteUri), _cts.Token);

        var dispatcher = App.MainWnd?.CurrentDispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }

        // Result is DB Error
        if (resInsert.IsError)
        {
            Debug.WriteLine("InsertFeed:" + "error");
            if (dispatcher is null)
            {
                return;
            }
            await dispatcher.EnqueueAsync(() =>
            {
                ErrorMain = resInsert.Error;
                IsMainErrorInfoBarVisible = true;
            });
        }
        else
        {
            if (dispatcher is null)
            {
                return;
            }
            await dispatcher.EnqueueAsync(() =>
            {
                NodeFeed a = new(feedlink.Title, feedlink.FeedUri)
                {
                    Title = feedlink.SiteTitle,
                    HtmlUri = feedlink.SiteUri,

                    //Client = _feedClientService.BaseClient
                };
                //a.Client.DebugOutput += new BaseClient.ClientDebugOutput(OnDebugOutput);

                if (SelectedTreeViewItem is null)
                {
                    a.Parent = _services;
                    Services.Insert(0, a);//.Add(a);
                }
                else if (SelectedTreeViewItem is NodeFolder)
                {
                    a.Parent = SelectedTreeViewItem;
                    SelectedTreeViewItem.Children.Add(a);
                    SelectedTreeViewItem.IsExpanded = true;
                }
                else if (SelectedTreeViewItem is NodeFeed)
                {
                    if (SelectedTreeViewItem.Parent != null)
                    {
                        if (SelectedTreeViewItem.Parent is NodeFolder folder)
                        {
                            a.Parent = folder;
                            folder.Children.Add(a);
                            folder.IsExpanded = true;
                        }
                        else
                        {
                            a.Parent = _services;
                            Services.Insert(0, a);//.Add(a);
                        }
                    }
                    else
                    {
                        a.Parent = _services;
                        Services.Insert(0, a);//.Add(a);
                    }
                }
                else
                {
                    return;
                }

                a.IsSelected = true;

                FeedRefreshAllCommand.NotifyCanExecuteChanged();

                _isFeedTreeLoaded = true;
                SaveServiceXml();
            });

        }
    }

    private bool IsFeedDupeCheck(string feedUri)
    {
        return FeedDupeCheckRecursiveLoop(Services, feedUri);
    }

    private static bool FeedDupeCheckRecursiveLoop(ObservableCollection<NodeTree> nt, string feedUri)
    {
        foreach (var c in nt)
        {
            if (c is NodeFeed nf)
            {
                if (nf.EndPoint.AbsoluteUri.Equals(feedUri))
                {
                    return true;
                }
            }

            if (c.Children.Count > 0)
            {
                if (FeedDupeCheckRecursiveLoop(c.Children, feedUri))
                {
                    return true;
                }
            }
        }

        return false;
    }

    [RelayCommand(CanExecute = nameof(CanNodeEdit))]
    private void NodeEdit()
    {
        if (SelectedTreeViewItem is null)
        {
            return;
        }

        var shell = App.GetService<ShellPage>();

        if (SelectedTreeViewItem is NodeFeed)
        {
            FeedToEDit = SelectedTreeViewItem as NodeFeed;

            //_navigationService.NavigateTo(typeof(FeedEditViewModel).FullName!, SelectedTreeViewItem);
            shell.NavFrame.Navigate(typeof(FeedEditPage), SelectedTreeViewItem, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
        }
        else if (SelectedTreeViewItem is NodeFolder)
        {
            FolderToEdit = SelectedTreeViewItem as NodeFolder;

            //_navigationService.NavigateTo(typeof(FolderEditViewModel).FullName!, SelectedTreeViewItem);
            shell.NavFrame.Navigate(typeof(FolderEditPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
        }
    }

    private bool CanNodeEdit()
    {
        return SelectedTreeViewItem is not null;
    }

    public async Task UpdateFeedAsync(NodeFeed feed, string name)
    {
        if (feed is null)
        {
            return;
        }

        if (feed.Name == name)
        {
            return;
        }
        
        var dispatcher = App.MainWnd?.CurrentDispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }

        // update db.
        await dispatcher.EnqueueAsync(() =>
        {
            feed.Name = name;

            feed.IsBusy = true;

            //Debug.WriteLine("Saving entries: " + feed.Name);
            feed.Status = NodeFeed.DownloadStatus.saving;
        });

        //
        var resInsert = await Task.Run(() => _dataAccessService.UpdateFeed(feed.Id, feed.EndPoint, feed.Name, feed.Title, feed.Description, feed.Updated, feed.HtmlUri!), _cts.Token);

        // Result is DB Error
        if (resInsert.IsError)
        {
            if (dispatcher is null)
            {
                return;
            }
            await dispatcher.EnqueueAsync(() =>
            {
                // Sets Node Error.
                feed.ErrorDatabase = resInsert.Error;

                // If Node is selected, show the Error.
                if (feed == _selectedTreeViewItem)
                {
                    ErrorObj = feed.ErrorDatabase;
                    IsShowFeedError = true;
                }

                feed.Status = NodeFeed.DownloadStatus.error;

                Debug.WriteLine(feed.ErrorDatabase.ErrText + ", " + feed.ErrorDatabase.ErrDescription + ", " + feed.ErrorDatabase.ErrPlace);

                feed.IsBusy = false;
            });
        }
        else
        {
            if (dispatcher is null)
            {
                return;
            }
            await dispatcher.EnqueueAsync(() =>
            {
                if (feed.Status != NodeFeed.DownloadStatus.error)
                {
                    feed.Status = NodeFeed.DownloadStatus.normal;
                }

                feed.IsBusy = false;
            });
        }
    }

    [RelayCommand(CanExecute = nameof(CanFolderAdd))]
    private void FolderAdd()
    {
        NodeTree? targetNode = null;
        NameToAddFolder = string.Empty;

        if (SelectedTreeViewItem is null) 
        {
            targetNode = _services;
        }
        else if (SelectedTreeViewItem is NodeFeed feed)
        {
            if (feed.Parent != null)
            {
                targetNode = feed.Parent;
            }
        }
        else if (SelectedTreeViewItem is NodeFolder folder)
        {
            if (folder != null)
            {
                targetNode = folder;
            }
        }

        if (targetNode is not null)
        {
            _targetNodeToAddFolder = targetNode;

            _isFeedTreeLoaded = true;
            var shell = App.GetService<ShellPage>();
            shell.NavFrame.Navigate(typeof(FolderAddPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
            //_navigationService.NavigateTo(typeof(FolderAddViewModel).FullName!, targetNode);
        }
    }

    private static bool CanFolderAdd()
    {
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanFeedRemove))]
    private void NodeRemove()
    {
        if (SelectedTreeViewItem is null)
        {
            return;
        }

        if (SelectedTreeViewItem.IsBusy)
        {
            // TODO: let users know.
            Debug.WriteLine("DeleteNodeTree: IsBusy.");
            return;
        }

        _ = Task.Run(() => NodeRemoveAsync(), _cts.Token);
        //_ = NodeRemoveAsync();
    }

    private async Task NodeRemoveAsync()
    {
        if (SelectedTreeViewItem is null)
        {
            return;
        }

        if (SelectedTreeViewItem.IsBusy)
        {
            // TODO: let users know.
            Debug.WriteLine("DeleteNodeTree: IsBusy.");
            return;
        }

        var dispatcher = App.MainWnd?.CurrentDispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }

        var isShowWaitDialog = false;
        if ((SelectedTreeViewItem is NodeFolder) && (SelectedTreeViewItem.Children.Count > 2))
        {
            // this may take some time, so let us show dialog.
            await dispatcher.EnqueueAsync(() =>
            {
                isShowWaitDialog = true;
                // Show wait dialog.
                ShowWaitDialog?.Invoke(this, true);
            });
        }

        List<NodeTree> nodeToBeDeleted = [];

        await DeleteNodesAsync(SelectedTreeViewItem, nodeToBeDeleted);
        if (dispatcher is null)
        {
            return;
        }
        await dispatcher.EnqueueAsync(() =>
        {
            foreach (var hoge in nodeToBeDeleted)
            {
                if (hoge.Parent != null)
                {
                    hoge.IsBusy = false; // remove self from parent IsBusyChildrenCount

                    // 
                    if (hoge.Parent is NodeFolder parentFolder)
                    {
                        MinusAllParentEntryCount(parentFolder, hoge.EntryNewCount);
                    }

                    SelectedTreeViewItem = null;
                    hoge.Parent.Children.Remove(hoge);
                }
                else
                {
                    //Debug.WriteLine("DeleteNodeTree: (hoge.Parent is null)");

                    SelectedTreeViewItem = null;
                    _services.Children.Remove(hoge);
                }
            }

            Entries.Clear();

            SaveServiceXml();

            if (isShowWaitDialog)
            {
                // Hide wait dialog.
                ShowWaitDialog?.Invoke(this, false);
            }

            FeedRefreshAllCommand.NotifyCanExecuteChanged();

        });

    }

    private async Task DeleteNodesAsync(NodeTree nt, List<NodeTree> nodeToBeDeleted)
    {
        if (nt.IsBusy)
        {
            // TODO: let users know.
            Debug.WriteLine("DeleteNodeTree: IsBusy.");
            return;
        }

        if (!((nt is NodeFolder) || (nt is NodeFeed)))
        {
            return;
        }

        var dispatcher = App.MainWnd?.CurrentDispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }

        if (nt is NodeFeed feed)
        {
            await dispatcher.EnqueueAsync(() =>
            {
                // check status
                if (!((feed.Status == NodeFeed.DownloadStatus.normal) || (feed.Status == NodeFeed.DownloadStatus.error)))
                {
                    return;
                }

                feed.IsBusy = true;
            });

            List<string> ids =
                [
                    feed.Id
                ];

            var resDelete = await Task.Run(() => _dataAccessService.DeleteFeed(feed.Id), _cts.Token);

            if (resDelete.IsError)
            {
                if (dispatcher is null)
                {
                    return;
                }
                await dispatcher.EnqueueAsync(() =>
                {
                    feed.ErrorDatabase = resDelete.Error;

                    if (feed == _selectedTreeViewItem)
                    {
                        ErrorObj = feed.ErrorDatabase;
                        IsShowFeedError = true;
                    }

                    feed.IsBusy = false;

                    return;
                });
            }
            else
            {
                if (dispatcher is null)
                {
                    return;
                }
                await dispatcher.EnqueueAsync(() =>
                {
                    feed.IsBusy = false;
                });

                nodeToBeDeleted.Add(feed);
            }
        }
        else if (nt is NodeFolder folder)
        {
            if (folder.Children.Count > 0)
            {
                foreach (var ndc in folder.Children)
                {
                    await DeleteNodesAsync(ndc, nodeToBeDeleted);
                }
            }

            nodeToBeDeleted.Add(folder);
        }
    }

    private bool CanFeedRemove()
    {
        return SelectedTreeViewItem is not null;
    }

    [RelayCommand(CanExecute = nameof(CanFeedRefreshAll))]
    private async Task FeedRefreshAll()
    {
        //Task.Run(RefreshAllFeedsAsync);
        await RefreshAllFeedsAsync();
    }

    private bool CanFeedRefreshAll()
    {
        return Services.Count > 0;
    }

    [RelayCommand(CanExecute = nameof(CanFeedRefresh))]
    private async Task FeedRefresh()
    {
        //Task.Run(RefreshFeedAsync);
        await RefreshFeedAsync();
    }

    private bool CanFeedRefresh()
    {
        return SelectedTreeViewItem is not null;
    }

    #endregion

    #region == Feed OPML ex/import commands ==

    [RelayCommand(CanExecute = nameof(CanOpmlImport))]
    public void OpmlImport()
    {
        //_ = Task.Run(() => OpmlImportAsync());
        // This is gonna freeze UI.
        //_ = OpmlImportAsync();

        _ = Task.Run(async () => 
        {
            try
            {
                await OpmlImportAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpmlImport: {ex.Message}");
                App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    (App.Current as App)?.AppendErrorLog("OpmlImport", ex.Message);
                });
            }
        }, _cts.Token);
    }

    public async Task OpmlImportAsync()
    {
        var dispatcher = App.MainWnd?.CurrentDispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }

        var hwnd = WindowNative.GetWindowHandle(App.MainWnd);
        var file = await _fileDialogService.GetOpenOpmlFileDialog(hwnd);
        if (file is null)
        {
            return;
        }

        var filepath = file.Path;

        if (!File.Exists(filepath.Trim()))
        {
            return;
        }
        if (dispatcher is null)
        {
            return;
        }
        await dispatcher.EnqueueAsync(() =>
        {
            // Show wait dialog.
            ShowWaitDialog?.Invoke(this, true);
        });

        var doc = new XmlDocument();
        try
        {
            doc.Load(filepath.Trim());

            //MainError = null;
        }
        catch (Exception ex)
        {
            if (dispatcher is null)
            {
                return;
            }
            await dispatcher.EnqueueAsync(() =>
            {
                ErrorMain = new ErrorObject
                {
                    ErrType = ErrorObject.ErrTypes.XML,
                    ErrCode = "",
                    ErrText = ex.Message,
                    ErrDescription = $"Error loading {file.Name}",
                    ErrDatetime = DateTime.Now,
                    ErrPlace = "MainViewModel::InitializeFeedTree",
                    ErrPlaceParent = "MainViewModel()"
                };

                IsMainErrorInfoBarVisible = true;

                // hide wait dialog.
                ShowWaitDialog?.Invoke(this, false);
            });

            Debug.WriteLine("OpmlImportAsync: " + ex);
            return;
        }

        //Opml opmlLoader = new();

        var dummyFolder = _opmlService.LoadOpml(doc);//opmlLoader.LoadOpml(doc);

        if (dummyFolder is not null)
        {
            List<NodeFeed> dupeFeeds = [];

            foreach (var nt in dummyFolder.Children)
            {
                if ((nt is NodeFeed) || (nt is NodeFolder))
                {
                    await OpmlImportProcessNodeChildAsync(nt, dupeFeeds);
                }
            }

            if (dupeFeeds.Count > 0)
            {
                if (dispatcher is null)
                {
                    return;
                }
                await dispatcher.EnqueueAsync(() =>
                {
                    var s = "";
                    foreach (var hoge in dupeFeeds)
                    {
                        hoge.Parent?.Children.Remove(hoge);

                        if (!string.IsNullOrEmpty(s))
                        {
                            s += Environment.NewLine;
                        }
                        s += "Skipped " + hoge.EndPoint;
                    }

                    WarningMainTitle = "One or more feed(s) already exist(s)";
                    WarningMainMessage = s;

                    IsMainWarningInfoBarVisible = true;
                });
            }
            if (dispatcher is null)
            {
                return;
            }
            await dispatcher.EnqueueAsync(() =>
            {
                Services.Insert(0, dummyFolder);
                _isFeedTreeLoaded = true;

                FeedRefreshAllCommand.NotifyCanExecuteChanged();
            });
        }
        if (dispatcher is null)
        {
            return;
        }
        await dispatcher.EnqueueAsync(() =>
        {
            SaveServiceXml();

            // hide wait dialog.
            ShowWaitDialog?.Invoke(this, false);
        });
    }

    private async Task OpmlImportProcessNodeChildAsync(NodeTree nt, List<NodeFeed> dupeFeeds)
    {
        if (nt is NodeFeed feed)
        {
            if (IsFeedDupeCheck(feed.EndPoint.AbsoluteUri))
            {
                // TODO: alart user?
                Debug.WriteLine("IsFeedDupeCheck == true:" + feed.EndPoint.AbsoluteUri);

                dupeFeeds.Add(feed);

                return;
            }
            else
            {
                //
                var resInsert = await Task.Run(()=>_dataAccessService.InsertFeed(feed.Id, feed.EndPoint, feed.Name, feed.Title, "", new DateTime(), feed.HtmlUri!), _cts.Token);
                
                // Result is DB Error
                if (resInsert.IsError)
                {
                    Debug.WriteLine("InsertFeed:" + "error");

                    App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
                    {
                        feed.ErrorDatabase = resInsert.Error;
                    });
                }
                else
                {

                }
                //feed.Client = _feedClientService.BaseClient;
            }
        }
        else if (nt is NodeFolder folder)
        {
            if (folder.Children.Count > 0)
            {
                foreach (var ntc in nt.Children)
                {
                    await OpmlImportProcessNodeChildAsync(ntc, dupeFeeds);
                }
            }
        }
    }

    private static bool CanOpmlImport()
    {
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanOpmlExport))]
    public async Task OpmlExport()
    {
        try
        {
            //Opml opmlWriter = new();
            var xdoc = _opmlService.WriteOpml(_services);//opmlWriter.WriteOpml(_services);

            if (xdoc is null)
            {
                Debug.WriteLine("xdoc is null");
                return;
            }

            var hwnd = WindowNative.GetWindowHandle(App.MainWnd);
            var file = await _fileDialogService.GetSaveOpmlFileDialog(hwnd);

            if (file is null)
            {
                // canceled or something.
                return;
            }

            if (!string.IsNullOrEmpty(file.Path))
            {
                xdoc.Save(file.Path.Trim());
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OpmlExport: {ex.Message}");
            (App.Current as App)?.AppendErrorLog("OpmlExport", ex.Message);
        }
    }

    private static bool CanOpmlExport()
    {
        return true;
    }

    #endregion

    #region == Entry Listview commands  ==

    [RelayCommand(CanExecute = nameof(CanEntryArchiveAll))]
    private void EntryArchiveAll()
    {
        if (SelectedTreeViewItem is null)
        {
            return;
        }

        if (!((SelectedTreeViewItem is NodeFeed) || (SelectedTreeViewItem is NodeFolder)))
        {
            return;
        }

        // This may freeze UI in certain situations.
        //await ArchiveAllAsync(SelectedTreeViewItem);

        //Task.Run(() => ArchiveAllAsync(SelectedTreeViewItem));

        var nt = SelectedTreeViewItem;
        Task.Run(async () => 
        {
            try
            {
                await ArchiveAllAsync(nt);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EntryArchiveAll: {ex.Message}");
                App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    (App.Current as App)?.AppendErrorLog("EntryArchiveAll", ex.Message);
                });
            }
        }, _cts.Token);

    }

    private bool CanEntryArchiveAll()
    {
        if (SelectedTreeViewItem is null)
        {
            return false;
        }

        if (!((SelectedTreeViewItem is NodeFeed) || (SelectedTreeViewItem is NodeFolder)))
        {
            return false;
        }

        if (SelectedTreeViewItem.EntryNewCount <= 0)
        {
            return false;
        }
        
        if (SelectedTreeViewItem.IsBusy)
        {
            //return false;
        }

        if (Entries.Count <= 0)
        {
            return false;
        }

        return true;
    }

    [RelayCommand(CanExecute = nameof(CanEntryViewInternal))]
    private void EntryViewInternal()
    {
        /*
        if (SelectedListViewItem is not null)
            _navigationService.NavigateTo(typeof(EntryDetailsViewModel).FullName!, SelectedListViewItem);
        */
    }
    private bool CanEntryViewInternal()
    {
        return SelectedListViewItem is not null;
    }

    [RelayCommand(CanExecute = nameof(CanEntryViewExternal))]
    private void EntryViewExternal()
    {
        if (SelectedListViewItem is not null)
        {
            if (SelectedListViewItem.AltHtmlUri is not null)
            {
                Task.Run(() => Windows.System.Launcher.LaunchUriAsync(SelectedListViewItem.AltHtmlUri), _cts.Token);
            }
        }
    }

    private bool CanEntryViewExternal()
    {
        if (SelectedListViewItem is null)
        {
            return false;
        }

        if (SelectedListViewItem.AltHtmlUri is null)
        {
            return false;
        }

        return true;
    }

    [RelayCommand(CanExecute = nameof(CanEntryCopyUrl))]
    private void EntryCopyUrl()
    {
        if (SelectedListViewItem is not null)
        {
            if (SelectedListViewItem.AltHtmlUri is not null)
            {
                var data = new DataPackage();
                data.SetText(SelectedListViewItem.AltHtmlUri.AbsoluteUri);
                Clipboard.SetContent(data);
            }
        }
    }

    private bool CanEntryCopyUrl()
    {
        if (SelectedListViewItem is null)
        {
            return false;
        }

        if (SelectedListViewItem.AltHtmlUri is null)
        {
            return false;
        }

        return true;
    }

    [RelayCommand(CanExecute = nameof(CanToggleShowAllEntries))]
    private void ToggleShowAllEntries()
    {
        if (SelectedTreeViewItem is null)
        {
            return;
        }

        IsShowAllEntries = !IsShowAllEntries;
        
        if (SelectedTreeViewItem is NodeFeed feed)
        {
            feed.IsDisplayUnarchivedOnly = !IsShowAllEntries;
            Task.Run(() => LoadEntriesAsync(SelectedTreeViewItem), _cts.Token);
            //await LoadEntriesAsync(SelectedTreeViewItem);
        }
        else if (SelectedTreeViewItem is NodeFolder folder)
        {
            folder.IsDisplayUnarchivedOnly = !IsShowAllEntries;
            Task.Run(() => LoadEntriesAsync(SelectedTreeViewItem), _cts.Token);
            //await LoadEntriesAsync(SelectedTreeViewItem);
        }
    }

    private bool CanToggleShowAllEntries()
    {
        if (SelectedTreeViewItem is null)
        {
            return false;
        }

        return true;
    }

    [RelayCommand(CanExecute = nameof(CanToggleShowInboxEntries))]
    private void ToggleShowInboxEntries()
    {
        if (SelectedTreeViewItem is null)
        {
            return;
        }

        IsShowInboxEntries = !IsShowInboxEntries;

        if (SelectedTreeViewItem is NodeFeed feed)
        {
            feed.IsDisplayUnarchivedOnly = IsShowInboxEntries;
            Task.Run(() => LoadEntriesAsync(SelectedTreeViewItem), _cts.Token);
            //await LoadEntriesAsync(SelectedTreeViewItem);
        }
        else if (SelectedTreeViewItem is NodeFolder folder)
        {
            folder.IsDisplayUnarchivedOnly = IsShowInboxEntries;
            Task.Run(() => LoadEntriesAsync(SelectedTreeViewItem), _cts.Token);
            //await LoadEntriesAsync(SelectedTreeViewItem);
        }
    }

    private bool CanToggleShowInboxEntries()
    {
        if (SelectedTreeViewItem is null)
        {
            return false;
        }

        return true;
    }

    #endregion

    #region == Shell page commands ==

    [RelayCommand]
    private static void MenuFileExit() => App.Current.Exit();

    [RelayCommand]
    private static void MenuSettings()
    {
        var shell = App.GetService<ShellPage>();
        _ = shell.NavFrame.Navigate(typeof(SettingsPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
    } //=> NavigationService.NavigateTo(typeof(SettingsViewModel).FullName!);

    [RelayCommand]
    private static void GoBackToMain()
    {
        var shell = App.GetService<ShellPage>();
        _ = shell.NavFrame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    [RelayCommand]
    private static async Task MenuHelpProjectPage()
    {
        var projectUri = new Uri("https://torum.github.io/FeedDesk/");

        await Windows.System.Launcher.LaunchUriAsync(projectUri);
    }

    [RelayCommand]
    private static async Task MenuHelpProjectGitHub()
    {
        var projectUri = new Uri("https://github.com/torum/FeedDesk");

        await Windows.System.Launcher.LaunchUriAsync(projectUri);
    }

    #endregion

    #region == Other command methods ==

    // Sets uri source to MediaPlayerElement for playback.
    [RelayCommand]
    private void DownloadAudioFile()
    {
        if (SelectedListViewItem is null)
        {
            return;
        }

        if (SelectedListViewItem.AudioUri != null)
        {
            IsMediaPlayerVisible = true;
            MediaSource = MediaSource.CreateFromUri(SelectedListViewItem.AudioUri);
        }
        else
        {
            IsMediaPlayerVisible = false;
            MediaSource = null;
        }
    }

    [RelayCommand]
    private void CopyAudioFileUrlToClipboard()
    {
        if (SelectedListViewItem is null)
        {
            return;
        }

        if (SelectedListViewItem.AudioUri != null)
        {
            var data = new DataPackage();
            data.SetText(SelectedListViewItem.AudioUri.AbsoluteUri);
            Clipboard.SetContent(data);
        }
    }

    [RelayCommand]
    private void CloseMediaPlayer()
    {
        IsMediaPlayerVisible = false;
        MediaSource = null;
    }

    #endregion

}
