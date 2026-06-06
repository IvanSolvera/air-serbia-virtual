using System.IO;
using System.Windows;
using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Acars.Desktop.Services;
using AirSerbiaVirtua.Acars.Desktop.Settings;
using AirSerbiaVirtua.Acars.Desktop.ViewModels;
using AirSerbiaVirtua.Acars.Desktop.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AirSerbiaVirtua.Acars.Desktop;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Last-resort guard: log UI-thread exceptions and keep the app alive
        // instead of silently terminating (e.g. a bad XAML resource in one view
        // must not take the whole client down mid-flight).
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash(args.Exception);
            MessageBox.Show(
                $"Unexpected error:\n\n{args.Exception.Message}\n\nDetails were written to the crash log.",
                "Air Serbia Virtual — ACARS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogCrash(args.ExceptionObject as Exception);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            })
            .ConfigureServices((ctx, services) =>
            {
                services.Configure<ApiOptions>(ctx.Configuration.GetSection("Api"));

                services.AddSingleton<ISessionService, SessionService>();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<SimulatorService>();
                services.AddSingleton<FlightSessionState>();

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();

                services.AddTransient<LoginViewModel>();
                services.AddTransient<LoginView>();

                // Pilot Centre / Logbook / Briefing / Debriefing are transient so
                // they reload current data each time they're navigated to.
                services.AddTransient<PilotCentreViewModel>();
                services.AddTransient<PilotCentreView>();
                services.AddTransient<LogbookViewModel>();
                services.AddTransient<LogbookView>();
                services.AddTransient<BriefingViewModel>();
                services.AddTransient<BriefingView>();
                services.AddTransient<DebriefingViewModel>();
                services.AddTransient<DebriefingView>();

                // Bookings + ACARS views are singleton so their loaded state and
                // background subscriptions survive navigation.
                services.AddSingleton<BookingsViewModel>();
                services.AddSingleton<BookingsView>();
                services.AddSingleton<AcarsViewModel>();
                services.AddSingleton<AcarsView>();
            })
            .Build();

        await _host.StartAsync();

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.DataContext = _host.Services.GetRequiredService<MainViewModel>();
        window.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }
        base.OnExit(e);
    }

    private static void LogCrash(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AirSerbiaVirtua");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "crash.log"),
                $"[{DateTimeOffset.UtcNow:O}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never throw from a crash handler.
        }
    }
}
