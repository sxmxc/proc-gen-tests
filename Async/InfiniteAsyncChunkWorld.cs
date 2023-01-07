using Godot;
using System;
using System.Threading.Tasks;
using static Godot.GD;
using System.Collections.Generic;
using System.Collections.Concurrent;

public partial class InfiniteAsyncChunkWorld : Node2D
{
	public const string ModuleName = "WorldGenerator";
	
	[Export]
	public TileSet WorldTileset = ResourceLoader.Load<TileSet>("res://assets/tilesets/BasicBiomes.tres");

    [Export]
    public Vector2i ChunkSize = new Vector2i(64, 64);
	[Export]
	public int TemperatureSeed 
	{
		get;
		set;
	}
	[Export]
	public int MoistureSeed 
	{
		get;
		set;
	}
	[Export]
	public int AltitudeSeed
	{
		get;
		set;
	}

	public Vector2i ChunkHalfSize
	{
		get { return ChunkSize / 2; }
	}

	private CharacterBody2D _player;

	private Vector2i _playerCurrentChunk = Vector2i.Zero;


	private FastNoiseLite _altNoise;
	private FastNoiseLite _tempNoise;
	private FastNoiseLite _moistNoise;
	private Label _playerChunkLabel;
    private Label _tileBenchMarkLabel;
	private Node2D _mapChunkContainer;
	private CanvasLayer _loadScreen;

	private ConcurrentDictionary<Vector2i, AsyncChunk> _chunks = new ConcurrentDictionary<Vector2i, AsyncChunk>();


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
        Randomize();
		_player = GetNode<CharacterBody2D>("CharacterPlayer");
		_playerChunkLabel = GetNode<Label>("%PlayerChunkLabel");
		_tileBenchMarkLabel = GetNode<Label>("%TileBenchmarkLabel");
		_mapChunkContainer = GetNode<Node2D>("MapChunkContainer");
		_loadScreen = GetNode<CanvasLayer>("LoadCanvas");

        TemperatureSeed = (int)Randi();
        MoistureSeed = (int)Randi();
        AltitudeSeed = (int)Randi();
        
		_altNoise = new FastNoiseLite();
        _altNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Value;
        _altNoise.Seed = AltitudeSeed;
        _moistNoise = new FastNoiseLite();
        _moistNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Value;
        _moistNoise.Seed = MoistureSeed;
        _tempNoise = new FastNoiseLite();
        _tempNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Value;
        _tempNoise.Seed = TemperatureSeed;

		//load center chunk
		GenerateChunksAroudPosition(Vector2i.Zero);
		
	}

	public Vector2i GetChunkPosition(Vector2 pos)
	{
        var chunkX = Mathf.FloorToInt(pos.x / (ChunkSize.x * WorldTileset.TileSize.x));
        var chunkY = Mathf.FloorToInt(pos.y / (ChunkSize.y * WorldTileset.TileSize.y));

		return new Vector2i(chunkX, chunkY);

	}

	public override void _Process(double delta)
	{
		
        // Get the current chunk position of the player
        var playerChunk = GetChunkPosition(_player.Position);
        if(playerChunk != _playerCurrentChunk)
		{
			Print(string.Format("Loading chunks around ({0},{1})", playerChunk.x, playerChunk.y));

			GenerateChunksAroudPosition(playerChunk);
            // Load the surrounding chunks if necessary
		}
        _playerCurrentChunk = playerChunk;
        _playerChunkLabel.Text = string.Format("Player in chunk: ({0},{1})", playerChunk.x, playerChunk.y);

	}
	
	public async void LoadChunk(int x, int y)
	{
		var chunkCoords = new Vector2i(x, y);
		//check if already loaded
        if (_chunks.ContainsKey(chunkCoords)) return;

		//create new chunk
		var chunk = new AsyncChunk(x,y);
		chunk.TileSet = WorldTileset;
		chunk.Name = string.Format("Chunk ({0},{1})", x, y);
		_mapChunkContainer.AddChild(chunk);
        //set position of chunk
        chunk.Position = new Vector2(chunkCoords.x * (ChunkSize.x * WorldTileset.TileSize.x), chunkCoords.y * (ChunkSize.y * WorldTileset.TileSize.y));
		//generate tiles for the chunk
		await chunk.GenerateAsync(x * ChunkSize.x, y * ChunkSize.y, _tempNoise, _altNoise, _moistNoise);
		//store chunk in chunk dictionary
		_chunks[chunkCoords] = chunk;

	}

	private void GenerateChunksAroudPosition(Vector2i playerChunk)
	{
		List<Vector2i> chunksToLoad = new List<Vector2i>();
		chunksToLoad.Add(new Vector2i(playerChunk.x - 1, playerChunk.y - 1));
		chunksToLoad.Add(new Vector2i(playerChunk.x - 1, playerChunk.y));
        chunksToLoad.Add(new Vector2i(playerChunk.x - 1, playerChunk.y + 1));
        chunksToLoad.Add(new Vector2i(playerChunk.x, playerChunk.y - 1));
        chunksToLoad.Add(new Vector2i(playerChunk.x, playerChunk.y));
        chunksToLoad.Add(new Vector2i(playerChunk.x, playerChunk.y + 1));
        chunksToLoad.Add(new Vector2i(playerChunk.x + 1, playerChunk.y - 1));
        chunksToLoad.Add(new Vector2i(playerChunk.x + 1, playerChunk.y));
        chunksToLoad.Add(new Vector2i(playerChunk.x + 1, playerChunk.y + 1));

		foreach (Vector2i coord in chunksToLoad)
		{
			LoadChunk(coord.x, coord.y);
		}

		ClearFarChunks(chunksToLoad);


	}

	private void ClearFarChunks(List<Vector2i> visibleChunks)
	{
		foreach (AsyncChunk chunk in _chunks.Values)
		{
			if (!visibleChunks.Contains(chunk.ChunkCoords))
			{
                _chunks.TryRemove(chunk.ChunkCoords, out AsyncChunk chunkToRemove);
				Print(string.Format("Removing chunk: {0}", chunk.ChunkCoords));
                chunk.QueueFree();
				
			}
		}
	}
}
