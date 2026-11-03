using Ciallo.Widget;
using Godot;
using ObservableCollections;
using R3;

namespace Ciallo.GuiControl;

public partial class WelcomePanel : PanelContainer
{
    public override void _Ready()
    {
        var newDocumentButton = GetNode<Button>("%NewDocumentButton");
        var openDocumentButton = GetNode<Button>("%OpenDocumentButton");
        var recentList = GetNode<RecentFilesTree>("%RecentList");
        var uiScaleSlider = GetNode<SpinSlider>("%SpinSlider");

        newDocumentButton.Pressed += AppGuiCommand.PopupNewDocumentDialog;
        openDocumentButton.Pressed += AppGuiCommand.PopupOpenDocumentDialog;

        recentList.SetColumnClipContent(0, true);
        var refreshRecentList = Callable.From(RefreshRecentList);
        AppPreference.RecentFiles.ObserveChanged()
            .Subscribe(_ => refreshRecentList.CallDeferred()).AddTo(this);
        AppPreference.Language.Subscribe(_ => refreshRecentList.CallDeferred()).AddTo(this);

        recentList.ItemMouseSelected += (_, buttonIndex) =>
        {
            if ((MouseButton)buttonIndex == MouseButton.Left) OpenSelectedRecentFile();
        };
        recentList.ItemActivated += OpenSelectedRecentFile;
        recentList.ButtonClicked += (item, _, _, buttonIndex) =>
        {
            if ((MouseButton)buttonIndex == MouseButton.Left)
                AppPreference.RecentFiles.Remove(item.GetMetadata(0).AsString());
        };

        uiScaleSlider.BindNumber(AppPreference.UIScale);

        return;

        void RefreshRecentList()
        {
            recentList.Clear();
            var root = recentList.CreateItem();
            var closeIcon = recentList.GetThemeIcon("close", "Window");
            foreach (var path in AppPreference.RecentFiles)
            {
                var item = recentList.CreateItem(root);
                item.SetAutoTranslateMode(0, AutoTranslateModeEnum.Disabled);
                item.SetText(0, path.GetFile().GetBaseName());
                item.SetMetadata(0, path);
                item.SetTooltipText(0, path);
                item.SetTextOverrunBehavior(0, TextServer.OverrunBehavior.NoTrimming);
                item.SetAutowrapMode(0, TextServer.AutowrapMode.Arbitrary);
                item.AddButton(0, closeIcon, tooltipText: Tr("Remove from recent files"));
            }
            recentList.RefreshPathLabels();
        }

        void OpenSelectedRecentFile()
        {
            var path = recentList.GetSelected().GetMetadata(0).AsString();
            Callable.From(() =>
            {
                if (!OpenDocumentDialog.LoadWorldFile(path)) AppPreference.RecentFiles.Remove(path);
            }).CallDeferred();
        }
    }
}
