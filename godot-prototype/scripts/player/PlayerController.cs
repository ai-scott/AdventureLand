using Godot;

namespace AdventureLandPrototype;

public partial class PlayerController : CharacterBody2D
{
    [Export] public float Speed = 80.0f;

    private ManaSeedAnimator _animator;
    private string _currentDirection = "down";
    private bool _isMoving = false;

    public bool InputLocked { get; set; } = false;

    public override void _Ready()
    {
        _animator = GetNode<ManaSeedAnimator>("ManaSeedAnimator");
        _animator.Play("idle_down");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (InputLocked)
        {
            Velocity = Vector2.Zero;
            if (_isMoving)
            {
                _isMoving = false;
                _animator.Play($"idle_{_currentDirection}");
            }
            MoveAndSlide();
            return;
        }

        var input = Input.GetVector("move_left", "move_right", "move_up", "move_down");

        if (input != Vector2.Zero)
        {
            Velocity = input.Normalized() * Speed;
            var newDirection = GetDirection(input);

            if (!_isMoving || newDirection != _currentDirection)
            {
                _currentDirection = newDirection;
                _animator.Play($"walk_{_currentDirection}");
            }
            _isMoving = true;
        }
        else
        {
            Velocity = Vector2.Zero;
            if (_isMoving)
            {
                _isMoving = false;
                _animator.Play($"idle_{_currentDirection}");
            }
        }

        MoveAndSlide();
    }

    private static string GetDirection(Vector2 input)
    {
        float angle = input.Angle();

        // 8 directions split into 45-degree sectors (PI/4 = 0.7854)
        // Angle 0 = right, PI/2 = down, PI = left, -PI/2 = up
        const float S = 0.3927f; // Half sector = PI/8

        if (angle >= -S && angle < S) return "right";
        if (angle >= S && angle < 3 * S) return "down_right";
        if (angle >= 3 * S && angle < 5 * S) return "down";
        if (angle >= 5 * S || angle < -5 * S) return "left";
        if (angle >= -5 * S && angle < -3 * S) return "up_left";
        if (angle >= -3 * S && angle < -S) return "up";

        // Remaining: up_right and down_left
        if (input.X > 0 && input.Y < 0) return "up_right";
        return "down_left";
    }
}
