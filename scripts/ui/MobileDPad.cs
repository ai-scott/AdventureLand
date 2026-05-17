using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Touchscreen movement input + visual.
///
/// Two parts:
///
///   1. <b>Input</b> — a touch zone covering the bottom-left of the viewport.
///      First touch latches an origin point; subsequent drag computes a
///      vector that is converted into <c>move_up/down/left/right</c> action
///      strengths via <see cref="Input.ParseInputEvent"/>. <see cref="PlayerController"/>'s
///      <c>Input.GetVector(...)</c> reads those without modification.
///
///   2. <b>Visual</b> — a fixed 8-arrow dpad built from <c>ui_hintarrow-*.png</c>
///      pixel sprites: 4 cardinals + 4 diagonals. Each arrow is a TextureRect
///      child of an arrows ring; cardinals use the matching directional sprite
///      and diagonals reuse the up sprite rotated ±45°. Each arrow shows in its
///      own ROYGBIV color while the joystick has a positive component along its
///      direction; idle arrows draw at <see cref="ArrowIdleAlpha"/>. Multiple
///      arrows can light at once for diagonals.
///
/// Mounted onto the <see cref="HUD"/> autoload at runtime when
/// <see cref="UiStyles.IsMobile"/> is true. Hidden alongside the rest of
/// the world HUD on title / game-over / dialogue screens.
/// </summary>
public partial class MobileDPad : Control
{
    /// <summary>Touch-zone size as a fraction of the viewport. Bottom-left
    /// corner; tunable in the Inspector for testing different phone sizes.</summary>
    [Export] public float TouchZoneFractionWidth = 0.35f;
    [Export] public float TouchZoneFractionHeight = 0.55f;

    /// <summary>Drag distance (screen pixels, post-canvas-stretch) past
    /// which the joystick reads "fully deflected." Below this threshold the
    /// strength scales linearly so a small drag yields a small move speed.</summary>
    [Export] public float JoystickFullDeflectionPx = 56f;

    /// <summary>Drag distance below which the joystick reads as "neutral"
    /// (no movement, no arrows lit). Prevents jitter on a stationary thumb.</summary>
    [Export] public float JoystickDeadzonePx = 8f;

    /// <summary>Arrow ring radius in viewport pixels. The ring sits above
    /// the bottom-left corner with a margin equal to the radius + Padding.</summary>
    [Export] public float ArrowRingRadius = 56f;

    /// <summary>Pixel-art arrow sprite scale factor. Btn_Arrow is 32×32
    /// native, so 1.5 → 48×48 on screen — chunky enough to read at a glance
    /// without dominating the bottom-left corner.</summary>
    [Export] public float ArrowScale = 1.5f;

    /// <summary>Native orientation of the Btn_Arrow sprite in degrees.
    /// Default 180 because the C3 source arrow's tip points LEFT (-X) — for
    /// the radial outward fan we rotate by atan2(dy, dx) + this offset.
    /// Bump this if the asset gets re-exported with a different native
    /// orientation:
    ///   tip natively right → 0
    ///   tip natively up    → 90
    ///   tip natively down  → -90 (or 270)
    ///   tip natively left  → 180 (default)</summary>
    [Export] public float BaseRotationDegrees = 180f;

    /// <summary>Center thumbpad disc radius, measured in chunky "pixel"
    /// blocks. The dot is rendered as a grid of 3×3 logical-px squares so
    /// it reads as 16-bit pixel art rather than a smooth disc. Default 5
    /// → ~10-block diameter → ~30 logical px wide.</summary>
    [Export] public int CenterDotRadiusBlocks = 5;

    /// <summary>Size of one rendered "pixel" inside the chunky thumbpad,
    /// in logical viewport units. 3 means the disc is drawn at 1/3 the
    /// resolution of the canvas — matching the user's "16-bit, choppy
    /// pixels" aesthetic.</summary>
    [Export] public int CenterDotPixelSize = 3;

    /// <summary>Padding from the bottom-left corner to the dpad ring center.</summary>
    [Export] public float DpadCornerPadding = 28f;

    /// <summary>Idle alpha for inactive arrows. Color stays paper-cream so
    /// the dpad reads as a "ready" UI element without competing with gameplay.</summary>
    [Export] public float ArrowIdleAlpha = 0.32f;

