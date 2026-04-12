using Godot;

namespace AdventureLandPrototype;

public partial class FollowCamera : Camera2D
{
    [Export] public NodePath TargetPath;
    [Export] public float SmoothSpeed = 5.0f;

    private Node2D _target;

    public override void _Ready()
    {
        if (TargetPath != null)
        {
            _target = GetNode<Node2D>(TargetPath);
        }

        // Pixel-perfect settings
        PositionSmoothingEnabled = true;
        PositionSmoothingSpeed = SmoothSpeed;

        // Set zoom for pixel art (2x zoom to make 16px tiles visible)
        Zoom = new Vector2(2.0f, 2.0f);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_target != null)
        {
            GlobalPosition = _target.GlobalPosition;
        }
    }
}
