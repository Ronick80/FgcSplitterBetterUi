using Wpf.Ui.Abstractions;

namespace FgcKeyboardSplitter.Services;

/// <summary>Hands the navigation view the page instances built at startup, so each page keeps its state.</summary>
public sealed class AppPageProvider(IReadOnlyDictionary<Type, object> pages) : INavigationViewPageProvider
{
    public object? GetPage(Type pageType) => pages.GetValueOrDefault(pageType);
}

public interface INavigator
{
    void ShowDrivers();

    void ShowMapping();
}
