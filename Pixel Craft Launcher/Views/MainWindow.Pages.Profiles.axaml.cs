using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Core.App.Pixel.Navigation;
using PCL.Core.App.Pixel.Slices.Profiles;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.MyMsg;
using Pixel_Craft_Launcher.Views.Pages;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private ProfileOperationPlatformBridge? _profileOperationBridge;

    private ProfileOperationPlatformBridge GetProfileOperationBridge()
    {
        return _profileOperationBridge ??= new ProfileOperationPlatformBridge(
            _profilePageService,
            _launchViewModel.RefreshProfileBindings,
            RefreshProfileManagerPage,
            ShowHint,
            CreateProfileLoginProgress,
            CreateProfileCallbacks);
    }

    private Control BuildProfileManagerLeftPage()
    {
        return new ProfileManagerLeftPageView(
            _profilePageService.GetManagerSidebarSnapshot(),
            ShowMicrosoftProfileDialog,
            () => ShowOfflineProfileDialog(),
            ShowAuthlibProfileDialog,
            ShowAuthServerDialog);
    }

    private Control BuildProfileManagerRightPage(RouteNode route)
    {
        var snapshot = MainWindowViewModel.GetProfileManagerRouteSnapshot(route);
        return snapshot.PageKind switch
        {
            PixelProfileManagerPageKind.Offline => BuildOfflineProfileForm(),
            PixelProfileManagerPageKind.Microsoft => BuildMicrosoftProfilePage(),
            PixelProfileManagerPageKind.Authlib => BuildAuthlibProfileForm(snapshot.AuthServerId),
            PixelProfileManagerPageKind.AuthServer => BuildAuthServerForm(),
            _ => BuildProfileListPage()
        };
    }

    private Control BuildProfileListPage()
    {
        return new ProfileListPageView(
            _profilePageService.GetListPageSnapshot(),
            GetProfileOperationBridge().SelectProfileAsync,
            snapshot => GetProfileOperationBridge().CopyProfileUuidAsync(Clipboard, snapshot),
            ShowOfflineProfileDialog,
            GetProfileOperationBridge().DeleteProfileAsync);
    }

    private void RefreshProfileManagerPage()
    {
        if (!IsProfileManagerRoute())
            return;
        SetPageHostContent(LeftContentHost, BuildProfileManagerLeftPage(), PageHostUpdateMode.SilentRefresh);
        SetPageHostContent(RightContentHost, BuildProfileListPage(), PageHostUpdateMode.SilentRefresh);
    }

    private PixelProfileLoginProgressAdapter CreateProfileLoginProgress(TextBlock status)
    {
        return new PixelProfileLoginProgressAdapter(
            _profileListService,
            progress => Avalonia.Threading.Dispatcher.UIThread.Post(() => status.Text = progress.Text));
    }

    private PixelProfileUiCallbacks CreateProfileCallbacks()
    {
        return new PixelProfileUiCallbacks(
            _profileListService,
            ShowProfileDeviceCodeDialogAsync,
            ShowAuthlibProfileChoiceDialogAsync);
    }

    private async Task ShowProfileDeviceCodeDialogAsync(
        PixelProfileDeviceCodeDialogSnapshot dialog,
        CancellationToken cancellationToken)
    {
        await Dispatcher.InvokeAsync(() =>
        {
            ShowMessageWithActions(
                dialog.Title,
                dialog.Markdown,
                button1: dialog.WaitButtonText,
                button2: dialog.CopyButtonText,
                button3: dialog.OpenButtonText,
                onButton2: async () =>
                {
                    await ClipboardCompatBridge.SetTextAsync(Clipboard, dialog.UserCode);
                },
                onButton3: () => OpenUrl(dialog.OpenUrl));
            });
    }

    private async Task<int?> ShowAuthlibProfileChoiceDialogAsync(
        IReadOnlyList<PixelAuthlibProfileChoiceSnapshot> choices,
        CancellationToken cancellationToken)
    {
        if (choices.Count <= 1)
            return 0;

        var completion = new TaskCompletionSource<int?>();
        await Dispatcher.InvokeAsync(() =>
        {
            var dialogSnapshot = _profileListService.GetAuthlibProfileChoiceDialogSnapshot(choices);
            var choiceLabels = new List<string>(dialogSnapshot.Choices.Count);
            foreach (var choice in dialogSnapshot.Choices)
                choiceLabels.Add(choice.Name);

            PanMsg.Children.Clear();
            PanMsgBackground.IsVisible = true;

            var choiceBox = new MyComboBox
            {
                HintText = dialogSnapshot.Title,
                ItemsSource = choiceLabels,
                SelectedIndex = 0
            };
            var dialog = new MyMsgForm { Title = dialogSnapshot.Title };
            dialog.ContentPanel.Children.Add(CreateBodyText(dialogSnapshot.Description));
            dialog.ContentPanel.Children.Add(choiceBox);
            dialog.Button1.Text = dialogSnapshot.ConfirmButtonText;
            dialog.Button2.Text = dialogSnapshot.CancelButtonText;
            dialog.Button1.Click += (_, e) =>
            {
                e.Handled = true;
                completion.TrySetResult(choiceBox.SelectedIndex < 0 ? 0 : choiceBox.SelectedIndex);
                CloseMessage();
            };
            dialog.Button2.Click += (_, e) =>
            {
                e.Handled = true;
                completion.TrySetResult(null);
                CloseMessage();
            };
            PanMsg.Children.Add(dialog);
        });

        using var registration = cancellationToken.Register(() =>
            Dispatcher.UIThread.Post(() =>
            {
                completion.TrySetCanceled(cancellationToken);
                CloseMessage();
            }));

        try
        {
            return await completion.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
