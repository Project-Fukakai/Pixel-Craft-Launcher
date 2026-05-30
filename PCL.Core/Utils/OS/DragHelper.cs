using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace PCL.Core.Utils.OS;

public partial class DragHelper
{
    public event EventHandler? DragDrop;

    public string[]? DropFilePaths { get; private set; }
    public Point DropDragPoint { get; private set; }
    private Interactive? _target;

    [Obsolete("Use Attach(Interactive) to bind Avalonia drag-and-drop events.")]
    public void AddHook()
    {
        if (_target is null)
            throw new InvalidOperationException("Avalonia 拖放需要先调用 Attach(Interactive) 绑定目标控件。");
    }

    [Obsolete("Use Detach() to remove Avalonia drag-and-drop events.")]
    public void RemoveHook()
    {
        Detach();
    }

    public void Attach(Interactive target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (ReferenceEquals(_target, target)) return;

        Detach();
        _target = target;
        Avalonia.Input.DragDrop.SetAllowDrop(target, true);
        Avalonia.Input.DragDrop.AddDragOverHandler(target, OnDragOver);
        Avalonia.Input.DragDrop.AddDropHandler(target, OnDrop);
    }

    public void Detach()
    {
        if (_target is null) return;
        Avalonia.Input.DragDrop.RemoveDragOverHandler(_target, OnDragOver);
        Avalonia.Input.DragDrop.RemoveDropHandler(_target, OnDrop);
        _target = null;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        try
        {
            var paths = await GetFilePathsAsync(e.DataTransfer).ConfigureAwait(false);
            if (paths.Length == 0) return;
            var point = sender is Visual visual ? e.GetPosition(visual) : default;
            RaiseDrop(paths, point);
            e.Handled = true;
        }
        catch
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    public void RaiseDrop(string[] filePaths, Point point)
    {
        DropFilePaths = filePaths;
        DropDragPoint = point;
        DragDrop?.Invoke(this, EventArgs.Empty);
    }

    private static async Task<string[]> GetFilePathsAsync(IDataTransfer dataTransfer)
    {
        var values = dataTransfer.TryGetValues(DataFormat.File);
        if (values is null) return [];

        var paths = values
            .Select(static item => item.TryGetLocalPath())
            .Where(static path => !string.IsNullOrEmpty(path))
            .Select(static path => path!)
            .ToArray();

        await Task.CompletedTask.ConfigureAwait(false);
        return paths;
    }
}
