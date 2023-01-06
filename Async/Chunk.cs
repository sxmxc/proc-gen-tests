using Godot;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

public partial class Chunk : TileMap
{
	public int ID 
	{
		get;
		set;
	}
	public Vector2i ChunkCoords 
	{
		get;
		set;
	}
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public TaskAwaiter GetAwaiter()
	{
		return new TaskAwaiter();
	}
}
