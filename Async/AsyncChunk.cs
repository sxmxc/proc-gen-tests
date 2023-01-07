using Godot;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

public partial class AsyncChunk : TileMap
{
    public AsyncChunk(int x, int y)
    {
        ChunkCoords = new Vector2i(x, y);
		_chunkSize = GameData.ChunkSize;
        CellQuadrantSize = GameData.ChunkSize.x;
    }
	
    public override void _Draw()
	{
		var rect = new Rect2(Vector2.Zero, _chunkSize * TileSet.TileSize);
		DrawRect(rect, Colors.Red, false, 3);
		Font font = ResourceLoader.Load<Font>("res://Fonts/Roboto-Black.ttf");
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
	private Vector2i _chunkSize;

	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
        QueueRedraw();
	}

	public async Task GenerateAsync(int x, int y, FastNoiseLite tempNoise, FastNoiseLite altNoise, FastNoiseLite moistNoise) 
	{
		{
            // Generate the tiles for the chunk
            // This is a time-consuming task, so we use the await keyword to ensure
            // that it is run asynchronously
            await Task.Run(() => Generate(x, y, tempNoise, altNoise, moistNoise));
		}
	}

	private float GetNoiseValue(Vector2 worldPosition, FastNoiseLite noise)
	{
        return 2f * Mathf.Abs(noise.GetNoise2d(worldPosition.x, worldPosition.y));
	}

    private void Generate(int x, int y, FastNoiseLite tempNoise, FastNoiseLite altNoise, FastNoiseLite moistNoise)
    {
        // Generate the tiles for the chunk
        // This is a time-consuming task, so we use the await keyword to ensure
        // that it is run asynchronously
        for (int i = 0; i < _chunkSize.x; i++)
        {
            for (int j = 0; j < _chunkSize.y; j++)
            {
                var worldPos = new Vector2(i + (GameData.ChunkSize.x * ChunkCoords.x), j + (GameData.ChunkSize.y * ChunkCoords.y));
                var pos = new Vector2i(i, j);
                var alt = GetNoiseValue(worldPos, tempNoise);
                var temp = GetNoiseValue(worldPos, altNoise);
                var moist = GetNoiseValue(worldPos, moistNoise);

                if (alt < 0.2f)
                {
                    SetCell(0, pos, 0, GameData.TileDictionary["Water"]);
                }
                else if (GameUtils.Between(alt, 0.2f, .25f))
                {
                    SetCell(0, pos, 0, GameData.TileDictionary["Sand"]);
                }
                else if (GameUtils.Between(alt, 0.25f, .8f))
                {
                    if (GameUtils.Between(moist, 0, 0.4f) && GameUtils.Between(temp, .2f, .6f))
                    {
                        SetCell(0, pos, 0, GameData.TileDictionary["Plains"]);
                    }
                    else if (GameUtils.Between(moist, 0.4f, 0.9f) && temp > .6f)
                    {
                        SetCell(0, pos, 0, GameData.TileDictionary["JungleGrass"]);
                    }
                    else if (GameUtils.Between(moist, 0.4f, 0.9f) && temp < .6f)
                    {
                        SetCell(0, pos, 0, GameData.TileDictionary["SwampGrass"]);
                    }
                    else if (temp > .7f && moist < .4f)
                    {
                        SetCell(0, pos, 0, GameData.TileDictionary["Dirt"]);
                    }
                    else
                    {
                        SetCell(0, pos, 0, GameData.TileDictionary["Grass"]);
                    }
                }
                else
                {
                    SetCell(0, pos, 0, GameData.TileDictionary["Snow"]);
                }
            }
        }
    }
}
