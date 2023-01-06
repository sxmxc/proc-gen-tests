using Godot;
using System;


public partial class CSTest : Node2D
{
    [Signal]
    public delegate void MySignalEventHandler();
	private Label _label;

	[Export]
	public int testExportInt;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_label = GetNode<Label>("Label");
		GD.Print("Hello from C# to Godot :)");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		GD.Print(delta);
		_label.Text = delta.ToString();

    }
}
