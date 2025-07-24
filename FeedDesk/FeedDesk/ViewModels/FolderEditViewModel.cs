using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FeedDesk.Models;
using FeedDesk.Services.Contracts;
using FeedDesk.Views;
using Microsoft.UI.Xaml.Media.Animation;
using System.Windows.Input;

namespace FeedDesk.ViewModels;

public class FolderEditViewModel : ObservableRecipient
{
    //private readonly INavigationService _navigationService;

    #region == Properties ==
    
    private NodeFolder? _folder;
    public NodeFolder? Folder
    {
        get => _folder;
        set => SetProperty(ref _folder, value);
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

    public ICommand UpdateFolderItemPropertyCommand
    {
        get;
    }


    #endregion

    public FolderEditViewModel()
    {
        //_navigationService = navigationService;

        GoBackCommand = new RelayCommand(OnGoBack);
        UpdateFolderItemPropertyCommand = new RelayCommand(OnUpdateFolderItemProperty);

        Name = "";
        Folder = null;
    }

    public void OnNavigatedTo(object parameter)
    {
        Name = "";
        Folder = null;

        if (parameter is NodeTree)
        {
            if (parameter is NodeFolder folder)
            {
                Folder = folder;
                Name = folder.Name;
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


    private void OnUpdateFolderItemProperty()
    {
        if (!string.IsNullOrEmpty(Name))
        {
            if (Folder != null)
            {
                Folder.Name = Name;
            }

            var shell = App.GetService<ShellPage>();
            _ = shell.NavFrame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });

            //_navigationService.NavigateTo(typeof(MainViewModel).FullName!, null);
        }
    }
}
