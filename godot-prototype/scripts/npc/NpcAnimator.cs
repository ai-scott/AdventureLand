using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Simple row-based spritesheet animator for NPCs.
/// Layout: each row is a direction, each column is a frame.
/// Row 0=Down, 1=Right, 2=Up, 3=Left, 4=Idle (2 frames).
/// Adjust FrameWidth/FrameHeight if the sprite looks cropped or offset.
/// </summary>
public partial class NpcAnimator : Node
{
    [Export] public int FrameWidth = 32;
    [Export] public int FrameHeight = 48;
    [Export] public int FramesPerRow = 4;
    [Export] public float FrameDurationMs = 200f;

    // Row indices in the spritesheet
    private const int RowDown = 0;
    private const int RowRight = 1;
    private const int RowUp = 2;
    private const int RowLeft = 3;
    private const int RowIdle = 4;

    private Sprite2D _sprite;
    private int _currentRow = RowIdle;
    private int _currentFrame = 0;
    private double _timer = 0;
    private int _maxFrames = 2; // idle has 2 frames

    public override void _Ready()
    {
        _sprite = GetParent().GetNode<Sprite2D>("Sprite2D");
        _sprite.RegionEnabled = true;
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
            "down" => RowDown,
            "right" => RowRight,
            "up" => RowUp,
            "left" => RowLeft,
            "down_right" => RowRight,
            "down_left" => RowLeft,
            "up_right" => RowRight,
            "up_left" => RowLeft,
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
        _sprite.RegionRect = new Rect2(x, y, FrameWidth, FrameHeight);
    }
}