    /// <summary>Color applied to an arrow when its direction is fully active.
    /// 8 entries, ordered Up, UpRight, Right, DownRight, Down, DownLeft,
    /// Left, UpLeft (clockwise from north). Default: ROYGBIV-style spread.</summary>
    [Export] public Color[] ArrowColors = new[]
    {
        new Color("ff3b30"), // Up        — Red
        new Color("ff9500"), // UpRight   — Orange
        new Color("ffcc00"), // Right     — Yellow
        new Color("34c759"), // DownRight — Green
        new Color("5ac8fa"), // Down      — Cyan
        new Color("007aff"), // DownLeft  — Blue
        new Color("5856d6"), // Left      — Indigo
        new Color("af52de"), // UpLeft    — Violet
    };

    private static readonly Vector2[] DirectionUnit =
    {
        new( 0, -1),                // Up
        new( 0.7071f, -0.7071f),    // UpRight
        new( 1,  0),                // Right
        new( 0.7071f,  0.7071f),    // DownRight
        new( 0,  1),                // Down
        new(-0.7071f,  0.7071f),    // DownLeft
        new(-1,  0),                // Left
        new(-0.7071f, -0.7071f),    // UpLeft
    };

    /// <summary>Per-direction rotation in radians. One Btn_Arrow texture
    /// is rotated to face outward along the ring, so all eight arrows share
    /// one sprite (no separate up/down/left/right files).</summary>
    private float[] _arrowRotations;
    private TextureRect[] _arrowRects;

    private int _activeTouchIndex = -1;
    private Vector2 _origin;
    private Vector2 _drag;
    private Vector2 _normalizedJoystick;
    private bool _moveUpHeld, _moveDownHeld, _moveLeftHeld, _moveRightHeld;

    public override void _Ready()
    {
        // Cover the full viewport so we can hit-test touches anywhere; only
        // the bottom-left rect actually claims them. MouseFilter=Ignore so the
        // rest of the screen still passes events to other UI (dialogue panel,
        // HUD chips). Touches in the bottom-left zone are filtered in
        // _UnhandledInput and converted to action strengths there.
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Always;

        BuildArrowSprites();
    }

    private void BuildArrowSprites()
    {
        // Single sprite, rotated 8 ways. Btn_Arrow's chunky pixel detail
        // reads better than the tiny ui_hintarrow set, especially on a
        // mobile screen where the dpad sits in the corner.
        var arrow = GD.Load<Texture2D>("res://assets/sprites/ui/btn_arrow.png");
        if (arrow == null)
        {
            GD.PushWarning("[MobileDPad] btn_arrow.png missing — dpad arrows will not render.");
            return;
        }

        // Rotate each arrow so its tip points RADIALLY OUTWARD (perpendicular
        // to the ring tangent at that position). Per direction this is just
        // atan2(dir.y, dir.x), which yields 0 = right, π/2 = down, π = left,
        // -π/2 = up — matching Godot's clockwise-positive 2D rotation.
        // BaseRotationDegrees compensates for sprites whose native tip is
        // not at angle 0 (right). A previous attempt added per-position
        // offsets relative to "up" which inadvertently rotated the arrows
        // tangentially when the source sprite was right-pointing.
        float baseRot = Mathf.DegToRad(BaseRotationDegrees);
        _arrowRotations = new float[8];
        for (int i = 0; i < 8; i++)
        {
            var dir = DirectionUnit[i];
            _arrowRotations[i] = Mathf.Atan2(dir.Y, dir.X) + baseRot;
        }

        var size = arrow.GetSize() * ArrowScale;
        _arrowRects = new TextureRect[8];
        for (int i = 0; i < 8; i++)
        {
            var rect = new TextureRect
            {
                Name = $"Arrow_{i}",
                Texture = arrow,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                MouseFilter = MouseFilterEnum.Ignore,
                CustomMinimumSize = size,
                Size = size,
                PivotOffset = size * 0.5f, // rotate around visual center
                Rotation = _arrowRotations[i],
                Modulate = new Color(1f, 1f, 1f, ArrowIdleAlpha),
            };
            AddChild(rect);
            _arrowRects[i] = rect;
        }

        LayoutArrows();
    }

    /// <summary>Position each arrow on the ring around the bottom-left corner.
    /// Re-runs each frame so a viewport resize (mobile orientation flip,
    /// browser window resize) keeps the dpad anchored. Also queues a redraw
    /// so the center pixel-dot follows the ring.</summary>
    private void LayoutArrows()
    {
        if (_arrowRects == null) return;
        var view = GetViewportRect().Size;
        var center = new Vector2(DpadCornerPadding + ArrowRingRadius, view.Y - DpadCornerPadding - ArrowRingRadius);
        for (int i = 0; i < 8; i++)
        {
            var rect = _arrowRects[i];
            if (rect == null) continue;
            var ringPos = center + DirectionUnit[i] * ArrowRingRadius;
            // TextureRect.Position is its top-left; offset by half-size so
            // the visual center sits on the ring point.
            rect.Position = ringPos - rect.Size * 0.5f;
        }
        QueueRedraw();
    }

