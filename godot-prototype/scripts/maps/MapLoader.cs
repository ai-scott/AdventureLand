using Godot;
using System.Collections.Generic;

namespace AdventureLandPrototype;

/// <summary>
/// Loads tile data from CSV files and populates TileMapLayer nodes at runtime.
///
/// TILE COLLISION MODEL
/// Godot stores physics shapes on the tile TYPE (atlas coord), not the placement.
/// Once a physics polygon is assigned to atlas coord (36,10), every cell using
/// that tile anywhere on any layer is automatically solid — no separate collision
/// layer needed. This matches Godot's TileSet editor workflow.
///
/// LayerCollisionModes controls which TMX layers produce solid tiles:
///   "full"   — full 16×16 box  (trees, rocks, fences, walls)
///   "bottom" — lower 8px strip  (e.g. passable-top / solid-base objects)
///   "none"   — walkable, no physics  (ground, grass, decor)
///
/// KNOWN LIMITATION
/// Physics is set on the TILE TYPE, so if the same atlas coord appears on
/// both a solid layer (Objects) and a ground layer, it will be solid there too.
/// In practice the FantasyForest tileset uses different tiles for ground vs
/// objects, so this rarely causes issues. Tune via LayerCollisionModes if needed.
///
/// CRITICAL: Godot 4 TileSetAtlasSource silently ignores SetCell() for atlas
/// coords that haven't been registered via CreateTile(). Always call HasTile()
/// before CreateTile() to avoid duplicate registration.
/// </summary>
[Tool]
public partial class MapLoader : Node2D
{
	[Export] public bool AutoLoad = true;

	/// <summary>
	/// Village-only: spawn the hardcoded building-footprint colliders (Blacksmith,
	/// Windmill, etc.) listed in <see cref="Buildings"/>. Interior scenes must set
	/// this false — otherwise the village footprints spawn in every world.
	/// </summary>
	[Export] public bool SpawnVillageBuildings = true;

	private const int TileSourceId = 0;
	private const int PhysicsLayerId = 0; // matches physics_layer_0 in TileSet

	// Tile-centered coordinate system: (0,0) = tile center, tile is 16×16
	private static readonly Vector2[] FullTilePolygon =
	{
		new(-8, -8), new(8, -8), new(8, 8), new(-8, 8)
	};

	private static readonly Vector2[] BottomHalfPolygon =
	{
		new(-8, 0), new(8, 0), new(8, 8), new(-8, 8)
	};

