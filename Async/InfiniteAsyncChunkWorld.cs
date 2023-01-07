using Godot;
using System;
using System.Threading.Tasks;
using static Godot.GD;
using System.Collections.Generic;
using System.Collections.Concurrent;

public partial class InfiniteAsyncChunkWorld : Node2D
{
	[Signal]
	public delegate void MapGenerationDoneEventHandler();
	public const string ModuleName = "WorldGenerator";
	
	[Export]
	public TileSet WorldTileset = ResourceLoader.Load<TileSet>("res://assets/tilesets/BasicBiomes.tres");

    [Export]
	public Vector2i WorldSize = new Vector2i(4096, 4096);
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

	public int TotalTiles
	{
		get { return WorldSize.x * WorldSize.y; }
	}

	public int TotalChunks
	{
		get { return _chunks.Count; }
	}

	public int ChunksLoaded
	{
		get;
		set;
	}

	public int TilesLoaded
	{
		get;
		set;
	}

	private string _currentStage = "Begin";

	private ConcurrentBag<Task<Chunk>> _spawnedTasks = new ConcurrentBag<Task<Chunk>>();

	private bool _isLoading = false;

	private CharacterBody2D _player;


	private FastNoiseLite _altNoise;
	private FastNoiseLite _tempNoise;
	private FastNoiseLite _moistNoise;
    private Label _dataBenchMarkLabel;
    private Label _tileBenchMarkLabel;
	private TextureRect _tempNoiseTextureRect;
    private TextureRect _moistNoiseTextureRect;
    private TextureRect _altNoiseTextureRect;
	private Node2D _mapChunkContainer;
	private Label _generatingTotalLabel;
	private ProgressBar _generatingTotalProgress;
	private Label _stageLabel;
	private CanvasLayer _loadScreen;
	private IDictionary<string , Vector2i> _tileDictionary = new ConcurrentDictionary<string ,Vector2i>(
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

	private ConcurrentDictionary<Vector2, float> _temperature = new ConcurrentDictionary<Vector2, float>();
    private ConcurrentDictionary<Vector2, float> _moisture = new ConcurrentDictionary<Vector2, float>();
    private ConcurrentDictionary<Vector2, float> _altitude = new ConcurrentDictionary<Vector2, float>();

	private ConcurrentDictionary<Vector2i, Chunk> _chunks = new ConcurrentDictionary<Vector2i, Chunk>();


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
        Print("Beginning of ready.");
        MapGenerationDone += OnMapGenerationDone;
        Randomize();
		_player = GetNode<CharacterBody2D>("CharacterPlayer");
		_tileBenchMarkLabel = GetNode<Label>("%TileBenchmarkLabel");
        _tempNoiseTextureRect = GetNode<TextureRect>("%TempNoiseRect");
        _altNoiseTextureRect = GetNode<TextureRect>("%AltitudeNoiseRect");
        _moistNoiseTextureRect = GetNode<TextureRect>("%MoistureNoiseRect");
		_mapChunkContainer = GetNode<Node2D>("MapChunkContainer");
		_generatingTotalLabel = GetNode<Label>("%GeneratingTotalLabel");
		_generatingTotalProgress = GetNode<ProgressBar>("%GeneratingTotalProgress");
		_stageLabel = GetNode<Label>("%StageLabel");
		_loadScreen = GetNode<CanvasLayer>("LoadCanvas");

        _generatingTotalProgress.Value = 0;
		// ShowLoadingScreen();
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
		
		Print("Trying to start task..");
		UpdateVisibleChunks();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (_isLoading) 
		{
			return;
		}
		else
		{
			UpdateVisibleChunks();
		}
	}

    public override void _Input(InputEvent @event)
    {

    }

	public override void _ExitTree()
	{
		//cleanup of orphaned chunks not in tree
        foreach (var key in _chunks.Keys)
        {
            _chunks[key].QueueFree();
        }
	}