    /// <summary>Chunky pixel-art "thumbpad" disc at the ring center.
    /// Behaves like a real virtual joystick: when a touch is held the disc
    /// moves with the drag (clamped to the ring radius) so the player gets
    /// proportional feedback. When idle it sits at the ring center as a
    /// "tap and drag from here" affordance. Rendered as a grid of 3×3
    /// (CenterDotPixelSize) logical-px squares so it reads as 16-bit pixel
    /// art rather than a smooth disc.</summary>
    public override void _Draw()
    {
        var view = GetViewportRect().Size;
        var ringCenter = new Vector2(DpadCornerPadding + ArrowRingRadius, view.Y - DpadCornerPadding - ArrowRingRadius);

        bool active = _activeTouchIndex != -1;
        // Drag offset clamped so the disc never escapes the arrow ring.
        // Use ArrowRingRadius as the cap — same radius the arrows live on,
        // so a fully deflected joystick visually "touches" the active arrow.
        Vector2 offset = Vector2.Zero;
        if (active && _drag.LengthSquared() > 0)
        {
            float maxOffset = ArrowRingRadius - CenterDotRadiusBlocks * CenterDotPixelSize;
            float len = _drag.Length();
            offset = _drag * (Mathf.Min(len, maxOffset) / len);
        }
        Vector2 dotCenter = ringCenter + offset;

        Color fill = active
            ? new Color(1f, 1f, 1f, 0.95f)
            : new Color(1f, 1f, 1f, ArrowIdleAlpha + 0.20f);
        Color outline = new(0f, 0f, 0f, 0.65f);

        DrawPixelDisc(dotCenter, CenterDotRadiusBlocks, CenterDotPixelSize, fill, outline);
    }

