using System;
using System.Collections.Generic;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Diagnostics;
using Godot;

namespace Ciallo.GuiControl;

public partial class MenuHelp : PopupMenu
{
    private FileDialog _researchAnimationDialog;

    public static readonly OrderedDictionary<string, Hotkey> MenuItems = new()
    {
        { "User manual", null },
        { "About Ciallo", null },
        { "Copy bug report", null },
        { "Open log folder", null },
        { "Report bug", null },
        // { "-Debug", null },
        // { "Load research animation", null },
    };

    public override void _Ready()
    {
        foreach (var (i, item) in MenuItems.Index())
        {
            if (item.Key.StartsWith('-'))
            {
                AddSeparator();
                continue;
            }
            AddItem(item.Key);
            if (item.Value != null) SetItemShortcut(i, item.Value.Shortcut);
        }

        IndexPressed += id => OnIndexPressed((int)id);
    }

    private void OnIndexPressed(int id)
    {
        switch (id)
        {
            case 0:
                OS.ShellOpen("https://www.patreon.com/posts/143863276");
                break;
            case 1:
                AppDialogHost.AboutCiallo.Popup();
                break;
            case 2:
                AppBugReport.CopyMarkdownToClipboard();
                break;
            case 3:
                AppBugReport.OpenLogFolder();
                break;
            case 4:
                AppBugReport.CopyMarkdownToClipboard();
                OS.ShellOpen("https://github.com/ShenCiao/Ciallo/issues/new");
                break;
            case 6:
                PopupResearchAnimationDialog();
                break;
        }
    }

    private void PopupResearchAnimationDialog()
    {
        if (AppDocumentManager.WorkingDocument.Value.IsNull) return;

        if (!IsInstanceValid(_researchAnimationDialog))
        {
            _researchAnimationDialog = new FileDialog
            {
                Access = FileDialog.AccessEnum.Filesystem,
                FileMode = FileDialog.FileModeEnum.OpenAny,
                Title = "Load research animation",
                CurrentDir = OS.GetSystemDir(OS.SystemDir.Documents),
                Size = new Vector2I(1080, 720),
                DisplayMode = FileDialog.DisplayModeEnum.List,
                UseNativeDialog = true,
                InitialPosition = Window.WindowInitialPosition.CenterScreenWithMouseFocus,
            };
            _researchAnimationDialog.Filters = ["*.csv;Research animation CSV"];
            _researchAnimationDialog.FileSelected += OnResearchAnimationPathSelected;
            _researchAnimationDialog.DirSelected += OnResearchAnimationPathSelected;
            AddChild(_researchAnimationDialog);
        }

        _researchAnimationDialog.Popup();
    }

    private void OnResearchAnimationPathSelected(string path)
    {
        try
        {
            ResearchAnimationImporter.Import(AppDocumentManager.WorkingDocument.Value, path);
        }
        catch (Exception exception)
        {
            AppBugReport.Exception(exception);
            GD.PrintErr(exception);
            AppDialogHost.WarnUser.DialogText = "Cannot load research animation.".Tr() + " " + exception.Message;
            AppDialogHost.WarnUser.Popup();
        }
    }

    public static string CollectSystemInfo() => AppBugReport.CollectSystemInfo();
}
