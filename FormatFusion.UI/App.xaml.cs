using FormatFusion.Compression;
using FormatFusion.Core.Interfaces;
using FormatFusion.Core.Services;
using FormatFusion.Infrastructure.Engines;
using FormatFusion.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.IO;
using System.Windows;
using FFMpegCore;
using System;

namespace FormatFusion.UI;

public partial class App : Application
    {
        public App()
        {
            FormatFusion.UI.Services.NotificationService.Initialize();
        }
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Log.Fatal(ex, "AppDomain Unhandled Exception");
            Dispatcher.Invoke(() => 
            {
                var win = new FormatFusion.UI.Views.ThemedDialogWindow($"A critical error occurred: {ex?.Message}\n\nThe application will try to continue, but may be unstable.", "Unexpected Error", hideCancel: true);
                if (MainWindow != null && MainWindow.IsLoaded) win.Owner = MainWindow;
                win.ShowDialog();
            });
        };

        DispatcherUnhandledException += (s, args) =>
        {
            Log.Fatal(args.Exception, "Dispatcher Unhandled Exception");
            var win = new FormatFusion.UI.Views.ThemedDialogWindow($"An unexpected error occurred: {args.Exception.Message}", "Unexpected Error", hideCancel: true);
            if (MainWindow != null && MainWindow.IsLoaded) win.Owner = MainWindow;
            win.ShowDialog();
            args.Handled = true; // Prevent the app from crashing
        };

        FormatFusion.Infrastructure.ProcessJobTracker.Initialize();

        ConfigureLogging();
        ConfigureFFmpeg();

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Register engines into the FormatRegistry
        var registry = Services.GetRequiredService<FormatRegistry>();
        registry.Register(Services.GetRequiredService<ImageEngine>());
        registry.Register(Services.GetRequiredService<AudioEngine>());
        registry.Register(Services.GetRequiredService<VideoEngine>());
        registry.Register(Services.GetRequiredService<DocumentEngine>());
        registry.Register(Services.GetRequiredService<ArchiveEngine>());

        var appSettings = Services.GetRequiredService<AppSettings>();
        var ffmpegExePath = Path.Combine(AppContext.BaseDirectory, "Tools", "ffmpeg.exe");
        if (File.Exists(ffmpegExePath))
        {
            _ = appSettings.InitializeHardwareDetectionAsync(ffmpegExePath, a => Application.Current.Dispatcher.Invoke(a));
        }

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<AppSettings>();
        services.AddSingleton<FormatFusion.Infrastructure.Services.HardwareAccelerationResolver>();
        services.AddSingleton<FormatRegistry>();
        services.AddSingleton<IFormatRegistry>(sp => sp.GetRequiredService<FormatRegistry>());
        services.AddSingleton<TempFileManager>();
        services.AddSingleton<RecentJobsService>();
        services.AddSingleton<IUserPromptService, FormatFusion.UI.Services.WpfUserPromptService>();
        services.AddSingleton<IJobOrchestrator>(sp =>
            new JobOrchestrator(
                sp.GetRequiredService<IFormatRegistry>(),
                maxConcurrentJobs: GetDefaultConcurrency(),
                photoCompressor: sp.GetRequiredService<PhotoCompressor>(),
                videoCompressor: sp.GetRequiredService<VideoCompressor>()));

        // Engines
        services.AddSingleton<ImageEngine>();
        services.AddSingleton<AudioEngine>();
        services.AddSingleton<VideoEngine>();
        services.AddSingleton<DocumentEngine>();
        services.AddSingleton<ArchiveEngine>();

        // Compressors
        services.AddSingleton<PhotoCompressor>();
        services.AddSingleton<VideoCompressor>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<ConverterViewModel>();
        services.AddTransient<CompressorViewModel>();
        services.AddSingleton<QueueViewModel>();
        services.AddTransient<CompletionViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
        services.AddTransient<Views.HomeView>();
        services.AddTransient<Views.ConverterView>();
        services.AddTransient<Views.CompressorView>();
        services.AddTransient<Views.QueueView>();
        services.AddTransient<Views.CompletionView>();
        services.AddTransient<Views.SettingsView>();
    }

    private static void ConfigureFFmpeg()
    {
        var toolsDir = Path.Combine(AppContext.BaseDirectory, "Tools");
        if (Directory.Exists(toolsDir))
        {
            GlobalFFOptions.Configure(opts =>
            {
                opts.BinaryFolder = toolsDir;
                opts.TemporaryFilesFolder = Path.Combine(Path.GetTempPath(), "FormatFusion");
            });
        }
    }

    private static void ConfigureLogging()
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FormatFusion", "logs", "app-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 5)
            .CreateLogger();
    }

    private static int GetDefaultConcurrency()
    {
        var cores = Environment.ProcessorCount;
        return cores <= 2 ? 1 : Math.Min(cores / 2, 4);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services?.GetService<IJobOrchestrator>()?.CancelAll();
        Services?.GetService<TempFileManager>()?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}


