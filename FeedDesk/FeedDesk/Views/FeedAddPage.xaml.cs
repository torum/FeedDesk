using FeedDesk.ViewModels;
using Microsoft.UI.Xaml.Controls;

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
}
