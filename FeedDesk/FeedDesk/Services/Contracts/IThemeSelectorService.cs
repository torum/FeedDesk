using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace FeedDesk.Services.Contracts;

public interface IThemeSelectorService
{
    void SetTheme(ElementTheme theme);
}
