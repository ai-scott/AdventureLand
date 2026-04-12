using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Simple row-based spritesheet animator for NPCs.
/// Layout: each row is a direction, each column is a frame.
/// Row 0=Down, 1=Right, 2=Up, 3=Left, 4=Idle (2 frames).
///
/// Adjust FrameWidth / FrameHeight / IdleRow in the inspector if the
/// sprite layout differs. On startup we log the detected texture size
/// and clamp regions that go out of bounds so the sprite never vanishes.
/// </summary>
public partial class NpcAnimator : Node
{
    [Export] public int FrameWidth = 32;
    [Export] public int FrameHeight = 48;
    [Export] public int FramesPerRow = 4;
    [Export] public float FrameDurationMs = 200f;
    [Export] public int RowDown = 0;
    [Export] public int RowRight = 1;
    [Export] public int RowUp = 2;
    [Export] public int RowLeft = 3;
    [Export] public int RowIdle = 4;

    private Sprite2D _sprite;
    private int _currentRow;
    private int _currentFrame = 0;
    private double _timer = 0;
    private int _maxFrames = 2;
    private Vector2I _textureSize;

    public override void _Ready()
    {
        _sprite = GetParent().GetNode<Sprite2D>("Sprite2D");
        _sprite.RegionEnabled = true;

        if (_sprite.Texture != null)
        {
            _textureSize = (Vector2I)_sprite.Texture.GetSize();
            GD.Print($"NPC sprite '{_sprite.Texture.ResourcePath}': {_textureSize.X}x{_textureSize.Y}");
        }
        else
        {
            GD.PrintErr("NPC Sprite2D has no texture assigned!");
        }

        PlayIdle();
    }

    public override void _Process(double delta)
    {
        _timer += delta * 1000;
        if (_timer >= FrameDurationMs)
        {
            _timer -= FrameDurationMs;
            _currentFrame = (_currentFrame + 1) % _maxFrames;
            ApplyFrame();
        }
    }

    public void PlayIdle()
    {
        _currentRow = RowIdle;
        _maxFrames = 2;
        _currentFrame = 0;
        _timer = 0;
        ApplyFrame();
    }

    public void PlayWalk(string direction)
    {
        int row = direction switch
        {
            "down" or "down_right" or "down_left" => RowDown,
            "up" or "up_right" or "up_left" => RowUp,
            "right" => RowRight,
            "left" => RowLeft,
            _ => RowDown
        };

        if (row == _currentRow && _maxFrames == FramesPerRow)
            return;

        _currentRow = row;
        _maxFrames = FramesPerRow;
        _currentFrame = 0;
        _timer = 0;
        ApplyFrame();
    }

    private void ApplyFrame()
    {
        int x = _currentFrame * FrameWidth;
        int y = _currentRow * FrameHeight;

        // If the requested region goes past the texture, fall back to down-walk frame 0.
        // This prevents Penny from becoming invisible if the sheet layout doesn't match.
        if (_textureSize.Y > 0 && y + FrameHeight > _textureSize.Y)
        {
            GD.PushWarning($"NPC region y={y}+{FrameHeight} exceeds texture height {_textureSize.Y}. " +
                           $"Falling back to row 0. Adjust RowIdle/FrameHeight in the inspector.");
            x = 0;
            y = 0;
        }

        _sprite.RegionRect = new Rect2(x, y, FrameWidth, FrameHeight);
    }
}
