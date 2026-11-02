using System.Windows.Controls;
using FgcKeyboardSplitter.ViewModels;

namespace FgcKeyboardSplitter.Views;

public partial class LatencyPage : Page
{
    public LatencyPage(LatencyViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        Loaded += (_, _) => viewModel.SetActive(true);
        Unloaded += (_, _) => viewModel.SetActive(false);
    }
}
