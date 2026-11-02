using System.Windows.Controls;
using FgcKeyboardSplitter.ViewModels;

namespace FgcKeyboardSplitter.Views;

public partial class MappingPage : Page
{
    public MappingPage(MappingViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        // A binding left open would keep swallowing keys after the user navigates away.
        Unloaded += (_, _) => viewModel.CancelBinding();
    }
}
