using FgcKeyboardSplitter.Services;
using FgcKeyboardSplitter.Views;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace FgcKeyboardSplitter;

public partial class MainWindow : FluentWindow, INavigator
{
    public MainWindow()
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
    }

    public void SetPages(IReadOnlyDictionary<Type, object> pages)
    {
        RootNavigation.SetPageProviderService(new AppPageProvider(pages));
        Loaded += (_, _) => RootNavigation.Navigate(typeof(KeyboardsPage));
    }

    public ContentDialogHost DialogHost => RootDialogHost;

    public void ShowDrivers() => RootNavigation.Navigate(typeof(DriversPage));

    public void ShowMapping() => RootNavigation.Navigate(typeof(MappingPage));
}
