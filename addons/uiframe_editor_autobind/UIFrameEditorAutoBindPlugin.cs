using Godot;

namespace GodotUIFrame.addons.uiframe_editor_autobind;

[Tool]
public partial class UIFrameEditorAutoBindPlugin : EditorPlugin
{
    private UIFrameEditorInspectorPlugin _inspectorPlugin;

    public override void _EnterTree()
    {
        _inspectorPlugin = new UIFrameEditorInspectorPlugin
        {
            UndoRedo = GetUndoRedo()
        };
        AddInspectorPlugin(_inspectorPlugin);
        AddToolMenuItem("UIFrame/Auto Bind Selected UIBase", Callable.From(AutoBindSelected));
        GD.Print("UIFrameEditorAutoBindPlugin loaded");
    }

    public override void _ExitTree()
    {
        RemoveToolMenuItem("UIFrame/Auto Bind Selected UIBase");

        if (_inspectorPlugin == null) return;

        RemoveInspectorPlugin(_inspectorPlugin);
        _inspectorPlugin = null;
    }

    private void AutoBindSelected()
    {
        var selection = EditorInterface.Singleton.GetSelection();
        if (selection == null) return;

        foreach (var item in selection.GetSelectedNodes())
        {
            if (item is UIFramework.UIBase uiBase)
            {
                _inspectorPlugin?.AutoBind(uiBase);
            }
        }
    }
}