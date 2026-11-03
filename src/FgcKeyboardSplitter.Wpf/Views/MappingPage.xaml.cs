using System.Windows.Controls;
using FgcKeyboardSplitter.ViewModels;

namespace FgcKeyboardSplitter.Views;

public partial class MappingPage : Page
{
    public MappingPage(MappingViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        Loaded += (_, _) => viewModel.SetPageVisible(true);
        Unloaded += (_, _) =>
        {
            // A binding left open would keep swallowing keys after the user navigates away.
            viewModel.CancelBinding();
            viewModel.SetPageVisible(false);
        };
    }
}