	// Auto-generated per-tile collision polygons for the Objects layer.
	// Derived from C3 tm_forest_fort.json tile-collision-polys (normalized → px, center-origin).
	// null = no C3 polygon for this tile; falls back to BottomHalfPolygon.
	private static readonly Dictionary<Vector2I, Vector2[]?> ObjectsPolygons = new()
	{
		[new Vector2I(2, 7)] = null,
		[new Vector2I(2, 8)] = null,
		[new Vector2I(3, 7)] = new Vector2[] { new(7f,2f), new(8f,2f), new(8f,8f), new(2f,8f), new(2f,7f) },
		[new Vector2I(3, 8)] = new Vector2[] { new(-2f,-8f), new(8f,-8f), new(8f,2f), new(7f,3f), new(6f,3f), new(3f,2f), new(0.64f,-0.05f), new(1.99f,-4.27f), new(-1.47f,-7.03f) },
		[new Vector2I(4, 7)] = new Vector2[] { new(-7f,-8f), new(8f,-8f), new(6.24f,-1.25f), new(7.96f,3.17f), new(8f,8f), new(-8f,8f), new(-8f,-6f) },
		[new Vector2I(4, 8)] = new Vector2[] { new(-8f,-8f), new(8f,-8f), new(8f,3f), new(3.46f,4.17f), new(-1f,5f), new(-3.68f,2.88f), new(-8f,1f) },
		[new Vector2I(5, 7)] = new Vector2[] { new(-3f,8f), new(-8f,8f), new(-8f,5f) },
		[new Vector2I(5, 8)] = new Vector2[] { new(-8f,-8f), new(-3f,-8f), new(-0.97f,-5.74f), new(0f,-3.82f), new(-5f,0.35f), new(-8f,1f) },
		[new Vector2I(6, 13)] = new Vector2[] { new(2f,-8f), new(8f,-8f), new(8f,8f), new(-1f,8f), new(-3f,5f), new(-3f,-1f), new(-2f,-3f) },
		[new Vector2I(6, 14)] = new Vector2[] { new(0f,-8f), new(8f,-8f), new(8f,-2f), new(4f,-2f), new(1f,-5f), new(0f,-7f) },
		[new Vector2I(7, 13)] = null,
		[new Vector2I(7, 14)] = new Vector2[] { new(-8f,-8f), new(8f,-8f), new(8f,-2f), new(7f,-1f), new(5f,0f), new(-4f,0f), new(-8f,-1f) },
		[new Vector2I(8, 10)] = new Vector2[] { new(-2f,-4f), new(5f,-4f), new(7f,-2f), new(7f,2f), new(6f,3f), new(4f,4f), new(-1f,5f), new(-5f,5f), new(-6f,4f), new(-6f,2f), new(-4f,-2f) },
		[new Vector2I(8, 13)] = new Vector2[] { new(-8f,-8f), new(2f,-8f), new(3f,-6f), new(3f,6f), new(2f,8f), new(-8f,8f) },
		[new Vector2I(8, 14)] = new Vector2[] { new(-8f,-8f), new(1f,-8f), new(1f,-6f), new(-6f,-2f), new(-7f,-2f), new(-8f,-3f) },
		[new Vector2I(9, 7)] = null,
		[new Vector2I(9, 8)] = new Vector2[] { new(4f,-8f), new(8f,-8f), new(8f,2f), new(5f,2f), new(2.56f,-0.4f), new(4.66f,-3.59f) },
		[new Vector2I(9, 10)] = new Vector2[] { new(7f,-3f), new(8f,-3f), new(8f,8f), new(-3f,8f), new(-3f,6f), new(5f,-2f) },
		[new Vector2I(9, 11)] = new Vector2[] { new(-3f,-8f), new(8f,-8f), new(8f,2f), new(7f,2f), new(0f,-1f), new(-1f,-2f), new(-3f,-6f) },
		[new Vector2I(10, 7)] = new Vector2[] { new(-5f,-8f), new(6f,-8f), new(5.57f,0.59f), new(8f,8f), new(-7.02f,7.98f), new(-5.66f,1.93f), new(-6f,-6f) },
		[new Vector2I(10, 8)] = new Vector2[] { new(-8f,-8f), new(8f,-8f), new(8f,1f), new(7f,3f), new(4f,5f), new(0f,5f), new(-4.34f,2.59f), new(-8f,3f) },
		[new Vector2I(10, 10)] = new Vector2[] { new(-8f,-3f), new(-4f,-3f), new(0f,1f), new(1f,3f), new(1f,8f), new(-8f,8f) },
		[new Vector2I(10, 11)] = new Vector2[] { new(-8f,-8f), new(2f,-8f), new(3f,-7f), new(3f,-1f), new(1f,1f), new(-4f,2f), new(-8f,2f) },
		[new Vector2I(11, 7)] = null,
		[new Vector2I(11, 8)] = null,
		[new Vector2I(12, 13)] = new Vector2[] { new(-5f,-8f), new(8f,-8f), new(8f,8f), new(-4f,8f), new(-5f,7f) },
		[new Vector2I(13, 10)] = new Vector2[] { new(-3f,-1f), new(0f,-1f), new(2f,0f), new(6f,4f), new(6f,8f), new(-7f,8f), new(-7f,7f), new(-6f,3f), new(-5f,1f) },
		[new Vector2I(13, 11)] = new Vector2[] { new(-7f,-8f), new(6f,-8f), new(7f,-7f), new(8f,-5f), new(8f,-1f), new(3f,4f), new(-1f,4f), new(-5f,2f), new(-8f,-1f), new(-8f,-5f) },
		[new Vector2I(13, 13)] = new Vector2[] { new(-8f,-8f), new(4f,-8f), new(7f,-2f), new(7f,4f), new(5f,8f), new(-8f,8f) },
		[new Vector2I(14, 7)] = null,
		[new Vector2I(14, 8)] = new Vector2[] { new(7f,-8f), new(8f,-8f), new(8f,1f), new(7f,1f), new(4.4f,-1.73f), new(5.61f,-5.03f) },
		[new Vector2I(14, 11)] = new Vector2[] { new(3f,-8f), new(8f,-8f), new(8f,8f), new(-7f,8f), new(-7f,5f), new(-6f,2f), new(-5f,0f) },
		[new Vector2I(14, 12)] = new Vector2[] { new(-7f,-8f), new(8f,-8f), new(8f,1f), new(5f,1f), new(-4f,-3f), new(-7f,-5f) },
		[new Vector2I(14, 13)] = new Vector2[] { new(6f,-8f), new(8f,-8f), new(8f,8f), new(-4f,8f), new(-4f,5f), new(1f,-3f) },
		[new Vector2I(14, 14)] = new Vector2[] { new(-5f,-8f), new(8f,-8f), new(8f,2f), new(7f,3f), new(5f,3f), new(1f,2f), new(-3f,0f), new(-5f,-3f) },
		[new Vector2I(14, 34)] = null,
		[new Vector2I(14, 35)] = null,
		[new Vector2I(15, 7)] = new Vector2[] { new(-5.24f,-1.05f), new(-3.36f,-7.98f), new(8f,-8f), new(8f,-2f), new(4.52f,1.37f), new(5f,8f), new(-8f,8f), new(-5.13f,3.96f) },
		[new Vector2I(15, 8)] = new Vector2[] { new(-8f,-8f), new(8f,-8f), new(8f,0f), new(7f,2f), new(3f,4f), new(-2.79f,1.67f), new(-8f,1f) },
		[new Vector2I(15, 11)] = null,
		[new Vector2I(15, 12)] = new Vector2[] { new(-8f,-8f), new(8f,-8f), new(8f,0f), new(5f,1f), new(-8f,2f) },
		[new Vector2I(15, 13)] = null,
		[new Vector2I(15, 14)] = new Vector2[] { new(-8f,-8f), new(8f,-8f), new(8f,4f), new(-6f,4f), new(-8f,2f) },
		[new Vector2I(15, 34)] = null,
		[new Vector2I(15, 35)] = null,
		[new Vector2I(16, 7)] = null,
		[new Vector2I(16, 8)] = new Vector2[] { new(-6.53f,-4.63f), new(-6f,0f), new(-8f,0f), new(-8f,-7f) },
		[new Vector2I(16, 11)] = new Vector2[] { new(-8f,-8f), new(0f,-8f), new(4f,-4f), new(5f,-2f), new(6f,1f), new(6f,8f), new(-8f,8f) },
		[new Vector2I(16, 13)] = new Vector2[] { new(-8f,-6f), new(-5f,-6f), new(-2f,-5f), new(2f,-3f), new(3f,-2f), new(5f,2f), new(5f,6f), new(4f,8f), new(-8f,8f) },
		[new Vector2I(16, 14)] = new Vector2[] { new(-8f,-8f), new(4f,-8f), new(4f,-5f), new(3f,-3f), new(2f,-2f), new(-3f,2f), new(-7f,3f), new(-8f,3f) },
		[new Vector2I(18, 7)] = null,
		[new Vector2I(18, 8)] = null,
		[new Vector2I(19, 7)] = new Vector2[] { new(-8f,-8f), new(8f,-8f), new(8f,0f), new(4.55f,0.3f), new(4.08f,8f), new(-2f,8f), new(-3.99f,1.73f), new(-8f,-1.01f) },
		[new Vector2I(19, 8)] = new Vector2[] { new(-2f,-8f), new(4f,-8f), new(8f,0f), new(8f,2f), new(2.69f,3.55f), new(-0.8f,3.74f), new(-5.14f,2.61f), new(-5.9f,-0.75f), new(-6.1f,-4.42f) },
		[new Vector2I(20, 7)] = null,
		[new Vector2I(20, 8)] = null,
		[new Vector2I(20, 31)] = null,
		[new Vector2I(21, 31)] = null,
		[new Vector2I(22, 31)] = null,
		[new Vector2I(23, 31)] = new Vector2[] { new(-8f,-8f), new(-4f,-8f), new(6f,7f), new(6f,8f), new(-8f,8f) },
		[new Vector2I(24, 11)] = new Vector2[] { new(-6f,-8f), new(8f,-8f), new(8f,6f), new(4f,8f), new(2f,8f), new(-1f,7f), new(-5f,-2f), new(-6f,-5f) },
		[new Vector2I(24, 12)] = null,
		[new Vector2I(25, 11)] = new Vector2[] { new(-8f,-8f), new(8f,-8f), new(8f,8f), new(-3.03f,7.98f), new(-4.44f,4.41f), new(-8f,3.29f) },
		[new Vector2I(25, 12)] = new Vector2[] { new(-3f,-8f), new(8f,-8f), new(8f,1f), new(6f,1f), new(-2f,-1f), new(-3f,-3f) },
		[new Vector2I(26, 11)] = null,
		[new Vector2I(26, 12)] = new Vector2[] { new(-8f,-8f), new(8f,-8f), new(8f,2f), new(7f,4f), new(5f,6f), new(2f,6f), new(-3f,5f), new(-5f,4f), new(-8f,1f) },
		[new Vector2I(26, 31)] = new Vector2[] { new(5f,-8f), new(8f,-8f), new(8f,8f), new(-5f,8f), new(-5f,5f), new(3f,-6f) },
		[new Vector2I(26, 32)] = new Vector2[] { new(-5f,-8f), new(8f,-8f), new(8f,8f), new(-8f,8f), new(-4.97f,-0.42f), new(-6.5f,-6.5f) },
		[new Vector2I(27, 11)] = new Vector2[] { new(-8f,-8f), new(8f,-8f), new(8f,8f), new(-1.51f,6.75f), new(-8f,7f) },
		[new Vector2I(27, 12)] = null,
		[new Vector2I(27, 31)] = null,
		[new Vector2I(27, 32)] = null,
		[new Vector2I(28, 11)] = new Vector2[] { new(-8f,-8f), new(-2f,-8f), new(8f,3f), new(8f,4f), new(4f,8f), new(-8f,8f) },
		[new Vector2I(28, 12)] = new Vector2[] { new(-8f,-8f), new(3f,-8f), new(4f,-7f), new(4f,-4f), new(2f,-4f), new(-7f,-6f), new(-8f,-7f) },
		[new Vector2I(28, 31)] = null,
		[new Vector2I(28, 32)] = null,
		[new Vector2I(29, 9)] = new Vector2[] { new(7f,3f), new(8f,3f), new(8f,8f), new(1f,8f), new(1f,7f) },
		[new Vector2I(29, 10)] = new Vector2[] { new(-2f,-8f), new(8f,-8f), new(8f,7f), new(7f,7f), new(3f,6f), new(-3f,3f), new(-4f,2f), new(-5f,-3f), new(-5f,-5f) },
		[new Vector2I(29, 11)] = new Vector2[] { new(7f,3f), new(8f,3f), new(8f,8f), new(1f,8f), new(1f,7f), new(5f,4f) },
		[new Vector2I(29, 12)] = new Vector2[] { new(-2f,-8f), new(8f,-8f), new(8f,7f), new(7f,7f), new(3f,6f), new(-3f,3f), new(-4f,2f), new(-5f,-3f), new(-5f,-5f) },
		[new Vector2I(29, 18)] = null,
		[new Vector2I(29, 19)] = null,
		[new Vector2I(29, 31)] = null,
		[new Vector2I(29, 32)] = null,
		[new Vector2I(30, 9)] = new Vector2[] { new(7f,-7f), new(8f,-7f), new(8f,8f), new(-8f,8f), new(-8f,3f) },
		[new Vector2I(30, 10)] = new Vector2[] { new(-8f,-8f), new(8f,-8f), new(8f,0f), new(-7f,8f), new(-8f,8f) },
		[new Vector2I(30, 11)] = new Vector2[] { new(7f,-7f), new(8f,-7f), new(8f,8f), new(-8f,8f), new(-8f,3f) },
		[new Vector2I(30, 12)] = new Vector2[] { new(-8f,-8f), new(8f,-8f), new(8f,0f), new(-7f,8f), new(-8f,8f) },
		[new Vector2I(30, 18)] = null,
		[new Vector2I(30, 19)] = null,
		[new Vector2I(30, 31)] = new Vector2[] { new(-8f,-8f), new(-4f,-8f), new(6f,7f), new(6f,8f), new(-8f,8f) },
		[new Vector2I(30, 32)] = new Vector2[] { new(-8f,-8f), new(7f,-8f), new(6.15f,1.22f), new(8f,8f), new(-8f,8f) },
		[new Vector2I(31, 9)] = new Vector2[] { new(-8f,-8f), new(-7f,-8f), new(-3f,-7f), new(4f,0f), new(4f,4f), new(0f,8f), new(-8f,8f) },
		[new Vector2I(31, 10)] = new Vector2[] { new(-8f,-8f), new(-2f,-8f), new(-2f,-7f), new(-4f,-5f), new(-7f,-3f), new(-8f,-3f) },
		[new Vector2I(31, 11)] = new Vector2[] { new(-8f,-8f), new(-7f,-8f), new(-3f,-7f), new(4f,0f), new(4f,4f), new(0f,8f), new(-8f,8f) },
		[new Vector2I(31, 12)] = new Vector2[] { new(-8f,-8f), new(-2f,-8f), new(-2f,-7f), new(-4f,-5f), new(-7f,-3f), new(-8f,-3f) },
		[new Vector2I(32, 9)] = null,
		[new Vector2I(32, 10)] = null,
		[new Vector2I(32, 12)] = null,
		[new Vector2I(32, 13)] = new Vector2[] { new(7f,-6f), new(8f,-6f), new(8f,1f), new(6f,1f), new(5f,-2f), new(5f,-3f), new(6f,-5f) },
		[new Vector2I(33, 9)] = new Vector2[] { new(-1f,-3f), new(2f,-3f), new(7f,-2f), new(8f,-1f), new(8f,8f), new(-5f,8f), new(-5f,2f), new(-4f,0f) },
		[new Vector2I(33, 10)] = new Vector2[] { new(-6f,-8f), new(8f,-8f), new(8f,5f), new(5f,5f), new(-2f,4f), new(-5f,3f), new(-7f,2f), new(-8f,1f), new(-8f,-6f) },
		[new Vector2I(33, 12)] = new Vector2[] { new(-7.03f,-7.99f), new(8f,-8f), new(8f,8f), new(-6f,8f), new(-5.52f,1.88f), new(-6.92f,-4.13f) },
		[new Vector2I(33, 13)] = new Vector2[] { new(-7f,-8f), new(8f,-8f), new(8f,5f), new(4f,5f), new(-3f,4f), new(-6f,3f), new(-8f,2f), new(-8f,-7f) },
		[new Vector2I(34, 9)] = new Vector2[] { new(-8f,0f), new(-7f,0f), new(-6f,1f), new(-6f,8f), new(-8f,8f) },
		[new Vector2I(34, 10)] = new Vector2[] { new(-8f,-8f), new(-6f,-8f), new(-3f,-6f), new(-1f,-2f), new(-1f,-1f), new(-2f,2f), new(-5f,4f), new(-7f,5f), new(-8f,5f) },
		[new Vector2I(34, 12)] = null,
		[new Vector2I(34, 13)] = new Vector2[] { new(-8f,-8f), new(-6f,-8f), new(-4f,-6f), new(-2f,-2f), new(-2f,-1f), new(-3f,2f), new(-6f,4f), new(-8f,4f) },
		[new Vector2I(35, 10)] = new Vector2[] { new(1f,-8f), new(8f,-8f), new(8f,6f), new(5f,6f), new(3f,5f), new(1f,1f), new(0f,-2f), new(0f,-5f) },
		[new Vector2I(35, 11)] = null,
		[new Vector2I(35, 18)] = null,
		[new Vector2I(35, 19)] = null,
		[new Vector2I(36, 10)] = null,
		[new Vector2I(36, 11)] = new Vector2[] { new(-6f,-8f), new(8f,-8f), new(8f,-2f), new(5f,-1f), new(4f,-1f), new(0f,-2f), new(-6f,-7f) },
		[new Vector2I(37, 10)] = null,
		[new Vector2I(37, 11)] = new Vector2[] { new(-8f,-8f), new(8f,-8f), new(8f,0f), new(7f,1f), new(-1f,1f), new(-8f,-2f) },
		[new Vector2I(38, 10)] = new Vector2[] { new(-8f,-6f), new(-6f,-6f), new(-1f,-2f), new(1f,0f), new(3f,3f), new(3f,5f), new(2f,8f), new(-8f,8f) },
		[new Vector2I(38, 11)] = new Vector2[] { new(-8f,-8f), new(2f,-8f), new(2f,-5f), new(-2f,-1f), new(-5f,0f), new(-8f,0f) },
		[new Vector2I(38, 14)] = new Vector2[] { new(-3f,-8f), new(8f,-8f), new(8f,8f), new(0f,8f), new(-0.42f,0.14f), new(-4f,-6f), new(-4f,-7f) },
		[new Vector2I(38, 15)] = new Vector2[] { new(-1f,-8f), new(8f,-8f), new(8f,5f), new(4f,5f), new(2.41f,-0.39f), new(-3f,-5f), new(-3f,-6f) },
		[new Vector2I(39, 14)] = new Vector2[] { new(-8f,-8f), new(3f,-8f), new(3f,-5f), new(-0.34f,-1.36f), new(0.18f,7.08f), new(-6f,8f), new(-8f,8f) },
		[new Vector2I(39, 15)] = new Vector2[] { new(-8f,-8f), new(3f,-8f), new(3f,-7f), new(-1.44f,-1.41f), new(-2.72f,2.86f), new(-7f,5f), new(-8f,5f) },
		[new Vector2I(40, 14)] = new Vector2[] { new(-2f,-8f), new(8f,-8f), new(8f,8f), new(-1f,8f), new(-4f,-2f), new(-4f,-3f) },
		[new Vector2I(40, 15)] = new Vector2[] { new(-2f,-8f), new(8f,-8f), new(8f,4f), new(6f,4f), new(2.42f,1.9f), new(2.13f,-2.35f), new(-3f,-4f), new(-3f,-5f) },
		[new Vector2I(40, 19)] = null,
		[new Vector2I(41, 14)] = new Vector2[] { new(-8f,-8f), new(4f,-8f), new(1.91f,-0.44f), new(4.06f,5.15f), new(3.96f,7.95f), new(-8f,8f) },
		[new Vector2I(41, 15)] = new Vector2[] { new(-8f,-8f), new(4f,-8f), new(3.53f,-5.03f), new(-4.3f,-5.77f), new(-3.9f,2.1f), new(-8f,3f) },
		[new Vector2I(41, 19)] = null,
		[new Vector2I(90, 3)] = new Vector2[] { new(2f,-7f), new(8f,-8f), new(8f,8f), new(-6f,8f), new(-6f,4f), new(-5f,0f), new(-1f,-5f), new(0f,-6f) },
		[new Vector2I(90, 4)] = new Vector2[] { new(-5f,-8f), new(8f,-8f), new(8f,4f), new(4f,4f), new(1f,1f), new(-5f,-7f) },
		[new Vector2I(90, 5)] = new Vector2[] { new(2f,-7f), new(8f,-8f), new(8f,8f), new(-6f,8f), new(-6f,4f), new(-5f,0f), new(-1f,-5f), new(0f,-6f) },
		[new Vector2I(90, 6)] = new Vector2[] { new(-5f,-8f), new(8f,-8f), new(8f,4f), new(4f,4f), new(1f,1f), new(-5f,-7f) },
		[new Vector2I(91, 3)] = new Vector2[] { new(-8f,-7f), new(-4f,-7f), new(0f,-6f), new(3f,-3f), new(6f,1f), new(7f,3f), new(7f,6f), new(6f,7f), new(4f,8f), new(-8f,8f) },
		[new Vector2I(91, 4)] = new Vector2[] { new(-8f,-8f), new(5f,-8f), new(5f,-6f), new(-4f,4f), new(-8f,4f) },
		[new Vector2I(91, 5)] = new Vector2[] { new(-8f,-7f), new(-4f,-7f), new(0f,-6f), new(3f,-3f), new(6f,1f), new(7f,3f), new(7f,6f), new(6f,7f), new(4f,8f), new(-8f,8f) },
		[new Vector2I(91, 6)] = new Vector2[] { new(-8f,-8f), new(5f,-8f), new(5f,-6f), new(-4f,4f), new(-8f,4f) },
	};