    /// <summary>Paint a chunky pixel-art disc at <paramref name="center"/>.
    /// Each rendered "pixel" is <paramref name="pixelSize"/> logical px on
    /// a side; the disc spans roughly <paramref name="radiusBlocks"/> blocks
    /// in either direction. The outermost ring of in-disc cells is painted
    /// with <paramref name="outlineColor"/> for a 16-bit silhouette;
    /// everything inside uses <paramref name="fillColor"/>.</summary>
    private void DrawPixelDisc(Vector2 center, int radiusBlocks, int pixelSize, Color fillColor, Color outlineColor)
    {
        // Snap the center onto the pixel-block grid so the disc doesn't
        // shimmer / sub-pixel-shift as the joystick drags around. Without
        // this the chunky cells appear to "swim" by one pixel during drag.
        float snappedX = Mathf.Round(center.X / pixelSize) * pixelSize;
        float snappedY = Mathf.Round(center.Y / pixelSize) * pixelSize;

        int rSq = radiusBlocks * radiusBlocks;
        int outerRSq = (radiusBlocks - 1) * (radiusBlocks - 1);
        for (int x = -radiusBlocks; x <= radiusBlocks; x++)
        {
            for (int y = -radiusBlocks; y <= radiusBlocks; y++)
            {
                int distSq = x * x + y * y;
                if (distSq > rSq) continue;
                Color c = distSq > outerRSq ? outlineColor : fillColor;
                var rect = new Rect2(
                    snappedX + x * pixelSize - pixelSize * 0.5f,
                    snappedY + y * pixelSize - pixelSize * 0.5f,
                    pixelSize, pixelSize);
                DrawRect(rect, c, filled: true);
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (GetTree().Paused) return; // dialogue / inventory open — no movement
        if (!Visible) return;

        switch (@event)
        {
            case InputEventScreenTouch touch:
                HandleTouch(touch);
                break;
            case InputEventScreenDrag drag:
                HandleDrag(drag);
                break;
            // emulate_touch_from_mouse is on, so editor mouse clicks arrive
            // as InputEventScreenTouch / InputEventScreenDrag — no need for
            // a separate mouse path.
        }
    }

    public override void _Process(double delta)
    {
        // Re-anchor the ring each frame in case the viewport resized
        // (browser window, phone rotation). Cheap — 8 Vector2 assignments.
        LayoutArrows();
    }

    private Rect2 GetTouchZoneRect()
    {
        var view = GetViewportRect().Size;
        var size = new Vector2(view.X * TouchZoneFractionWidth, view.Y * TouchZoneFractionHeight);
        return new Rect2(new Vector2(0, view.Y - size.Y), size);
    }

    private void HandleTouch(InputEventScreenTouch touch)
    {
        if (touch.Pressed)
        {
            if (_activeTouchIndex != -1) return; // multi-touch: keep the first
            if (!GetTouchZoneRect().HasPoint(touch.Position)) return;
            _activeTouchIndex = touch.Index;
            _origin = touch.Position;
            _drag = Vector2.Zero;
            _normalizedJoystick = Vector2.Zero;
            ApplyMovement(Vector2.Zero);
            UpdateArrowTints();
            GetViewport().SetInputAsHandled();
        }
        else if (touch.Index == _activeTouchIndex)
        {
            _activeTouchIndex = -1;
            _drag = Vector2.Zero;
            _normalizedJoystick = Vector2.Zero;
            ApplyMovement(Vector2.Zero);
            UpdateArrowTints();
            GetViewport().SetInputAsHandled();
        }
    }

    private void HandleDrag(InputEventScreenDrag drag)
    {
        if (drag.Index != _activeTouchIndex) return;
        _drag = drag.Position - _origin;
        float len = _drag.Length();
        if (len < JoystickDeadzonePx)
        {
            _normalizedJoystick = Vector2.Zero;
        }
        else
        {
            float strength = Mathf.Min(len / JoystickFullDeflectionPx, 1f);
            _normalizedJoystick = _drag.Normalized() * strength;
        }
        ApplyMovement(_normalizedJoystick);
        UpdateArrowTints();
    }

    /// <summary>Translate the joystick vector into Godot input action
    /// strengths. Pressed/released edges are emitted only on transition so
    /// IsActionJustPressed listeners don't fire every frame the stick is
    /// held. The strength field on the pressed event drives the analog
    /// reading that <c>Input.GetVector</c> produces.</summary>
    private void ApplyMovement(Vector2 joystick)
    {
        // joystick.Y is screen-down-positive; movement_up wants a negative Y
        // component, so flipping is implicit in the per-axis split below.
        float up    = Mathf.Max(0f, -joystick.Y);
        float down  = Mathf.Max(0f,  joystick.Y);
        float left  = Mathf.Max(0f, -joystick.X);
        float right = Mathf.Max(0f,  joystick.X);

        UpdateActionAxis("move_up",    up,    ref _moveUpHeld);
        UpdateActionAxis("move_down",  down,  ref _moveDownHeld);
        UpdateActionAxis("move_left",  left,  ref _moveLeftHeld);
        UpdateActionAxis("move_right", right, ref _moveRightHeld);
    }

    private static void UpdateActionAxis(string action, float strength, ref bool held)
    {
        // Threshold mirrors the keyboard input map's deadzone (0.5) so the
        // analog/digital distinction stays consistent across input devices.
        const float Threshold = 0.5f;
        bool shouldHold = strength >= Threshold;

        if (shouldHold != held)
        {
            // Edge: emit the press/release transition that listeners
            // expect (IsActionJustPressed / IsActionJustReleased).
            var transition = new InputEventAction
            {
                Action = action,
                Pressed = shouldHold,
                Strength = shouldHold ? strength : 0f,
            };
            Input.ParseInputEvent(transition);
            held = shouldHold;
        }
        else if (shouldHold)
        {
            // Continuous update: keep the analog strength fresh so
            // GetVector reads the latest joystick magnitude.
            var keep = new InputEventAction
            {
                Action = action,
                Pressed = true,
                Strength = strength,
            };
            Input.ParseInputEvent(keep);
        }
    }

    /// <summary>Drive each arrow's <c>Modulate</c> color from the current
    /// joystick vector. Smoothstep so adjacent cardinals share a glow on
    /// diagonal drags (e.g., dragging up-right lights Up + Right + UpRight).</summary>
    private void UpdateArrowTints()
    {
        if (_arrowRects == null) return;
        for (int i = 0; i < 8; i++)
        {
            var rect = _arrowRects[i];
            if (rect == null) continue;
            float strength = Mathf.Max(0f, _normalizedJoystick.Dot(DirectionUnit[i]));
            float t = Mathf.SmoothStep(0f, 0.6f, strength);
            // Idle: white at ArrowIdleAlpha. Active: full ROYGBIV color.
            Color idle = new(1f, 1f, 1f, ArrowIdleAlpha);
            Color active = ArrowColors[i];
            rect.Modulate = idle.Lerp(active, t);
        }
    }
}
