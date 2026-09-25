using System.Threading.Tasks;
using Godot;

namespace Ciallo.GuiControl;

public partial class YesNoDialog : ConfirmationDialog
{
    private TaskCompletionSource<bool> _dialogResultSource;

    public YesNoDialog()
    {
        GetOkButton().Text = "Yes";
        GetCancelButton().Text = "No";

        GetOkButton().Pressed += OnYes;
        Canceled += OnNo;
    }

    public Task<bool> PopupCollectInput()
    {
        _dialogResultSource = new TaskCompletionSource<bool>();
        Popup();

        return _dialogResultSource.Task;
    }

    public void OnYes()
    {
        _dialogResultSource?.SetResult(true);
        _dialogResultSource = null;
    }

    private void OnNo()
    {
        _dialogResultSource?.SetResult(false);
        _dialogResultSource = null;
    }
}
