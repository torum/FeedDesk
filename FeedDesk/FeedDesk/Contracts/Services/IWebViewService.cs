using Microsoft.UI.Xaml.Controls;

using Microsoft.Web.WebView2.Core;
namespace FeedDesk.Contracts.Services;

public interface IWebViewService
{
    Uri? Source
    {
        get;
    }

    void NavigateToString(string str);

    bool CanGoBack
    {
        get;
    }

    bool CanGoForward
    {
        get;
    }

    event EventHandler<CoreWebView2WebErrorStatus>? NavigationCompleted;
    event EventHandler<CoreWebView2InitializedEventArgs>? CoreWebView2Initialized;

    void Initialize(WebView2 webView);

    void GoBack();

    void GoForward();

    void Reload();

    void UnregisterEvents();
}
