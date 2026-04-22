using Microsoft.UI.Xaml;

namespace FeedDesk.Services.Contracts;

public interface IThemeSelectorService
{
    void SetTheme(ElementTheme theme);
}
