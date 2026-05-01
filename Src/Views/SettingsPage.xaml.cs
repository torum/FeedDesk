using FeedDesk.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FeedDesk.Views;

public sealed partial class SettingsPage : Page
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
