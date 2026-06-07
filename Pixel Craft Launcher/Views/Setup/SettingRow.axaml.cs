using Avalonia.Controls;

namespace Pixel_Craft_Launcher.Views.Setup;

public partial class SettingRow : UserControl
{
    public SettingRow()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => TitleBlock.Text ?? string.Empty;
        set => TitleBlock.Text = value;
    }

    public string Description
    {
        get => DescriptionBlock.Text ?? string.Empty;
        set
        {
            DescriptionBlock.Text = value;
            DescriptionBlock.IsVisible = !string.IsNullOrWhiteSpace(value);
        }
    }

    public Control? SettingControl
    {
        get => ControlHost.Content as Control;
        set => ControlHost.Content = value;
    }
}
