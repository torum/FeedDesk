using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FeedDesk.Models;
using FeedDesk.Services.Contracts;
using FeedDesk.Views;
using Microsoft.UI.Xaml.Media.Animation;
using System.Windows.Input;

namespace FeedDesk.ViewModels;

public class FolderAddViewModel : ObservableRecipient
{
    //private readonly INavigationService _navigationService;

    #region == Properties ==
    
    private NodeTree? _targetNode;
    /*
    public NodeTree? Folder
    {
        get => _folder;
        set => SetProperty(ref _folder, value);
    }
    */

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

    public ICommand AddFolderItemPropertyCommand
    {
        get;
    }


    #endregion

    public FolderAddViewModel()
    {
        //_navigationService = navigationService;

        GoBackCommand = new RelayCommand(OnGoBack);
        AddFolderItemPropertyCommand = new RelayCommand(OnAddFolderItemProperty);

        Name = "";
        _targetNode = null;
    }

    public void OnNavigatedTo(object parameter)
    {
        Name = "";
        _targetNode = null;

        if (parameter is null)
            return;

        if (parameter is NodeTree)
        {
            _targetNode = parameter as NodeTree;
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

    private void OnAddFolderItemProperty()
    {
        if (!string.IsNullOrEmpty(Name))
        {
            if (_targetNode != null)
            {
                //Folder.Name = Name;
                //_targetNode

                NodeFolder folder = new NodeFolder(Name);
                folder.Parent = _targetNode;

                _targetNode.IsExpanded = true;
                _targetNode.Children.Insert(0,folder);

                folder.IsSelected = true;

                //_navigationService.NavigateTo(typeof(MainViewModel).FullName!, null);

                var shell = App.GetService<ShellPage>();
                _ = shell.NavFrame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
            }
        }
    }
}
