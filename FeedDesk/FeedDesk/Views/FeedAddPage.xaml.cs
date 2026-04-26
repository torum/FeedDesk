using FeedDesk.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;

namespace FeedDesk.Views;

public sealed partial class FeedAddPage : Page
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
