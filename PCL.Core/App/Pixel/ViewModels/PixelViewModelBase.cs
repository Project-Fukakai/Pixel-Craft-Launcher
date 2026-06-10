using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PCL.Core.App.Pixel.ViewModels;

public abstract class PixelViewModelBase : ObservableObject
{
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) =>
        SetProperty(ref field, value, propertyName);
}

