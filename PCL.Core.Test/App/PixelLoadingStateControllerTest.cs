using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Pixel.Infrastructure;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelLoadingStateControllerTest
{
    [TestMethod]
    public void StateChangePublishesOldAndNewState()
    {
        var controller = new PixelLoadingStateController();
        PixelLoadingState? oldState = null;
        PixelLoadingState? newState = null;
        controller.StateChanged += (_, args) =>
        {
            oldState = args.OldState;
            newState = args.State;
        };

        controller.State = PixelLoadingState.Run;

        Assert.AreEqual(PixelLoadingState.Unloaded, oldState);
        Assert.AreEqual(PixelLoadingState.Run, newState);
    }

    [TestMethod]
    public void ProgressIsClampedAndPublished()
    {
        var controller = new PixelLoadingStateController();
        double? progress = null;
        controller.ProgressChanged += (_, value) => progress = value;

        controller.SetProgress(1.5);

        Assert.AreEqual(1d, controller.Progress);
        Assert.AreEqual(1d, progress);
    }
}
