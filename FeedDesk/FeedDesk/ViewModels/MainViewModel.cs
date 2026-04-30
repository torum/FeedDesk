using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using FeedDesk.Helpers;
using FeedDesk.Models;
using FeedDesk.Models.Clients;
using FeedDesk.Services;
using FeedDesk.Services.Contracts;
using FeedDesk.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections;
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
using System.Xml;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Core;
using WinRT.Interop;

namespace FeedDesk.ViewModels;

public partial class MainViewModel : ObservableRecipient
{
    #region == Flags ==

    [ObservableProperty]
    public partial bool IsTreeWorking { get; set;}

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
        get; set
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
            ctsForSelectedTreeViewItem?.Cancel();
            ctsForSelectedTreeViewItem?.Dispose();
            ctsForSelectedTreeViewItem = new CancellationTokenSource();

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
                        _ = LoadEntriesAsync(nfeed, ctsForSelectedTreeViewItem.Token); // Fire and forget
                    }
                    else
                    {
                        // TODO: 
                        Entries.Clear();
                    }
                }
                else if (field is NodeFolder folder)
                {
                    IsShowInboxEntries = folder.IsInboxOnly;

                    // Let's not clear here.
                    //Entries.Clear();

                    //LoadEntries(folder);
                    _ = LoadEntriesAsync(folder, ctsForSelectedTreeViewItem.Token);// Fire and forget
                }

                // notify at last.
                //EntryArchiveAllCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SelectedTreeViewItem: {ex.Message}");
                (App.Current as App)?.AppendErrorLog("SelectedTreeViewItem", ex.Message);
            }

        }
    }

    [ObservableProperty]
    public partial string SelectedServiceName { get; set; } = string.Empty;

    #endregion

    #region == Entry ListViews ==

    public ObservableCollection<EntryItem> Entries
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                EntryArchiveAllCommand.NotifyCanExecuteChanged();
            }
        }
    } = [];

    public FeedEntryItem? SelectedListViewItem
    {
        get; set
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

            if ((field as EntryItem).ContentType == EntryItem.ContentTypes.text)
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

            if (((field as EntryItem).ContentType == EntryItem.ContentTypes.textHtml) ||
                ((field as EntryItem).ContentType == EntryItem.ContentTypes.unknown))
            {
                IsContentHTML = true;

                if (!string.IsNullOrEmpty(field.Content.Trim()))
                {
                    IsSummaryExists = false;
                }
            }
            else
            {
                IsContentHTML = false;
            }

            if ((field as EntryItem).AltHtmlUri != null)
            {
                IsAltLinkExists = true;
            }
            else
            {
                IsAltLinkExists = false;
            }

            if (field.ImageUri != null)
            {
                IsImageLinkExists = true;
            }
            else
            {
                IsImageLinkExists = false;
            }

            if (field.AudioUri != null)
            {
                IsAudioLinkExists = true;
            }
            else
            {
                IsAudioLinkExists = false;
            }

            if (field.CommentUri != null)
            {
                IsCommentPageLinkExists = true;
            }
            else
            {
                IsCommentPageLinkExists = false;
            }

            if ((field.Status != FeedEntryItem.ReadStatus.rsNewVisited) && (field.Status != FeedEntryItem.ReadStatus.rsNormalVisited))
            {
                //Task.Run(() => UpdateEntryStatusAsReadAsync(SelectedTreeViewItem!, _selectedListViewItem));
                //UpdateEntryStatusAsRead(SelectedTreeViewItem!, field);
                if (SelectedTreeViewItem is null)
                {
                    return;
                }
                _ = UpdateEntryStatusAsReadAsync(SelectedTreeViewItem, field); // Fire and forget
            }
        }
    } = null;

    [ObservableProperty]
    public partial bool IsSummaryExists { get; set; }
    [ObservableProperty]
    public partial bool IsContentText { get; set; }
    [ObservableProperty]
    public partial bool IsContentHTML { get; set; }

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
    } = false;

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

            if (nt is NodeFeed feed)
            {

            }
            else if (nt is NodeFolder folder)
            {

            }

            if (_selectedEntryArchiveStatus.Key == Models.EntryArchivingStatusKeys.Inbox)
            {
                nt.IsInboxOnly = true;
            }
            else if (_selectedEntryArchiveStatus.Key == Models.EntryArchivingStatusKeys.All)
            {
                nt.IsInboxOnly = false;
            }
            else
            {
                return;
            }

            _ = LoadEntriesAsync(nt, ctsForSelectedTreeViewItem.Token);// Fire and forget
        }
    }

    #endregion

    #region == Errors ==

    // Feed node error obj
    [ObservableProperty]
    public partial ErrorObject? ErrorObj { get; set; }

    public bool IsShowFeedError { get; set
        {
            SetProperty(ref field, value);
            IsNotShowFeedError = !value;
        } } = false;

    [ObservableProperty]
    public partial bool IsNotShowFeedError { get; set; } = true;

    // Main error
    private ErrorObject? _errorMain;
    public ErrorObject? ErrorMain
    {
        get => _errorMain;
        set => SetProperty(ref _errorMain, value);
    }
    [ObservableProperty]
    public partial string? ErrorMainTitle { get; set; }
    [ObservableProperty]
    public partial string? ErrorMainMessage { get; set; }

    public bool IsMainErrorInfoBarVisible { get; set
        {
            if (value && (ErrorMain != null))
            {
                ErrorMainTitle = ErrorMain.ErrDescription;
                ErrorMainMessage = ErrorMain.ErrText;
            }
            else if (value == false)
            {
                _errorMain = null;
            }

            SetProperty(ref field, value);
        } } = false;

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

    private readonly StringBuilder _debugEventLogStringBuilder = new();

    [ObservableProperty]
    public partial string? DebugEventLog { get; set; }

    private readonly Queue<string> _debugEvents = new(101);

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

        //var que = App.MainWnd?.CurrentDispatcherQueue;
        _dispatcherService.TryEnqueue(() =>
        {
            _debugEventLogStringBuilder.AppendLine(data);
            DebugEventLog = _debugEventLogStringBuilder.ToString();
        });
    }

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

    #region == Events ==

    public event EventHandler<bool>? ShowWaitDialog;

    //public event EventHandler<string>? DebugOutput;

    #endregion

    #region == Services ==

    private readonly IFileDialogService _fileDialogService;
    private readonly IDataAccessService _dataAccessService;
    private readonly IFeedClientService _feedClientService;
    private readonly IOpmlService _opmlService;
    private readonly IDispatcherService _dispatcherService;

    #endregion

    private readonly CancellationTokenSource _cts = new();
    private CancellationTokenSource ctsForSelectedTreeViewItem = new();

    public MainViewModel(IFileDialogService fileDialogService, IDataAccessService dataAccessService, IFeedClientService feedClientService, IOpmlService opmlService, IDispatcherService dispatcherService)
    {
        _fileDialogService = fileDialogService;
        _dataAccessService = dataAccessService;
        _feedClientService = feedClientService;
        _feedClientService.BaseClient.DebugOutput += OnDebugOutput;
        _opmlService = opmlService;
        _dispatcherService = dispatcherService;

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

    #region == Methods ==

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

                Root.LoadXmlDoc(doc);

                IsFeedTreeLoaded = true;
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

        try
        {
            var res = await Task.Run(() => _dataAccessService.InitializeDatabase(filePath), _cts.Token);
            if (res.IsError)
            {
                ErrorMain = res.Error;
                IsMainErrorInfoBarVisible = true;

                Debug.WriteLine("SQLite DB init: " + res.Error.ErrText + ": " + res.Error.ErrDescription + " @" + res.Error.ErrPlace + "@" + res.Error.ErrPlaceParent);
            }
        }
        catch (Exception e)
        {
            _ = e;
            Debug.WriteLine($"Exception @InitializeDatabase: {e}");
        }
    }

    #endregion

    #region == Finalization ==

    public void CleanUp()
    {
        try
        {
            _feedClientService.BaseClient.DebugOutput -= OnDebugOutput;
            _feedClientService?.BaseClient?.Dispose();

            _cts?.Cancel();

            _cts?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error while CleanUp() : " + ex);
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

        var xdoc = Root.AsXmlDoc();

        xdoc.Save(filePath);
    }

    #endregion

    #region == Entries Refreshing Methods ==

    // gets entries recursively and save to db.
    private async Task<List<EntryItem>> GetEntriesAsync(NodeFeed feed)
    {
        var res = new List<EntryItem>();

        if (feed.Api != ApiTypes.AtFeed)
        {
            return res;
        }

        var dispatcher = _dispatcherService;//App.MainWnd?.CurrentDispatcherQueue;
        if (dispatcher is null)
        {
            return res;
        }
        // Update Node Downloading Status

        feed.IsBusy = true;
        await dispatcher.EnqueueAsync(() =>
        {
            feed.Status = NodeFeed.DownloadStatus.Downloading;
        });

        if (feed == SelectedTreeViewItem)
        {
            //EntryArchiveAllCommand.NotifyCanExecuteChanged();
        }

        await Task.Delay(30);

        //Debug.WriteLine("Getting Entries from: " + feed.Name);

        // Get Entries from web.
        var resEntries = await _feedClientService.GetEntries(feed.EndPoint, feed.Id, _cts.Token);

        feed.LastFetched = DateTime.Now;

        // Check Node exists. Could have been deleted.
        if (feed is null)
        {
            return res;
        }

        // Result is HTTP Error
        if (!resEntries.IsError)
        {
            //await dispatcher.EnqueueAsync(() =>
            //{
            // Clear Node Error
            feed.ErrorHttp = null;
            if (feed == SelectedTreeViewItem)
            {
                // Hide any Error Message
                ErrorObj = null;
                IsShowFeedError = false;
            }

            feed.LastFetched = DateTime.Now;
            feed.Title = resEntries.Title;
            feed.Description = resEntries.Description;
            feed.HtmlUri = resEntries.HtmlUri;
            feed.Updated = resEntries.Updated;

            if (dispatcher is null)
            {
                return res;
            }
            await dispatcher.EnqueueAsync(() =>
            {
                feed.Status = NodeFeed.DownloadStatus.Normal;
            });
            feed.IsBusy = false;

            if (feed == SelectedTreeViewItem)
            {
                //EntryArchiveAllCommand.NotifyCanExecuteChanged();
            }
            //});

            if (resEntries.Entries.Count > 0)
            {
                //await SaveEntryListAsync(resEntries.Entries, feed);
                return resEntries.Entries;
                //Entries = new ObservableCollection<EntryItem>(resEntries.Entries);
            }
            else
            {
                return res;
            }
        }
        else
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
                        //MinusAllParentEntryCount(parentFolder, feed.EntryNewCount);
                    }
                }
                feed.EntryNewCount = 0;

                // Update Node Downloading Status
                feed.Status = NodeFeed.DownloadStatus.Error;
                
            });

            feed.IsBusy = false;

            return res;
        }
    }

    private async Task<List<EntryItem>> SaveEntriesAsync(List<EntryItem> list, NodeFeed feed)
    {
        var res = new List<EntryItem>();

        if (list.Count == 0)
        {
            return res;
        }

        var dispatcher = _dispatcherService;//App.MainWnd?.CurrentDispatcherQueue;

        if (dispatcher is null)
        {
            return res;
        }

        //Debug.WriteLine("Saving entries: " + feed.Name);

        feed.IsBusy = true;

        // Update Node Downloading Status
        await dispatcher.EnqueueAsync(() =>
        {
            feed.Status = NodeFeed.DownloadStatus.Saving;

            // reset errors here.
            feed.ErrorDatabase = null;
        });

        if (feed == SelectedTreeViewItem)
        {
            //EntryArchiveAllCommand.NotifyCanExecuteChanged();
        }
        //});
        await Task.Delay(100);

        //var resInsert = await Task.FromResult(InsertEntriesLock(list));
        var resInsert = await Task.Run(() => _dataAccessService.InsertEntries(list, feed.Id, feed.Name, feed.Title, feed.Description, feed.Updated, feed.HtmlUri!), _cts.Token);

        // Result is DB Error
        if (!resInsert.IsError)
        {
            if (resInsert.AffectedCount > 0)
            {
                //Debug.WriteLine("Saving entries success: " + feed.Name);

                if (dispatcher is null)
                {
                    return res;
                }
                await dispatcher.EnqueueAsync(() =>
                {
                    feed.IsPendingReload = true;
                    UpdateEntryAncestors(feed, resInsert.AffectedCount);

                    // Update Node Downloading Status
                    if (feed.Status != NodeFeed.DownloadStatus.Error)
                    {
                        feed.Status = NodeFeed.DownloadStatus.Normal;
                    }
                });

                await Task.Delay(100);

                feed.IsBusy = false;
            }
            else
            {
                if (dispatcher is null)
                {
                    return res;
                }
                await dispatcher.EnqueueAsync(() =>
                {
                    // Update Node Downloading Status
                    if (feed.Status != NodeFeed.DownloadStatus.Error)
                    {
                        feed.Status = NodeFeed.DownloadStatus.Normal;
                    }

                    //feed.EntryCount = newItems.Count;
                });

                feed.IsPendingReload = false;

                feed.IsBusy = false;

                await Task.Delay(100);
            }

            return resInsert.InsertedEntries;
        }
        else
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
                if (feed == SelectedTreeViewItem)
                {
                    ErrorObj = feed.ErrorDatabase;
                    IsShowFeedError = true;
                }

                feed.Status = NodeFeed.DownloadStatus.Error;

                Debug.WriteLine(feed.ErrorDatabase.ErrText + ", " + feed.ErrorDatabase.ErrDescription + ", " + feed.ErrorDatabase.ErrPlace);

            });

            feed.IsBusy = false;

            return res;
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
        folder.EntryNewCount += newCount;
        folder.IsPendingReload = true;

        if (folder.Parent is NodeFolder parentFolder)
        {
            UpdateFolderAncestors(parentFolder, newCount);
        }
    }

    // update all feeds recursive loop.
    private async Task<List<Task>> GetAllEntriesAndSaveTaskAsync(NodeTree nt)
    {
        var tasks = new List<Task>();

        if (nt.Children.Count <= 0) return tasks;
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
                            Debug.WriteLine($"Getting {feed.Name} @GetAllEntriesAndSaveTaskAsync");

                            feed.IsBusy = true;

                            await _dispatcherService.EnqueueAsync(() =>
                            {
                                EntryArchiveAllCommand.NotifyCanExecuteChanged();
                            });

                            var list = await GetEntriesAsync(feed);

                            _cts.Token.ThrowIfCancellationRequested();

                            if (list.Count > 0)
                            {
                                var res = await SaveEntriesAsync(list, feed);

                                //await Task.Delay(100);

                                _cts.Token.ThrowIfCancellationRequested();

                                if (res.Count > 0)
                                {
                                    // pending flag.. < done in SaveEntriesAsync.


                                    // reload entries if selected.
                                    //await CheckParentSelectedAndLoadEntriesIfNotBusyAsync(feed); ;
                                }
                                else
                                {
                                    ///
                                    //await CheckParentSelectedAndNotifyAsync(feed); ;
                                }
                            }
                            else
                            {
                                feed.IsPendingReload = false;
                                //
                                //await CheckParentSelectedAndNotifyAsync(feed);
                            }

                            feed.IsBusy = false;

                            await _dispatcherService.EnqueueAsync(() =>
                            {
                                EntryArchiveAllCommand.NotifyCanExecuteChanged();
                            });

                            await Task.Delay(100);

                            //await CheckParentSelectedAndLoadEntriesIfPendingAsync(feed);

                            //App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
                            //{
                            //    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                            //});

                        }, _cts.Token));
                    }
                }
            }

            if (c.Children.Count > 0)
            {
                var list = await GetAllEntriesAndSaveTaskAsync(c);
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

    // Loads node's all (including children) entries from database.
    private async Task LoadEntriesAsync(NodeTree nt, CancellationToken cancellationToken)
    {
        if (nt == null) return;

        var dispatcher = _dispatcherService;//App.MainWnd?.CurrentDispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }

        SqliteDataAccessSelectResultWrapper? res;

        try
        {
            if (dispatcher is null)
            {
                return;
            }
            await dispatcher.EnqueueAsync( () =>
            {
                //nt.IsBusy = true;
                if (nt == SelectedTreeViewItem)
                {
                    // Let's not clear
                    //Entries.Clear();
                    // Let's not notify here..
                    //EntryArchiveAllCommand.NotifyCanExecuteChanged();
                }
            });

            if (nt is NodeFeed feed)
            {
                res = await Task.Run(() => _dataAccessService.SelectEntriesByFeedId(feed.Id, feed.IsInboxOnly), _cts.Token);

                cancellationToken.ThrowIfCancellationRequested();

                if (res.IsError)
                {
                    if (dispatcher is null)
                    {
                        return;
                    }
                    await dispatcher.EnqueueAsync( () =>
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
                    await dispatcher.EnqueueAsync( () =>
                    {
                        folder.IsBusy = false;
                    });
                    return;
                }

                res = await Task.Run(() => _dataAccessService.SelectEntriesByFeedIds(tmpList, folder.IsInboxOnly), _cts.Token);

                cancellationToken.ThrowIfCancellationRequested();

                if (res.IsError)
                {
                    if (dispatcher is null)
                    {
                        return;
                    }
                    await dispatcher.EnqueueAsync( () =>
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

            if (dispatcher is null)
            {
                return;
            }
            await dispatcher.EnqueueAsync( () =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Debug.WriteLine("cancellationToken.IsCancellationRequested @LoadEntriesAsync");
                    nt.IsBusy = false;
                    return;
                }

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
                                // Add
                                Entries.Insert(0,ent);
                            }
                        }
                    }
                    else
                    {
                        // Replace
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

                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                }
                else
                {
                    //
                }
            });

            nt.IsBusy = false;
        }
        catch (Exception ex)
        {
            _ = ex;
            Debug.WriteLine($"Exception@LoadEntriesAsync: {ex}");
            nt.IsBusy = false;
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
        });

        feed.IsBusy = true;

        var resInsert = await Task.Run(() => _dataAccessService.UpdateFeed(feed.Id, feed.EndPoint, feed.Name, feed.Title, feed.Description, feed.Updated, feed.HtmlUri!), _cts.Token);

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
            });

            feed.IsBusy = false;
        }
    }

    #endregion

    #region == Entries Archiving ==

    private async Task ArchiveAllAsync(NodeTree nd)
    {
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
            //await dispatcher.EnqueueAsync(() =>
            //{
                feed.IsBusy = true;

                if (feed == SelectedTreeViewItem)
                {
                    EntryArchiveAllCommand.NotifyCanExecuteChanged();
                }

                // TODO: not really saving
                //feed.Status = NodeFeed.DownloadStatus.saving;
            //});

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

                    feed.Status = NodeFeed.DownloadStatus.Error;

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
                            if (!feed.IsInboxOnly)
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
                        if (!feed.IsInboxOnly)
                        {
                            // not needed_
                            //await LoadEntriesAsync(SelectedTreeViewItem);
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
                    if (folder == SelectedTreeViewItem)
                    {
                        //Entries.Clear();
                        EntryArchiveAllCommand.NotifyCanExecuteChanged();
                    }
                });

                folder.IsBusy = true;

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
                        });

                        folder.IsBusy = false;

                        if (folder == SelectedTreeViewItem)
                        {
                            if (!folder.IsInboxOnly)
                            {
                                //await LoadEntriesAsync(folder);
                                //await LoadEntriesAsync(folder, ctsForSelectedTreeViewItem.Token);
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

        var dispatcher = _dispatcherService;// App.MainWnd?.CurrentDispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }

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
    }

    #endregion

    #endregion

    #region == Commands ==

    #region == Treeview commands ==

    [RelayCommand]
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

        var resInsert = await Task.Run(()=>_dataAccessService.InsertFeed(feedlink.FeedUri.AbsoluteUri, feedlink.FeedUri, feedlink.Title, feedlink.SiteTitle, "", new DateTime(), feedlink.SiteUri), _cts.Token);

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
        await Task.Delay(100);

        var tasks = await GetAllEntriesAndSaveTaskAsync(Root);

        if (tasks.Count <= 0)
        {
            IsTreeWorking = false;
            Root.IsBusy = false;
            EntryArchiveAllCommand.NotifyCanExecuteChanged();
            return;
        }

        await Task.WhenAll(tasks);

        if ((SelectedTreeViewItem is NodeFolder) || (SelectedTreeViewItem is NodeFeed))
        {
            if (SelectedTreeViewItem.IsPendingReload)
            {
                await LoadEntriesAsync(SelectedTreeViewItem, ctsForSelectedTreeViewItem.Token);
            }
            else
            {
                Debug.WriteLine($"No updates({SelectedTreeViewItem.Name}) @FeedRefreshAll");
            }
        }

        await Task.Delay(100);

        IsTreeWorking = false;
        Root.IsBusy = false;

        EntryArchiveAllCommand.NotifyCanExecuteChanged();
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

        if (tvn is NodeFeed feed)
        {
            IsTreeWorking = true;

            feed.IsBusy = true;
            EntryArchiveAllCommand.NotifyCanExecuteChanged();

            var list = await GetEntriesAsync(feed);

            if (list.Count > 0)
            {
                await SaveEntriesAsync(list, feed);
            }
            feed.IsBusy = false;

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
                await LoadEntriesAsync(tvn, ctsForSelectedTreeViewItem.Token);
            }
            else
            {
                Debug.WriteLine($"No updates({tvn.Name}) @FeedRefresh");
            }
        }

        EntryArchiveAllCommand.NotifyCanExecuteChanged();
    }

    private bool CanFeedRefresh()
    {
        return SelectedTreeViewItem is not null;
    }

    #endregion

    #region == Listview commands  ==

    [RelayCommand(CanExecute = nameof(CanEntryArchiveAll))]
    private async Task EntryArchiveAll()
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
            await ArchiveAllAsync(nt);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"EntryArchiveAll: {ex.Message}");
            _dispatcherService.TryEnqueue(() =>
            {
                (App.Current as App)?.AppendErrorLog("EntryArchiveAll", ex.Message);
            });
        }
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

        //
        Entries.Clear();

        if (SelectedTreeViewItem is NodeFeed feed)
        {
            feed.IsInboxOnly = !IsShowAllEntries;
            //Task.Run(() => LoadEntriesAsync(SelectedTreeViewItem), _cts.Token);
            await LoadEntriesAsync(feed, ctsForSelectedTreeViewItem.Token);
        }
        else if (SelectedTreeViewItem is NodeFolder folder)
        {
            folder.IsInboxOnly = !IsShowAllEntries;
            //Task.Run(() => LoadEntriesAsync(SelectedTreeViewItem), _cts.Token);
            await LoadEntriesAsync(folder, ctsForSelectedTreeViewItem.Token);
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

        //
        Entries.Clear();

        if (SelectedTreeViewItem is NodeFeed feed)
        {
            feed.IsInboxOnly = IsShowInboxEntries;
            //Task.Run(() => LoadEntriesAsync(SelectedTreeViewItem), _cts.Token);
            await LoadEntriesAsync(feed, ctsForSelectedTreeViewItem.Token);
        }
        else if (SelectedTreeViewItem is NodeFolder folder)
        {
            folder.IsInboxOnly = IsShowInboxEntries;
            //Task.Run(() => LoadEntriesAsync(SelectedTreeViewItem), _cts.Token);
            await LoadEntriesAsync(folder, ctsForSelectedTreeViewItem.Token);
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
    public async Task OpmlImport()
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
                (App.Current as App)?.AppendErrorLog("OpmlImport", ex.Message);
            });
        }
    }

    public async Task OpmlImportAsync()
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
                var resInsert = await Task.Run(()=>_dataAccessService.InsertFeed(feed.Id, feed.EndPoint, feed.Name, feed.Title, "", new DateTime(), feed.HtmlUri!), _cts.Token);
                
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
            Debug.WriteLine($"OpmlExport: {ex.Message}");
            (App.Current as App)?.AppendErrorLog("OpmlExport", ex.Message);
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

    #endregion

}
