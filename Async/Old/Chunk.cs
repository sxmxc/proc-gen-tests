using Godot;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

public partial class Chunk : TileMap
{
	public override void _Draw()
	{
		var rect = new Rect2(Position, ChunkSize);
		DrawRect(rect, Colors.Red, false, 3);
		var font = ResourceLoader.Load<Font>("res://Fonts/Roboto-Black.ttf");
		DrawString(font, Position, string.Format("({0},{1})", ChunkCoords.x, ChunkCoords.y));

	}
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
	public Vector2i ChunkSize
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
        QueueRedraw();
	}
}
