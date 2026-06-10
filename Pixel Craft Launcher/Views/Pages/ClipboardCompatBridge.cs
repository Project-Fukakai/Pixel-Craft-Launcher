using System.Reflection;
using System.Threading.Tasks;

namespace Pixel_Craft_Launcher.Views.Pages;

public static class ClipboardCompatBridge
{
    public static async Task<string?> GetTextAsync(object? clipboard)
    {
        if (clipboard is null)
            return null;

        var asyncMethod = clipboard.GetType().GetMethod("GetTextAsync", BindingFlags.Instance | BindingFlags.Public);
        if (asyncMethod?.Invoke(clipboard, null) is Task<string?> asyncResult)
            return await asyncResult;

        var syncMethod = clipboard.GetType().GetMethod("GetText", BindingFlags.Instance | BindingFlags.Public);
        return syncMethod?.Invoke(clipboard, null) as string;
    }

    public static async Task SetTextAsync(object? clipboard, string text)
    {
        if (clipboard is null)
            return;

        var asyncMethod = clipboard.GetType().GetMethod("SetTextAsync", BindingFlags.Instance | BindingFlags.Public);
        if (asyncMethod?.Invoke(clipboard, [text]) is Task asyncResult)
        {
            await asyncResult;
            return;
        }

        var syncMethod = clipboard.GetType().GetMethod("SetText", BindingFlags.Instance | BindingFlags.Public);
        syncMethod?.Invoke(clipboard, [text]);
    }
}
