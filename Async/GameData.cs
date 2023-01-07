using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;


public partial class GameData : Node
{
    public static IDictionary<string, Vector2i> TileDictionary = new ConcurrentDictionary<string, Vector2i>(
        new Dictionary<string, Vector2i>
        {
            { "Grass", new Vector2i(0,1) },
            { "Sand", new Vector2i(1,1) },
            { "Water", new Vector2i(2,1) },
            { "Plains", new Vector2i(3,0) },
            { "Dirt", new Vector2i(4,0) },
            { "JungleGrass", new Vector2i(0,0) },
            { "SwampGrass", new Vector2i(2,0) },
            { "Snow", new Vector2i(1,0) }
        });

    private static int _chunkSize = 64;

	public static Vector2i ChunkSize
	{
		get {return new Vector2i(_chunkSize, _chunkSize);}
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
