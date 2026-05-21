using FeedDesk.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FeedDesk.Views;

internal sealed partial class SettingsPage : Page
{
    public MainViewModel ViewModel
    {
        get;
    }

    public SettingsPage()
    {
        ViewModel = App.GetService<MainViewModel>();

        InitializeComponent();
    }
}
