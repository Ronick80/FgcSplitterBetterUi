using System.Windows;
using Fgc.Interop;
using FgcKeyboardSplitter.Services;
using FgcKeyboardSplitter.ViewModels;
using FgcKeyboardSplitter.Views;

namespace FgcKeyboardSplitter;

public partial class App : Application
{
    private EngineService? _engine;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            _engine?.EmergencyStop();
            MessageBox.Show(args.Exception.Message, "FGC Keyboard Splitter", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, _) => _engine?.EmergencyStop();

        FgcClient client;
        try
        {
            client = FgcClient.Create();
        }
        catch (Exception ex) when (ex is DllNotFoundException or FgcException or BadImageFormatException)
        {
            MessageBox.Show(
                $"The engine (FgcCore.dll) could not be started.\n\n{ex.Message}\n\nKeep FgcCore.dll next to this program.",
                "FGC Keyboard Splitter", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        _engine = new EngineService(client);

        var window = new MainWindow();
        window.UseScale(new UiScaleService(UiScaleService.DefaultSettingsPath));
        var selection = new KeyboardSelection();
        var capture = new KeyCaptureService();
        capture.Attach(window);
        var dialogs = new DialogService(window.DialogOverlay);

        var keyboardsViewModel = new KeyboardsViewModel(_engine, window, selection);
        var mappingViewModel = new MappingViewModel(_engine, selection, capture, dialogs);
        var driversViewModel = new DriversViewModel(_engine, $"FGC Keyboard Splitter UI {AppInfo.Version} - engine {FgcClient.EngineVersion}");
        var latencyViewModel = new LatencyViewModel(_engine, keyboardsViewModel);
        window.SetPages(new Dictionary<Type, object>
        {
            [typeof(KeyboardsPage)] = new KeyboardsPage(keyboardsViewModel),
            [typeof(MappingPage)] = new MappingPage(mappingViewModel),
            [typeof(LatencyPage)] = new LatencyPage(latencyViewModel),
            [typeof(DriversPage)] = new DriversPage(driversViewModel),
        });

        MainWindow = window;
        window.Show();
        _engine.Start();
        _ = keyboardsViewModel.InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _engine?.Dispose();
        base.OnExit(e);
    }
}
