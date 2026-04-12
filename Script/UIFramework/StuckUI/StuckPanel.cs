using Godot;

namespace UIFramework;

public partial class StuckPanel : Control
{
    [Export] private TextureRect icon;

    public override void _Process(double delta)
    {
        icon.Rotation += (float)delta*5;
    }
}