	/// <summary>
	/// Maps TMX layer names to collision mode.
	/// Extend this when adding new worlds or adjusting collision behaviour.
	/// </summary>
	private static readonly Dictionary<string, string> LayerCollisionModes = new()
	{
		["Ground3underP"]  = "none",
		["Ground2underP"]  = "none",
		["Ground1underP"]  = "none",
		["Objects"]        = "objects-poly", // per-tile C3 polygons; falls back to BottomHalfPolygon
		["Collisions"]     = "full",   // dedicated invisible collision layer (if exported)
		["Decor1PLevel"]   = "none",   // flowers, mushrooms — walk through
		["Decor1Plevel"]   = "none",   // alternate casing from TMX converter
		["Decor2overP"]    = "none",
		["Decor3overP"]    = "none",
	};

	// Tracks atlas coords we've already registered (shared across layers —
	// all layers reference the same TileSet/TileSetAtlasSource).
	private readonly HashSet<Vector2I> _createdTiles = new();

	// Tracks atlas coords that have already had physics polygons assigned.
	// Physics is per tile-type, so we only need to assign once even if the
	// same atlas coord appears across multiple layers.
	private readonly HashSet<Vector2I> _physicsAssigned = new();

	// Building footprints: (label, spriteX, spriteY, spriteW, spriteH, footprintH)
	// Positions match tmx_to_godot.py BUILDINGS list. FootprintH = collision strip height.
	private static readonly (string Label, float X, float Y, float W, float H, float FootH)[] Buildings =
	{
		("Blacksmith",  213.077f, 235.893f, 120f, 112f, 14f),
		("Cabin1",       35.983f, 274.895f,  48f,  80f, 14f),
		("Cabin2",      122.176f,  65.690f,  48f,  80f, 14f),
		("Shop",        468.769f, 110.484f, 120f, 112f, 14f),
		("WeaponShop",  343.953f,  74.242f, 120f, 112f, 14f),
		("Windmill",    228.961f,  77.124f, 102f, 112f, 14f),
		("Well",        369.743f, 301.073f,  44f,  52f, 36f),
		("TreeSign",    459.530f, 251.027f,  52f,  45f, 14f),
	};

