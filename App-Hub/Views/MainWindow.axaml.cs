using AppHub.ViewModels;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace AppHub.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Drag & Drop: Programme aus dem Explorer ins Fenster ziehen
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
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

    private static bool HasFiles(DragEventArgs e) => e.DataTransfer.Contains(DataFormat.File);

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (HasFiles(e))
            DropOverlay.IsVisible = true;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = HasFiles(e) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        // Sobald das Overlay sichtbar ist, liegt es über allem. Verlässt die Maus es, ist sie aus dem Fenster raus.
        if (e.Source == DropOverlay)
            DropOverlay.IsVisible = false;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        DropOverlay.IsVisible = false;

        if (DataContext is not MainWindowViewModel vm || e.DataTransfer.TryGetFiles() is not { } files)
            return;

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .OfType<string>()
            .ToList();

        if (paths.Count > 0)
            vm.AddDroppedPaths(paths);
    }
}
