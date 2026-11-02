using System.Windows.Controls;
using FgcKeyboardSplitter.ViewModels;

namespace FgcKeyboardSplitter.Views;

public partial class DriversPage : Page
{
    public DriversPage(DriversViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
