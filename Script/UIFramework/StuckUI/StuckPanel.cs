using Godot;

namespace UIFramework;

public partial class StuckPanel : Control
{
    [Export] private TextureRect icon;

    public override void _Ready()
    {
        icon.Texture = ResourceLoader.Load("res://icon.svg") as Texture2D;
    }

    public override void _Process(double delta)
    {
        icon.Rotation += (float)delta*10;
    }
}