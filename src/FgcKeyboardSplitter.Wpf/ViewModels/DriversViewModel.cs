using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fgc.Interop;
using FgcKeyboardSplitter.Services;

namespace FgcKeyboardSplitter.ViewModels;

public sealed partial class DriversViewModel : ObservableObject
{
    private readonly EngineService _engine;

    public DriversViewModel(EngineService engine, string aboutText = "")
    {
        _engine = engine;
        AboutText = aboutText;
        _engine.DriversChanged += (_, _) => Update();
        Update();
    }

    public string AboutText { get; }

    [ObservableProperty]
    private string _interceptionStatus = string.Empty;

    [ObservableProperty]
    private string _interceptionDetail = string.Empty;

    [ObservableProperty]
    private StatusKind _interceptionKind;

    [ObservableProperty]
    private string _scpVBusStatus = string.Empty;

    [ObservableProperty]
    private string _scpVBusDetail = string.Empty;

    [ObservableProperty]
    private StatusKind _scpVBusKind;

    [ObservableProperty]
    private string _queryProblem = string.Empty;

    [ObservableProperty]
    private string _setupMessage = string.Empty;

    [ObservableProperty]
    private bool _setupMessageIsError;

    public bool HasQueryProblem => QueryProblem.Length > 0;

    public bool HasSetupMessage => SetupMessage.Length > 0;

    public bool SetupAvailable => DriverSetupLauncher.IsAvailable;

    public string SetupUnavailableText => $"FgcDriverSetup.exe was not found in {AppContext.BaseDirectory}. Keep it next to this program.";

    [RelayCommand]
    private void Recheck() => _engine.RefreshDrivers();

    [RelayCommand(CanExecute = nameof(SetupAvailable))]
    private async Task RunSetupAsync()
    {
        SetupMessageIsError = false;
        SetupMessage = "Driver setup is running in its own window. This page updates when it closes.";
        SetupOutcome outcome = await DriverSetupLauncher.RunAsync();
        _engine.RefreshDrivers();

        (SetupMessage, SetupMessageIsError) = outcome switch
        {
            SetupOutcome.Finished => ("Driver setup finished.", false),
            SetupOutcome.RestartPending => ("Driver setup finished. Restart Windows to activate the drivers.", false),
            SetupOutcome.Cancelled => ("Driver setup was cancelled.", false),
            SetupOutcome.ToolMissing => (SetupUnavailableText, true),
            _ => ("Driver setup reported a problem. FgcDriverSetup.log next to the tool has the details.", true),
        };
    }

    partial void OnQueryProblemChanged(string value) => OnPropertyChanged(nameof(HasQueryProblem));

    partial void OnSetupMessageChanged(string value) => OnPropertyChanged(nameof(HasSetupMessage));

    private void Update()
    {
        if (_engine.Drivers is not { } drivers)
        {
            QueryProblem = $"Could not query the drivers: {FgcErrors.Describe(_engine.DriverResult)}";
            (InterceptionStatus, InterceptionDetail, InterceptionKind) = ("Unknown", string.Empty, StatusKind.Neutral);
            (ScpVBusStatus, ScpVBusDetail, ScpVBusKind) = ("Unknown", string.Empty, StatusKind.Neutral);
            return;
        }

        QueryProblem = string.Empty;
        (InterceptionStatus, InterceptionDetail, InterceptionKind) = drivers.Interception switch
        {
            InterceptionState.Installed => ("Installed", "Captures one keyboard and hides it from other programs.", StatusKind.Good),
            InterceptionState.RebootRequired => ("Restart required", "Installed but not active yet. Restart Windows to finish.", StatusKind.Warning),
            _ => ("Not installed", "Run the driver setup below, then restart Windows.", StatusKind.Bad),
        };
        (ScpVBusStatus, ScpVBusDetail, ScpVBusKind) = drivers.ScpVBusInstalled
            ? ("Installed", "Provides the virtual Xbox 360 controllers.", StatusKind.Good)
            : ("Not installed", "Run the driver setup below, then check again.", StatusKind.Bad);
    }
}
