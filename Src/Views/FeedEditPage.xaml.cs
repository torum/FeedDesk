using FeedDesk.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FeedDesk.Views;

internal sealed partial class FeedEditPage : Page
{
    public FeedEditPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        this.InitializeComponent();
    }

    public MainViewModel ViewModel
    {
        get;
    }

    private void Page_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        this.NameTextBox.Focus(FocusState.Programmatic);
    }
}
