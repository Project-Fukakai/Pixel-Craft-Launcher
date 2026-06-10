using System;
using Avalonia.Controls;
using Pixel_Craft_Launcher.Controls.MyMsg;

namespace Pixel_Craft_Launcher.Views;

public sealed class PixelDialogPresenter(Panel host, Control background)
{
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
        host.Children.Clear();
        background.IsVisible = true;
        var message = new MyMsgMarkdown
        {
            Title = title,
            Markdown = markdown,
            IsWarn = isWarn,
            Button1 = button1,
            Button2 = button2,
            Button3 = button3
        };
        message.Button1Click += (_, _) =>
        {
            onButton1?.Invoke();
            CloseMessage();
        };
        message.Button2Click += (_, _) =>
        {
            onButton2?.Invoke();
            CloseMessage();
        };
        message.Button3Click += (_, _) =>
        {
            onButton3?.Invoke();
            CloseMessage();
        };
        host.Children.Add(message);
    }

    public MyMsgForm ShowFormDialog(string title, string button1, string? button2)
    {
        host.Children.Clear();
        background.IsVisible = true;
        var form = new MyMsgForm { Title = title };
        form.Button1.Text = button1;
        form.Button2.Text = button2;
        form.Button2.IsVisible = !string.IsNullOrWhiteSpace(button2);
        form.Button1Click += (_, _) => background.IsVisible = false;
        form.Button2Click += (_, _) => background.IsVisible = false;
        host.Children.Add(form);
        return form;
    }

    public void CloseMessage()
    {
        foreach (var child in host.Children)
        {
            if (child is MyMsgMarkdown message)
                message.Close();
            else if (child is MyMsgForm form)
                form.Close();
        }

        background.IsVisible = false;
    }
}
