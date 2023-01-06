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
	public int NumberChunksX 
	{ 
		get {return WorldSize.x / ChunkSize.x;} 
	}
    public int NumberChunksY
    {
        get { return WorldSize.y / ChunkSize.y; }
    }

	public int TotalTiles
	{
		get { return WorldSize.x * WorldSize.y; }
	}

	public int TotalChunks
	{
		get { return NumberChunksX * NumberChunksY; }
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

	private ulong _time_start;
	private string _currentStage = "Begin";

	private ConcurrentBag<Task<Chunk>> _spawnedTasks = new ConcurrentBag<Task<Chunk>>();

	private bool _isLoading = false;

	private CharacterBody2D _player;

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
		ShowLoadingScreen();
        TemperatureSeed = (int)Randi();
        MoistureSeed = (int)Randi();
        AltitudeSeed = (int)Randi();
		
		Print("Trying to start task..");
		UpdateVisibleChunks();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (_isLoading) 
		{
			_generatingTotalLabel.Text = string.Format("Tiles ({0}/{1})", TilesLoaded.ToString(), TotalTiles.ToString());
			_stageLabel.Text = _currentStage;
			var progress = ((float)TilesLoaded / (float)TotalTiles) * 100;
			_generatingTotalProgress.Value = progress;
		}
		else
		{
			UpdateVisibleChunks();
		}
	}

    public override void _Input(InputEvent @event)
    {
		if (!_isLoading)
		{
			if (@event is InputEventKey eventKey)
			{
				if (eventKey.Pressed && eventKey.Keycode == Key.Space)
				{
					ShowLoadingScreen();
                    TemperatureSeed = (int)Randi();
                    MoistureSeed = (int)Randi();
                    AltitudeSeed = (int)Randi();
					var toDelete = _chunks;
					_chunks.Clear();
                    foreach (var key in toDelete.Keys)
                    {
                        toDelete[key].QueueFree();
                    }
                    Task.Run(async () => { await Generate(); });
				}
			}
		}
    }

	public override void _ExitTree()
	{
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

	private async Task GenerateChunks(List<Vector2i> chunksToLoad)
	{
		Chunk chunk;
        var timeStart = Time.GetTicksMsec();

        var altNoise = new FastNoiseLite();
        altNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Value;
        altNoise.Seed = AltitudeSeed;

        var moistNoise = new FastNoiseLite();
        moistNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Value;
        moistNoise.Seed = MoistureSeed;

        var tempNoise = new FastNoiseLite();
        tempNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Value;
        tempNoise.Seed = TemperatureSeed;

		foreach (var x in chunksToLoad)
		{
            _spawnedTasks.Add(CreateChunkAsync(x, altNoise, tempNoise, moistNoise));
		}

        while (_spawnedTasks.Count > 0)
        {
            try
            {
                Task<Chunk> chunkTask = await Task.WhenAny(_spawnedTasks);
                _spawnedTasks.TryTake(out chunkTask);

                chunk = await chunkTask;
                _chunks[chunk.ChunkCoords] = chunk;
            }
            catch (Exception exc)
            {
                PrintErr(exc);
            }
        }



	}
	private async Task Generate()
	{
        Print("Generate task started..");
		_time_start = Time.GetTicksMsec();
		TilesLoaded = 0;
		_isLoading = true;

		_currentStage = "Generating chunks of tiles...";
        var altNoise = new FastNoiseLite();
        altNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Value;
        altNoise.Seed = AltitudeSeed;
        var altNoiseTexture = new NoiseTexture2D();
        altNoiseTexture.Noise = altNoise;
        _altNoiseTextureRect.CallDeferred("set_texture", altNoiseTexture);

        var moistNoise = new FastNoiseLite();
        moistNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Value;
        moistNoise.Seed = MoistureSeed;
        var moistNoiseTexture = new NoiseTexture2D();
        moistNoiseTexture.Noise = moistNoise;
        _moistNoiseTextureRect.CallDeferred("set_texture", moistNoiseTexture);

        var tempNoise = new FastNoiseLite();
        tempNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Value;
        tempNoise.Seed = TemperatureSeed;
        var tempNoiseTexture = new NoiseTexture2D();
        tempNoiseTexture.Noise = tempNoise;
        _tempNoiseTextureRect.CallDeferred("set_texture", tempNoiseTexture);

		var tally = 0;
		foreach (var x in Range(NumberChunksX))
		{
			foreach (var y in Range(NumberChunksY))
			{
				var temp = new Chunk();
				temp.ChunkCoords = new Vector2i(x,y);
				temp.ID = tally;
				temp.Name = string.Format("Chunk({0}/{1})", x.ToString(), y.ToString());
				temp.TileSet = WorldTileset;
				temp.Position = new Vector2(WorldTileset.TileSize.x * ChunkSize.x * x, WorldTileset.TileSize.y * ChunkSize.y * y);
                _spawnedTasks.Add(SetTilesAsync(temp, temp.ChunkCoords, altNoise, tempNoise, moistNoise));
				tally += 1; 
			}
		}
		while(_spawnedTasks.Count > 0 )
		{
            try
            {
				Task<Chunk> chunkTask = await Task.WhenAny(_spawnedTasks);
				_spawnedTasks.TryTake(out chunkTask);

				Chunk chunk = await chunkTask;
				_chunks[chunk.ChunkCoords] = chunk;
            }
            catch (Exception exc)
            {
                PrintErr(exc);
            }
		}
		ulong tileTimeEnd = Time.GetTicksMsec();
        _tileBenchMarkLabel.Text = string.Format("Tile generation time: {0}ms", tileTimeEnd - _time_start);
        EmitSignal(nameof(MapGenerationDone));
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

	private async Task<Chunk> CreateChunkAsync(Vector2i chunkCoords, FastNoiseLite altNoise, FastNoiseLite tempNoise, FastNoiseLite moistNoise)
	{
        Print(string.Format("Generating Chunk ({0}/{1})", chunkCoords.x, chunkCoords.y));
		Chunk chunk = await Task<Chunk>.Run(() => {return CreateChunk(chunkCoords, altNoise, tempNoise, moistNoise);});
		return chunk;
	}
	private Chunk CreateChunk(Vector2i chunkCoords, FastNoiseLite altNoise, FastNoiseLite tempNoise, FastNoiseLite moistNoise) 
	{
		var chunk = new Chunk();
		chunk.ChunkCoords = chunkCoords;
        chunk.ID = _chunks.Count + 1;
        chunk.Name = string.Format("Chunk({0}/{1})", chunkCoords.ToString(), chunkCoords.ToString());
        chunk.TileSet = WorldTileset;
        chunk.Position = new Vector2(WorldTileset.TileSize.x * ChunkSize.x * chunkCoords.x, WorldTileset.TileSize.y * ChunkSize.y * chunkCoords.y);

		//generate the tiles
        foreach (var x in Range(ChunkSize.x))
        {
            foreach (var y in Range(ChunkSize.y))
            {
                var worldPos = new Vector2(x + (ChunkSize.x * chunkCoords.x), y + (ChunkSize.y * chunkCoords.y));
                var pos = new Vector2i(x, y);
                var alt = GetAltitudeNoise(worldPos, altNoise);
                var temp = GetTemperatureNoise(worldPos, tempNoise);
                var moist = GetMoistureNoise(worldPos, moistNoise);

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
        return chunk;
    }

    private async Task<Chunk> SetTilesAsync(Chunk chunkTileMap, Vector2i chunkCoords, FastNoiseLite altNoise, FastNoiseLite tempNoise, FastNoiseLite moistNoise)
    {
        Chunk chunk = await Task<Chunk>.Run(() => { return SetTiles(chunkTileMap, chunkCoords, altNoise, tempNoise, moistNoise); });
        return chunk;
    }

	private Chunk SetTiles(Chunk chunkTileMap, Vector2i chunkCoords, FastNoiseLite altNoise, FastNoiseLite tempNoise, FastNoiseLite moistNoise)
	{	
		foreach (var x in Range(ChunkSize.x))
		{
			foreach (var y in Range(ChunkSize.y))
			{
				var worldPos = new Vector2(x + (ChunkSize.x * chunkCoords.x), y + (ChunkSize.y * chunkCoords.y));
				var pos = new Vector2i(x,y);
				var alt = GetAltitudeNoise(worldPos, altNoise);
				var temp = GetTemperatureNoise(worldPos, tempNoise);
				var moist = GetMoistureNoise(worldPos, moistNoise);

				if (alt < 0.2f)
				{
					chunkTileMap.SetCell(0, pos, 0, _tileDictionary["Water"]);
				}
				else if (Between(alt, 0.2f, .25f))
				{
                    chunkTileMap.SetCell(0, pos, 0, _tileDictionary["Sand"]);
				}
				else if (Between(alt, 0.25f, .8f))
				{
					if (Between(moist, 0, 0.4f) && Between(temp, .2f, .6f))
					{
                        chunkTileMap.SetCell(0, pos, 0, _tileDictionary["Plains"]);
					}
					else if (Between(moist, 0.4f, 0.9f) && temp > .6f)
					{
                        chunkTileMap.SetCell(0, pos, 0, _tileDictionary["JungleGrass"]);
					}
					else if (Between(moist, 0.4f, 0.9f) && temp < .6f)
					{
                        chunkTileMap.SetCell(0, pos, 0, _tileDictionary["SwampGrass"]);
					}
					else if (temp > .7f && moist < .4f)
					{
                        chunkTileMap.SetCell(0, pos, 0, _tileDictionary["Dirt"]);
					}
					else
					{
                        chunkTileMap.SetCell(0, pos, 0, _tileDictionary["Grass"]);
					}
				}
				else 
				{
                    chunkTileMap.SetCell(0, pos, 0, _tileDictionary["Snow"]);
				}
                TilesLoaded += 1;
			}
			ChunksLoaded += 1;
		}
		return chunkTileMap;
	}

	private void UpdateVisibleChunks()
	{
		var playerChunkPos =  (Vector2i)_player.Position / (ChunkSize * WorldTileset.TileSize);
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

        Task.Run(async () => { await GenerateChunks(chunksToLoad); });

		foreach (var coords in _chunks.Keys)
		{
			if (!chunksToLoad.Contains(coords))
			{
				_chunks[coords].Visible = false;
				_chunks[coords].GetParent().RemoveChild(_chunks[coords]);
			}
			else
			{
				_mapChunkContainer.AddChild(_chunks[coords]);
			}
		}
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
