using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Creates physics collision bodies for all buildings at runtime.
/// Reads from the same position/size data as tmx_to_godot.py BUILDINGS list.
///
/// Collision layer 2 matches the NPC StaticBody2D — player collision_mask
/// must be 3 (layers 1+2) to block on both NPCs and buildings.
///
/// Each building gets a thin rectangle at its base (the "wall bottom" in
/// top-down view). FootprintH can be overridden per building for structures
/// like the Well that need fuller coverage.
/// </summary>
public partial class BuildingCollider : Node2D
{
    // (label, spriteX, spriteY, spriteW, spriteH, footprintH)
    // Position = top-left of Sprite2D (centered = false in scene).
    // FootprintH = height of collision box; default 14 covers the wall base.
    private static readonly (string Label, float X, float Y, float W, float H, float FootH)[] Buildings =
    {
        ("Blacksmith",  213.077f, 235.893f, 120f, 112f, 14f),
        ("Cabin1",       35.983f, 274.895f,  48f,  80f, 14f),
        ("Cabin2",      122.176f,  65.690f,  48f,  80f, 14f),
        ("Shop",        468.769f, 110.484f, 120f, 112f, 14f),
        ("WeaponShop",  343.953f,  74.242f, 120f, 112f, 14f),
        ("Windmill",    228.961f,  77.124f, 102f, 112f, 14f),
        ("Well",        369.743f, 301.073f,  44f,  52f, 36f),  // block most of the well
        ("TreeSign",    459.530f, 251.027f,  52f,  45f, 14f),
    };

    public override void _Ready()
    {
        foreach (var (label, x, y, w, h, footH) in Buildings)
        {
            var body = new StaticBody2D();
            body.Name = $"{label}Wall";
            body.CollisionLayer = 2;
            body.CollisionMask = 0;
            // Center the body at the middle of the footprint strip
            body.Position = new Vector2(x + w / 2f, y + h - footH / 2f);

            var rect = new RectangleShape2D();
            rect.Size = new Vector2(w - 4f, footH);

            var shape = new CollisionShape2D();
            shape.Shape = rect;

            body.AddChild(shape);
            AddChild(body);
        }

        GD.Print($"[BuildingCollider] {Buildings.Length} building walls active");
    }
}
