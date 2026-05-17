using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Pixel-art StyleBox with an ink border + asymmetric inset bevel
/// (top/left = highlight, bottom/right = shadow) and the classic
/// "corners don't connect" gap at each of the four corners.
///
/// Drawing layers (back to front):
///   1. Body fill (innermost rect)
///   2. Bevel highlight strips (top + left)
///   3. Bevel shadow strips (bottom + right)
///   4. Border ink — 4 separate strips with CornerGap-sized gaps so
///      the four outermost corner pixels stay transparent
///
/// Set <see cref="BevelWidth"/> = 0 for a flat (no-bevel) bordered shape;
/// set <see cref="CornerGap"/> = 0 for fully connected borders.
/// </summary>
public partial class BevelStyleBox : StyleBox
{
    public Color Fill { get; set; } = Colors.White;
    public Color BevelHi { get; set; } = Colors.White;
    public Color BevelLo { get; set; } = Colors.Black;
    public Color Border { get; set; } = Colors.Black;
    public int BorderWidth { get; set; } = 3;
    public int BevelWidth { get; set; } = 3;
    /// <summary>Inner content padding. Inherited <c>ContentMargin*</c>
    /// properties are set by <c>UiFrames.MakeBevelPanel</c> based on this
    /// so layout containers know the safe drawing area.</summary>
    public int Padding { get; set; } = 16;
    /// <summary>Pixels of transparent gap at each of the four corners —
    /// classic NES/SNES "corners don't connect" pixel-art look. Defaults
    /// to 3 so the gap fully cuts the typical 3px ink border. Smaller
    /// styleboxes (like the 1px-bordered kbd chip) override this.</summary>
    public int CornerGap { get; set; } = 3;

    public override void _Draw(Rid toCanvasItem, Rect2 rect)
    {
        int g = CornerGap;
        int b = BorderWidth;
        int v = BevelWidth;
        var pos = rect.Position;
        var size = rect.Size;

        // Body fill — innermost rect, inset past the corners by
        // (border + bevel) so it never touches the corner-gap zone.
        Draw(toCanvasItem, pos.X + b + v, pos.Y + b + v,
            size.X - 2 * (b + v), size.Y - 2 * (b + v), Fill);

        if (v > 0)
        {
            // Bevel highlight (top + left strips). Inset by CornerGap on
            // their long axis so the strip ends don't reach the outer
            // corners — keeps the corner pixels transparent.
            Draw(toCanvasItem, pos.X + g, pos.Y + b, size.X - 2 * g, v, BevelHi); // top
            Draw(toCanvasItem, pos.X + b, pos.Y + g, v, size.Y - 2 * g, BevelHi); // left
            // Bevel shadow (bottom + right strips). Painted after the
            // highlight, so the top-right and bottom-left intersections
            // resolve to shadow — matches typical pixel-art panels.
            Draw(toCanvasItem, pos.X + g, pos.Y + size.Y - b - v, size.X - 2 * g, v, BevelLo); // bottom
            Draw(toCanvasItem, pos.X + size.X - b - v, pos.Y + g, v, size.Y - 2 * g, BevelLo); // right
        }

        // Border ink — 4 separate strips with CornerGap-sized gaps at
        // each corner, leaving the four corner pixels transparent.
        Draw(toCanvasItem, pos.X + g, pos.Y, size.X - 2 * g, b, Border);                  // top
        Draw(toCanvasItem, pos.X + g, pos.Y + size.Y - b, size.X - 2 * g, b, Border);     // bottom
        Draw(toCanvasItem, pos.X, pos.Y + g, b, size.Y - 2 * g, Border);                  // left
        Draw(toCanvasItem, pos.X + size.X - b, pos.Y + g, b, size.Y - 2 * g, Border);     // right
    }

    private static void Draw(Rid canvas, float x, float y, float w, float h, Color c)
    {
        if (w <= 0 || h <= 0) return;
        RenderingServer.CanvasItemAddRect(canvas, new Rect2(x, y, w, h), c);
    }
}
