using System.ComponentModel;
using System.Windows;
using ClickCSValheimLauncher.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ClickCSValheimLauncher.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _viewModel;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.LogOutput))
        {
            LogTextBox.ScrollToEnd();
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.Owner = this;
        if (settingsWindow.ShowDialog() == true)
        {
            var settingsService = App.Services.GetRequiredService<Services.SettingsService>();
            _viewModel.ValheimPath = settingsService.Settings.ValheimPath ?? "Not detected - please set in Settings";
        }
    }

    private void RollbackButton_Click(object sender, RoutedEventArgs e)
    {
        var rollbackWindow = new RollbackWindow(_viewModel.ValheimPath);
        rollbackWindow.Owner = this;
        rollbackWindow.ShowDialog();
    }
}
