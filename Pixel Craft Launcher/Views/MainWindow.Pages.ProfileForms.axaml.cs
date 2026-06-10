using Avalonia.Controls;
using Avalonia.Layout;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Views.Pages;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private Control BuildOfflineProfileForm(string? editingProfileId = null)
    {
        var editor = _profilePageService.GetOfflineEditorSnapshot(editingProfileId);
        var username = new MyTextBox { HintText = editor.UsernameHint, Text = editor.Username };
        var uuid = new MyTextBox { HintText = editor.UuidHint, Text = editor.Uuid };
        var mode = new MyComboBox
        {
            HintText = editor.UuidModeHint,
            ItemsSource = editor.UuidModeOptions,
            SelectedIndex = editor.UuidModeIndex
        };
        var save = new MyButton
        {
            Text = editor.FormPrimaryButtonText,
            PrependIcon = "mdi-check",
            Variant = MyButtonVariant.Flat,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        save.Click += async (_, _) =>
        {
            var bridge = GetProfileOperationBridge();
            await bridge.RunWithBusyStateAsync(
                () => bridge.SaveOfflineProfileAsync(
                    editor,
                    mode.SelectedIndex,
                    username.Text ?? string.Empty,
                    uuid.Text),
                save);
        };

        return new ProfileFormPageView(editor.PageTitle, [username, mode, uuid, save]);
    }

    private Control BuildMicrosoftProfilePage()
    {
        var messages = _profilePageService.GetPageMessages();
        var status = CreateBodyText(messages.MicrosoftLoginStatus);
        var button = new MyButton
        {
            Text = messages.MicrosoftLoginButtonText,
            PrependIcon = "mdi-microsoft",
            Variant = MyButtonVariant.Flat,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        button.Click += async (_, _) =>
        {
            var bridge = GetProfileOperationBridge();
            await bridge.RunWithBusyStateAsync(
                () => bridge.AddMicrosoftProfileAsync(status),
                button);
        };

        return new ProfileFormPageView(messages.MicrosoftPageTitle, [status, button]);
    }

    private Control BuildAuthlibProfileForm(string? serverId = null)
    {
        var form = _profilePageService.GetAuthlibProfileFormSnapshot(serverId);
        var messages = _profilePageService.GetPageMessages();
        var serverInfo = CreateBodyText(form.ServerInfoText);
        var name = new MyTextBox { HintText = form.LoginNameHint };
        var password = new MyTextBox { HintText = form.PasswordHint, PasswordChar = '●' };
        var status = CreateBodyText(messages.AuthlibLoginStatus);
        var login = new MyButton
        {
            Text = form.SubmitButtonText,
            PrependIcon = "mdi-login",
            Variant = MyButtonVariant.Flat,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        login.Click += async (_, _) =>
        {
            var bridge = GetProfileOperationBridge();
            await bridge.RunWithBusyStateAsync(
                () => bridge.AddAuthlibProfileAsync(
                    form,
                    name.Text ?? string.Empty,
                    password.Text ?? string.Empty,
                    status),
                login);
        };

        return new ProfileFormPageView(form.PageTitle, [serverInfo, name, password, status, login]);
    }

    private Control BuildAuthServerForm()
    {
        var editor = _profilePageService.GetAuthServerEditorSnapshot();
        var name = new MyTextBox { HintText = editor.NameHint, Text = editor.Name };
        var api = new MyTextBox { HintText = editor.ApiRootHint, Text = editor.ApiRoot };
        var register = new MyTextBox { HintText = editor.RegisterUrlHint, Text = editor.RegisterUrl };
        var save = new MyButton
        {
            Text = _profilePageService.GetPageMessages().SaveAuthServerButtonText,
            PrependIcon = "mdi-server-plus",
            Variant = MyButtonVariant.Flat,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        save.Click += async (_, _) =>
        {
            var bridge = GetProfileOperationBridge();
            await bridge.RunWithBusyStateAsync(
                () => bridge.AddAuthServerAsync(
                    name.Text ?? string.Empty,
                    api.Text ?? string.Empty,
                    register.Text ?? string.Empty),
                save);
        };

        return new ProfileFormPageView(editor.PageTitle, [name, api, register, save]);
    }
}
