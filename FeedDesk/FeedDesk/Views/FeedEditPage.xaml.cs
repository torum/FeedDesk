using FeedDesk.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace FeedDesk.Views;

public sealed partial class FeedEditPage : Page
{
    public MainViewModel ViewModel
    {
        get;
    }

    public FeedEditPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        this.InitializeComponent();
    }

    private void Page_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        this.NameTextBox.Focus(FocusState.Programmatic);
    }
}
