using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Pixel_Craft_Launcher.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) =>
        SetProperty(ref field, value, propertyName);
}
