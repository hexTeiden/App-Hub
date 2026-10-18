using AppHub.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

namespace AppHub.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // Doppelklick auf einen Eintrag startet das Programm
    private void AppList_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm
            && (e.Source as Control)?.DataContext is AppEntryViewModel app
            && e.Source is not Button)
        {
            vm.LaunchCommand.Execute(app);
        }
    }
}
