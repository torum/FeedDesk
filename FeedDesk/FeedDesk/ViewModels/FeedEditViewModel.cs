using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FeedDesk.Models;
using FeedDesk.Services.Contracts;
using FeedDesk.Views;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Input;

namespace FeedDesk.ViewModels;

public class FeedEditViewModel : ObservableRecipient
{
    //private readonly INavigationService _navigationService;

    #region == Properties ==

    private NodeFeed? _feed;
    public NodeFeed? Feed
    {
        get => _feed;
        set => SetProperty(ref _feed, value);
    }

    private string? _name = "";
    public string? Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    #endregion

    #region == Command ==

    public ICommand GoBackCommand
    {
        get;
    }

    public ICommand UpdateFeedItemPropertyCommand
    {
        get;
    }

    public ICommand CheckIfFeedIsValidUsingValidatorCommand
    {
        get;
    }

    #endregion

    public FeedEditViewModel()
    {
        //_navigationService = navigationService;

        GoBackCommand = new RelayCommand(OnGoBack);
        UpdateFeedItemPropertyCommand = new RelayCommand(OnUpdateFeedItemProperty);
        CheckIfFeedIsValidUsingValidatorCommand = new RelayCommand(OnCheckIfFeedIsValidUsingValidator);

        Feed = null;
        Name = "";
    }

    public void OnNavigatedTo(object parameter)
    {
        Debug.WriteLine("OnNavigatedTo FeedEditViewModel");
        Feed = null;
        Name = "";

        if (parameter is NodeTree)
        {
            if (parameter is NodeFeed feed)
            {
                Feed = feed;
                Name = feed.Name;
            }
        }
    }

    public void OnNavigatedFrom()
    {
    }

    private void OnGoBack()
    {
        var shell = App.GetService<ShellPage>();
        _ = shell.NavFrame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        /*
        if (_navigationService.CanGoBack)
        {
            _navigationService.GoBack();
        }
        */
    }

    private void OnCheckIfFeedIsValidUsingValidator()
    {
        if (Feed is null)
            return;

        if (Feed.EndPoint is not null)
        {
            var hoge = new Uri("https://validator.w3.org/feed/check.cgi?url=" + HttpUtility.UrlEncode(Feed.EndPoint.AbsoluteUri));

            Task.Run(() => Windows.System.Launcher.LaunchUriAsync(hoge));
        }
    }

    private void OnUpdateFeedItemProperty()
    {
        if (!string.IsNullOrEmpty(Name))
        {
            if (Feed != null)
            {
                /* Not good when navigate go back.
                var hoge = new NodeTreePropertyChangedArgs();
                hoge.Name = Name;
                hoge.Node = Feed;
                _navigationService.NavigateTo(typeof(MainViewModel).FullName!, hoge);
                */

                Task.Run(() =>
                {
                    var vm = App.GetService<MainViewModel>();
                    _ = vm.UpdateFeedAsync(Feed, Name);
                });
                
            }

            var shell = App.GetService<ShellPage>();
            _ = shell.NavFrame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
            //_navigationService.NavigateTo(typeof(MainViewModel).FullName!, null);
        }
    }
}
