using Godot;
using System.Collections.Generic;

namespace AdventureLandPrototype;

/// <summary>
/// Loads tile data from CSV files and populates TileMapLayer nodes at runtime.
///
/// COLLISION MODEL
/// MapLoader does not assign physics — all collision shapes live in the
/// TileSet sub-resource inside each scene (editable via Godot's TileSet panel
/// → Paint → Physics Layer 0 → polygon tool).
///
/// World-object collision (building walls, custom rectangles) comes from the
/// Tiled "Walls" object layer, baked into World_*.tres by tmx_triggers_to_tres.py
/// and spawned by TriggerSpawner.
///
/// CRITICAL: Godot 4 TileSetAtlasSource silently ignores SetCell() for atlas
/// coords that haven't been registered via CreateTile(). We call HasTile()
/// before CreateTile() to avoid duplicate registration, tracked per
/// (sourceId, atlasCoord) for multi-tileset maps.
/// </summary>
[Tool]
public partial class MapLoader : Node2D
{
	[Export] public bool AutoLoad = true;

	/// <summary>
	/// Legacy toggle retained for scene compatibility only; building collision
	/// now comes from the Walls object layer in Tiled → .tres → TriggerSpawner.
	/// </summary>
	[Export] public bool SpawnVillageBuildings = false;

	// Tracks (sourceId, atlasCoord) pairs we've already registered. Multi-tileset
	// maps use different sourceIds for different atlas sheets; single-tileset
	// maps always use sourceId=0.
	private readonly HashSet<(int, Vector2I)> _createdTiles = new();

	public override void _Ready()
	{
		// Collision-shape visualization starts OFF. Toggle at runtime with
		// Shift+D (see _UnhandledInput below). D alone is bound to move_right,
		// hence the Shift chord to avoid stealing movement input.
		if (!Engine.IsEditorHint())
			GetTree().DebugCollisionsHint = false;

		if (AutoLoad)
			LoadAllLayers();
	}

	// Backtick-toggle collision debug moved to WorldManager (autoload) so it
	// fires in every scene, not just worlds that host MapLoader.

	private void LoadAllLayers()
	{
		int totalTiles = 0;
		foreach (var child in GetChildren())
		{
			if (child is TileMapLayer layer)
			{
				string csvPath = $"res://assets/map_data/{layer.Name}.csv";
				totalTiles += LoadLayerFromCsv(layer, csvPath);
			}
		}
		GD.Print($"Map loaded: {totalTiles} tiles, {_createdTiles.Count} unique atlas positions");
	}

	private int LoadLayerFromCsv(TileMapLayer layer, string csvPath)
	{
		if (!FileAccess.FileExists(csvPath))
		{
			GD.PrintErr($"Map data not found: {csvPath}");
			return 0;
		}

		var tileSet = layer.TileSet;
		if (tileSet == null) { GD.PrintErr($"Layer '{layer.Name}' has no TileSet"); return 0; }

		// CSVs are the source of truth — wipe any tile_map_data baked into the
		// .tscn so removals in Tiled actually disappear. Without this, SetCell
		// only adds/overwrites, leaving deleted tiles visibly stuck in-game.
		layer.Clear();

		var file = FileAccess.Open(csvPath, FileAccess.ModeFlags.Read);
		int tileCount = 0;

		while (!file.EofReached())
		{
			string line = file.GetLine().StripEdges();
			if (string.IsNullOrEmpty(line)) continue;

			string[] parts = line.Split(',');
			if (parts.Length < 4) continue;

			int x       = int.Parse(parts[0]);
			int y       = int.Parse(parts[1]);
			int atlasX  = int.Parse(parts[2]);
			int atlasY  = int.Parse(parts[3]);
			// Column 5: tileset index for multi-tileset TMXs (Gray Mist Mountain).
			// Single-tileset TMXs emit no 5th column → default to source id 0.
			int sourceId = parts.Length >= 5 && int.TryParse(parts[4], out int si) ? si : 0;
			// Column 6: Tiled flip mask (1=H, 2=V, 4=diagonal/transpose). Older
			// CSVs without this column default to 0 (un-flipped).
			int flipMask = parts.Length >= 6 && int.TryParse(parts[5], out int fm) ? fm : 0;

			var atlasCoord = new Vector2I(atlasX, atlasY);
			var key = (sourceId, atlasCoord);

			var atlasSource = tileSet.GetSource(sourceId) as TileSetAtlasSource;
			if (atlasSource == null)
			{
				GD.PrintErr($"Layer '{layer.Name}': TileSet has no source with id={sourceId}");
				continue;
			}

			// Register tile in atlas if new. Physics (if any) is baked into the
			// TileSet sub-resource — we don't touch it here.
			if (!_createdTiles.Contains(key))
			{
				if (!atlasSource.HasTile(atlasCoord))
					atlasSource.CreateTile(atlasCoord);
				_createdTiles.Add(key);
			}

			// Translate the Tiled flip mask to Godot's atlas transform bits
			// that ride in the alternative_tile int: a virtual alt tile
			// identified by a combination of TRANSFORM_FLIP_* flags will
			// render the base tile with those flips applied, without needing
			// a separately-registered alternative.
			int altId = 0;
			if ((flipMask & 1) != 0) altId |= (int)TileSetAtlasSource.TransformFlipH;
			if ((flipMask & 2) != 0) altId |= (int)TileSetAtlasSource.TransformFlipV;
			if ((flipMask & 4) != 0) altId |= (int)TileSetAtlasSource.TransformTranspose;

			layer.SetCell(new Vector2I(x, y), sourceId, atlasCoord, altId);
			tileCount++;
		}

		file.Close();
		GD.Print($"  {layer.Name}: {tileCount} tiles");
		return tileCount;
	}
}
