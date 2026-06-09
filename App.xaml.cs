using Microsoft.Extensions.DependencyInjection;
using NMEAReceiver.Services;
using NMEAReceiver.Services.Interfaces;
using NMEAReceiver.ViewModels.Panels;
using NMEAReceiver.ViewModels.Shell;
using System.Windows;

namespace NMEAReceiver;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        Services = ConfigureServices();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<MainStateStore>();
        services.AddSingleton<IComPortService, ComPortService>();
        services.AddSingleton<IIniPersistenceService, IniService>();

        services.AddTransient<INmeaSentenceProcessorService, NmeaSentenceProcessorService>();
        services.AddTransient<IIosSentenceSocketService, IosSentenceSocketService>();
        services.AddTransient<IWinRs232cReceiverService, WinRs232cReceiverService>();
        services.AddTransient<Func<IWinRs232cReceiverService>>(sp => () => sp.GetRequiredService<IWinRs232cReceiverService>());

        services.AddSingleton<IReceiverChannelService, ReceiverChannelService>();

        services.AddTransient<ChannelSetupViewModel>();
        services.AddTransient<ChannelsTableViewModel>();
        services.AddTransient<SnapshotViewModel>();
        services.AddTransient<DiagnosticViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MainWindow = Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (Services is IDisposable disposable)
            disposable.Dispose();
        base.OnExit(e);
    }
}
