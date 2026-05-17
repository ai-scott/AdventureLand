using Godot;

namespace AdventureLandPrototype;

public partial class FollowCamera : Camera2D
{
	[Export] public NodePath TargetPath;

	private Node2D _target;

	public override void _Ready()
	{
		// Resolve follow target:
		// 1) explicit TargetPath (Inspector)
		// 2) auto-find node named "Player" in the scene (for top-level cameras)
		// 3) null — camera inherits transform from parent (when parented under Player)
		if (TargetPath != null && !TargetPath.IsEmpty)
		{
			_target = GetNodeOrNull<Node2D>(TargetPath);
		}
		if (_target == null)
		{
			var scene = GetTree().CurrentScene;
			_target = scene?.FindChild("Player", true, false) as Node2D;
		}

		// Disable smoothing — it conflicts with hard clamping at map edges.
		PositionSmoothingEnabled = false;

		// Respect the Zoom set in the scene file (2x for exterior, higher for
		// small interior maps). If unset (identity), fall back to 2x pixel-art default.
		if (Zoom == Vector2.One)
			Zoom = new Vector2(2.0f, 2.0f);

		// Force this camera to be the active one — Player.tscn has a redundant
		// Camera2D child that would otherwise take over as current.
		MakeCurrent();

		// Read WorldMeta from the current scene and set camera bounds.
		ApplyWorldBounds();
	}

	/// <summary>
	/// Looks up the active scene's WorldMeta node and sets camera Limit* properties
	/// so the visible rect clamps at the map edges. Godot's Camera2D limits constrain
	/// the VISIBLE RECT edges (not the camera center), so we can pass the map bounds
	/// directly.
	/// </summary>
	public void ApplyWorldBounds()
	{
		var scene = GetTree().CurrentScene;
		if (scene == null) return;

		// WorldMeta is now GDScript (Cluster 4b). Access via Variant
		// property dispatch — `as Node` cast + .Get("map_size") rather
		// than the C# strong-typed reference.
		var meta = scene.FindChild("WorldMeta", true, false) ?? scene;
		var mapSizeVar = meta?.Get("map_size");
		if (mapSizeVar == null || mapSizeVar.Value.VariantType == Variant.Type.Nil)
		{
			GD.Print($"[FollowCamera] No WorldMeta in {scene.Name} — camera bounds not set");
			return;
		}
		var mapSize = mapSizeVar.Value.AsVector2I();

		LimitLeft = 0;
		LimitTop = 0;
		LimitRight = mapSize.X;
		LimitBottom = mapSize.Y;
		LimitSmoothed = false;
		GD.Print($"[FollowCamera] World bounds set from '{scene.Name}': map=({mapSize.X}, {mapSize.Y})");
	}

	public override void _PhysicsProcess(double delta)
	{
		// Only actively drive position if this camera is NOT already a child of
		// the target. When parented under Player, transform inheritance handles
		// following and this would fight it.
		if (_target != null && _target != GetParent())
		{
			GlobalPosition = _target.GlobalPosition;
		}
	}
}
