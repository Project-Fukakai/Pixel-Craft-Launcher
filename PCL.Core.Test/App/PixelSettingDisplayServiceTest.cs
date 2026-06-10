using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Pixel;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelSettingDisplayServiceTest
{
    private object? _oldWindowType;
    private object? _oldPreLaunchCommand;

    [TestInitialize]
    public void Initialize()
    {
        ConfigService.EnsureInitializedForTesting();
        _oldWindowType = PixelSettingsBinder.LoadValue("LaunchArgumentWindowType");
        _oldPreLaunchCommand = PixelSettingsBinder.LoadValue("LaunchAdvanceRun");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_oldWindowType is not null)
            PixelSettingsBinder.SetValue("LaunchArgumentWindowType", _oldWindowType);
        if (_oldPreLaunchCommand is not null)
            PixelSettingsBinder.SetValue("LaunchAdvanceRun", _oldPreLaunchCommand);
    }

    [TestMethod]
    public void CustomWindowSizeFieldsOnlyDisplayForCustomWindowMode()
    {
        var service = new PixelSettingDisplayService();
        var width = new PixelSettingDescriptor("Width", PixelSettingControlKind.Text, "LaunchArgumentWindowWidth");

        PixelSettingsBinder.SetValue("LaunchArgumentWindowType", (int)GameWindowSizeMode.Default);
        Assert.IsFalse(service.ShouldDisplay(width));

        PixelSettingsBinder.SetValue("LaunchArgumentWindowType", (int)GameWindowSizeMode.Custom);
        Assert.IsTrue(service.ShouldDisplay(width));
    }

    [TestMethod]
    public void RunWaitOnlyDisplaysWhenPreLaunchCommandExists()
    {
        var service = new PixelSettingDisplayService();
        var runWait = new PixelSettingDescriptor("Wait", PixelSettingControlKind.Toggle, "LaunchAdvanceRunWait");

        PixelSettingsBinder.SetValue("LaunchAdvanceRun", string.Empty);
        Assert.IsFalse(service.ShouldDisplay(runWait));
        Assert.IsFalse(service.IsRunWaitVisible());

        PixelSettingsBinder.SetValue("LaunchAdvanceRun", "echo ok");
        Assert.IsTrue(service.ShouldDisplay(runWait));
        Assert.IsTrue(service.IsRunWaitVisible());
    }
}
