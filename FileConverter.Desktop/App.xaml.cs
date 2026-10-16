using System.Windows;
using FileConverter.Core.Interfaces;
using FileConverter.Core.Services;
using FileConverter.Desktop.Bridge;
using FileConverter.Desktop.Services;
using FileConverter.Infrastructure.Cli;
using FileConverter.Infrastructure.Converters;
using FileConverter.Infrastructure.FileSystem;
using FileConverter.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FileConverter.Desktop;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Binary Locator
        services.AddSingleton<IBinaryLocator, BinaryLocator>();

        // Infrastructure Layer - Base Services
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddTransient<IPdfConversionService, PdfConversionService>();
        services.AddTransient<IPdfExtractionService, PdfExtractionService>();
        services.AddTransient<IDocumentMergeService, DocumentMergeService>();
        services.AddTransient<IDocumentSplitService, DocumentSplitService>();
        services.AddTransient<IImageConversionService, ImageConversionService>();
        services.AddTransient<IDataConversionService, DataConversionService>();
        services.AddTransient<IDocumentConversionService, DocumentConversionService>();
        services.AddTransient<IPresentationConversionService, PresentationConversionService>();

        // Extensible IFileConverter Engine Adapters
        services.AddTransient<IFileConverter, PowerPointConverter>();
        services.AddTransient<IFileConverter, WordConverter>();
        services.AddTransient<IFileConverter, ExcelConverter>();
        services.AddTransient<IFileConverter, FfmpegConverter>();
        services.AddTransient<IFileConverter, LibreOfficeConverter>();
        services.AddTransient<IFileConverter, PandocConverter>();
        services.AddTransient<IFileConverter, ImageMagickConverter>();
        services.AddTransient<IFileConverter, SharpCompressArchiveConverter>();
        services.AddTransient<IFileConverter, AssimpModelConverter>();
        services.AddTransient<IFileConverter, ManagedFallbackConverter>();

        // Core Business Logic Layer
        services.AddTransient<IConversionOrchestrator, ConversionOrchestrator>();

        // Desktop Presentation Layer
        services.AddSingleton<INativeDialogService, NativeDialogService>();
        services.AddSingleton<IpcBridge>();
        services.AddTransient<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
