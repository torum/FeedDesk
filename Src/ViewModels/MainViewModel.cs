using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using FeedDesk.Helpers;
using FeedDesk.Models;
using FeedDesk.Services;
using FeedDesk.Services.Contracts;
using FeedDesk.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Core;
using WinRT;
using WinRT.Interop;

namespace FeedDesk.ViewModels;

public sealed partial class MainViewModel : ObservableRecipient, IDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly StringBuilder _debugEventLogStringBuilder = new();
    private CancellationTokenSource _ctsForSelectedTreeViewItem = new();
    private volatile bool _isShuttingDown;
    private readonly Lock _activeRefreshTasksLock = new();
    private readonly HashSet<Task> _activeRefreshTasks = [];

    private readonly IFileDialogService _fileDialogService;
    private readonly IDataAccessService _dataAccessService;
    private readonly IOpmlService _opmlService;
    private readonly IDispatcherService _dispatcherService;
    //private readonly IFeedRefreshService _feedRefreshService;
    private readonly IFeedTreePersistenceService _feedTreePersistenceService;
    private readonly IFeedClientService _feedClientService;

    #region == Events ==

    public event EventHandler<bool>? ShowWaitDialog;

    #endregion

    private readonly IConfigurationService _configurationService;

    public MainViewModel(IFileDialogService fileDialogService, IDataAccessService dataAccessService, IFeedClientService feedClientService, IFeedTreePersistenceService feedTreePersistenceService, IOpmlService opmlService, IDispatcherService dispatcherService, IConfigurationService configurationService)
    {
        _fileDialogService = fileDialogService;
        _dataAccessService = dataAccessService;
        _feedClientService = feedClientService;
        //_feedRefreshService = feedRefreshService; , IFeedRefreshService feedRefreshService
        _opmlService = opmlService;
        _dispatcherService = dispatcherService;
        _feedTreePersistenceService = feedTreePersistenceService;
        _configurationService = configurationService;

        //_feedClientService.BaseClient.DebugOutput += OnDebugOutput;

        _cts = (App.Current as App)?.AppCancellationToken ?? new();

        var config = _configurationService.CurrentConfiguration;
        WidthLeftPane = config.WidthLeftPane;
        WidthListViewPane = config.WidthListViewPane;

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

    #region == Properties ==

    #region == Flags ==

    [ObservableProperty]
    public partial bool IsTreeWorking { get; set; }

    [ObservableProperty]
    public partial bool IsBackEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsDebugWindowEnabled { get; set; } = false;

    [ObservableProperty]
    public partial bool IsEntryDetailVisible { get; set; } = false;

    public bool IsFeedTreeLoaded { get; private set; }

    #endregion

    #region == Service Treeview ==

    public ObservableCollection<NodeTree> Services
    {
        get => Root.Children;
        set
        {
            Root.Children = value;
            OnPropertyChanged();
        }
    }

    public FeedTreeBuilder Root { get; } = new();

    public NodeTree? SelectedTreeViewItem
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            if (_cts.IsCancellationRequested)
            {
                return;
            }

            field = value;

            OnPropertyChanged();

            // Reset token.
            _ctsForSelectedTreeViewItem?.Cancel();
            _ctsForSelectedTreeViewItem?.Dispose();
            _ctsForSelectedTreeViewItem = new CancellationTokenSource();
            Entries.Clear();

            try
            {
                // Clear Listview selected Item.
                SelectedListViewItem = null;

                // Clear error if shown.
                ErrorObj = null;
                IsShowFeedError = false;

                if (field == null)
                {
                    IsToggleInboxAppButtonEnabled = false;
                    Entries.Clear();
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                    return;
                }

                IsToggleInboxAppButtonEnabled = true;

                // Update Title bar info
                SelectedServiceName = field.Name;

                if (field is NodeService nds)
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

                    IsShowInboxEntries = nds.IsInboxOnly;

                    // NodeFeed is selected
                    if (field is NodeFeed nfeed)
                    {
                        // Let's not clear here.
                        //Entries.Clear();

                        //LoadEntries(nfeed);
                        //_ = LoadEntriesAsync(nfeed, _ctsForSelectedTreeViewItem.Token); // Fire and forget
                        var selectionToken = _ctsForSelectedTreeViewItem.Token;

                        _ = ObserveBackgroundTaskAsync(LoadEntriesAsync(nfeed, selectionToken), "LoadEntriesAsync", selectionToken);
                    }
                    else
                    {
                        Entries.Clear();
                    }
                }
                else if (field is NodeFolder folder)
                {
                    IsShowInboxEntries = folder.IsInboxOnly;

                    // Let's not clear here.
                    //Entries.Clear();

                    //LoadEntries(folder);
                    //_ = LoadEntriesAsync(folder, _ctsForSelectedTreeViewItem.Token);// Fire and forget
                    var selectionToken = _ctsForSelectedTreeViewItem.Token;

                    _ = ObserveBackgroundTaskAsync(LoadEntriesAsync(folder, selectionToken), "LoadEntriesAsync", selectionToken);
                }

                // notify at last.
                EntryArchiveAllCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SelectedTreeViewItem: {ex.Message}");
                (Application.Current as App)?.AppendErrorLog("SelectedTreeViewItem", ex.Message);
            }

        }
    }

    [ObservableProperty]
    public partial string SelectedServiceName { get; set; } = string.Empty;

    #endregion

    #region == Entry ListViews ==

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EntryArchiveAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(EntryArchiveReadCommand))]
    public partial ObservableCollection<EntryItem> Entries { get; set; } = [];

    public FeedEntryItem? SelectedListViewItem
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();

            if (field == null)
            {
                IsEntryDetailVisible = false;

                return;
            }

            // WinUI3 is very limited in fuctionality. So manually update IsSelected.
            foreach (var item in Entries)
            {
                if (item is FeedEntryItem fei)
                {
                    fei.IsSelected = false;
                }
            }
            field.IsSelected = true;

            IsEntryDetailVisible = true;

            EntryViewExternalCommand.NotifyCanExecuteChanged();

            if (string.IsNullOrEmpty(field.Summary.Trim()))
            {
                IsSummaryExists = false;
            }
            else
            {
                IsSummaryExists = true;
            }

            if (field.ContentType == EntryItem.ContentTypes.text)
            {
                IsContentText = true;

                if (!string.IsNullOrEmpty(field.Content.Trim()))
                {
                    IsSummaryExists = false;
                }
            }
            else
            {
                IsContentText = false;
            }

            if ((field.ContentType == EntryItem.ContentTypes.textHtml) ||
                (field.ContentType == EntryItem.ContentTypes.unknown))
            {
                IsContentHtml = true;

                if (!string.IsNullOrEmpty(field.Content.Trim()))
                {
                    IsSummaryExists = false;
                }
            }
            else
            {
                IsContentHtml = false;
            }

            IsAltLinkExists = (field).AltHtmlUri != null;

            IsImageLinkExists = field.ImageUri != null;

            IsAudioLinkExists = field.AudioUri != null;

            IsCommentPageLinkExists = field.CommentUri != null;

            if (IsContentHtml)
            {
                //Debug.WriteLine("SelectedListViewItem.Content: " + IsContentHtml);
                HtmlSource = new HtmlWithBaseUri(
                    field.Content,
                    field.ContentBaseUri
                );
            }
            else if (IsSummaryExists)
            {
                //Debug.WriteLine("SelectedListViewItem.Summary: " + IsSummaryExists);
                HtmlSource = new HtmlWithBaseUri(
                    field.Summary,
                    field.ContentBaseUri
                );
            }

            //Debug.WriteLine("SelectedListViewItem AbsBaseUri: " + field.ContentBaseUri?.AbsoluteUri);

            if ((field.Status != FeedEntryItem.ReadStatus.rsNewVisited) && (field.Status != FeedEntryItem.ReadStatus.rsNormalVisited))
            {
                //Task.Run(() => UpdateEntryStatusAsReadAsync(SelectedTreeViewItem!, _selectedListViewItem));
                //UpdateEntryStatusAsRead(SelectedTreeViewItem!, field);
                if (SelectedTreeViewItem is null)
                {
                    return;
                }
                //_ = UpdateEntryStatusAsReadAsync(SelectedTreeViewItem, field); // Fire and forget
                var lifetimeToken = _cts.Token;

                _ = ObserveBackgroundTaskAsync(UpdateEntryStatusAsReadAsync(SelectedTreeViewItem, field),"UpdateEntryStatusAsReadAsync",lifetimeToken);
            }
        }
    }

    [ObservableProperty]
    public partial HtmlWithBaseUri? HtmlSource { get; set; }

    [ObservableProperty]
    public partial bool IsSummaryExists { get; set; }

    [ObservableProperty]
    public partial bool IsContentText { get; set; }

    [ObservableProperty]
    public partial bool IsContentHtml { get; set; }

    public bool IsAltLinkExists
    {
        get; set
        {
            SetProperty(ref field, value);
            IsNoAltLinkExists = !value;
        }
    }

    [ObservableProperty]
    public partial bool IsNoAltLinkExists { get; set; }

    [ObservableProperty]
    public partial bool IsImageLinkExists { get; set; }

    [ObservableProperty]
    public partial bool IsAudioLinkExists { get; set; }

    [ObservableProperty]
    public partial MediaSource? MediaSource { get; set; }

    public bool IsMediaPlayerVisible
    {
        get; set
        {
            SetProperty(ref field, value);
            IsNotMediaPlayerVisible = !value;
        }
    }

    [ObservableProperty]
    public partial bool IsNotMediaPlayerVisible { get; set; }

    [ObservableProperty]
    public partial bool IsCommentPageLinkExists { get; set; }

    [ObservableProperty]
    public partial bool IsToggleInboxAppButtonEnabled { get; set; }

    public string? InboxAppButtonLabel { get => field ?? "Inbox"; set => SetProperty(ref field, value); } = "Inbox".GetLocalized();

    [ObservableProperty]
    public partial string ToggleInboxAppButtonIcon { get; set; } = "M19,15H15A3,3 0 0,1 12,18A3,3 0 0,1 9,15H5V5H19M19,3H5C3.89,3 3,3.9 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5A2,2 0 0,0 19,3Z";

    public bool IsShowInboxEntries
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                IsShowAllEntries = !value;
                ToggleInboxAppButtonLabel();
            }
        }
    } = true;

    public bool IsShowAllEntries
    {
        get; set
        {
            if (SetProperty(ref field, value))
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

    // NEW. For ComboBox.
    public static ObservableCollection<Models.EntryArchivingStatus> EntryArchiveStatusList { get; } = [
        new Models.EntryArchivingStatus(EntryArchivingStatusKeys.Inbox, "Inbox"),
        new Models.EntryArchivingStatus(EntryArchivingStatusKeys.All, "All") ];
    // TODO: Archived,Read,Unread

    private Models.EntryArchivingStatus _selectedEntryArchiveStatus = EntryArchiveStatusList[0]; // TODO:
    public Models.EntryArchivingStatus SelectedEntryArchiveStatus
    {
        get => _selectedEntryArchiveStatus;
        set
        {
            if (!SetProperty(ref _selectedEntryArchiveStatus, value))
            {
                return;
            }

            if (SelectedTreeViewItem is not NodeTree nt) return;

            if (_selectedEntryArchiveStatus.Key == EntryArchivingStatusKeys.Inbox)
            {
                nt.IsInboxOnly = true;
            }
            else if (_selectedEntryArchiveStatus.Key == EntryArchivingStatusKeys.All)
            {
                nt.IsInboxOnly = false;
            }
            else
            {
                return;
            }

            //_ = LoadEntriesAsync(nt, _ctsForSelectedTreeViewItem.Token);// Fire and forget
            var selectionToken = _ctsForSelectedTreeViewItem.Token;

            _ = ObserveBackgroundTaskAsync(LoadEntriesAsync(nt, selectionToken), "LoadEntriesAsync", selectionToken);
        }
    }

    #endregion

    #region == Errors ==

    // Feed node error obj
    [ObservableProperty]
    public partial ErrorObject? ErrorObj { get; set; }

    public bool IsShowFeedError
    {
        get; set
        {
            SetProperty(ref field, value);
            IsNotShowFeedError = !value;
        }
    }

    [ObservableProperty]
    public partial bool IsNotShowFeedError { get; set; } = true;

    [ObservableProperty]
    public partial ErrorObject? ErrorMain { get; set; }

    [ObservableProperty]
    public partial string? ErrorMainTitle { get; set; }

    [ObservableProperty]
    public partial string? ErrorMainMessage { get; set; }

    public bool IsMainErrorInfoBarVisible
    {
        get; set
        {
            if (value && (ErrorMain != null))
            {
                ErrorMainTitle = ErrorMain.ErrDescription;
                ErrorMainMessage = ErrorMain.ErrText;
            }
            else if (value == false)
            {
                ErrorMain = null;
            }

            SetProperty(ref field, value);
        }
    }

    #endregion

    #region == Warning ==

    [ObservableProperty]
    public partial string? WarningMainTitle { get; set; }
    [ObservableProperty]
    public partial string? WarningMainMessage { get; set; }
    [ObservableProperty]
    public partial bool IsMainWarningInfoBarVisible { get; set; } = false;

    #endregion

    #region == Debug Event Window ==

    [ObservableProperty]
    public partial string? DebugEventLog { get; set; }

    #endregion

    #region == FeedEdit ==

    public NodeFeed? FeedToEDit
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                NameToEditFeed = field?.Name;
            }
        }
    }

    //private string? _nameToEditFeed = string.Empty;
    public string? NameToEditFeed
    {
        get => field ?? string.Empty;
        set => SetProperty(ref field, value);
    }

    public bool IsExcludeFromRefreshAll
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                FeedToEDit?.IsExcludeFromRefreshAll = value;
            }
        }
    
    }

    [RelayCommand]
    private void CheckIfFeedIsValidUsingValidator()
    {
        if (FeedToEDit?.EndPoint == null) return;

        var hoge = new Uri("https://validator.w3.org/feed/check.cgi?url=" + HttpUtility.UrlEncode(FeedToEDit.EndPoint.AbsoluteUri));

        _ = Task.Run(() => Windows.System.Launcher.LaunchUriAsync(hoge), _cts.Token);
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

    public NodeFolder? FolderToEdit
    {
        get;
        set
        {

            if (SetProperty(ref field, value))
            {
                NameToEditFolder = field?.Name;
            }
        }
    }

    [ObservableProperty]
    public partial string? NameToEditFolder { get; set; } = string.Empty;

    [RelayCommand]
    private void UpdateFolderItemProperty()
    {
        if (string.IsNullOrEmpty(NameToEditFolder)) return;

        FolderToEdit?.Name = NameToEditFolder;

        var shell = App.GetService<ShellPage>();
        _ = shell.NavFrame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        //_navigationService.NavigateTo(typeof(MainViewModel).FullName!, null);
    }

    #endregion

    #region == FolderAdd ==

    private NodeTree? _targetNodeToAddFolder;

    [ObservableProperty]
    public partial string? NameToAddFolder { get; set; } = string.Empty;

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

    #region == Options ==

    [ObservableProperty]
    public partial double WidthLeftPane { get; set; } = 256;

    [ObservableProperty]
    public partial double WidthListViewPane { get; set; } = 300;//256;

    #endregion

    #region == Setting ==

    [ObservableProperty]
    public partial ElementTheme Theme { get; set; } = ElementTheme.Default;

    [ObservableProperty]
    public partial SystemBackdropOption Material { get; set; } = SystemBackdropOption.Mica;

    [ObservableProperty]
    public partial bool IsAcrylicSupported { get; set; } = false;

    [ObservableProperty]
    public partial bool IsBackdropEnabled { get; set; } = false;

    [ObservableProperty]
    public partial bool IsMicaSupported { get; set; } = false;

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

    #endregion

    #endregion

    #region == Methods ==

    #region == Initialization ==

    private Task? _initializeTask;

    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Idempotent. Call once from the UI thread after the window content is shown, e.g. ShellPage.Loaded.
    /// </summary>
    public Task InitializeAsync() => _initializeTask ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        try
        {
            await InitializeDatabaseAsync();

            if (_cts.IsCancellationRequested)
            {
                return;
            }

            await InitializeFeedTreeAsync();
        }
        finally
        {
            IsInitialized = true;

            // Command state depends on the tree, which is now loaded.
            FeedRefreshAllCommand.NotifyCanExecuteChanged();
            FolderAddCommand.NotifyCanExecuteChanged();
            FeedAddCommand.NotifyCanExecuteChanged();
            OpmlImportCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task InitializeFeedTreeAsync()
    {
        var cancellationToken = _cts.Token;

        try
        {
            // File I/O and XML parsing off the UI thread.
            var document = await Task.Run(() => _feedTreePersistenceService.Load(), cancellationToken);
            if (document is null)
            {
                // Could be the first time the app is run, or the file was deleted.
                // In either case, we want to create a new tree and save it so that the file exists for future runs.
                IsFeedTreeLoaded = true;// otherwise the data won't be saved when the app is shutting down.
                return;
            }

            // Back on the UI thread (captured context): the tree is bound to the UI.
            Root.LoadXmlDoc(document);
            IsFeedTreeLoaded = true;

            OnPropertyChanged(nameof(Services));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when the app is shutting down.
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
                ErrPlace = "MainViewModel::InitializeFeedTreeAsync",
                ErrPlaceParent = "MainViewModel.InitializeAsync()"
            };
            IsMainErrorInfoBarVisible = true;

            Debug.WriteLine("Exception while loading service.xml:" + ex);
        }
    }

    private async Task InitializeDatabaseAsync()
    {
        var filePath = Path.Combine(App.AppDataFolder, "Feeds.db");
        if (RuntimeHelper.IsMSIX)
        {
            filePath = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "Feeds.db");
        }

        var cancellationToken = _cts.Token;
        try
        {
            var res = await Task.Run(() => _dataAccessService.InitializeDatabase(filePath), cancellationToken);
            if (res.IsError)
            {
                ErrorMain = res.Error;
                IsMainErrorInfoBarVisible = true;

                Debug.WriteLine("SQLite DB init: " + res.Error.ErrText + ": " + res.Error.ErrDescription + " @" + res.Error.ErrPlace + "@" + res.Error.ErrPlaceParent);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when the app is shutting down.
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Exception @InitializeDatabaseAsync: {e}");
        }
    }

    #endregion

    #region == Finalization ==

    public void SaveServiceXml()
    {
        // This may be a bad idea.
        if (!IsFeedTreeLoaded)
        {
            return;
        }

        try
        {
            var document = Root.AsXmlDoc();
            _feedTreePersistenceService.Save(document);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception @SaveServiceXml: {ex}");

            (App.Current as App)?.AppendErrorLog("SaveServiceXml", ex.ToString());
            (App.Current as App)?.SaveErrorLog();
        }

    }

    public async Task WaitForBackgroundOperationsAsync()
    {
        Task[] pending;

        lock (_activeRefreshTasksLock)
        {
            pending = [.. _activeRefreshTasks];
        }

        Debug.WriteLine($"[{DateTimeOffset.UtcNow:O}] Waiting for {pending.Length} background operations");

        try
        {
            await Task.WhenAll(pending)
                .WaitAsync(TimeSpan.FromSeconds(16))
                .ConfigureAwait(false);

            Debug.WriteLine($"[{DateTimeOffset.UtcNow:O}] Background operations completed");
        }
        catch (TimeoutException)
        {
            Debug.WriteLine($"[{DateTimeOffset.UtcNow:O}] Timed out waiting for background operations");

            foreach (var task in pending)
            {
                if (!task.IsCompleted)
                {
                    Debug.WriteLine($"[{DateTimeOffset.UtcNow:O}] Still pending: task {task.Id}, status={task.Status}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"[{DateTimeOffset.UtcNow:O}] Background operations canceled during shutdown");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{DateTimeOffset.UtcNow:O}] Background operation failed: {ex}");
        }
    }

    public void BeginShutdown()
    {
        lock (_activeRefreshTasksLock)
        {
            _isShuttingDown = true;
        }

        try
        {
            if (_ctsForSelectedTreeViewItem?.IsCancellationRequested == false)
            {
                _ctsForSelectedTreeViewItem?.Cancel();
            }
        }
        catch (Exception ex)
        {
            _ = ex;
            Debug.WriteLine($"Exception canceling _ctsForSelectedTreeViewItem during BeginShutdown(): {ex}");
            //AppendErrorLog("BeginShutdown", ex.ToString());
            //SaveErrorLog();
        }

        try
        {
            if (_cts?.IsCancellationRequested == false)
            {
                _cts?.Cancel();
            }
        }
        catch (Exception ex)
        {
            _ = ex;
            Debug.WriteLine($"Exception canceling _cts during BeginShutdown(): {ex}");
            //AppendErrorLog("BeginShutdown", ex.ToString());
            //SaveErrorLog();
        }
    }

    private static void CancelAndDispose(CancellationTokenSource? cts, string name)
    {
        try
        {
            cts?.Cancel();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception canceling {name} during Dispose(): {ex}");
        }
        finally
        {
            cts?.Dispose();
        }
    }

    public void Dispose()
    {
        //_feedClientService.BaseClient.DebugOutput -= OnDebugOutput;
        if (_ctsForSelectedTreeViewItem is not null)
        {
            CancelAndDispose(_ctsForSelectedTreeViewItem, nameof(_ctsForSelectedTreeViewItem));
        }

        if (_cts is not null)
        {
            // Moved to the App, don't dispose untill all the requests are canceled and caught appropriately.
            //CancelAndDispose(_cts, nameof(_cts));
        }
    }

    #endregion

    #region == Entries Refreshing Methods ==

    public async Task<FeedRefreshResult> RefreshAsync(FeedRefreshRequest request, Func<FeedRefreshStage, Task> reportStageAsync, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await reportStageAsync(FeedRefreshStage.Downloading);
        cancellationToken.ThrowIfCancellationRequested();

        var fetchResult = await _feedClientService.GetEntries(request.EndPoint, request.FeedId, request.Etag, cancellationToken);

        var fetchedAt = DateTime.Now;

        if (fetchResult.IsError || fetchResult.Entries.Count == 0)
        {
            return new FeedRefreshResult(fetchedAt, fetchResult, null);
        }
        cancellationToken.ThrowIfCancellationRequested();

        await reportStageAsync(FeedRefreshStage.Saving);
        cancellationToken.ThrowIfCancellationRequested();

        var saveResult = await Task.Run(
            () => _dataAccessService.InsertEntries(
                fetchResult.Entries,
                request.FeedId,
                request.Name,
                fetchResult.Title,
                fetchResult.Description,
                fetchResult.Updated,
                fetchResult.HtmlUri),
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        return new FeedRefreshResult(fetchedAt, fetchResult, saveResult);
    }

    private async Task RefreshFeedAsync(NodeFeed feed, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _dispatcherService.EnqueueAsync(() =>
        {
            feed.IsBusy = true;
            feed.Status = NodeFeed.DownloadStatus.Downloading;
            feed.ErrorDatabase = null;
        });

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = new FeedRefreshRequest(feed.Id, feed.EndPoint, feed.Name, feed.Etag);
            var result = await RefreshAsync(request,
                stage => _dispatcherService.EnqueueAsync(() =>
                {
                    feed.Status = stage switch
                    {
                        FeedRefreshStage.Downloading => NodeFeed.DownloadStatus.Downloading,
                        FeedRefreshStage.Saving => NodeFeed.DownloadStatus.Saving,
                        _ => throw new ArgumentOutOfRangeException(nameof(stage))
                    };
                }),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            await _dispatcherService.EnqueueAsync(() =>
            {
                feed.LastFetched = result.FetchedAt;

                if (result.FetchResult.IsError)
                {
                    feed.ErrorHttp = result.FetchResult.Error;
                    //feed.EntryNewCount = 0;
                    feed.IsPendingReload = false;

                    if (feed == SelectedTreeViewItem)
                    {
                        ErrorObj = feed.ErrorHttp;
                        IsShowFeedError = true;
                    }

                    feed.Status = NodeFeed.DownloadStatus.Error;
                    return;
                }

                feed.ErrorHttp = null;
                feed.Title = result.FetchResult.Title;
                feed.Description = result.FetchResult.Description;
                feed.HtmlUri = result.FetchResult.HtmlUri;
                feed.Updated = result.FetchResult.Updated;
                feed.Etag = result.FetchResult.Etag;

                if (feed == SelectedTreeViewItem)
                {
                    ErrorObj = null;
                    IsShowFeedError = false;
                }

                if (result.SaveResult is { IsError: true } saveResult)
                {
                    feed.ErrorDatabase = saveResult.Error;

                    if (feed == SelectedTreeViewItem)
                    {
                        ErrorObj = feed.ErrorDatabase;
                        IsShowFeedError = true;
                    }

                    feed.Status = NodeFeed.DownloadStatus.Error;
                    return;
                }

                feed.ErrorDatabase = null;

                if (result.SaveResult is { AffectedCount: > 0 } successfulSave)
                {
                    UpdateEntryAncestors(feed, successfulSave.AffectedCount);
                }
                else
                {
                    feed.IsPendingReload = false;
                }

                feed.Status = NodeFeed.DownloadStatus.Normal;
            });
        }
        finally
        {
            cancellationToken.ThrowIfCancellationRequested();
            _dispatcherService.TryEnqueue(() =>
            {
                feed.IsBusy = false;
                EntryArchiveAllCommand.NotifyCanExecuteChanged();
            });
        }
    }

    private static void UpdateEntryAncestors(NodeFeed feed, int newCount)
    {
        if (feed == null) return;
        if (newCount <= 0) return;
        feed.EntryNewCount += newCount;
        feed.IsPendingReload = true;

        if (feed.Parent is NodeFolder folder)
        {
            UpdateFolderAncestors(folder, newCount);
        }
    }

    private static void UpdateFolderAncestors(NodeFolder folder, int newCount)
    {
        if (folder == null) return;
        if (newCount <= 0) return;
        folder.EntryNewCount += newCount;// > 1000 ? 1000 : newCount;
        folder.IsPendingReload = true;

        if (folder.Parent is NodeFolder parentFolder)
        {
            UpdateFolderAncestors(parentFolder, newCount);
        }
    }

    // Starts a tracked refresh operation, adding it to the list of active refresh tasks in order to wait for shutting down.
    private Task StartTrackedRefresh(Func<Task> operation)
    {
        lock (_activeRefreshTasksLock)
        {
            if (_isShuttingDown || _cts.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            var task = Task.Run(operation, _cts.Token);
            _activeRefreshTasks.Add(task);

            _ = task.ContinueWith(
                completed =>
                {
                    int remaining;

                    lock (_activeRefreshTasksLock)
                    {
                        _activeRefreshTasks.Remove(completed);
                        remaining = _activeRefreshTasks.Count;
                    }

                    Debug.WriteLine($"[{DateTimeOffset.UtcNow:O}] Refresh task ID {completed.Id} finished: {completed.Status}; {remaining} remain");

                    if (completed.IsFaulted)
                    {
                        Debug.WriteLine($"[{DateTimeOffset.UtcNow:O}] Refresh task ID {completed.Id} IsFaulted; exception: {completed.Exception}");
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return task;
        }
    }

    // update all feeds recursive loop.
    private async Task<List<Task>> GetAllEntriesAndSaveTaskAsync(NodeTree nt)
    {
        var tasks = new List<Task>();

        if (nt.Children.Count <= 0) return tasks;
        foreach (var c in nt.Children)
        {
            if (c is NodeFeed feed && feed.Api == ApiTypes.AtFeed && !feed.IsExcludeFromRefreshAll)
            {
                var now = DateTime.Now;
                var last = feed.LastFetched;
                // Calculate how much time has passed since the last fetch
                TimeSpan timeSinceLastFetch = now - last;

                // Skip if it was fetched less than n minutes ago (and protect against future clocks)
                //if ((last > now.AddMinutes(-1)) && (last <= now))
                if (timeSinceLastFetch < TimeSpan.FromMinutes(1) && last <= now)
                {
                    Debug.WriteLine("Skipping " + feed.Name + ": " + last.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                }
                else
                {
                    tasks.Add(StartTrackedRefresh(async () =>
                    {
                        // Existing refresh body:
                        await RefreshFeedAsync(feed, _cts.Token);
                        /*
                        if (_cts.IsCancellationRequested)
                        {
                            return;
                        }
                        */

                        //await Task.Delay(100);
                    }));

                    /*
                    tasks.Add(Task.Run(async () =>
                    {

                        await RefreshFeedAsync(feed, _cts.Token);

                        if (_cts.IsCancellationRequested)
                        {
                            return;
                        }

                        await Task.Delay(100);

                    }, _cts.Token));
                    */
                }
            }

            if (c.Children.Count > 0)
            {
                var list = await GetAllEntriesAndSaveTaskAsync(c);
                if (_cts.IsCancellationRequested)
                {
                    return tasks;
                }

                tasks.AddRange(list);
            }
        }

        return tasks;
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

    // Wraps a background task with error handling and cancellation support.
    private async Task ObserveBackgroundTaskAsync(Task task, string operation, CancellationToken cancellationToken)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when the selection changes or the app closes.
        }
        catch (OperationCanceledException) when (_isShuttingDown)
        {
            // Expected: refresh was canceled during app shutdown.
        }
        catch (ObjectDisposedException) when (_isShuttingDown)
        {
            // Expected: the shared HTTP client was disposed during shutdown.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{operation} failed: {ex}");
            (Application.Current as App)?.AppendErrorLog(operation, ex.ToString());
        }
    }

    // Loads node's all (including children) entries from database.
    private async Task LoadEntriesAsync(NodeTree nt, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (nt == null) return;

        var dispatcher = _dispatcherService;//App.MainWnd?.CurrentDispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }

        SqliteDataAccessSelectResultWrapper? res;

        if (dispatcher is null)
        {
            return;
        }
        await dispatcher.EnqueueAsync(() =>
        {
            nt.IsBusy = true;
        });

        try
        {

            if (nt is NodeFeed feed)
            {
                res = await Task.Run(() => _dataAccessService.SelectEntriesByFeedId(feed.Id, feed.IsInboxOnly), cancellationToken);

                //cancellationToken.ThrowIfCancellationRequested();
                if (cancellationToken.IsCancellationRequested)
                {
                    Debug.WriteLine("cancellationToken.IsCancellationRequested @LoadEntriesAsync1");
                    await dispatcher.EnqueueAsync(() =>
                    {
                        feed.IsBusy = false;
                        EntryArchiveAllCommand.NotifyCanExecuteChanged();
                    });
                    return;
                }

                if (res.IsError)
                {
                    if (dispatcher is null)
                    {
                        return;
                    }
                    await dispatcher.EnqueueAsync(() =>
                    {
                        // set's error
                        feed.ErrorDatabase = res.Error;

                        if (feed == SelectedTreeViewItem)
                        {
                            // show error
                            ErrorObj = feed.ErrorDatabase;
                            IsShowFeedError = true;
                            Entries.Clear();
                        }

                        feed.Status = NodeFeed.DownloadStatus.Error;

                        Debug.WriteLine(feed.ErrorDatabase.ErrText + ", " + feed.ErrorDatabase.ErrDescription + ", " + feed.ErrorDatabase.ErrPlace);

                        feed.IsBusy = false;
                        EntryArchiveAllCommand.NotifyCanExecuteChanged();
                    });

                    return;
                }
            }
            else if (nt is NodeFolder folder)
            {
                List<string> tmpList = [];

                if (folder.Children.Count > 0)
                {
                    tmpList = GetAllFeedIdsFromChildNodes(folder.Children);
                }

                if (tmpList.Count == 0)
                {
                    if (dispatcher is null)
                    {
                        return;
                    }
                    await dispatcher.EnqueueAsync(() =>
                    {
                        folder.IsBusy = false;
                        EntryArchiveAllCommand.NotifyCanExecuteChanged();
                    });
                    return;
                }
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                //res = await Task.Run(() => _dataAccessService.SelectEntriesByFeedIds(tmpList, folder.IsInboxOnly), _cts.Token);
                res = await Task.Run(() => _dataAccessService.SelectEntriesByFeedIds(tmpList, folder.IsInboxOnly), cancellationToken);

                //cancellationToken.ThrowIfCancellationRequested();
                if (cancellationToken.IsCancellationRequested)
                {
                    Debug.WriteLine("cancellationToken.IsCancellationRequested @LoadEntriesAsync2");
                    await dispatcher.EnqueueAsync(() =>
                    {
                        folder.IsBusy = false;
                        EntryArchiveAllCommand.NotifyCanExecuteChanged();

                    });
                    return;
                }

                if (res.IsError)
                {
                    if (dispatcher is null)
                    {
                        return;
                    }
                    await dispatcher.EnqueueAsync(() =>
                    {
                        // show error
                        ErrorObj = res.Error;

                        if (folder == SelectedTreeViewItem)
                        {
                            IsShowFeedError = true;
                            Entries.Clear();
                        }

                        folder.IsBusy = false;
                        EntryArchiveAllCommand.NotifyCanExecuteChanged();
                    });

                    return;
                }
            }
            else
            {
                // TODO:
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Debug.WriteLine("cancellationToken.IsCancellationRequested @LoadEntriesAsync3");
                await dispatcher.EnqueueAsync(() =>
                {
                    nt.IsBusy = false;
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                });
                return;
            }
            if (dispatcher is null)
            {
                return;
            }
            await dispatcher.EnqueueAsync(() =>
            {
                // Update the count
                nt.EntryNewCount = res.UnreadCount;

                // If this is selected Node.
                if (nt == SelectedTreeViewItem)
                {
                    // TODO:inbox only no baai....

                    if (Entries.Count > 0)
                    {
                        //
                        res.SelectedEntries.Reverse();

                        // Check if exists for each entries.
                        foreach (var ent in res.SelectedEntries)
                        {
                            var queitem = Entries.FirstOrDefault(i => i.EntryId == ent.EntryId);
                            if (queitem is not null)
                            {
                                // Update
                            }
                            else
                            {
                                Entries.Insert(0, ent);
                            }
                        }
                    }
                    else
                    {
                        Entries = new ObservableCollection<EntryItem>(res.SelectedEntries);
                    }

                    if (nt is NodeFeed feed)
                    {
                        feed.IsPendingReload = false;
                    }
                    if (nt is NodeFolder folder)
                    {
                        folder.IsPendingReload = false;
                    }

                    nt.IsBusy = false;
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                }
                else
                {
                    //
                }

                nt.IsBusy = false;
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await dispatcher.EnqueueAsync(() =>
            {
                nt.IsBusy = false;
                EntryArchiveAllCommand.NotifyCanExecuteChanged();
            });
        }
        catch (Exception ex)
        {
            _ = ex;
            Debug.WriteLine($"Exception@LoadEntriesAsync: {ex}");
            await dispatcher.EnqueueAsync(() =>
            {
                nt.IsBusy = false;
            });
        }

        return;
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

        var dispatcher = _dispatcherService;// App.MainWnd?.CurrentDispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }
        await dispatcher.EnqueueAsync(() =>
        {
            // Update name just 
            feed.Name = name;
            //Debug.WriteLine("Saving entries: " + feed.Name);
            feed.Status = NodeFeed.DownloadStatus.Saving;
            feed.IsBusy = true;
        });

        if (_cts.IsCancellationRequested)
        {
            return;
        }

        var resInsert = await Task.Run(() => _dataAccessService.UpdateFeed(feed.Id, feed.EndPoint, feed.Name, feed.Title, feed.Description, feed.Updated, feed.HtmlUri!), _cts.Token);
        
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        if (!resInsert.IsError)
        {
            if (dispatcher is null)
            {
                return;
            }
            await dispatcher.EnqueueAsync(() =>
            {
                if (feed.Status != NodeFeed.DownloadStatus.Error)
                {
                    feed.Status = NodeFeed.DownloadStatus.Normal;
                }

                feed.IsBusy = false;
                EntryArchiveAllCommand.NotifyCanExecuteChanged();
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
                // Sets Node Error.
                feed.ErrorDatabase = resInsert.Error;

                // If Node is selected, show the Error.
                if (feed == SelectedTreeViewItem)
                {
                    ErrorObj = feed.ErrorDatabase;
                    IsShowFeedError = true;
                }

                feed.Status = NodeFeed.DownloadStatus.Error;

                Debug.WriteLine(feed.ErrorDatabase.ErrText + ", " + feed.ErrorDatabase.ErrDescription + ", " + feed.ErrorDatabase.ErrPlace);
                feed.IsBusy = false;
                EntryArchiveAllCommand.NotifyCanExecuteChanged();
            });

            //feed.IsBusy = false;
        }
    }

    #endregion

    #region == Entries Archiving ==

    private async Task ArchiveAllAsync(NodeTree nd, bool isReadOnly = false)
    {
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        if (nd == null)
        {
            return;
        }

        var dispatcher = _dispatcherService;// App.MainWnd?.CurrentDispatcherQueue;
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
            });

            List<string> list =
            [
                feed.Id
            ];

            var res = await Task.Run(() => _dataAccessService.UpdateAllEntriesAsArchived(list, isReadOnly), _cts.Token);
            
            if (_cts.IsCancellationRequested)
            {
                return;
            }

            if (res.IsError)
            {
                Debug.WriteLine("ArchiveAllAsync(NodeFeed):" + res.Error.ErrText);

                if (_cts.IsCancellationRequested)
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

                    feed.Status = NodeFeed.DownloadStatus.Error;

                    feed.IsBusy = false;
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                });

                return;
            }
            else
            {
                if (_cts.IsCancellationRequested)
                {
                    return;
                }
                await dispatcher.EnqueueAsync(async () =>
                {
                    // Clear error
                    feed.ErrorDatabase = null;
                    /*
                    if (feed.Status != NodeFeed.DownloadStatus.error)
                        feed.Status = NodeFeed.DownloadStatus.normal;
                    */
                    if (res.AffectedCount > 0)
                    {
                        if (feed == SelectedTreeViewItem && !_ctsForSelectedTreeViewItem.IsCancellationRequested)
                        {
                            if (isReadOnly)
                            {
                                /*
                                var tmpList = Entries
                                    .OfType<FeedEntryItem>()
                                    .Where(entry => entry.Status == FeedEntryItem.ReadStatus.rsNewVisited)
                                    .ToList();
                                */
                                // Iterate backward and remove matching items in place. This avoids creating tmpList and safely handles the collection changing as we remove items:
                                for (int index = Entries.Count - 1; index >= 0; index--)
                                {
                                    if (Entries[index] is FeedEntryItem { Status: FeedEntryItem.ReadStatus.rsNewVisited })
                                    {
                                        Entries.RemoveAt(index);
                                    }
                                }
                            }
                            else
                            {
                                Entries.Clear();
                                // nah
                                if (!feed.IsInboxOnly)
                                {
                                    //LoadEntriesAwaiter(feed);
                                    //await LoadEntriesAsync(SelectedTreeViewItem);
                                }
                            }

                        }

                        // reset unread count.
                        feed.EntryNewCount -= res.AffectedCount;
                        if (feed.Parent != null)
                        {
                            if (feed.Parent is NodeFolder parentFolder)
                            {
                                await MinusAllParentEntryCount(parentFolder, res.AffectedCount);
                            }
                            if (_cts.IsCancellationRequested)
                            {
                                return;
                            }
                        }

                        //var newCount = await Task.Run(() => _dataAccessService.CountEntriesByFeedId(feed.Id, true));
                        //feed.EntryNewCount = newCount > 1000 ? 1000 : newCount;

                        if (_cts.IsCancellationRequested)
                        {
                            return;
                        }
                        if (_ctsForSelectedTreeViewItem.IsCancellationRequested)
                        {
                            return;
                        }
                    }

                    feed.IsBusy = false;
                    if (feed == SelectedTreeViewItem)
                    {
                        EntryArchiveAllCommand.NotifyCanExecuteChanged();
                    }
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
                await dispatcher.EnqueueAsync(async () =>
                {
                    folder.IsBusy = true;
                    if (folder == SelectedTreeViewItem)
                    {
                        //Entries.Clear();
                        EntryArchiveAllCommand.NotifyCanExecuteChanged();
                    }
                });

                List<string> tmpList = [];

                tmpList = GetAllFeedIdsFromChildNodes(folder.Children);
                if (_cts.IsCancellationRequested)
                {
                    return;
                }
                var res = await Task.Run(() => _dataAccessService.UpdateAllEntriesAsArchived(tmpList, isReadOnly), _cts.Token);
                if (_cts.IsCancellationRequested)
                {
                    return;
                }
                if (res.IsError)
                {
                    // TODO:
                    Debug.WriteLine("ArchiveAllAsync(NodeFolder):" + res.Error.ErrText);
                }
                else
                {
                    if (res.AffectedCount > 0)
                    {
                        await dispatcher.EnqueueAsync(async () =>
                        {
                            if (folder == SelectedTreeViewItem && !_ctsForSelectedTreeViewItem.IsCancellationRequested)
                            {
                                if (isReadOnly)
                                {
                                    /*
                                    var tmpList = Entries
                                        .OfType<FeedEntryItem>()
                                        .Where(entry => entry.Status == FeedEntryItem.ReadStatus.rsNewVisited)
                                        .ToList();
                                    */
                                    // Iterate backward and remove matching items in place. This avoids creating tmpList and safely handles the collection changing as we remove items:
                                    for (int index = Entries.Count - 1; index >= 0; index--)
                                    {
                                        if (Entries[index] is FeedEntryItem { Status: FeedEntryItem.ReadStatus.rsNewVisited })
                                        {
                                            Entries.RemoveAt(index);
                                        }
                                    }
                                }
                                else
                                {
                                    Entries.Clear();
                                }
                            }

                            if (folder.Parent is NodeFolder parentFolder)
                            {
                                await MinusAllParentEntryCount(parentFolder, res.AffectedCount);
                            }
                            if (_cts.IsCancellationRequested)
                            {
                                return;
                            }
                            if (_ctsForSelectedTreeViewItem.IsCancellationRequested)
                            {
                                return;
                            }
                            var newCount = await Task.Run(() => _dataAccessService.CountEntriesByFeedIds(tmpList, true), _cts.Token);
                            if (_cts.IsCancellationRequested)
                            {
                                return;
                            }
                            if (_ctsForSelectedTreeViewItem.IsCancellationRequested)
                            {
                                return;
                            }
                            folder.EntryNewCount = newCount > 1000 ? 1000 : newCount;
                            await ResetAllEntryCountAtChildNodes(folder.Children, isReadOnly);


                            //folder.IsBusy = false;
                            //EntryArchiveAllCommand.NotifyCanExecuteChanged();
                        });

                    }

                    await dispatcher.EnqueueAsync(() =>
                    {
                        folder.IsBusy = false;
                        if (folder == SelectedTreeViewItem)
                        {
                            EntryArchiveAllCommand.NotifyCanExecuteChanged();
                        }
                    });
                }

            }
        }
    }

    private async Task ArchiveSelectedAsync(NodeTree nd, FeedEntryItem entry)
    {
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        if (nd is not (NodeFeed or NodeFolder))
        {
            return;
        }

        await _dispatcherService.EnqueueAsync(() =>
        {
            nd.IsBusy = true;
            if (nd == SelectedTreeViewItem)
            {
                SelectedEntryArchiveCommand.NotifyCanExecuteChanged();
            }
        });

        var res = await Task.Run(() => _dataAccessService.UpdateEntryAsArchived(entry.EntryId), _cts.Token);

        if (_cts.IsCancellationRequested)
        {
            return;
        }

        if (res.IsError)
        {
            Debug.WriteLine("ArchiveAsync:" + res.Error.ErrText);

            if (_cts.IsCancellationRequested)
            {
                return;
            }
            await _dispatcherService.EnqueueAsync(() =>
            {
                nd.ErrorDatabase = res.Error;

                if (nd == SelectedTreeViewItem)
                {
                    ErrorObj = nd.ErrorDatabase;
                    IsShowFeedError = true;
                }

                if (nd is NodeFeed feed)
                {
                    feed.Status = NodeFeed.DownloadStatus.Error;
                }

                nd.IsBusy = false;
                SelectedEntryArchiveCommand.NotifyCanExecuteChanged();
            });

            return;
        }

        if (res.AffectedCount <= 0)
        {
            return;
        }

        //Debug.WriteLine("ArchiveAsync: AffectedCount=" + res.AffectedCount);
        await _dispatcherService.EnqueueAsync(async () =>
        {
            if (nd is NodeFeed feed)
            {
                feed.EntryNewCount -= 1;

                if (feed.Parent is NodeFolder parentFolder)
                {
                    await MinusAllParentEntryCount(parentFolder, 1);
                }
            }
            else if (nd is NodeFolder folder)
            {
                folder.EntryNewCount -= 1;

                if (folder.Parent is NodeFolder parentFolder)
                {
                    await MinusAllParentEntryCount(parentFolder, 1);

                }

                await ResetAllEntryCountAtChildNodes(folder.Children, true);
            }

            if (nd == SelectedTreeViewItem)
            {
                if (entry.Status == FeedEntryItem.ReadStatus.rsNewVisited)
                {
                    Entries.Remove(entry);
                }
            }

            nd.IsBusy = false;
            SelectedEntryArchiveCommand.NotifyCanExecuteChanged();
        });
    }

    private async Task ResetAllEntryCountAtChildNodes(ObservableCollection<NodeTree> list, bool isReadOnly)
    {
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        foreach (var nt in list)
        {
            if (nt is NodeFeed feed)
            {
                if (isReadOnly)
                {
                    var count = await Task.Run(() => _dataAccessService.CountEntriesByFeedId(feed.Id, true));
                    feed.EntryNewCount = count > 1000 ? 1000 : count;
                }
                else
                {
                    feed.EntryNewCount = 0;
                }
                if (_cts.IsCancellationRequested)
                {
                    return;
                }
            }
            else if (nt is NodeFolder folder)
            {
                if (isReadOnly)
                {
                    var count = await Task.Run(() => _dataAccessService.CountEntriesByFeedIds(GetAllFeedIdsFromChildNodes(folder.Children), true));
                    folder.EntryNewCount = count > 1000 ? 1000 : count;
                }
                else
                {
                    folder.EntryNewCount = 0;
                }

                if (_cts.IsCancellationRequested)
                {
                    return;
                }
                if (folder.Children.Count > 0)
                {
                    await ResetAllEntryCountAtChildNodes(folder.Children, isReadOnly);
                }
            }
        }
    }

    private async Task MinusAllParentEntryCount(NodeFolder folder, int minusCount)
    {
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        if (folder is not null)
        {
            if ((minusCount > 0) && (folder.EntryNewCount >= minusCount))
            {
                //folder.EntryNewCount -= minusCount;
                var count = await Task.Run(() => _dataAccessService.CountEntriesByFeedIds(GetAllFeedIdsFromChildNodes(folder.Children), true));
                folder.EntryNewCount = count > 1000 ? 1000 : count;
            }

            if (folder.Parent is not null)
            {
                if (folder.Parent is NodeFolder parentFolder)
                {
                    await MinusAllParentEntryCount(parentFolder, minusCount);
                }
            }
        }
    }

    private async Task UpdateEntryStatusAsReadAsync(NodeTree nd, FeedEntryItem entry)
    {
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        if (nd is not (NodeFeed or NodeFolder))
        {
            return;
        }

        var dispatcher = _dispatcherService;// App.MainWnd?.CurrentDispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }

        if ((entry.Status == FeedEntryItem.ReadStatus.rsNewVisited) || entry.Status == FeedEntryItem.ReadStatus.rsNormalVisited)
        {
            return;
        }
        /*
        var rs = FeedEntryItem.ReadStatus.rsNewVisited;
        if (entry.IsArchived)
        {
            rs = FeedEntryItem.ReadStatus.rsNormalVisited;
        }
        */
        var status = entry.IsArchived ? FeedEntryItem.ReadStatus.rsNormalVisited : FeedEntryItem.ReadStatus.rsNewVisited;
        /*
        var res = await Task.Run(() => _dataAccessService.UpdateEntryReadStatus(entry.EntryId, rs), _cts.Token);

        if (!res.IsError)
        {
            if (dispatcher is null)
            {
                return;
            }
            await dispatcher.EnqueueAsync(() =>
            {
                entry?.Status = rs;
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
                Debug.WriteLine("UpdateEntryStatusAsReadAsync:" + res.Error.ErrText);

                if ((nd == null) || (entry == null))
                {
                    return;
                }

                if (nd == SelectedTreeViewItem)
                {
                    //DatabaseError = (nd as NodeFeed).ErrorDatabase;
                    //IsShowDatabaseErrorMessage = true;

                    if (nd is NodeFeed feed)
                    {
                        feed.Status = NodeFeed.DownloadStatus.Error;
                    }
                }
            });

            return;
        }
        */
        var cancellationToken = _cts.Token;

        try
        {
            var result = await Task.Run(() => _dataAccessService.UpdateEntryReadStatus(entry.EntryId, status),cancellationToken);

            // The synchronous database call may have finished after shutdown began.
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!result.IsError)
            {
                await dispatcher.EnqueueAsync(() => entry.Status = status);
                return;
            }

            Debug.WriteLine($"UpdateEntryStatusAsReadAsync: {result.Error.ErrText}");

            await dispatcher.EnqueueAsync(() =>
            {
                if (nd == SelectedTreeViewItem && nd is NodeFeed feed)
                {
                    feed.Status = NodeFeed.DownloadStatus.Error;
                }
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when the app is shutting down.
        }
        // The caller may have already handled the exception, so we don't log it here.
        /*
        catch (Exception ex)
        {
            Debug.WriteLine($"UpdateEntryStatusAsReadAsync failed: {ex}");
            (Application.Current as App)?.AppendErrorLog(nameof(UpdateEntryStatusAsReadAsync), ex.ToString());
        }
        */
    }

    #endregion

    /*
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

        _dispatcherService.TryEnqueue(() =>
        {
            _debugEventLogStringBuilder.AppendLine(data);
            DebugEventLog = _debugEventLogStringBuilder.ToString();
        });
    }
    */

    public void UpdateSettings()
    {
        var config = _configurationService.CurrentConfiguration;
        config.WidthLeftPane = WidthLeftPane;
        config.WidthListViewPane = WidthListViewPane;
    }

    #endregion

    #region == Commands ==

    #region == Theme commands ==

    [DynamicWindowsRuntimeCast(typeof(FrameworkElement))]
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

    #region == Treeview commands ==

    [RelayCommand(CanExecute = nameof(CanFeedAdd))]
    private static void FeedAdd()
    {
        var shell = App.GetService<ShellPage>();
        shell.NavFrame.Navigate(typeof(FeedAddPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
    }

    public async Task AddFeed(FeedLink feedlink)
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

        var resInsert = await Task.Run(() => _dataAccessService.InsertFeed(feedlink.FeedUri.AbsoluteUri, feedlink.FeedUri, feedlink.Title, feedlink.SiteTitle, "", new DateTime(), feedlink.SiteUri), _cts.Token);

        var dispatcher = _dispatcherService;// App.MainWnd?.CurrentDispatcherQueue;
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
                    a.Parent = Root;
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
                            a.Parent = Root;
                            Services.Insert(0, a);//.Add(a);
                        }
                    }
                    else
                    {
                        a.Parent = Root;
                        Services.Insert(0, a);//.Add(a);
                    }
                }
                else
                {
                    return;
                }

                a.IsSelected = true;

                FeedRefreshAllCommand.NotifyCanExecuteChanged();

                IsFeedTreeLoaded = true;
                SaveServiceXml();
            });

        }
    }
    private bool CanFeedAdd()
    {
        return IsInitialized;
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
                if (nf.EndPoint.AbsoluteUri.Equals(feedUri, StringComparison.OrdinalIgnoreCase))
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
            IsExcludeFromRefreshAll = FeedToEDit?.IsExcludeFromRefreshAll ?? false;

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

    [RelayCommand(CanExecute = nameof(CanNodeIsEnabledToggle))]
    private void NodeIsEnabledToggle()
    {
        if (SelectedTreeViewItem is null)
        {
            return;
        }

        SelectedTreeViewItem.IsExcludeFromRefreshAll = !SelectedTreeViewItem.IsExcludeFromRefreshAll;
    }

    private bool CanNodeIsEnabledToggle()
    {
        return SelectedTreeViewItem is not null;
    }
    
    [RelayCommand(CanExecute = nameof(CanFolderAdd))]
    private void FolderAdd()
    {
        NodeTree? targetNode = null;
        NameToAddFolder = string.Empty;

        if (SelectedTreeViewItem is null)
        {
            targetNode = Root;
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

            IsFeedTreeLoaded = true;
            var shell = App.GetService<ShellPage>();
            shell.NavFrame.Navigate(typeof(FolderAddPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
            //_navigationService.NavigateTo(typeof(FolderAddViewModel).FullName!, targetNode);
        }
    }

    private bool CanFolderAdd()
    {
        return IsInitialized;
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

        var dispatcher = _dispatcherService;// App.MainWnd?.CurrentDispatcherQueue;
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
        await dispatcher.EnqueueAsync(async () =>
        {
            foreach (var hoge in nodeToBeDeleted)
            {
                if (hoge.Parent != null)
                {
                    hoge.IsBusy = false; // remove self from parent IsBusyChildrenCount

                    // 
                    if (hoge.Parent is NodeFolder parentFolder)
                    {
                        await MinusAllParentEntryCount(parentFolder, hoge.EntryNewCount);
                    }

                    SelectedTreeViewItem = null;
                    hoge.Parent.Children.Remove(hoge);
                }
                else
                {
                    //Debug.WriteLine("DeleteNodeTree: (hoge.Parent is null)");

                    SelectedTreeViewItem = null;
                    Root.Children.Remove(hoge);
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

        var dispatcher = _dispatcherService;// App.MainWnd?.CurrentDispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }

        if (nt is NodeFeed feed)
        {
            await dispatcher.EnqueueAsync(() =>
            {
                // check status
                if (!((feed.Status == NodeFeed.DownloadStatus.Normal) || (feed.Status == NodeFeed.DownloadStatus.Error)))
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

                    if (feed == SelectedTreeViewItem)
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
        IsTreeWorking = true;
        DebugEventLog = string.Empty;
        Root.IsBusy = true;

        try
        {
            await Task.Delay(100);

            var tasks = await GetAllEntriesAndSaveTaskAsync(Root);
            if(_cts.IsCancellationRequested)
            {
                return;
            }
            if (tasks.Count <= 0)
            {
                return;
            }

            await Task.WhenAll(tasks);
            if (_cts.IsCancellationRequested)
            {
                return;
            }
            if ((SelectedTreeViewItem is NodeFolder) || (SelectedTreeViewItem is NodeFeed))
            {
                if (SelectedTreeViewItem.IsPendingReload)
                {
                    await LoadEntriesAsync(SelectedTreeViewItem, _ctsForSelectedTreeViewItem.Token);
                    if (_cts.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }

            await Task.Delay(100);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // Expected when shutdown cancels the refresh.
        }
        catch (OperationCanceledException)
        {
            // This may include the cancellation of timeout of the HttpClient request. We don't need to log this, as it is expected.
            Debug.WriteLine("FeedRefreshAll: OperationCanceledException");
        }
        catch (ObjectDisposedException)
        {
            // This may include the cancellation of timeout of the HttpClient request. We don't need to log this, as it is expected.
            Debug.WriteLine("FeedRefreshAll: ObjectDisposedException");

        }
        catch (AggregateException ae)
        {
            foreach (var ex in ae.InnerExceptions)
            {
                Debug.WriteLine($"FeedRefreshAll: {ex}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FeedRefreshAll: {ex}");
            /*
            _dispatcherService.TryEnqueue(() =>
            {
                (Application.Current as App)?.AppendErrorLog("FeedRefreshAll", ex.Message);
            });
            */
        }
        finally
        {
            IsTreeWorking = false;
            Root.IsBusy = false;
            EntryArchiveAllCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanFeedRefreshAll()
    {
        if (IsTreeWorking) return false;
        if (Services.Count == 0) return false;
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanFeedRefresh))]
    private async Task FeedRefresh()
    {
        if (SelectedTreeViewItem is null)
        {
            return;
        }

        var tvn = SelectedTreeViewItem;
        NodeFolder? busyFolder = null;

        try
        {
            if (tvn is NodeFeed feed)
            {
                IsTreeWorking = true;
                /*
                feed.IsBusy = true;
                EntryArchiveAllCommand.NotifyCanExecuteChanged();

                var list = await GetEntriesAsync(feed);

                if (list.Count > 0)
                {
                    await SaveEntriesAsync(list, feed);
                }
                feed.IsBusy = false;
                */
                await RefreshFeedAsync(feed, _cts.Token);

                if (_cts.IsCancellationRequested)
                {
                    return;
                }

                IsTreeWorking = false;
            }
            else if (tvn is NodeFolder folder)
            {
                IsTreeWorking = true;

                //await dispatcher.EnqueueAsync(async () =>
                //{
                folder.IsBusy = true;
                await Task.Delay(100);
                //EntryArchiveAllCommand.NotifyCanExecuteChanged();
                //});

                var tasks = await GetAllEntriesAndSaveTaskAsync(folder);
                if (_cts.IsCancellationRequested)
                {
                    return;
                }
                await Task.WhenAll(tasks);

                //await dispatcher.EnqueueAsync(async () =>
                //{
                folder.IsBusy = false;
                await Task.Delay(100);
                //EntryArchiveAllCommand.NotifyCanExecuteChanged();
                //});

                IsTreeWorking = false;
            }
            else
            {
                EntryArchiveAllCommand.NotifyCanExecuteChanged();
                return;
            }

            if (tvn == SelectedTreeViewItem)
            {
                if (tvn.IsPendingReload)
                {
                    if (_cts.IsCancellationRequested)
                    {
                        return;
                    }
                    await LoadEntriesAsync(tvn, _ctsForSelectedTreeViewItem.Token);
                }
            }
        }
        catch (OperationCanceledException) when (_ctsForSelectedTreeViewItem.IsCancellationRequested)
        {
            // Shutdown cancellation is expected.
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // Shutdown cancellation is expected.
        }
        catch (OperationCanceledException)
        {
            // refresh was cancelled
        }
        finally
        {
            busyFolder?.IsBusy = false;

            IsTreeWorking = false;
            EntryArchiveAllCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanFeedRefresh()
    {
        return SelectedTreeViewItem is not null;
    }


    [RelayCommand(CanExecute = nameof(CanNodeSelectedArchiveAll))]
    private async Task NodeSelectedArchiveAll(NodeTree nd)
    {
        if (nd is null)
        {
            return;
        }
        if (!((nd is NodeFeed) || (nd is NodeFolder)))
        {
            return;
        }
        await ArchiveAllAsync(nd, false);
    }
    private bool CanNodeSelectedArchiveAll(NodeTree nd)
    {
        if (nd is null)
        {
            return false;
        }

        if (!((nd is NodeFeed) || (nd is NodeFolder)))
        {
            return false;
        }

        if (nd.EntryNewCount <= 0)
        {
            return false;
        }

        if (nd.IsBusy)
        {
            //return false;
        }

        if (nd.IsBusyChildrenCount > 0)
        {
            return false;
        }

        if (Entries.Count <= 0)
        {
            return false;
        }

        return true;
    }


    #endregion

    #region == Listview commands  ==

    [RelayCommand(CanExecute = nameof(CanSelectedEntryArchive))]
    private async Task SelectedEntryArchive()
    {
        if (SelectedTreeViewItem is null)
        {
            return;
        }

        if (!((SelectedTreeViewItem is NodeFeed) || (SelectedTreeViewItem is NodeFolder)))
        {
            return;
        }

        var nt = SelectedTreeViewItem;
        var selectedEntry = SelectedListViewItem;

        if (selectedEntry is null || nt is null)
        {
            return;
        }

        await ArchiveSelectedAsync(nt, selectedEntry);

    }
    private bool CanSelectedEntryArchive()
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

        if (SelectedTreeViewItem.IsBusyChildrenCount > 0)
        {
            return false;
        }

        if (Entries.Count <= 0)
        {
            return false;
        }

        if (SelectedListViewItem is null)
        {
            return false;
        }

        return true;
    }

    private async Task EntryArchive(bool isReadOnly = false)
    {
        if (SelectedTreeViewItem is null)
        {
            return;
        }

        if (!((SelectedTreeViewItem is NodeFeed) || (SelectedTreeViewItem is NodeFolder)))
        {
            return;
        }

        var nt = SelectedTreeViewItem;

        try
        {
            // TODO: Don't archive all; Archive only loaded entries in the listview.
            await ArchiveAllAsync(nt, isReadOnly);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"EntryArchive: {ex.Message}");
            _dispatcherService.TryEnqueue(() =>
            {
                (Application.Current as App)?.AppendErrorLog("EntryArchive", ex.Message);
            });
        }
    }

    [RelayCommand(CanExecute = nameof(CanEntryArchiveAll))]
    private async Task EntryArchiveAll()
    {
        await EntryArchive(isReadOnly: false);
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

        if (SelectedTreeViewItem.IsBusyChildrenCount > 0)
        {
            return false;
        }

        if (Entries.Count <= 0)
        {
            return false;
        }
        
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanEntryArchiveRead))]
    private async Task EntryArchiveRead()
    {
        await EntryArchive(isReadOnly: true);
    }

    private bool CanEntryArchiveRead()
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

        if (SelectedTreeViewItem.IsBusyChildrenCount > 0)
        {
            return false;
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
    private async Task ToggleShowAllEntries()
    {
        if (SelectedTreeViewItem is null)
        {
            return;
        }

        IsShowAllEntries = !IsShowAllEntries;
        Entries.Clear();

        if (SelectedTreeViewItem is NodeFeed feed)
        {
            feed.IsInboxOnly = !IsShowAllEntries;
            //Task.Run(() => LoadEntriesAsync(SelectedTreeViewItem), _cts.Token);
            await LoadEntriesAsync(feed, _ctsForSelectedTreeViewItem.Token);
        }
        else if (SelectedTreeViewItem is NodeFolder folder)
        {
            folder.IsInboxOnly = !IsShowAllEntries;
            //Task.Run(() => LoadEntriesAsync(SelectedTreeViewItem), _cts.Token);
            await LoadEntriesAsync(folder, _ctsForSelectedTreeViewItem.Token);
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
    private async Task ToggleShowInboxEntries()
    {
        if (SelectedTreeViewItem is null)
        {
            return;
        }

        IsShowInboxEntries = !IsShowInboxEntries;
        Entries.Clear();

        if (SelectedTreeViewItem is NodeFeed feed)
        {
            feed.IsInboxOnly = IsShowInboxEntries;
            //Task.Run(() => LoadEntriesAsync(SelectedTreeViewItem), _cts.Token);
            await LoadEntriesAsync(feed, _ctsForSelectedTreeViewItem.Token);
        }
        else if (SelectedTreeViewItem is NodeFolder folder)
        {
            folder.IsInboxOnly = IsShowInboxEntries;
            //Task.Run(() => LoadEntriesAsync(SelectedTreeViewItem), _cts.Token);
            await LoadEntriesAsync(folder, _ctsForSelectedTreeViewItem.Token);
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

    #region == OPML ex/import commands ==

    [RelayCommand(CanExecute = nameof(CanOpmlImport))]
    private async Task OpmlImport()
    {
        try
        {
            await OpmlImportAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OpmlImport: {ex.Message}");
            _dispatcherService.TryEnqueue(() =>
            {
                (Application.Current as App)?.AppendErrorLog("OpmlImport", ex.Message);
            });
        }
    }

    private async Task OpmlImportAsync()
    {
        var dispatcher = _dispatcherService;//App.MainWnd?.CurrentDispatcherQueue;
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
                        /*
                        if (!string.IsNullOrEmpty(s))
                        {
                            s += Environment.NewLine;
                        }
                        s += "Skipped " + hoge.EndPoint;
                        */
                    }

                    WarningMainTitle = "One or more feed(s) already exist(s). Duplicated feeds are skipped.";
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
                IsFeedTreeLoaded = true;

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
                var resInsert = await Task.Run(() => _dataAccessService.InsertFeed(feed.Id, feed.EndPoint, feed.Name, feed.Title, "", new DateTime(), feed.HtmlUri!), _cts.Token);

                // Result is DB Error
                if (resInsert.IsError)
                {
                    Debug.WriteLine("InsertFeed:" + "error");

                    await _dispatcherService.EnqueueAsync(() =>
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

    private bool CanOpmlImport()
    {
        return IsInitialized;
    }

    [RelayCommand(CanExecute = nameof(CanOpmlExport))]
    private async Task OpmlExport()
    {
        try
        {
            //Opml opmlWriter = new();
            var xdoc = _opmlService.WriteOpml(Root);//opmlWriter.WriteOpml(_services);

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
            Debug.WriteLine($"OpmlExport: {ex}");
            (Application.Current as App)?.AppendErrorLog("Exception @OpmlExport", $"{ex.Message} {Environment.NewLine}StackTrace: {ex.StackTrace}, Source: {ex.Source}");
        }
    }

    private static bool CanOpmlExport()
    {
        return true;
    }

    #endregion

    #region == Audio commands ==

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

    #region == Shell page commands ==

    [RelayCommand]
    private static void MenuFileExit()
    {
        //Application.Current.Exit();// DON'T.
        //MainWindow.Close() // DON'T call that either. It will bypass the MainWindow.OnClosing() event and will failed disposing resource.
        Debug.WriteLine("MenuFileExit (Alt+F4)");
    }

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

    #endregion

}
