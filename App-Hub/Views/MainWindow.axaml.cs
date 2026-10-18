using AppHub.ViewModels;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AppHub.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // Doppelklick auf einen Eintrag startet das Programm (nicht wenn auf einen Button geklickt wurde)
    private void AppList_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm
            && (e.Source as Control)?.DataContext is AppEntryViewModel app
            && (e.Source as Visual)?.FindAncestorOfType<Button>(includeSelf: true) is null)
        {
            vm.LaunchCommand.Execute(app);
        }
    }
}
