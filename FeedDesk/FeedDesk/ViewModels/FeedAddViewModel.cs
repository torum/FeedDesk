using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FeedDesk.Models;
using FeedDesk.Services;
using FeedDesk.Services.Contracts;
using FeedDesk.Views;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace FeedDesk.ViewModels;

public partial class FeedAddViewModel : ObservableRecipient
{
    private readonly IAutoDiscoveryService _serviceDiscovery;

    #region == Properties ==

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            SetProperty(ref _isBusy, value);

            IsButtonEnabled = value;
        }
    }

    private bool _isShowError;
    public bool IsShowError
    {
        get => _isShowError;
        set => SetProperty(ref _isShowError, value);
    }

    private bool _isShowLog;
    public bool IsShowLog
    {
        get => _isShowLog;
        set => SetProperty(ref _isShowLog, value);
    }

    private bool _isButtonEnabled;
    public bool IsButtonEnabled
    {
        get => _isButtonEnabled;
        private set=> SetProperty(ref _isButtonEnabled, value);
    }

    private string _websiteOrEndpointUrl = "";
    public string WebsiteOrEndpointUrl
    {
        get => _websiteOrEndpointUrl;
        //set => SetProperty(ref _websiteOrEndpointUrl, value.Trim());
        set
        {
            if (SetProperty(ref _websiteOrEndpointUrl, value.Trim()))
            {
                GoCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private string _userIdAtomPub = "";
    public string UserIdAtomPub
    {
        get => _userIdAtomPub;
        set => SetProperty(ref _userIdAtomPub, value);
    }

    private string _apiKeyAtomPub = "";
    public string ApiKeyAtomPub
    {
        get => _apiKeyAtomPub;
        set => SetProperty(ref _apiKeyAtomPub, value);
    }

    private AuthTypes _authType = AuthTypes.Wsse;
    public AuthTypes AuthType
    {
        get => _authType;
        set => SetProperty(ref _authType, value);
    }

    private string? _selectedItemType;
    public string? SelectedItemType
    {
        get => _selectedItemType;
        set => SetProperty(ref _selectedItemType, value);
    }

    private string? _selectedItemTitleLabel;
    public string? SelectedItemTitleLabel
    {
        get => _selectedItemTitleLabel;
        //set => SetProperty(ref _selectedItemTitleLabel, value);
        set
        {
            if (SetProperty(ref _selectedItemTitleLabel, value))
            {
                AddSelectedAndCloseCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool _isXmlRpc = false;
    public bool IsXmlRpc
    {
        get => _isXmlRpc;
        set => SetProperty(ref _isXmlRpc, value);
    }

    private string _userIdXmlRpc = "";
    public string UserIdXmlRpc
    {
        get => _userIdXmlRpc;
        set => SetProperty(ref _userIdXmlRpc, value);
    }

    private string _passwordXmlRpc = "";
    public string PasswordXmlRpc
    {
        get => _passwordXmlRpc;
        set => SetProperty(ref _passwordXmlRpc, value);
    }

    private string _statusString = "";
    public string StatusText
    {
        get => _statusString;
        set => SetProperty(ref _statusString, value);
    }

    private string _statusTitleString = "";
    public string StatusTitleText
    {
        get => _statusTitleString;
        set => SetProperty(ref _statusTitleString, value);
    }

    private string _statusLogString = "";
    public string StatusLogText
    {
        get => _statusLogString;
        set => SetProperty(ref _statusLogString, value);
    }

    private int _selectedTabIndex = 0;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    private ObservableCollection<LinkItem> _linkItems = [];
    public ObservableCollection<LinkItem> LinkItems
    {
        get => _linkItems;
        set => SetProperty(ref _linkItems, value);
    }

    private LinkItem? _selectedLinkItem;
    public LinkItem? SelectedLinkItem
    {
        get => _selectedLinkItem;
        //set => SetProperty(ref _selectedLinkItem, value);
        set
        {
            if (SetProperty(ref _selectedLinkItem, value))
            {
                GoSelectedCommand.NotifyCanExecuteChanged();
            }
        }
    }

    #endregion

    #region == Command ==

    public ICommand GoBackCommand
    {
        get;
    }

    public IRelayCommand GoCommand
    {
        get;
    }

    public IRelayCommand GoSelectedCommand
    {
        get;
    }

    public IRelayCommand AddSelectedAndCloseCommand
    {
        get;
    }


    public ICommand GoToFirstTabCommand
    {
        get;
    }

    public ICommand GoToSecondTabCommand
    {
        get;
    }

    public ICommand GoToThirdTabCommand
    {
        get;
    }

    public ICommand GoToFourthTabCommand
    {
        get;
    }

    #endregion

    public FeedAddViewModel(IAutoDiscoveryService serviceDiscovery)
    {
        _serviceDiscovery = serviceDiscovery;
        _serviceDiscovery.StatusUpdate += new AutoDiscoveryStatusUpdateEventHandler(OnStatusUpdate);//new ServiceDiscovery.ServiceDiscoveryStatusUpdate(OnStatusUpdate);

        GoBackCommand = new RelayCommand(OnGoBack);
        GoCommand = new RelayCommand(OnGo, CanGo);
        GoSelectedCommand = new RelayCommand(OnGoSelected, CanGoSelected);
        AddSelectedAndCloseCommand = new RelayCommand(OnAddSelectedAndClose, CanAddSelectedAndClose);

        GoToFirstTabCommand = new RelayCommand(OnGoToFirstTab);
        GoToSecondTabCommand = new RelayCommand(OnGoToSecondTab);
        GoToThirdTabCommand = new RelayCommand(OnGoToThirdTab);
        GoToFourthTabCommand = new RelayCommand(OnGoToFourthTab);
    }

    private void OnGoBack()
    {
        var shell = App.GetService<ShellPage>();
        _ = shell.NavFrame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    private void OnGoToFirstTab()
    {
        GoToFirstPage();
    }

    private void OnGoToSecondTab()
    {
        GoToSelectFeedOrServicePage();
    }

    private void OnGoToThirdTab()
    {
        if (SelectedLinkItem is FeedLinkItem)
        {
            GoToSelectFeedOrServicePage();
        }
        else if (SelectedLinkItem is ServiceDocumentLinkItem)
        {
            GoToAuthInputPage();
        }
        else
        {
            GoToSelectFeedOrServicePage();
        }
    }

    private void OnGoToFourthTab()
    {
        GoToServiceFoundPage();
    }

    #region == Methods ==

    private void GoToFirstPage()
    {
        SelectedTabIndex = 0;
    }

    private void GoToSelectFeedOrServicePage()
    {
        SelectedTabIndex = 1;
    }

    private void GoToAuthInputPage()
    {
        SelectedTabIndex = 2;
    }
    private void GoToServiceFoundPage()
    {
        SelectedTabIndex = 3;
    }

    #endregion

    private void OnStatusUpdate(AutoDiscoveryService sender, string data)
    {
        var uithread = App.MainWnd?.CurrentDispatcherQueue?.HasThreadAccess;

        if (uithread != null)
        {
            if (uithread == true)
            {
                StatusLogText = StatusLogText + data + Environment.NewLine;
            }
            else
            {
                App.MainWnd?.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    StatusLogText = StatusLogText + data + Environment.NewLine;
                });
            }
        }
    }

    private async void OnGo()
    {
        StatusTitleText = "";
        StatusText = "";
        StatusLogText = "";
        LinkItems.Clear();
        SelectedLinkItem = null;


        if (string.IsNullOrEmpty(WebsiteOrEndpointUrl))
        {
            StatusTitleText = "Invalid URL format";
            StatusText = "Text input field is empty.";

            IsShowError = true;
            IsShowLog = false;

            GoToFirstPage();

            return;
        }


        Uri uri;
        try
        {
            uri = new Uri(WebsiteOrEndpointUrl);
        }
        catch
        {
            StatusTitleText = "Invalid URL format";
            StatusText = $"{WebsiteOrEndpointUrl} is not a valid URL.";//"Should be something like https://www.example.com/app/atom";

            IsShowError = true;
            IsShowLog = false;

            GoToFirstPage();

            return;
        }

        if (!(uri.Scheme.Equals("http") || uri.Scheme.Equals("https")))
        {
            StatusTitleText = "Invalid URI scheme";
            StatusText = "Should be http or https: " + uri.Scheme;

            IsShowError = true;
            IsShowLog = false;

            GoToFirstPage();

            return;
        }

        IsBusy = true;

        try
        {
            var sr = await _serviceDiscovery.DiscoverService(uri, true);

            if (sr == null)
            {
                IsBusy = false;
                return;
            }

            if (sr is ServiceResultErr sre)
            {
                StatusTitleText = sre.ErrTitle;
                StatusText = sre.ErrDescription;

                IsShowError = true;
                IsShowLog = true;

                IsBusy = false;
                return;
            }

            // Aut hRequired returned. Probably API endpoint.
            if (sr is ServiceResultAuthRequired)
            {
                IsShowError = false;
                IsShowLog = false;

                // Auth input page.
                GoToAuthInputPage();

                IsBusy = false;
                return;
            }

            if (sr is ServiceResultHtmlPage srhp)
            {
                if ((srhp.Feeds.Count > 0) || (srhp.Services.Count > 0))
                {
                    // Feeds
                    if (srhp.Feeds.Count > 0)
                    {
                        foreach (var f in srhp.Feeds)
                        {
                            FeedLinkItem li = new(f);

                            LinkItems.Add(li);
                        }
                    }

                    // Services
                    if (srhp.Services.Count > 0)
                    {
                        foreach (var s in srhp.Services)
                        {
                            if (s is RsdLink rl)
                            {
                                if (rl.Apis != null)
                                {
                                    if (rl.Apis.Count > 0)
                                    {
                                        ServiceDocumentLinkItem li = new(s);

                                        if (li.IsSupported)
                                        {
                                            LinkItems.Add(li);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // AtomApi?
                                //....
                            }
                        }
                    }

                    IsShowError = false;
                    IsShowLog = false;

                    GoToSelectFeedOrServicePage();

                    IsBusy = false;
                }
                else
                {
                    StatusTitleText = "Found 0 item";
                    StatusText = "Could not find any feeds.";

                    IsShowError = true;
                    IsShowLog = true;

                    IsBusy = false;
                    return;
                }
            }
            else if (sr is ServiceResultFeed srf)
            {
                var feed = srf.FeedlinkInfo;

                if (feed != null)
                {
                    FeedLinkItem li = new(feed);

                    LinkItems.Add(li);
                }

                IsShowError = false;
                IsShowLog = false;

                GoToSelectFeedOrServicePage();

                IsBusy = false;
            }
            else if (sr is ServiceResultRsd srr)
            {
                if (srr.Rsd is not null)
                {
                    var hoge = srr.Rsd;

                    if (hoge.Apis != null)
                    {
                        if (hoge.Apis.Count > 0)
                        {
                            ServiceDocumentLinkItem li = new(hoge);

                            if (li.IsSupported)
                            {
                                LinkItems.Add(li);

                                GoToSelectFeedOrServicePage();
                            }
                            else
                            {
                                StatusTitleText = "Found 0 item";
                                StatusText = "RSD found but no supported service found.";

                                IsShowError = true;
                                IsShowLog = true;

                                IsBusy = false;
                                return;
                            }
                        }
                        else
                        {
                            StatusTitleText = "Found 0 item";
                            StatusText = "RSD found but no supported api found.";

                            IsShowError = true;
                            IsShowLog = true;

                            IsBusy = false;
                            return;
                        }
                    }
                    else
                    {
                        StatusTitleText = "Found 0 item";
                        StatusText = "RSD found but no supported api found.";

                        IsShowError = true;
                        IsShowLog = true;

                        IsBusy = false;
                        return;
                    }

                }
                else
                {
                    // AtomApi?
                    //.
                }
            }
        }
        finally
        {
            IsBusy = false;
        }

        IsBusy = false;
    }

    private bool CanGo()
    {
        if (string.IsNullOrEmpty(WebsiteOrEndpointUrl))
        {
            return false;
        }

        if (!WebsiteOrEndpointUrl.StartsWith("http"))
        {
            return false;
        }

        return true;
    }

    private void OnGoSelected()
    {
        if (SelectedLinkItem == null)
        {
            return; 
        }

        if (SelectedLinkItem is FeedLinkItem fli)
        {
            SelectedItemTitleLabel = fli.Title;

            SelectedItemType = fli.TypeText;

            IsXmlRpc = false;
        }
        else if (SelectedLinkItem is ServiceDocumentLinkItem sdli)
        {
            SelectedItemTitleLabel = sdli.Title;

            SelectedItemType = sdli.TypeText;

            if (sdli.SearviceDocumentLinkData is RsdLink)
            {
                IsXmlRpc = true;
            }
            else if (sdli.SearviceDocumentLinkData is AppLink)
            {
                IsXmlRpc = false;
            }
        }

        GoToServiceFoundPage();
    }

    private bool CanGoSelected()
    {
        if (SelectedLinkItem == null)
        {
            return false;
        }

        //
        if (SelectedLinkItem is not FeedLinkItem)
        {
            return false;
        }

        return true;
    }

    private void OnAddSelectedAndClose()
    {
        if (SelectedLinkItem == null)
        {
            return;
        }

        if (IsXmlRpc)
        {
            if (string.IsNullOrEmpty(UserIdXmlRpc))
            {
                return;
            }

            if (string.IsNullOrEmpty(PasswordXmlRpc))
            {
                return;
            }
        }

        if (SelectedLinkItem is FeedLinkItem fli)
        {
            if (!string.IsNullOrEmpty(SelectedItemTitleLabel))
            {
                fli.FeedLinkData.Title = SelectedItemTitleLabel;
            }

            /* Not good when navigate go back.
            RegisterFeedEventArgs arg = new();
            arg.FeedLinkData = (SelectedLinkItem as FeedLinkItem).FeedLinkData;

            //RegisterFeed?.Invoke(this, arg);

            _navigationService.NavigateTo(typeof(MainViewModel).FullName!, arg);
            */

            var vm = App.GetService<MainViewModel>();
            vm.AddFeed(fli.FeedLinkData);

            var shell = App.GetService<ShellPage>();
            _ = shell.NavFrame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
            //_navigationService.NavigateTo(typeof(MainViewModel).FullName!, null);
        }
        else if (SelectedLinkItem is ServiceDocumentLinkItem sdli)
        {
            if (sdli.SearviceDocumentLinkData is RsdLink rl)
            {
                var sd = rl;

                if (!string.IsNullOrEmpty(SelectedItemTitleLabel))
                {
                    sd.Title = SelectedItemTitleLabel;
                }
                /*
                RegisterXmlRpcEventArgs arg = new();
                arg.RsdLink = sd;
                arg.UserIdXmlRpc = UserIdXmlRpc;
                arg.PasswordXmlRpc = PasswordXmlRpc;
                */

                // TODO: check XML-RPC call?

                // TODO
                //RegisterXmlRpc?.Invoke(this, arg);
            }
            else if (sdli.SearviceDocumentLinkData is AppLink al)
            {
                var sd = al;
                if (sd.NodeService != null)
                {
                    if (!string.IsNullOrEmpty(SelectedItemTitleLabel))
                    {
                        sd.NodeService.Name = SelectedItemTitleLabel;
                    }
                }
                /*
                RegisterAtomPubEventArgs arg = new();
                arg.NodeService = sd.NodeService;
                */
                // TODO
                //RegisterAtomPub?.Invoke(this, arg);
            }
        }
    }

    private bool CanAddSelectedAndClose()
    {
        if (SelectedLinkItem == null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(SelectedItemTitleLabel))
        {
            return false;
        }

        //
        if (SelectedLinkItem is not FeedLinkItem)
        {
            return false;
        }

        return true;
    }
}
