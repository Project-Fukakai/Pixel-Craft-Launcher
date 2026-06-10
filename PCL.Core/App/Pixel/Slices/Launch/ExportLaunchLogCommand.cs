using System.IO;
using MediatR;
using PCL.Core.App;
using PCL.Core.App.Pixel.Events;

namespace PCL.Core.App.Pixel.Slices.Launch;

public sealed record ExportLaunchLogCommand(
    string Content,
    string? DirectoryPath = null,
    DateTimeOffset? Timestamp = null)
    : IRequest<string>;

public sealed record LaunchLogExportedEvent(string Path);

public sealed class ExportLaunchLogCommandHandler(IPixelEventBus eventBus)
    : IRequestHandler<ExportLaunchLogCommand, string>
{
    public Task<string> Handle(ExportLaunchLogCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = MinecraftLaunchLogExporter.Export(request.Content, request.DirectoryPath, request.Timestamp);
        eventBus.Publish(new LaunchLogExportedEvent(path));
        return Task.FromResult(path);
    }
}

public static class MinecraftLaunchLogExporter
{
    public static string Export(string content, string? directoryPath = null, DateTimeOffset? timestamp = null)
    {
        var directory = directoryPath ?? Path.Combine(Paths.SharedLocalData, "Launch", "Logs");
        Directory.CreateDirectory(directory);
        var time = timestamp?.DateTime ?? DateTime.Now;
        var fileName = $"launch-{time:yyyyMMdd-HHmmss}.log";
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, content);
        return path;
    }
}