	public override void _Ready()
	{
		if (AutoLoad)
			LoadAllLayers();
		if (!Engine.IsEditorHint() && SpawnVillageBuildings)
			SpawnBuildingColliders();
	}

	private void SpawnBuildingColliders()
	{
		foreach (var (label, x, y, w, h, footH) in Buildings)
		{
			var body = new StaticBody2D();
			body.Name = $"{label}Wall";
			body.CollisionLayer = 2;
			body.CollisionMask = 0;
			body.Position = new Vector2(x + w / 2f, y + h - footH / 2f);

			var rect = new RectangleShape2D();
			rect.Size = new Vector2(w - 4f, footH);
			var shape = new CollisionShape2D();
			shape.Shape = rect;
			body.AddChild(shape);
			AddChild(body);
		}
		GD.Print($"[MapLoader] {Buildings.Length} building colliders spawned");
	}

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
		GD.Print($"Map loaded: {totalTiles} tiles, {_createdTiles.Count} unique atlas positions, {_physicsAssigned.Count} with physics");
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

		var atlasSource = tileSet.GetSource(TileSourceId) as TileSetAtlasSource;
		if (atlasSource == null) { GD.PrintErr($"TileSet source {TileSourceId} is not TileSetAtlasSource"); return 0; }

		string collisionMode = LayerCollisionModes.TryGetValue(layer.Name, out var m) ? m : "none";

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

