using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using RemittanceAdviceManager.Services.Interfaces;
using RemittanceAdviceManager.Services.Implementation;
using RemittanceAdviceManager.ViewModels;

namespace RemittanceAdviceManager;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    public App()
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
            .WriteTo.Console()
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;

                // Register HttpClient
                services.AddHttpClient<IWebDbAuthenticationService, WebDbAuthenticationService>(client =>
                {
                    var baseUrl = configuration["WebDb:BaseUrl"] ?? "https://localhost:44356";
                    client.BaseAddress = new Uri(baseUrl);
                    client.Timeout = TimeSpan.FromSeconds(
                        int.Parse(configuration["WebDb:TimeoutSeconds"] ?? "300"));
                });

                services.AddHttpClient<IRemittanceUploadService, RemittanceUploadService>();
                services.AddHttpClient<IReportDownloadService, ReportDownloadService>();

                // Register Services
                services.AddSingleton<IPdfProcessingService, PdfProcessingService>();
                services.AddSingleton<IWebDbAuthenticationService, WebDbAuthenticationService>();
                services.AddSingleton<IRemittanceUploadService, RemittanceUploadService>();
                services.AddSingleton<IReportDownloadService, ReportDownloadService>();
                services.AddSingleton<IPrintService, PrintService>();
                services.AddSingleton<IRemittanceDownloadService, RemittanceDownloadService>();

                // Register ViewModels
                services.AddTransient<MainWindowViewModel>();
                services.AddTransient<DownloadedFilesViewModel>();
                services.AddTransient<AlteredPdfsViewModel>();
                services.AddTransient<UploadToWebDbViewModel>();
                services.AddTransient<DownloadReportsViewModel>();

                // Register Windows
                services.AddTransient<MainWindow>();

            })
            .Build();
    }

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        await _host!.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(System.Windows.ExitEventArgs e)
    {
        await _host!.StopAsync();
        _host.Dispose();
        Log.CloseAndFlush();

        base.OnExit(e);
    }
}

