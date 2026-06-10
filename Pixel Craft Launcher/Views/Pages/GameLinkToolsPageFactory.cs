using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using PCL.Core.App.Pixel;
using PCL.Core.App.Pixel.Shell;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class GameLinkToolsPageFactory(
    PixelGameLinkViewModel viewModel,
    Func<GameLinkToolsPlatformBridge> toolsBridge,
    Func<GameLinkDialogPlatformBridge> dialogBridge,
    Func<Task<string?>> pasteClipboard,
    Action<string, HintType> showHint,
    Action<string> openUrl,
    Action acceptEula,
    Func<Task> refreshWorlds,
    Action refreshLeft,
    Action openTestEntry)
{
    public Control Build(bool showTestEntry)
    {
        var sections = new List<Control>
        {
            new GameLinkAnnouncementHintView(viewModel.AnnouncementSnapshot)
        };

        switch (viewModel.Subpage)
        {
            case PixelGameLinkSubpage.Eula:
                sections.Add(BuildEulaCard());
                break;
            case PixelGameLinkSubpage.Finish:
                sections.Add(BuildEasyTierCard());
                sections.Add(BuildFinishPanel());
                break;
            default:
                sections.Add(BuildEasyTierCard());
                sections.Add(BuildAccountToolbar());
                sections.Add(BuildJoinCard());
                sections.Add(BuildCreateCard());
                break;
        }

        sections.Add(BuildFooterCard());

        return new GameLinkToolsPageView(
            sections,
            viewModel.SidebarSnapshot,
            showTestEntry,
            openTestEntry);
    }

    private Control BuildEulaCard()
    {
        return new GameLinkEulaCardView(
            viewModel.EulaSnapshot,
            acceptEula,
            openUrl);
    }

    private Control BuildAccountToolbar()
    {
        return new GameLinkAccountToolbarView(
            viewModel.AccountSnapshot,
            runNatTest: toolsBridge().RunToolsNatTestAsync,
            toggleLogin: toolsBridge().HandleNatayarkLoginClickAsync);
    }

    private MyCard BuildEasyTierCard()
    {
        var easyTier = viewModel.EasyTierSnapshot;

        return new GameLinkEasyTierCardView(
            easyTier,
            install: async () => await toolsBridge().EnsureEasyTierInstalledAsync(true),
            check: () =>
            {
                showHint(easyTier.CheckMessage, easyTier.IsReady ? HintType.Finish : HintType.Info);
                refreshLeft();
            });
    }

    private MyCard BuildJoinCard()
    {
        return new GameLinkJoinCardView(
            viewModel.JoinCardSnapshot,
            join: toolsBridge().JoinLobbyAsync,
            paste: pasteClipboard);
    }

    private MyCard BuildCreateCard()
    {
        return new GameLinkCreateCardView(
            viewModel.CreateCardSnapshot,
            create: toolsBridge().CreateLobbyAsync,
            refresh: refreshWorlds,
            manual: () => dialogBridge().ShowManualPortDialog(
                viewModel.CreateCardSnapshot,
                toolsBridge().CreateLobbyAsync));
    }

    private Control BuildFinishPanel()
    {
        var finish = viewModel.FinishSnapshot;

        return new GameLinkFinishPanelView(
            finish,
            copyCode: async () => await dialogBridge().CopyLobbyCodeAsync(finish),
            copyVirtualIp: ip => dialogBridge().ShowVirtualIpDialog(finish, ip),
            leave: toolsBridge().LeaveLobbyAsync,
            showPlayerDetails: dialogBridge().ShowPlayerDetails);
    }

    private MyCard BuildFooterCard()
    {
        var footer = viewModel.FooterSnapshot;
        return new GameLinkFooterView(
            footer,
            openUrl,
            disableGameLink: () => dialogBridge().ConfirmDisableGameLink(footer));
    }
}
