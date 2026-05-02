using Godot;
using System;
using System.Collections.Generic;

namespace AdventureLandPrototype;

/// <summary>
/// Player appearance roster + apply path. Owns the lists of hair-style
/// textures, hair-color ramps, and skin-color ramps; applies a chosen
/// (style, color, skin) tuple to the player's SpriteLayers via
/// <see cref="PaletteSwapper"/>.
///
/// Lives outside InventoryUI so it survives the inventory script being
/// rebuilt — the rosters are needed at world-load time (CostumeController
/// applies the saved indices) and at NewGame time (random pick), neither of
/// which depend on the inventory screen being open.
///
/// Mana Seed packed-ramp dedup mirrors what the bake script does for
/// clothing palettes — Seliel packs each color as 2 adjacent columns + 2
/// rows, so without scan-order dedup we'd burn the 8-color shader budget
/// on duplicates and drop the trailing black outline.
/// </summary>
public static class CharacterCustomization
{
    private const string HairSheetDir = "res://assets/sprites/player/farmer/sheets/13hair/";
    private const string SkinBaseRampPath = "res://assets/sprites/player/farmer/palettes/base ramps/skin color base ramp.png";
    private const string SkinRampSheetPath = "res://assets/sprites/player/farmer/palettes/mana seed skin ramps.png";
    private const string HairColorBaseRampPath = "res://assets/sprites/player/farmer/palettes/base ramps/hair color base ramp.png";
    private const string HairColorRampSheetPath = "res://assets/sprites/player/farmer/palettes/mana seed hair ramps.png";

    private static List<Texture2D> _hairStyles;
    private static List<Color[]> _hairColors;
    private static List<Color[]> _skins;
    private static Color[] _hairColorBase;
    private static Color[] _skinBase;
    private static bool _loaded;

    public static int HairStyleCount { get { EnsureLoaded(); return _hairStyles.Count; } }
    public static int HairColorCount { get { EnsureLoaded(); return _hairColors.Count; } }
    public static int SkinCount      { get { EnsureLoaded(); return _skins.Count; } }

    /// <summary>Apply a (style, color, skin) tuple to the player's
    /// SpriteLayers. Indices &lt; 0 leave the corresponding layer alone.</summary>
    public static void Apply(Node spriteLayers, int hairStyleIdx, int hairColorIdx, int skinIdx)
    {
        if (spriteLayers == null) return;
        EnsureLoaded();

        if (hairStyleIdx >= 0 && hairStyleIdx < _hairStyles.Count)
        {
            var hair = spriteLayers.GetNodeOrNull<Sprite2D>("13hair");
            if (hair != null)
            {
                hair.Texture = _hairStyles[hairStyleIdx];
                hair.Visible = true;
            }
        }
        if (hairColorIdx >= 0 && hairColorIdx < _hairColors.Count && _hairColorBase.Length > 0)
        {
            PaletteSwapper.ApplyToLayer(spriteLayers, "13hair", _hairColorBase, _hairColors[hairColorIdx]);
        }
        if (skinIdx >= 0 && skinIdx < _skins.Count && _skinBase.Length > 0)
        {
            PaletteSwapper.ApplyToLayer(spriteLayers, "01body", _skinBase, _skins[skinIdx]);
        }
    }

    /// <summary>Pick random indices into each roster and write them onto
    /// the supplied <see cref="SaveData"/>. Used by NewGame so each fresh
    /// character has a distinct look.</summary>
    public static void Randomize(SaveData data)
    {
        if (data == null) return;
        EnsureLoaded();
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        if (_hairStyles.Count > 0) data.HairStyleIndex = rng.RandiRange(0, _hairStyles.Count - 1);
        if (_hairColors.Count > 0) data.HairColorIndex = rng.RandiRange(0, _hairColors.Count - 1);
        if (_skins.Count > 0)      data.SkinIndex      = rng.RandiRange(0, _skins.Count - 1);
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _hairStyles = LoadHairRoster();
        (_hairColors, _hairColorBase) = LoadPaletteRoster(HairColorBaseRampPath, HairColorRampSheetPath);
        (_skins, _skinBase)           = LoadPaletteRoster(SkinBaseRampPath, SkinRampSheetPath);
    }

    private static List<Texture2D> LoadHairRoster()
    {
        var list = new List<Texture2D>();
        using var dir = DirAccess.Open(HairSheetDir);
        if (dir == null) return list;
        var files = dir.GetFiles();
        Array.Sort(files);
        foreach (var f in files)
        {
            if (!f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
            var tex = GD.Load<Texture2D>(HairSheetDir + f);
            if (tex != null) list.Add(tex);
        }
        return list;
    }

    private static (List<Color[]>, Color[]) LoadPaletteRoster(string baseRampPath, string sheetPath)
    {
        var roster = new List<Color[]>();
        Color[] baseRamp = Array.Empty<Color>();

        var baseTex = GD.Load<Texture2D>(baseRampPath);
        if (baseTex == null) { GD.PushWarning($"[Customization] base ramp missing: {baseRampPath}"); return (roster, baseRamp); }
        baseRamp = PaletteSwapper.ReadRampFromTexture(baseTex);

        var sheetTex = GD.Load<Texture2D>(sheetPath);
        if (sheetTex == null) { GD.PushWarning($"[Customization] ramp sheet missing: {sheetPath}"); return (roster, baseRamp); }
        var img = sheetTex.GetImage();
        if (img == null) return (roster, baseRamp);

        Color[] previous = null;
        for (int row = 0; row < img.GetHeight(); row++)
        {
            var raw = PaletteSwapper.ReadRampRow(sheetTex, row, maxColors: 16);
            if (raw.Length == 0) continue;
            var ramp = DedupeInOrder(raw);
            if (previous != null && RampsEqual(previous, ramp)) continue;
            roster.Add(ramp);
            previous = ramp;
        }
        return (roster, baseRamp);
    }

    private static Color[] DedupeInOrder(Color[] input)
    {
        var seen = new List<Color>(input.Length);
        foreach (var c in input) if (!seen.Contains(c)) seen.Add(c);
        return seen.ToArray();
    }

    private static bool RampsEqual(Color[] a, Color[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
}
