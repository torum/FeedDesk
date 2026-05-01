using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FeedDesk.Models;
using FeedDesk.Services;
using FeedDesk.Services.Contracts;
using FeedDesk.Views;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FeedDesk.ViewModels;

public partial class FeedAddViewModel : ObservableRecipient
{
    #region == Properties ==

    public bool IsBusy
    {
        get;
        set
        {
            SetProperty(ref field, value);

            IsButtonEnabled = value;
        }
    }

    [ObservableProperty]
    public partial bool IsShowError { get; set; }
    [ObservableProperty]
    public partial bool IsShowLog { get; set; }
    [ObservableProperty]
    public partial bool IsButtonEnabled { get; private set; }

    public string WebsiteOrEndpointUrl
    {
        get;
        set
        {
            if (SetProperty(ref field, value.Trim()))
            {
                GoCommand.NotifyCanExecuteChanged();
            }
        }
    } = "";

    [ObservableProperty]
    public partial string UserIdAtomPub { get; set; } = "";
    [ObservableProperty]
    public partial string ApiKeyAtomPub { get; set; } = "";
    [ObservableProperty]
    public partial AuthTypes AuthType { get; set; } = AuthTypes.Wsse;
    [ObservableProperty]
    public partial string? SelectedItemType { get; set; }

    public string? SelectedItemTitleLabel
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AddSelectedAndCloseCommand.NotifyCanExecuteChanged();
            }
        }
    }

    [ObservableProperty]
    public partial bool IsXmlRpc { get; set; } = false;
    [ObservableProperty]
    public partial string UserIdXmlRpc { get; set; } = "";
    [ObservableProperty]
    public partial string PasswordXmlRpc { get; set; } = "";
    [ObservableProperty]
    public partial string StatusText { get; set; } = "";
    [ObservableProperty]
    public partial string StatusTitleText { get; set; } = "";
    [ObservableProperty]
    public partial string StatusLogText { get; set; } = "";
    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; } = 0;
    [ObservableProperty]
    public partial ObservableCollection<LinkItem> LinkItems { get; set; } = [];

    public LinkItem? SelectedLinkItem
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                GoSelectedCommand.NotifyCanExecuteChanged();
            }
        }
    }

    #endregion

    #region == Services ==

    private readonly IAutoDiscoveryService _serviceDiscovery;
    private readonly IDispatcherService _dispatcherService;

    #endregion

    public FeedAddViewModel(IAutoDiscoveryService serviceDiscovery, IDispatcherService dispatcherService)
    {
        _serviceDiscovery = serviceDiscovery;
        _serviceDiscovery.StatusUpdate += new AutoDiscoveryStatusUpdateEventHandler(OnStatusUpdate);//new ServiceDiscovery.ServiceDiscoveryStatusUpdate(OnStatusUpdate);
        _dispatcherService = dispatcherService;
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

    #region == Commands ==

    [RelayCommand]
    private void GoBack()
    {
        var shell = App.GetService<ShellPage>();
        _ = shell.NavFrame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    [RelayCommand]
    private void GoToFirstTab()
    {
        GoToFirstPage();
    }

    [RelayCommand]
    private void GoToSecondTab()
    {
        GoToSelectFeedOrServicePage();
    }

    [RelayCommand]
    private void GoToThirdTab()
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

    [RelayCommand]
    private void GoToFourthTab()
    {
        GoToServiceFoundPage();
    }

    private void OnStatusUpdate(AutoDiscoveryService sender, string data)
    {
        _ = _dispatcherService.TryEnqueue(() =>
        {
            StatusLogText = StatusLogText + data + Environment.NewLine;
        });
    }

    [RelayCommand(CanExecute = nameof(CanGo))]
    private async Task Go()
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
            var sr = await _serviceDiscovery.DiscoverService(uri, true, true);

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

            // TODO: not implemented.
            // Aut hRequired returned. Probably API endpoint.
            if (sr is ServiceResultAuthRequired sra)
            {
                IsShowError = false;
                IsShowLog = false;

                // Auth input page.(Not implemented)
                //GoToAuthInputPage();

                StatusTitleText = "Authorization Required";
                StatusText = $"";

                IsShowError = true;
                IsShowLog = false;

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

    [RelayCommand(CanExecute = nameof(CanGoSelected))]
    private void GoSelected()
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

    [RelayCommand(CanExecute = nameof(CanAddSelectedAndClose))]
    private async Task AddSelectedAndClose()
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
            await vm.AddFeed(fli.FeedLinkData);

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

    #endregion
}
