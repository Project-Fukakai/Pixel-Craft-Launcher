using System;
using PCL.Core.App.Pixel.Slices.Launch;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.MyMsg;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private PixelHintPresenter? _hintPresenter;
    private PixelFloatingActionPresenter? _floatingActionPresenter;
    private PixelDialogPresenter? _dialogPresenter;

    public void ShowMessage(string title, string markdown, bool isWarn = false)
    {
        ShowMessageWithActions(title, markdown, isWarn, button1: _shellSettingsService.GetMessages().CloseButtonText);
    }

    public void ShowMessageWithActions(
        string title,
        string markdown,
        bool isWarn = false,
        string? button1 = null,
        string? button2 = null,
        string? button3 = null,
        Action? onButton1 = null,
        Action? onButton2 = null,
        Action? onButton3 = null)
    {
        button1 ??= _shellSettingsService.GetMessages().CloseButtonText;
        GetDialogPresenter().ShowMessageWithActions(
            title,
            markdown,
            isWarn,
            button1,
            button2,
            button3,
            onButton1,
            onButton2,
            onButton3);
    }

    private MyMsgForm ShowFormDialog(string title, string? button1 = null, string? button2 = null)
    {
        var messages = _shellSettingsService.GetMessages();
        button1 ??= messages.ConfirmButtonText;
        button2 ??= messages.CancelButtonText;
        return GetDialogPresenter().ShowFormDialog(title, button1, button2);
    }

    public void CloseMessage() => GetDialogPresenter().CloseMessage();

    private PixelDialogPresenter GetDialogPresenter() => _dialogPresenter ??= new PixelDialogPresenter(PanMsg, PanMsgBackground);

    private void HidePopupOverlay()
    {
        PanPopupOverlay.Children.Clear();
        PanPopupOverlay.IsHitTestVisible = false;
    }

    private void InitializeFloatingActionButtons()
    {
        var presenter = GetFloatingActionPresenter();
        presenter.Register(BtnDownloadTasksFloating, 0);
        presenter.Register(BtnForceCloseGame, 1);
        BtnDownloadTasksFloating.Click += (_, _) => NavigateToDownloadTasks();
        BtnForceCloseGame.Click += (_, _) => _launchViewModel.ForceCloseGame();
        UpdateDownloadTasksButton();
        UpdateForceCloseGameButton();
    }

    private PixelFloatingActionPresenter GetFloatingActionPresenter() =>
        _floatingActionPresenter ??= new PixelFloatingActionPresenter(PanFloatingActions, PanFloatingRipple, () => Bounds.Size);

    private void UpdateForceCloseGameButton()
    {
        GetFloatingActionPresenter().Show(BtnForceCloseGame, 1, _launchViewModel.IsGameRunning);
    }

    private void UpdateDownloadTasksButton()
    {
        GetFloatingActionPresenter().Show(
            BtnDownloadTasksFloating,
            0,
            HasVisibleDownloadTasks() && !IsDownloadTaskRoute(),
            playRippleWhenFirstShown: true);
    }

    private void ShowLaunchIssueDialog(PixelLaunchIssueDialogSnapshot dialog)
    {
        ShowMessageWithActions(
            dialog.Title,
            dialog.Markdown,
            isWarn: dialog.IsWarning,
            button1: dialog.CloseButtonText,
            button2: dialog.ExportLogButtonText,
            button3: dialog.OpenLogButtonText,
            onButton2: ExportLaunchLogWithHint,
            onButton3: ExportAndOpenLaunchLog);
    }

    private void ShowAccessibilityPermissionDialog()
    {
        var dialog = _shellSettingsService.GetMessages().AccessibilityPermissionDialog;
        ShowMessageWithActions(
            dialog.Title,
            dialog.Body,
            isWarn: true,
            button1: dialog.LaterButtonText,
            button2: dialog.OpenSettingsButtonText,
            onButton2: _launchViewModel.RequestAccessibilityPermission);
    }

    private void ExportLaunchLogWithHint()
    {
        var path = _launchViewModel.ExportLaunchLog();
        var messages = _launchViewModel.GetWindowMessagesSnapshot();
        ShowHint(messages.GetLaunchLogExportedMessage(path), HintType.Finish);
    }

    private void ExportAndOpenLaunchLog()
    {
        var path = _launchViewModel.ExportLaunchLog();
        OpenPath(path);
    }

    private void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        _externalProcessService.OpenPath(path);
    }

    private void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;
        _externalProcessService.OpenUrl(url);
    }

    private PixelHintPresenter GetHintPresenter() => _hintPresenter ??= new PixelHintPresenter(PanHint);

    public void ShowHint(string text, MyHint.Themes theme) => GetHintPresenter().Show(text, theme);

    public void ShowHint(string text, HintType type) => GetHintPresenter().Show(text, type);
}
