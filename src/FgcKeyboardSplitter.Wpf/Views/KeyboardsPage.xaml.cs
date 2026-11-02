using System.Windows.Controls;
using FgcKeyboardSplitter.ViewModels;

namespace FgcKeyboardSplitter.Views;

public partial class KeyboardsPage : Page
{
    public KeyboardsPage(KeyboardsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
