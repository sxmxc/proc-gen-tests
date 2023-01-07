using Godot;
using System;

public partial class GameUtils : Node
{
    public static bool Between(float value, float start, float end)
    {
        if (start <= value && value < end)
        {
            return true;
        }

        return false;
    }
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
