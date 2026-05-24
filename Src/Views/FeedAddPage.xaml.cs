using FeedDesk.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FeedDesk.Views;

internal sealed partial class FeedAddPage : Page
{
    public FeedAddViewModel ViewModel
    {
        get;
    }

    public FeedAddPage()
    {
        ViewModel = App.GetService<FeedAddViewModel>();
        this.InitializeComponent();
    }

    private void UrlTextBox_EnterInvoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.GoCommand.Execute(null);
    }

    private void Page_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        this.UrlTextBox.Focus(FocusState.Programmatic);
    }
}
