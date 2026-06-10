using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel.Slices.GameLink;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class GameLinkNetworkTestPanelView : StackPanel
{
    public GameLinkNetworkTestPanelView(
        PixelGameLinkNetworkTestPanelSnapshot snapshot,
        Func<Task<PixelGameLinkSetupNatTestResult>> runNatTest,
        Action<string, bool> notify)
    {
        Spacing = 8;

        var initialStatus = snapshot.InitialStatus;
        var messages = snapshot.Messages;
        var udpText = CreateBodyText(initialStatus.UdpText);
        var tcpText = CreateBodyText(initialStatus.TcpText);
        var ipv6Text = CreateBodyText(initialStatus.Ipv6Text);
        var button = CreateActionButton(messages.StartButtonText, "mdi-earth");
        button.IsEnabled = snapshot.CanRun;
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            button.Text = messages.TestingButtonText;
            try
            {
                var result = await runNatTest();
                if (!result.IsSuccess)
                {
                    notify(result.NotificationMessage, false);
                    return;
                }

                if (result.Status is { } status)
                {
                    udpText.Text = status.UdpText;
                    tcpText.Text = status.TcpText;
                    ipv6Text.Text = status.Ipv6Text;
                }
                notify(result.NotificationMessage, true);
            }
            catch (Exception ex)
            {
                notify(messages.GetFailureMessage(ex), false);
            }
            finally
            {
                button.Text = messages.StartButtonText;
                button.IsEnabled = snapshot.CanRun;
            }
        };

        Children.Add(CreateSubText(snapshot.PlatformText));
        Children.Add(udpText);
        Children.Add(tcpText);
        Children.Add(ipv6Text);
        Children.Add(new WrapPanel { Children = { button } });
    }

    private static MyButton CreateActionButton(string text, string icon)
    {
        return new MyButton
        {
            Text = text,
            PrependIcon = icon,
            Variant = MyButtonVariant.Flat
        };
    }

    private static TextBlock CreateBodyText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
    }

    private static TextBlock CreateSubText(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = Brushes.Gray,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
    }
}
