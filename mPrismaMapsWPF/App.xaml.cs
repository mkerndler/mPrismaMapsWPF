using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using mPrismaMapsWPF.Services;
using mPrismaMapsWPF.ViewModels;

namespace mPrismaMapsWPF;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<IDocumentService, DocumentService>();
        services.AddSingleton<ISelectionService, SelectionService>();
        services.AddSingleton<IUndoRedoService, UndoRedoService>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