	private void OnMapGenerationDone()
	{
		_isLoading = false;
		_currentStage = "Done";
		_mapChunkContainer.AddChild(_chunks[Vector2i.Zero]);
		_chunks[Vector2i.Zero].Visible = true;
		// _temperature.Clear();
		// _altitude.Clear();
		// _moisture.Clear();
		HideLoadingScreen();

	}

	private async Task GenerateChunk(Vector2i chunkCoords)
	{
		ConcurrentBag<Task<Chunk>> spawnedTasks = new ConcurrentBag<Task<Chunk>>();
        var timeStart = Time.GetTicksMsec();

		spawnedTasks.Add(CreateChunkAsync(chunkCoords));
		

        while (spawnedTasks.Count > 0)
        {
            try
            {
                Task<Chunk> chunkTask = await Task.WhenAny(spawnedTasks);
                spawnedTasks.TryTake(out chunkTask);

                Chunk chunk = await chunkTask;
                _chunks[chunk.ChunkCoords] = chunk;
            }
            catch (Exception exc)
            {
                PrintErr(exc);
            }
        }
        Print(string.Format("Chunk Generated"));

	}
	
	private float GetTemperatureNoise(Vector2 worldPosition, FastNoiseLite noise)
	{
		return 2f * Mathf.Abs(noise.GetNoise2d(worldPosition.x, worldPosition.y));
	}

    private float GetAltitudeNoise(Vector2 worldPosition, FastNoiseLite noise)
    {
        return 2f * Mathf.Abs(noise.GetNoise2d(worldPosition.x, worldPosition.y));
    }

    private float GetMoistureNoise(Vector2 worldPosition, FastNoiseLite noise)
    {
        return 2f * Mathf.Abs(noise.GetNoise2d(worldPosition.x, worldPosition.y));
    }

	private async Task<Chunk> CreateChunkAsync(Vector2i chunkCoords)
	{
		Chunk chunk = await Task<Chunk>.Run(() => {return CreateChunk(chunkCoords);});
		return chunk;
	}
	private Chunk CreateChunk(Vector2i chunkCoords) 
	{
        Print(string.Format("Creating Chunk ({0},{1})", chunkCoords.x, chunkCoords.y));
		var chunk = new Chunk();
		chunk.ChunkCoords = chunkCoords;
        chunk.ID = _chunks.Count + 1;
        chunk.Name = string.Format("Chunk({0},{1})", chunkCoords.x.ToString(), chunkCoords.y.ToString());
        chunk.TileSet = WorldTileset;
        chunk.Position = new Vector2(WorldTileset.TileSize.x * ChunkSize.x * chunkCoords.x, WorldTileset.TileSize.y * ChunkSize.y * chunkCoords.y);

		//generate the tiles
        foreach (var x in Range(ChunkSize.x))
        {
            foreach (var y in Range(ChunkSize.y))
            {
                var worldPos = new Vector2(x + (ChunkSize.x * chunkCoords.x), y + (ChunkSize.y * chunkCoords.y));
                var pos = new Vector2i(x, y);
                var alt = GetAltitudeNoise(worldPos, _altNoise);
                var temp = GetTemperatureNoise(worldPos, _tempNoise);
                var moist = GetMoistureNoise(worldPos, _moistNoise);

                if (alt < 0.2f)
                {
                    chunk.SetCell(0, pos, 0, _tileDictionary["Water"]);
                }
                else if (Between(alt, 0.2f, .25f))
                {
                    chunk.SetCell(0, pos, 0, _tileDictionary["Sand"]);
                }
                else if (Between(alt, 0.25f, .8f))
                {
                    if (Between(moist, 0, 0.4f) && Between(temp, .2f, .6f))
                    {
                        chunk.SetCell(0, pos, 0, _tileDictionary["Plains"]);
                    }
                    else if (Between(moist, 0.4f, 0.9f) && temp > .6f)
                    {
                        chunk.SetCell(0, pos, 0, _tileDictionary["JungleGrass"]);
                    }
                    else if (Between(moist, 0.4f, 0.9f) && temp < .6f)
                    {
                        chunk.SetCell(0, pos, 0, _tileDictionary["SwampGrass"]);
                    }
                    else if (temp > .7f && moist < .4f)
                    {
                        chunk.SetCell(0, pos, 0, _tileDictionary["Dirt"]);
                    }
                    else
                    {
                        chunk.SetCell(0, pos, 0, _tileDictionary["Grass"]);
                    }
                }
                else
                {
                    chunk.SetCell(0, pos, 0, _tileDictionary["Snow"]);
                }
                TilesLoaded += 1;
            }
            ChunksLoaded += 1;
        }
        Print(string.Format("Chunk ({0},{1} done)", chunkCoords.x, chunkCoords.y));
		_mapChunkContainer.AddChild(chunk);
        return chunk;
    }