			var atlasCoord = new Vector2I(atlasX, atlasY);

			// Register tile in atlas if new
			if (!_createdTiles.Contains(atlasCoord))
			{
				if (!atlasSource.HasTile(atlasCoord))
					atlasSource.CreateTile(atlasCoord);
				_createdTiles.Add(atlasCoord);
			}

			// Assign physics polygon to tile type (once per atlas coord)
			if (collisionMode != "none" && !_physicsAssigned.Contains(atlasCoord))
			{
				var tileData = atlasSource.GetTileData(atlasCoord, 0);
				if (tileData != null)
				{
					Vector2[] polygon;
					if (collisionMode == "objects-poly")
					{
						// Use C3-derived per-tile polygon; null entry falls back to bottom strip
						polygon = ObjectsPolygons.TryGetValue(atlasCoord, out var poly) && poly != null
							? poly
							: BottomHalfPolygon;
					}
					else
					{
						polygon = collisionMode == "bottom" ? BottomHalfPolygon : FullTilePolygon;
					}
					tileData.SetCollisionPolygonsCount(PhysicsLayerId, 1);
					tileData.SetCollisionPolygonPoints(PhysicsLayerId, 0, polygon);
				}
				_physicsAssigned.Add(atlasCoord);
			}

			layer.SetCell(new Vector2I(x, y), TileSourceId, atlasCoord);
			tileCount++;
		}

		file.Close();
		GD.Print($"  {layer.Name}: {tileCount} tiles [{collisionMode}]");
		return tileCount;
	}
}
