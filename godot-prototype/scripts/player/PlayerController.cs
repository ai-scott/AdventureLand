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
		// Map 8-direction input to 4 available animations.
		// Horizontal dominates for left/right; vertical dominates for up/down.
		if (Mathf.Abs(input.X) >= Mathf.Abs(input.Y))
			return input.X > 0 ? "right" : "left";
		return input.Y > 0 ? "down" : "up";
	}
}