	private List<Vector2i> GetVisibleChunks()
	{
        var playerChunkPos = (Vector2i)_player.Position / (ChunkSize * WorldTileset.TileSize);
        var chunksToLoad = new List<Vector2i>();
        chunksToLoad.Add(playerChunkPos);
        chunksToLoad.Add(playerChunkPos + Vector2i.Right);
        chunksToLoad.Add(playerChunkPos + Vector2i.Left);
        chunksToLoad.Add(playerChunkPos + Vector2i.Up);
        chunksToLoad.Add(playerChunkPos + Vector2i.Down);
        chunksToLoad.Add(playerChunkPos + new Vector2i(1, 1));
        chunksToLoad.Add(playerChunkPos + new Vector2i(-1, 1));
        chunksToLoad.Add(playerChunkPos + new Vector2i(1, -1));
        chunksToLoad.Add(playerChunkPos + new Vector2i(-1, -1));

		return chunksToLoad;
	}

	private async void UpdateVisibleChunks()
	{
		_isLoading = true;
		var chunksToLoad = GetVisibleChunks();

		foreach (var coord in chunksToLoad)
		{
			if (!_chunks.ContainsKey(coord))
			{
                await GenerateChunk(coord);
                _chunks[coord].Visible = true;
                Print(string.Format("Chunks generated, marking visible"));
            }
			else 
			{

				if (!_mapChunkContainer.GetChildren().Contains(_chunks[coord]))
				{
                    _mapChunkContainer.AddChild(_chunks[coord]);
				}
                _chunks[coord].Visible = true;
                //Print(string.Format("Chunks exists, marking visible"));
            }
                      
		}
        

		foreach (var coords in _chunks.Keys)
		{
            if (!chunksToLoad.Contains(coords))
			{
                
				_chunks[coords].Visible = false;
				if (_mapChunkContainer.GetChildren().Contains(_chunks[coords]))
				{
                    _mapChunkContainer.RemoveChild(_chunks[coords]);
                    Print(string.Format("Removing Chunk"));
				}
				
			}
		}
		_isLoading = false;
	}	

	private void ShowLoadingScreen()
	{
		_loadScreen.Visible = true;
		var tween = GetTree().CreateTween();
		tween.TweenProperty(_loadScreen.GetNode<ColorRect>("ColorRect"), "modulate", new Color(Colors.White, 1), .5);
		
	}

	private void HideLoadingScreen()
	{
        var tween = GetTree().CreateTween();
        tween.TweenProperty(_loadScreen.GetNode<ColorRect>("ColorRect"), "modulate", new Color(Colors.White, 0), .5);
        tween.TweenCallback(new Callable(this, nameof(HideCallback)));
	}

	private bool Between(float value, float start, float end)
	{
		if (start <= value && value < end) 
		{
			return true;
		}

		return false;
	}

	public void HideCallback()
	{
		_loadScreen.Visible = false;
	}
}
