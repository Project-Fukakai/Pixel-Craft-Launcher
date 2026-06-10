using PCL.Core.App;
using PCL.Core.App.Pixel;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils.OS;

namespace PCL.Core.App.Pixel.Slices.Launch;

public sealed class PixelMemoryPreviewService
{
    internal PixelMemoryPreviewSnapshot GetSnapshot(MinecraftInstanceInfo? selectedInstance)
    {
        var memory = KernelInterop.GetPhysicalMemoryBytes();
        var totalGb = memory.Total / 1024d / 1024d / 1024d;
        var availableGb = memory.Available / 1024d / 1024d / 1024d;
        var gameGb = EstimateGameMemoryGb(availableGb, selectedInstance);
        return CreateSnapshot(totalGb, availableGb, gameGb);
    }

    public static PixelMemoryPreviewSnapshot CreateSnapshot(double totalGb, double availableGb, double gameGb)
    {
        var usedGb = Math.Max(0, totalGb - availableGb);
        var gameActualGb = Math.Min(gameGb, Math.Max(availableGb, 0));
        var freeAfterLaunchGb = Math.Max(0, totalGb - usedGb - gameActualGb);
        return new PixelMemoryPreviewSnapshot(totalGb, usedGb, availableGb, gameGb, gameActualGb, freeAfterLaunchGb);
    }

    public static PixelMemoryPreviewTextSnapshot CreateTextSnapshot(PixelMemoryPreviewSnapshot snapshot)
    {
        var warningText = snapshot.GameGb > snapshot.AvailableGb
            ? $"当前可用内存只有 {FormatGb(snapshot.AvailableGb)}，游戏可实际分配约 {FormatGb(snapshot.GameActualGb)}。"
            : string.Empty;
        return new PixelMemoryPreviewTextSnapshot(
            $"总内存 {FormatGb(snapshot.TotalGb)}",
            $"已用 {FormatGb(snapshot.UsedGb)}",
            $"游戏预估 {FormatGb(snapshot.GameGb)}",
            $"启动后空闲 {FormatGb(snapshot.FreeAfterLaunchGb)}",
            warningText);
    }

    public static double EstimateGameMemoryGb(double availableGb) =>
        EstimateGameMemoryGb(availableGb, selectedInstance: null);

    internal static double EstimateGameMemoryGb(double availableGb, MinecraftInstanceInfo? selectedInstance)
    {
        if (selectedInstance is not null)
            return MinecraftLaunchService.GetGlobalConfiguredMemoryGb(selectedInstance, false);

        if (Config.Launch.MemoryAllocationMode == 1)
            return PixelSettingsBinder.RamScaleToGb(Config.Launch.CustomMemorySize);

        var give = 0d;
        Add(1.5d, 1d);
        Add(1d, 0.7d);
        Add(1.5d, 0.4d);
        Add(4d, 0.15d);
        return Math.Round(Math.Max(give, 0.5d), 1);

        void Add(double delta, double ratio)
        {
            if (availableGb < 0.1d) return;
            give += Math.Min(availableGb * ratio, delta);
            availableGb -= delta / ratio;
        }
    }

    private static string FormatGb(double value) => $"{Math.Round(value, 1):0.#} GB";
}

public readonly record struct PixelMemoryPreviewSnapshot(
    double TotalGb,
    double UsedGb,
    double AvailableGb,
    double GameGb,
    double GameActualGb,
    double FreeAfterLaunchGb);

public readonly record struct PixelMemoryPreviewTextSnapshot(
    string TotalText,
    string UsedText,
    string GameText,
    string FreeText,
    string WarningText)
{
    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningText);
}
