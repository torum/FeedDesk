using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FeedDesk.Contracts.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;

namespace FeedDesk.ViewModels;

public class ShellViewModel : ObservableRecipient
{
    public ICommand MenuFileExitCommand
    {
        get;
    }

    public ICommand MenuSettingsCommand
    {
        get;
    }

    public ICommand MenuHelpProjectPageCommand
    {
        get;
    }

    public ICommand MenuHelpProjectGitHubCommand
    {
        get;
    }

    public ICommand MenuGoToMainCommand
    {
        get;
    }

    public INavigationService NavigationService
    {
        get;
    }

    /*
    public INavigationViewService NavigationViewService
    {
        get;
    }

    private object? _selected;
    public object? Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }
    */

    private bool _isBackEnabled;
    public bool IsBackEnabled
    {
        get => _isBackEnabled;
        set => SetProperty(ref _isBackEnabled, value);
    }

    public ShellViewModel(INavigationService navigationService) //, INavigationViewService navigationViewService
    {
        NavigationService = navigationService;
        NavigationService.Navigated += OnNavigated;
        //NavigationViewService = navigationViewService;

        MenuFileExitCommand = new RelayCommand(OnMenuFileExit);
        MenuSettingsCommand = new RelayCommand(OnMenuSettings);
        MenuGoToMainCommand = new RelayCommand(OnMenuGoToMain);
        MenuHelpProjectPageCommand = new RelayCommand(OnMenuHelpProjectPageCommand);
        MenuHelpProjectGitHubCommand = new RelayCommand(OnMenuHelpProjectGitHubCommand);

    }

    private void OnNavigated(object sender, NavigationEventArgs e) //=> IsBackEnabled = NavigationService.CanGoBack;
    {
        IsBackEnabled = NavigationService.CanGoBack;

        /*
        if (e.SourcePageType == typeof(SettingsPage))
        {
            Selected = NavigationViewService.SettingsItem;
            return;
        }

        var selectedItem = NavigationViewService.GetSelectedItem(e.SourcePageType);
        if (selectedItem != null)
        {
            Selected = selectedItem;
        }
        */
    }

    private void OnMenuFileExit() => Application.Current.Exit();

    private void OnMenuSettings() => NavigationService.NavigateTo(typeof(SettingsViewModel).FullName!);

    private void OnMenuGoToMain() => NavigationService.NavigateTo(typeof(MainViewModel).FullName!);

    private async void OnMenuHelpProjectPageCommand()
    {
        Uri projectUri = new Uri("https://torum.github.io/FeedDesk/");
        
        await Windows.System.Launcher.LaunchUriAsync(projectUri);
    }

    private async void OnMenuHelpProjectGitHubCommand()
    {
        Uri projectUri = new Uri("https://github.com/torum/FeedDesk");

        await Windows.System.Launcher.LaunchUriAsync(projectUri);
    }

}
