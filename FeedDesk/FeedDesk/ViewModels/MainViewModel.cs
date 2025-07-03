﻿using System.Collections.ObjectModel;
using System.Xml;
using XmlClients.Core.Contracts.Services;
using XmlClients.Core.Helpers;
using XmlClients.Core.Models;
using XmlClients.Core.Models.Clients;
using XmlClients.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FeedDesk.Contracts.Services;
using FeedDesk.Contracts.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Core;
using WinRT.Interop;

namespace FeedDesk.ViewModels;

public partial class MainViewModel : ObservableRecipient, INavigationAware
{

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
                /*

                */

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
                        Entries = new ObservableCollection<EntryItem>();
                        
                        LoadEntriesAwaiter(nfeed);
                    }
                    else
                    {
                        // TODO: 
                        Entries = new ObservableCollection<EntryItem>();
                    }
                }
                else if (_selectedTreeViewItem is NodeFolder folder)
                {
                    IsShowInboxEntries = folder.IsDisplayUnarchivedOnly;

                    Entries = new ObservableCollection<EntryItem>();

                    LoadEntriesAwaiter(folder);
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
    /*
    private TreeViewNode? _selectedTreeViewNode;
    public TreeViewNode? SelectedTreeViewNode
    {
        get => _selectedTreeViewNode;
        set => SetProperty(ref _selectedTreeViewNode, value);
    }
    */

    private string _selectedServiceName = "";
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

    private ObservableCollection<EntryItem> _entries = new();
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
                    UpdateEntryStatusAsReadAwaiter(SelectedTreeViewItem!, _selectedListViewItem);
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

    private string _inboxnboxAppButtonLabel = "Inbox".GetLocalized();
    public string InboxAppButtonLabel
    {
        get => _inboxnboxAppButtonLabel;
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

    #region == Events ==

    public event EventHandler<bool>? ShowWaitDialog;

    //public event EventHandler<string>? DebugOutput;

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

        if (!App.CurrentDispatcherQueue.HasThreadAccess)
        {
            App.CurrentDispatcherQueue.TryEnqueue(() =>
            {
                OnDebugOutput(sender, data);
            });
            return;
        }

        debugEvents.Enqueue(data);

        if (debugEvents.Count > 100)
        {
            debugEvents.Dequeue();
        }

        DebugEventLog = string.Join('\n', debugEvents.Reverse());

        /*
        if (!App.CurrentDispatcherQueue.HasThreadAccess)
        {
            App.CurrentDispatcherQueue.TryEnqueue(() =>
            {
                DebugOutput?.Invoke(this, Environment.NewLine + data + Environment.NewLine + Environment.NewLine);
            });
            return;
        }
        */

        //IsDebugTextHasText = true;
    }

    //public delegate void DebugClearEventHandler();
    //public event DebugClearEventHandler? DebugClear;

    #endregion

    #region == Services ==

    private readonly INavigationService _navigationService;

    private readonly IFileDialogService _fileDialogService;

    private readonly IDataAccessService _dataAccessService;

    private readonly IFeedClientService _feedClientService;

    private readonly IOpmlService _opmlService;

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

    public MainViewModel(INavigationService navigationService, IFileDialogService fileDialogService, IDataAccessService dataAccessService, IFeedClientService feedClientService, IOpmlService opmlService)
    {
        _navigationService = navigationService;
        _navigationService.Navigated += OnNavigated;
        _fileDialogService = fileDialogService;
        _dataAccessService = dataAccessService;
        _feedClientService = feedClientService;
        _feedClientService.BaseClient.DebugOutput += OnDebugOutput;
        _opmlService = opmlService;
        
        InitializeFeedTree();
        InitializeDatabase();
        InitializeFeedClient();

        //IsDebugWindowEnabled = true;

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

        var res = await Task.FromResult(_dataAccessService.InitializeDatabase(filePath));
        if (res.IsError)
        {
            ErrorMain = res.Error;
            IsMainErrorInfoBarVisible = true;

            Debug.WriteLine("SQLite DB init: " + res.Error.ErrText + ": " + res.Error.ErrDescription + " @" + res.Error.ErrPlace + "@" + res.Error.ErrPlaceParent);
        }
    }

    private void InitializeFeedClient()
    {
        // subscribe to DebugOutput event.
        //_feedClientService.BaseClient.DebugOutput += new BaseClient.ClientDebugOutput(OnDebugOutput);

        InitClientsRecursiveLoop(_services.Children);
    }

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

    #endregion

    #region == INavigationService ==

    private void OnNavigated(object sender, NavigationEventArgs e) => IsBackEnabled = _navigationService.CanGoBack;

    public void OnNavigatedTo(object parameter)
    {

    }

    public void OnNavigatedFrom()
    {

    }

    #endregion

    #region == Finalization ==
    public void CleanUp()
    {
        try
        {
            _feedClientService?.BaseClient?.Dispose();
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

    private void LoadEntriesAwaiter(NodeTree nt)
    {
        /*
        try
        {
            _= await LoadEntriesAsync(nt).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadEntriesAwaiter: {ex.Message}");
            (App.Current as App)?.AppendErrorLog("LoadEntriesAwaiter", ex.Message);
        }
        */

        //Task.Run(() => LoadEntriesAsync(nd).ConfigureAwait(false));
        Task.Run(async () =>
        {
            try
            {
                await LoadEntriesAsync(nt).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadEntriesAwaiter: {ex.Message}");
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    (App.Current as App)?.AppendErrorLog("LoadEntriesAwaiter", ex.Message);
                });
            }
        });
    }

    // Loads node's all (including children) entries from database.
    private async Task<List<EntryItem>> LoadEntriesAsync(NodeTree nt)
    {
        if (nt == null)
        {
            return new List<EntryItem>();
        }

        // don't clear Entries here.

        if (nt is NodeFeed feed)
        {
            App.CurrentDispatcherQueue?.TryEnqueue(() =>
            {
                feed.IsBusy = true;
                if (feed == _selectedTreeViewItem)
                {
                    //EntryArchiveAllCommand.NotifyCanExecuteChanged();
                }
            });
            await Task.Delay(100);

            var res = await Task.FromResult(_dataAccessService.SelectEntriesByFeedId(feed.Id, feed.IsDisplayUnarchivedOnly));

            if (res.IsError)
            {
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
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

                return new List<EntryItem>();
            }
            else
            {
                var tmp = new List<EntryItem>();
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
            List<string> tmpList = new();

            App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    folder.IsBusy = false;
                });
                return new List<EntryItem>();
            }

            var res = await Task.FromResult(_dataAccessService.SelectEntriesByFeedIds(tmpList, folder.IsDisplayUnarchivedOnly));

            if (res.IsError)
            {
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    // show error
                    ErrorObj = res.Error;

                    if (folder == _selectedTreeViewItem)
                    {
                        IsShowFeedError = true;
                    }

                    folder.IsBusy = false;
                });

                return new List<EntryItem>();
            }
            else
            {
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
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

        return new List<EntryItem>();
    }

    // gets all children's feed ids.
    private List<string> GetAllFeedIdsFromChildNodes(ObservableCollection<NodeTree> list)
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
            await GetEntriesAsync(_selectedTreeViewItem).ConfigureAwait(false);
        }
    }

    // gets entries recursively and save to db.
    private async Task GetEntriesAsync(NodeTree nt)
    {
        if (nt == null)
        {
            return;
        }

        if (nt is NodeFeed feed)
        {
            // check some conditions.
            if ((feed.Api != ApiTypes.atFeed) || (feed.Client == null))
            {
                return;
            }

            // Update Node Downloading Status
            App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
            await Task.Delay(100);

            //Debug.WriteLine("Getting Entries from: " + feed.Name);

            // Get Entries from web.
            var resEntries = await feed.Client.GetEntries(feed.EndPoint, feed.Id);

            // Check Node exists. Could have been deleted.
            if (feed == null)
            {
                return;
            }

            // Result is HTTP Error
            if (resEntries.IsError)
            {
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
            //
            App.CurrentDispatcherQueue?.TryEnqueue(() =>
            {
                nt.IsBusy = true;
            });
            await Task.Delay(100);
            App.CurrentDispatcherQueue?.TryEnqueue(() =>
            {
                EntryArchiveAllCommand.NotifyCanExecuteChanged();
            });

            var tasks = new List<Task>();
            
            await RefreshAllFeedsRecursiveLoopAsync(tasks, folder).ConfigureAwait(false); ;

            await Task.WhenAll(tasks).ConfigureAwait(true);

            //
            App.CurrentDispatcherQueue?.TryEnqueue(() =>
            {
                nt.IsBusy = false;
            });
            await Task.Delay(100);

            App.CurrentDispatcherQueue?.TryEnqueue(() =>
            {
                EntryArchiveAllCommand.NotifyCanExecuteChanged();
            });
        }
    }

    // update all feeds.
    private async Task RefreshAllFeedsAsync()
    {
        var tasks = new List<Task>();
        
        await RefreshAllFeedsRecursiveLoopAsync(tasks, _services).ConfigureAwait(false);

        await Task.WhenAll(tasks).ConfigureAwait(false);

        if (_selectedTreeViewItem is NodeFolder folder)
        {
            if (folder.IsPendingReload)
            {
                await LoadEntriesAsync(folder).ConfigureAwait(false); ;
                //folder.IsPendingReload = false;
            }
        }

        await Task.Delay(100);

        App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
                        tasks.Add(Task.Run(async () =>
                        {
                            //await Task.Delay(100);

                            var now = DateTime.Now;
                            var last = feed.LastFetched;

                            if ((last > now.AddMinutes(-1)) && (last <= now))
                            {
                                //Debug.WriteLine("Skipping " + feed.Name + ": " + last.ToString());
                            }
                            else
                            {
                                var list = await GetEntryListAsync(feed).ConfigureAwait(false);

                                if (list.Count > 0)
                                {
                                    var res = await SaveEntryListAsync(list, feed).ConfigureAwait(false);

                                    await Task.Delay(100);

                                    if (res.Count > 0)
                                    {
                                        // reload entries if selected.
                                        await CheckParentSelectedAndLoadEntriesIfNotBusyAsync(feed).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        ///
                                        await CheckParentSelectedAndNotify(feed).ConfigureAwait(false);
                                    }
                                }
                                else
                                {
                                    //
                                    await CheckParentSelectedAndNotify(feed).ConfigureAwait(false);
                                }

                                await Task.Delay(100);

                                await CheckParentSelectedAndLoadEntriesIfPendingAsync(feed).ConfigureAwait(false);

                                App.CurrentDispatcherQueue?.TryEnqueue(() =>
                                {
                                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                                });
                            }
                        }));
                    }
                }

                if (c.Children.Count > 0)
                {
                    await RefreshAllFeedsRecursiveLoopAsync(tasks, c).ConfigureAwait(false);
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
                        await LoadEntriesAsync(parentFolder).ConfigureAwait(false);

                        App.CurrentDispatcherQueue?.TryEnqueue(() =>
                        {
                            parentFolder.IsPendingReload = false;
                            EntryArchiveAllCommand.NotifyCanExecuteChanged();
                        });
                    }
                    else
                    {
                        App.CurrentDispatcherQueue?.TryEnqueue(() =>
                        {
                            parentFolder.IsPendingReload = true;
                            //EntryArchiveAllCommand.NotifyCanExecuteChanged();
                        });
                    }
                }
                else
                {
                    await CheckParentSelectedAndLoadEntriesIfNotBusyAsync(parentFolder).ConfigureAwait(false);
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
                        await LoadEntriesAsync(parentFolder).ConfigureAwait(false);

                        App.CurrentDispatcherQueue?.TryEnqueue(() =>
                        {
                            parentFolder.IsPendingReload = false;
                            //EntryArchiveAllCommand.NotifyCanExecuteChanged();
                        });
                    }
                }
                else
                {
                    /*
                    App.CurrentDispatcherQueue?.TryEnqueue(() =>
                    {
                        //parentFolder.IsPendingReload = false;
                        EntryArchiveAllCommand.NotifyCanExecuteChanged();
                    });
                    */
                    await CheckParentSelectedAndLoadEntriesIfPendingAsync(parentFolder).ConfigureAwait(false);
                }
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                });
            }
        }
    }

    private async Task CheckParentSelectedAndNotify(NodeTree nt)
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
                    await CheckParentSelectedAndNotify(parentFolder).ConfigureAwait(false);
                }
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
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

        // check some conditions.
        if ((feed.Api != ApiTypes.atFeed) || (feed.Client == null))
        {
            return res;
        }

        // Update Node Downloading Status
        App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
        //
        await Task.Delay(100);

        // Get Entries from web.
        var resEntries = await feed.Client.GetEntries(feed.EndPoint, feed.Id);

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
            App.CurrentDispatcherQueue?.TryEnqueue(() =>
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

            return res;
        }
        else
        {
            // Result is success.

            App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
            //
            await Task.Delay(100);

            if (resEntries.Entries.Count > 0)
            {
                return new List<EntryItem>(resEntries.Entries);

                /*
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
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

        // Update Node Downloading Status
        App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
        //
        await Task.Delay(100);

        //var resInsert = await Task.FromResult(InsertEntriesLock(list));
        var resInsert = _dataAccessService.InsertEntries(list, feed.Id, feed.Name, feed.Title, feed.Description, feed.Updated, feed.HtmlUri!);

        // Result is DB Error
        if (resInsert.IsError)
        {
            App.CurrentDispatcherQueue?.TryEnqueue(() =>
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

                // Update Node Downloading Status
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
                //
                await Task.Delay(100);

                if (feed == SelectedTreeViewItem)
                {
                    await LoadEntriesAsync(feed).ConfigureAwait(false);
                }
            }
            else
            {
                // Update Node Downloading Status
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
                //
                await Task.Delay(100);
            }

            return resInsert.InsertedEntries;
        }
    }

    private void UpdateNewEntryCount(NodeFeed feed, int newCount)
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
        App.CurrentDispatcherQueue?.TryEnqueue(() =>
        {

        });
        */
    }

    private void UpdateParentNewEntryCount(NodeFolder folder, int newCount)
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
        App.CurrentDispatcherQueue?.TryEnqueue(() =>
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

        if (nd is NodeFeed feed)
        {
            App.CurrentDispatcherQueue?.TryEnqueue(() =>
            {
                feed.IsBusy = true;

                if (feed == SelectedTreeViewItem)
                {
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                }

                // TODO: not really saving
                //feed.Status = NodeFeed.DownloadStatus.saving;
            });

            List<string> list = new()
            {
                feed.Id
            };

            var res = await Task.FromResult(_dataAccessService.UpdateAllEntriesAsArchived(list));

            if (res.IsError)
            {
                Debug.WriteLine("ArchiveAllAsync(NodeFeed):" + res.Error.ErrText);

                App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
                                //await LoadEntriesAsync(SelectedTreeViewItem).ConfigureAwait(false);
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
                            await LoadEntriesAsync(SelectedTreeViewItem).ConfigureAwait(false);
                            //LoadEntriesAwaiter(feed);
                        }
                    }
                }
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                });
            }
        }
        else if (nd is NodeFolder folder)
        {
            if (folder.Children.Count > 0)
            {
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    folder.IsBusy = true;// test
                    if (folder == SelectedTreeViewItem)
                    {
                        //Entries.Clear();
                        EntryArchiveAllCommand.NotifyCanExecuteChanged();
                    }
                });

                List<string> tmpList = new();

                tmpList = GetAllFeedIdsFromChildNodes(folder.Children);

                var res = await Task.FromResult(_dataAccessService.UpdateAllEntriesAsArchived(tmpList));

                if (res.IsError)
                {
                    // TODO:
                    Debug.WriteLine("ArchiveAllAsync(NodeFolder):" + res.Error.ErrText);
                }
                else
                {
                    if (res.AffectedCount > 0)
                    {
                        App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
                                await LoadEntriesAsync(folder).ConfigureAwait(false); ;
                                //LoadEntriesAwaiter(folder);
                            }
                        }
                    }
                }

                App.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                });
            }
        }
    }

    private void ResetAllEntryCountAtChildNodes(ObservableCollection<NodeTree> list)
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

    private void MinusAllParentEntryCount(NodeFolder folder, int minusCount)
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

    private void UpdateEntryStatusAsReadAwaiter(NodeTree nd, FeedEntryItem entry)
    {
        // This may freeze UI in certain situations.
        //await UpdateEntryStatusAsReadAsync(nd,entry).ConfigureAwait(false);

        //Task.Run(() => UpdateEntryStatusAsReadAsync(nd, entry).ConfigureAwait(false));
        Task.Run(async () =>
        {
            try
            {
                await UpdateEntryStatusAsReadAsync(nd, entry).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateEntryStatusAsReadAwaiter: {ex.Message}");
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    (App.Current as App)?.AppendErrorLog("UpdateEntryStatusAsReadAwaiter", ex.Message);
                });
            }
        });
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

        /*
        App.CurrentDispatcherQueue?.TryEnqueue(() =>
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

        var res = await Task.FromResult(_dataAccessService.UpdateEntryReadStatus(entry.EntryId, rs));

        if (res.IsError)
        {
            App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
            App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
    private void FeedAdd() => _navigationService.NavigateTo(typeof(FeedAddViewModel).FullName!, null);

    public void AddFeed(FeedLink feedlink)
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

        var resInsert = _dataAccessService.InsertFeed(feedlink.FeedUri.AbsoluteUri, feedlink.FeedUri, feedlink.Title, feedlink.SiteTitle, "", new DateTime(), feedlink.SiteUri);

        // Result is DB Error
        if (resInsert.IsError)
        {
            Debug.WriteLine("InsertFeed:" + "error");

            App.CurrentDispatcherQueue?.TryEnqueue(() =>
            {
                ErrorMain = resInsert.Error;
                IsMainErrorInfoBarVisible = true;
            });
        }
        else
        {
            App.CurrentDispatcherQueue?.TryEnqueue(() =>
            {
                NodeFeed a = new(feedlink.Title, feedlink.FeedUri)
                {
                    Title = feedlink.SiteTitle,
                    HtmlUri = feedlink.SiteUri,

                    Client = _feedClientService.BaseClient
                };
                a.Client.DebugOutput += new BaseClient.ClientDebugOutput(OnDebugOutput);

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

    private bool FeedDupeCheckRecursiveLoop(ObservableCollection<NodeTree> nt, string feedUri)
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

        if (SelectedTreeViewItem is NodeFeed)
        {
            _navigationService.NavigateTo(typeof(FeedEditViewModel).FullName!, SelectedTreeViewItem);
        }
        else if (SelectedTreeViewItem is NodeFolder)
        {
            _navigationService.NavigateTo(typeof(FolderEditViewModel).FullName!, SelectedTreeViewItem);
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

        // update db.
        App.CurrentDispatcherQueue?.TryEnqueue(() =>
        {
            feed.Name = name;

            feed.IsBusy = true;

            //Debug.WriteLine("Saving entries: " + feed.Name);
            feed.Status = NodeFeed.DownloadStatus.saving;
        });

        //
        var resInsert = await Task.FromResult(_dataAccessService.UpdateFeed(feed.Id,feed.EndPoint,feed.Name,feed.Title,feed.Description,feed.Updated,feed.HtmlUri!));

        // Result is DB Error
        if (resInsert.IsError)
        {
            App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
            App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
            _isFeedTreeLoaded = true;
            _navigationService.NavigateTo(typeof(FolderAddViewModel).FullName!, targetNode);
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

        _ = Task.Run(() => NodeRemoveAsync().ConfigureAwait(false));
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

        var IsShowWaitDialog = false;
        if ((SelectedTreeViewItem is NodeFolder) && (SelectedTreeViewItem.Children.Count > 2))
        {
            // this may take some time, so let us show dialog.
            App.CurrentDispatcherQueue?.TryEnqueue(() =>
            {
                IsShowWaitDialog = true;
                // Show wait dialog.
                ShowWaitDialog?.Invoke(this, true);
            });
        }

        List<NodeTree> nodeToBeDeleted = new();

        await DeleteNodesAsync(SelectedTreeViewItem, nodeToBeDeleted);

        App.CurrentDispatcherQueue?.TryEnqueue(() =>
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

                    hoge.Parent.Children.Remove(hoge);
                }
                else
                {
                    //Debug.WriteLine("DeleteNodeTree: (hoge.Parent is null)");
                    _services.Children.Remove(hoge);
                }
            }

            Entries.Clear();

            SaveServiceXml();

            if (IsShowWaitDialog)
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

        if (nt is NodeFeed feed)
        {
            App.CurrentDispatcherQueue?.TryEnqueue(() =>
            {
                // check status
                if (!((feed.Status == NodeFeed.DownloadStatus.normal) || (feed.Status == NodeFeed.DownloadStatus.error)))
                {
                    return;
                }

                feed.IsBusy = true;
            });

            List<string> ids = new()
                {
                    feed.Id
                };

            var resDelete = await Task.FromResult(_dataAccessService.DeleteFeed(feed.Id));

            if (resDelete.IsError)
            {
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
    private void FeedRefreshAll()
    {
        _ = RefreshAllFeedsAsync();
    }

    private bool CanFeedRefreshAll()
    {
        return Services.Count > 0;
    }

    [RelayCommand(CanExecute = nameof(CanFeedRefresh))]
    private void FeedRefresh()
    {
        _ = RefreshFeedAsync();
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
        //_ = Task.Run(() => OpmlImportAsync().ConfigureAwait(false));
        // This is gonna freeze UI.
        //_ = OpmlImportAsync();

        _ = Task.Run(async () => 
        {
            try
            {
                await OpmlImportAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpmlImport: {ex.Message}");
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    (App.Current as App)?.AppendErrorLog("OpmlImport", ex.Message);
                });
            }
        });
    }

    public async Task OpmlImportAsync()
    {
        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
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

        App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
            App.CurrentDispatcherQueue?.TryEnqueue(() =>
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
            List<NodeFeed> dupeFeeds = new();

            foreach (var nt in dummyFolder.Children)
            {
                if ((nt is NodeFeed) || (nt is NodeFolder))
                {
                    await OpmlImportProcessNodeChild(nt, dupeFeeds);
                }
            }

            if (dupeFeeds.Count > 0)
            {
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
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

            App.CurrentDispatcherQueue?.TryEnqueue(() =>
            {
                Services.Insert(0, dummyFolder);
                _isFeedTreeLoaded = true;

                FeedRefreshAllCommand.NotifyCanExecuteChanged();
            });
        }

        App.CurrentDispatcherQueue?.TryEnqueue(() =>
        {
            SaveServiceXml();

            // hide wait dialog.
            ShowWaitDialog?.Invoke(this, false);
        });
    }

    private async Task OpmlImportProcessNodeChild(NodeTree nt, List<NodeFeed> dupeFeeds)
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
                var resInsert = await Task.FromResult(_dataAccessService.InsertFeed(feed.Id, feed.EndPoint, feed.Name, feed.Title, "", new DateTime(), feed.HtmlUri!));
                
                // Result is DB Error
                if (resInsert.IsError)
                {
                    Debug.WriteLine("InsertFeed:" + "error");

                    App.CurrentDispatcherQueue?.TryEnqueue(() =>
                    {
                        feed.ErrorDatabase = resInsert.Error;
                    });
                }
                else
                {

                }
                feed.Client = _feedClientService.BaseClient;
            }
        }
        else if (nt is NodeFolder folder)
        {
            if (folder.Children.Count > 0)
            {
                foreach (var ntc in nt.Children)
                {
                    await OpmlImportProcessNodeChild(ntc, dupeFeeds);
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

            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
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
        //await ArchiveAllAsync(SelectedTreeViewItem).ConfigureAwait(false);

        //Task.Run(() => ArchiveAllAsync(SelectedTreeViewItem).ConfigureAwait(false));

        var nt = SelectedTreeViewItem;
        Task.Run(async () => 
        {
            try
            {
                await ArchiveAllAsync(nt).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EntryArchiveAll: {ex.Message}");
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    (App.Current as App)?.AppendErrorLog("EntryArchiveAll", ex.Message);
                });
            }
        });

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
                Task.Run(() => Windows.System.Launcher.LaunchUriAsync(SelectedListViewItem.AltHtmlUri));
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
            Task.Run(() => LoadEntriesAsync(SelectedTreeViewItem).ConfigureAwait(false));
            //await LoadEntriesAsync(SelectedTreeViewItem).ConfigureAwait(false);
        }
        else if (SelectedTreeViewItem is NodeFolder folder)
        {
            folder.IsDisplayUnarchivedOnly = !IsShowAllEntries;
            Task.Run(() => LoadEntriesAsync(SelectedTreeViewItem).ConfigureAwait(false));
            //await LoadEntriesAsync(SelectedTreeViewItem).ConfigureAwait(false);
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
            Task.Run(() => LoadEntriesAsync(SelectedTreeViewItem).ConfigureAwait(false));
            //await LoadEntriesAsync(SelectedTreeViewItem).ConfigureAwait(false);
        }
        else if (SelectedTreeViewItem is NodeFolder folder)
        {
            folder.IsDisplayUnarchivedOnly = IsShowInboxEntries;
            Task.Run(() => LoadEntriesAsync(SelectedTreeViewItem).ConfigureAwait(false));
            //await LoadEntriesAsync(SelectedTreeViewItem).ConfigureAwait(false);
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
