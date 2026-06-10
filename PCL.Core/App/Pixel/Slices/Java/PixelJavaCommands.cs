using System.Collections.Generic;
using System.IO;
using MediatR;
using PCL.Core.App.Pixel.Events;

namespace PCL.Core.App.Pixel.Slices.Java;

public sealed record RefreshJavaEntriesCommand : IRequest<IReadOnlyList<PixelJavaEntrySnapshot>>;

public sealed record AddJavaEntryCommand(string JavaExecutablePath) : IRequest<PixelJavaEntrySnapshot>;

public sealed record SelectDefaultJavaByPathCommand(string? JavaExecutablePath) : IRequest;

public sealed record ToggleJavaEntryEnabledByPathCommand(string JavaExecutablePath) : IRequest<bool>;

public sealed record JavaEntriesRefreshedEvent(IReadOnlyList<PixelJavaEntrySnapshot> Entries);

public sealed record JavaEntryAddedEvent(PixelJavaEntrySnapshot Entry);

public sealed record DefaultJavaSelectedEvent(string? JavaExecutablePath);

public sealed class RefreshJavaEntriesCommandHandler(
    PixelJavaService javaService,
    IPixelEventBus eventBus)
    : IRequestHandler<RefreshJavaEntriesCommand, IReadOnlyList<PixelJavaEntrySnapshot>>
{
    public async Task<IReadOnlyList<PixelJavaEntrySnapshot>> Handle(RefreshJavaEntriesCommand request, CancellationToken cancellationToken)
    {
        var entries = await javaService.RefreshAsync(cancellationToken).ConfigureAwait(false);
        var snapshots = javaService.CreateEntrySnapshots(entries);
        eventBus.Publish(new JavaEntriesRefreshedEvent(snapshots));
        return snapshots;
    }
}

public sealed class AddJavaEntryCommandHandler(
    PixelJavaService javaService,
    IPixelEventBus eventBus)
    : IRequestHandler<AddJavaEntryCommand, PixelJavaEntrySnapshot>
{
    public async Task<PixelJavaEntrySnapshot> Handle(AddJavaEntryCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.JavaExecutablePath) || !File.Exists(request.JavaExecutablePath))
            throw new FileNotFoundException("请选择有效的 Java 程序。", request.JavaExecutablePath);

        var entry = await javaService.AddAsync(request.JavaExecutablePath, cancellationToken).ConfigureAwait(false);
        var snapshot = javaService.CreateEntrySnapshot(entry);
        eventBus.Publish(new JavaEntryAddedEvent(snapshot));
        return snapshot;
    }
}

public sealed class SelectDefaultJavaByPathCommandHandler(
    PixelJavaService javaService,
    IPixelEventBus eventBus)
    : IRequestHandler<SelectDefaultJavaByPathCommand>
{
    public Task Handle(SelectDefaultJavaByPathCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        javaService.SelectDefault(request.JavaExecutablePath);
        eventBus.Publish(new DefaultJavaSelectedEvent(request.JavaExecutablePath));
        return Task.CompletedTask;
    }
}

public sealed class ToggleJavaEntryEnabledByPathCommandHandler(
    PixelJavaService javaService,
    IPixelEventBus eventBus)
    : IRequestHandler<ToggleJavaEntryEnabledByPathCommand, bool>
{
    public Task<bool> Handle(ToggleJavaEntryEnabledByPathCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var enabled = javaService.ToggleEnabled(request.JavaExecutablePath);
        eventBus.Publish(new JavaEntryEnabledChangedByPathEvent(request.JavaExecutablePath, enabled));
        return Task.FromResult(enabled);
    }
}

public sealed record JavaEntryEnabledChangedByPathEvent(string JavaExecutablePath, bool IsEnabled);
