using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Applies Seliel's palette-swap shader (res://addons/msca/shader/simple_ramp_shader.gdshader)
/// to Mana Seed sprite layers. Replaces up to 8 exact colors from a base ramp
/// with a new ramp — used for hair/skin/outfit recoloring.
///
/// Base ramps live in _supporting files/palettes/base ramps/ in Seliel's download.
/// Replacement ramps live in _supporting files/palettes/.
/// Each ramp PNG is a small strip of 3–4 colors; you can hard-code the values
/// by inspection or load the PNG and read pixels via MSCAPaletteSwaps (GDScript).
///
/// C# equivalent of MSCAPaletteSwaps.create_shader_material — we don't call
/// the GDScript helper from C# so we don't pay the cross-language bridge cost
/// per swap.
/// </summary>
public static class PaletteSwapper
{
    private const string ShaderPath = "res://addons/msca/shader/simple_ramp_shader.gdshader";
    private const int MaxColors = 8;

    private static Shader _shader;

    /// <summary>
    /// Build a ShaderMaterial mapping each original ramp color to its replacement.
    /// Arrays can be shorter than 8; indices beyond the length are left unmapped (act as identity).
    /// </summary>
    public static ShaderMaterial CreateMaterial(Color[] originalRamp, Color[] newRamp)
    {
        _shader ??= GD.Load<Shader>(ShaderPath);
        if (_shader == null)
        {
            GD.PrintErr($"[PaletteSwapper] Shader not found at {ShaderPath}");
            return null;
        }

        var mat = new ShaderMaterial { Shader = _shader };
        int count = Mathf.Min(Mathf.Min(originalRamp.Length, newRamp.Length), MaxColors);

        for (int i = 0; i < count; i++)
        {
            mat.SetShaderParameter($"original_{i}", originalRamp[i]);
            mat.SetShaderParameter($"replace_{i}", newRamp[i]);
        }

        // Unused slots: set original == replace so the shader's exact-match test is a no-op.
        for (int i = count; i < MaxColors; i++)
        {
            var passthrough = new Color(0, 0, 0, 0);
            mat.SetShaderParameter($"original_{i}", passthrough);
            mat.SetShaderParameter($"replace_{i}", passthrough);
        }

        return mat;
    }

    /// <summary>Apply a palette swap to a named layer under the SpriteLayers node.</summary>
    public static void ApplyToLayer(Node spriteLayersParent, string layerName,
                                    Color[] originalRamp, Color[] newRamp)
    {
        var layer = spriteLayersParent.GetNodeOrNull<Sprite2D>(layerName);
        if (layer == null)
        {
            GD.PrintErr($"[PaletteSwapper] Layer '{layerName}' not found");
            return;
        }
        layer.Material = CreateMaterial(originalRamp, newRamp);
    }

    /// <summary>Remove any palette swap, returning the layer to its original colors.</summary>
    public static void Clear(Node spriteLayersParent, string layerName)
    {
        var layer = spriteLayersParent.GetNodeOrNull<Sprite2D>(layerName);
        if (layer != null) layer.Material = null;
    }

    /// <summary>
    /// Read a color ramp from a small image (e.g. the ramp PNGs Seliel provides).
    /// Walks left-to-right, top-to-bottom, returning the first `maxColors` unique
    /// non-transparent pixels in order.
    /// </summary>
    public static Color[] ReadRampFromTexture(Texture2D texture, int maxColors = MaxColors)
    {
        if (texture == null) return System.Array.Empty<Color>();
        var image = texture.GetImage();
        if (image == null) return System.Array.Empty<Color>();

        var colors = new System.Collections.Generic.List<Color>(maxColors);
        for (int y = 0; y < image.GetHeight() && colors.Count < maxColors; y++)
        {
            for (int x = 0; x < image.GetWidth() && colors.Count < maxColors; x++)
            {
                var c = image.GetPixel(x, y);
                if (c.A > 0 && !colors.Contains(c)) colors.Add(c);
            }
        }
        return colors.ToArray();
    }
}
