using System.Windows.Input;
using FeedDesk.Contracts.Services;
using FeedDesk.Contracts.ViewModels;
using XmlClients.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FeedDesk.ViewModels;

public class FolderEditViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;

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

    public FolderEditViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;

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
        if (_navigationService.CanGoBack)
        {
            _navigationService.GoBack();
        }
    }


    private void OnUpdateFolderItemProperty()
    {
        if (!string.IsNullOrEmpty(Name))
        {
            if (Folder != null)
            {
                Folder.Name = Name;
            }
            _navigationService.NavigateTo(typeof(MainViewModel).FullName!, null);
        }
    }
}
