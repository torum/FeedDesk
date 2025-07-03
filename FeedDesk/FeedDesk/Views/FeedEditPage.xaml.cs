using FeedDesk.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FeedDesk.Views;

public sealed partial class FeedEditPage : Page
{
    public FeedEditViewModel ViewModel
    {
        get;
    }

    public FeedEditPage()
    {
        ViewModel = App.GetService<FeedEditViewModel>();
        this.InitializeComponent();
    }
}
