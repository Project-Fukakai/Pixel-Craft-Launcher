using PCL.Core.App.Pixel;
using Pixel_Craft_Launcher.Views;

namespace Pixel_Craft_Launcher.Settings;

public enum PixelToolFeature
{
    GameLink,
    Help,
    Test
}

public enum PixelInstanceFeature
{
    Edit,
    Export,
    Save,
    Screenshot,
    Mod,
    ResourcePack,
    Shader,
    Schematic,
    Server
}

public enum PixelFunctionFeature
{
    Select,
    ModUpdate,
    Hidden
}

public static class FeatureVisibilityService
{
    public static bool IsMainPageVisible(MainPageKind page)
    {
        return page switch
        {
            MainPageKind.Download => !IsHidden("UiHiddenPageDownload"),
            MainPageKind.Setup => !IsHidden("UiHiddenPageSetup"),
            MainPageKind.Tools => !IsHidden("UiHiddenPageTools"),
            _ => true
        };
    }

    public static bool IsSetupSectionVisible(PixelSettingSectionKind section)
    {
        if (section == PixelSettingSectionKind.LauncherMisc)
            return true;

        return !IsHidden(PixelSettingsCatalog.Get(section).HiddenConfigKey);
    }

    public static bool IsToolVisible(PixelToolFeature feature)
    {
        return feature switch
        {
            PixelToolFeature.GameLink => !IsHidden("UiHiddenToolsGameLink"),
            PixelToolFeature.Help => !IsHidden("UiHiddenToolsHelp"),
            PixelToolFeature.Test => !IsHidden("UiHiddenToolsTest"),
            _ => true
        };
    }

    public static bool IsInstanceFeatureVisible(PixelInstanceFeature feature)
    {
        return feature switch
        {
            PixelInstanceFeature.Edit => !IsHidden("UiHiddenVersionEdit"),
            PixelInstanceFeature.Export => !IsHidden("UiHiddenVersionExport"),
            PixelInstanceFeature.Save => !IsHidden("UiHiddenVersionSave"),
            PixelInstanceFeature.Screenshot => !IsHidden("UiHiddenVersionScreenshot"),
            PixelInstanceFeature.Mod => !IsHidden("UiHiddenVersionMod"),
            PixelInstanceFeature.ResourcePack => !IsHidden("UiHiddenVersionResourcePack"),
            PixelInstanceFeature.Shader => !IsHidden("UiHiddenVersionShader"),
            PixelInstanceFeature.Schematic => !IsHidden("UiHiddenVersionSchematic"),
            PixelInstanceFeature.Server => !IsHidden("UiHiddenVersionServer"),
            _ => true
        };
    }

    public static bool IsFunctionVisible(PixelFunctionFeature feature)
    {
        return feature switch
        {
            PixelFunctionFeature.Select => !IsHidden("UiHiddenFunctionSelect"),
            PixelFunctionFeature.ModUpdate => !IsHidden("UiHiddenFunctionModUpdate"),
            PixelFunctionFeature.Hidden => !IsHidden("UiHiddenFunctionHidden"),
            _ => true
        };
    }

    private static bool IsHidden(string? key) => PixelSettingsBinder.IsHidden(key);
}
