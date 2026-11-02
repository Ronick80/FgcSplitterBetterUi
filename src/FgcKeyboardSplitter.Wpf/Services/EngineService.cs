using System.Windows.Threading;
using Fgc.Interop;

namespace FgcKeyboardSplitter.Services;

/// <summary>Owns the engine context and ticks on the UI thread so pages can poll status, sessions and events.</summary>
public sealed class EngineService : IDisposable
{
    private readonly DispatcherTimer _timer;

    public EngineService(FgcClient client)
    {
        Client = client;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (_, _) => Poll();
    }

    public FgcClient Client { get; }

    public FgcResult DriverResult { get; private set; } = FgcResult.NotFound;

    public DriverStatus? Drivers { get; private set; }

    public bool DriversReady => Drivers is { Ready: true };

    /// <summary>Every 200 ms, after pending engine events were delivered.</summary>
    public event EventHandler? Polled;

    public event EventHandler? DriversChanged;

    public event EventHandler<EngineEvent>? EngineEventReceived;

    public void Start()
    {
        RefreshDrivers();
        _timer.Start();
    }

    /// <summary>Queries Windows, so call it on demand rather than on every tick.</summary>
    public void RefreshDrivers()
    {
        DriverResult = Client.GetDriverStatus(out DriverStatus status);
        Drivers = DriverResult == FgcResult.Ok ? status : null;
        DriversChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Hands every keyboard back and unplugs the virtual pads; for crash paths that cannot run a normal shutdown.</summary>
    public void EmergencyStop()
    {
        try
        {
            Client.StopAll();
        }
        catch (Exception)
        {
            // Nothing more can be done while the process is going down.
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        Client.Dispose();
    }

    private void Poll()
    {
        while (Client.PollEvent(out EngineEvent? engineEvent) == FgcResult.Ok && engineEvent is { } received)
        {
            EngineEventReceived?.Invoke(this, received);
        }

        Polled?.Invoke(this, EventArgs.Empty);
    }
}
