using Godot;
using UIFramework;

namespace GodotUIFrame.addons.uiframe_editor_autobind;

[Tool]
public partial class UIFrameEditorInspectorPlugin : EditorInspectorPlugin
{
    public EditorUndoRedoManager UndoRedo { get; set; }

    public override bool _CanHandle(GodotObject @object)
    {
        return IsUIBaseOrDerived(@object);
    }
    
    private static bool IsUIBaseOrDerived(GodotObject obj)
    {
        if (obj is not Node node)
            return false;

        var scriptVar = node.GetScript();
        if (scriptVar.VariantType == Variant.Type.Nil)
            return false;

        var script = scriptVar.As<Godot.Script>();
        while (script != null)
        {
            if (script.GetGlobalName() == nameof(UIBase))
            {
                return true;
            }
            script = script.GetBaseScript();
        }

        return false;
    }

    public override void _ParseBegin(GodotObject @object)
    {
        Node node = @object as Node;
        var container = new VBoxContainer();
        var button = new Button
        {
            Text = "Auto Bind '-' Nodes"
        };
        
        button.Pressed += () => AutoBind(node);
        button.Icon = ResourceLoader.Load("res://Script/UIFramework/Image/AutoBindIcon.png") as Texture2D;
        container.AddChild(button);
        AddCustomControl(container);
    }

    public void AutoBind(Node uiBase)
    {
        if (uiBase == null || UndoRedo == null) return;

        var matches = UIFrameEditorBindingUtility.CollectBindings(uiBase);
        if (matches.Count == 0) return;

        UndoRedo.CreateAction("UIFrame Auto Bind Nodes");
        foreach (var match in matches)
        {
            UndoRedo.AddDoProperty(match.UiBase, match.MemberName, match.TargetNode);
            UndoRedo.AddUndoProperty(match.UiBase, match.MemberName, match.CurrentNode);
        }
        UndoRedo.CommitAction();

        uiBase.NotifyPropertyListChanged();
        EditorInterface.Singleton.InspectObject(uiBase);
    }
}