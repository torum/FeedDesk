using FeedDesk.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FeedDesk.Views;

internal sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        ViewModel = App.GetService<MainViewModel>();

        InitializeComponent();
    }

    public MainViewModel ViewModel
    {
        get;
    }
}
