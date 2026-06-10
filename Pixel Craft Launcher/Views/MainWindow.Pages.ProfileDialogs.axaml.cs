using System.Collections.Generic;
using Avalonia.Controls;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.MyMsg;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private void ShowAuthlibProfileDialog(string serverId)
    {
        PanMsg.Children.Clear();
        PanMsgBackground.IsVisible = true;

        var form = _profilePageService.GetAuthlibProfileFormSnapshot(serverId);
        var messages = _profilePageService.GetPageMessages();
        var name = new MyTextBox { HintText = form.LoginNameHint };
        var password = new MyTextBox { HintText = form.PasswordHint, PasswordChar = '●' };
        var status = CreateBodyText(messages.AuthlibDialogStatus);
        var dialog = BuildProfileDialog(form.ServerName, new Control[] { CreateBodyText(form.ApiRoot), name, password, status }, messages.CancelButtonText, form.SubmitButtonText, "mdi-login");

        dialog.Button2.Click += (_, e) =>
        {
            e.Handled = true;
            CloseMessage();
        };
        dialog.Button1.Click += async (_, e) =>
        {
            e.Handled = true;
            var bridge = GetProfileOperationBridge();
            await bridge.RunWithBusyStateAsync(
                () => bridge.AddAuthlibProfileAsync(
                    form,
                    name.Text ?? string.Empty,
                    password.Text ?? string.Empty,
                    status,
                    CloseMessage),
                dialog.Button1,
                dialog.Button2);
        };

        PanMsg.Children.Add(dialog);
    }

    private void ShowOfflineProfileDialog(string? editingProfileId = null)
    {
        PanMsg.Children.Clear();
        PanMsgBackground.IsVisible = true;

        var editor = _profilePageService.GetOfflineEditorSnapshot(editingProfileId);
        var messages = _profilePageService.GetPageMessages();
        var username = new MyTextBox { HintText = editor.UsernameHint, Text = editor.Username };
        var uuid = new MyTextBox { HintText = editor.UuidHint, Text = editor.Uuid };
        var mode = new MyComboBox
        {
            HintText = editor.UuidModeHint,
            ItemsSource = editor.UuidModeOptions,
            SelectedIndex = editor.UuidModeIndex
        };
        var status = CreateBodyText(messages.OfflineDialogStatus);
        var dialog = BuildProfileDialog(
            editor.Title,
            new Control[] { username, mode, uuid, status },
            messages.CancelButtonText,
            editor.PrimaryButtonText,
            "mdi-check");

        dialog.Button2.Click += (_, e) =>
        {
            e.Handled = true;
            CloseMessage();
        };
        dialog.Button1.Click += async (_, e) =>
        {
            e.Handled = true;
            var bridge = GetProfileOperationBridge();
            await bridge.RunWithBusyStateAsync(
                () => bridge.SaveOfflineProfileAsync(
                    editor,
                    mode.SelectedIndex,
                    username.Text ?? string.Empty,
                    uuid.Text,
                    status,
                    CloseMessage),
                dialog.Button1,
                dialog.Button2);
        };

        PanMsg.Children.Add(dialog);
    }

    private void ShowMicrosoftProfileDialog()
    {
        PanMsg.Children.Clear();
        PanMsgBackground.IsVisible = true;

        var messages = _profilePageService.GetPageMessages();
        var status = CreateBodyText(messages.MicrosoftLoginStatus);
        var dialog = BuildProfileDialog(messages.MicrosoftDialogTitle, new Control[] { status }, messages.CancelButtonText, messages.MicrosoftLoginButtonText, "mdi-microsoft");

        dialog.Button2.Click += (_, e) =>
        {
            e.Handled = true;
            CloseMessage();
        };
        dialog.Button1.Click += async (_, e) =>
        {
            e.Handled = true;
            var bridge = GetProfileOperationBridge();
            await bridge.RunWithBusyStateAsync(
                () => bridge.AddMicrosoftProfileAsync(status, CloseMessage),
                dialog.Button1,
                dialog.Button2);
        };

        PanMsg.Children.Add(dialog);
    }

    private void ShowAuthServerDialog()
    {
        PanMsg.Children.Clear();
        PanMsgBackground.IsVisible = true;

        var editor = _profilePageService.GetAuthServerEditorSnapshot();
        var messages = _profilePageService.GetPageMessages();
        var name = new MyTextBox { HintText = editor.NameHint, Text = editor.Name };
        var api = new MyTextBox { HintText = editor.ApiRootHint, Text = editor.ApiRoot };
        var register = new MyTextBox { HintText = editor.RegisterUrlHint, Text = editor.RegisterUrl };
        var status = CreateBodyText(messages.AuthServerDialogStatus);
        var dialog = BuildProfileDialog(editor.PageTitle, new Control[] { name, api, register, status }, messages.CancelButtonText, messages.SaveButtonText, "mdi-server-plus");

        dialog.Button2.Click += (_, e) =>
        {
            e.Handled = true;
            CloseMessage();
        };
        dialog.Button1.Click += async (_, e) =>
        {
            e.Handled = true;
            var bridge = GetProfileOperationBridge();
            await bridge.RunWithBusyStateAsync(
                () => bridge.AddAuthServerAsync(
                    name.Text ?? string.Empty,
                    api.Text ?? string.Empty,
                    register.Text ?? string.Empty,
                    status,
                    CloseMessage),
                dialog.Button1,
                dialog.Button2);
        };

        PanMsg.Children.Add(dialog);
    }

    private static MyMsgForm BuildProfileDialog(
        string title,
        IReadOnlyList<Control> fields,
        string cancelText,
        string primaryText,
        string? primaryIcon)
    {
        var dialog = new MyMsgForm { Title = title };
        foreach (var field in fields)
            dialog.ContentPanel.Children.Add(field);
        dialog.Button2.Text = cancelText;
        dialog.Button1.Text = primaryText;
        dialog.Button1.PrependIcon = primaryIcon;
        return dialog;
    }
}
