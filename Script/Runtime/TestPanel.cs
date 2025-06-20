using System.Threading.Tasks;
using Godot;
using UIFramework;

namespace HappySquad.Script.Runtime;

public class TestPanelData : UIData
{
    public string TestString { get; set; }
}

[PanelLayer]
public partial class TestPanel : UINode<TestPanelData>
{
    [Export] private BaseButton _button;
    
    public override void _EnterTree()
    {
        GD.Print("TestPanel entered tree");
    }

    public override void _ExitTree()
    {
        GD.Print("TestPanel exited tree");
    }

    public override void _Ready()
    {
        GD.Print("TestPanel ready");
    }

    protected override async Task OnCreate()
    {
        GD.Print("TestPanel created");
        //模拟耗时操作
        await ToSignal(GetTree().CreateTimer(3f), "timeout");
        // return Task.CompletedTask;
    }

    protected override Task OnRefresh()
    {
        GD.Print($"TestPanel refreshed Data:{Data.TestString}");
        return Task.CompletedTask;
    }

    protected override void OnBind()
    {
        _button.Pressed += () =>
        {
            UIFrame.Hide(this);
        };
        GD.Print("TestPanel bind");
    }

    protected override void OnUnbind()
    {
        GD.Print("TestPanel unbind");
    }

    protected override void OnShow()
    {
        GD.Print("TestPanel show");
    }

    protected override void OnHide()
    {
        GD.Print("TestPanel hide");
    }

    protected override void OnDied()
    {
        GD.Print("TestPanel died");
    }
}