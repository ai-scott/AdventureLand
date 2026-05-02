using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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
    private static List<string>    _hairStyleNames;
    private static List<Color[]>   _hairColors;
    private static List<string>    _hairColorNames;
    private static List<Color[]>   _skins;
    private static List<string>    _skinNames;
    private static Color[] _hairColorBase;
    private static Color[] _skinBase;
    private static bool _loaded;

    public static int HairStyleCount { get { EnsureLoaded(); return _hairStyles.Count; } }
    public static int HairColorCount { get { EnsureLoaded(); return _hairColors.Count; } }
    public static int SkinCount      { get { EnsureLoaded(); return _skins.Count; } }

    /// <summary>Display name for the hair style at <paramref name="idx"/>
    /// — derived from the sheet filename via <see cref="StylePrettyNames"/>,
    /// falling back to a title-cased version of the file stem.</summary>
    public static string HairStyleName(int idx)
    {
        EnsureLoaded();
        if (idx < 0 || idx >= _hairStyleNames.Count) return "—";
        return _hairStyleNames[idx];
    }

    /// <summary>Heuristic name for the hair color at <paramref name="idx"/>
    /// based on the dominant ramp color (Blonde / Auburn / Black / etc.).
    /// Approximate — collisions are expected when several ramps land in the
    /// same hue bucket; the swatch row will eventually carry the visual
    /// distinction.</summary>
    public static string HairColorName(int idx)
    {
        EnsureLoaded();
        if (idx < 0 || idx >= _hairColorNames.Count) return "—";
        return _hairColorNames[idx];
    }

    /// <summary>Heuristic skin tone name (Pale / Fair / Tan / …).</summary>
    public static string SkinName(int idx)
    {
        EnsureLoaded();
        if (idx < 0 || idx >= _skinNames.Count) return "—";
        return _skinNames[idx];
    }

    /// <summary>Single representative color for the hair-color ramp at
    /// <paramref name="idx"/> — what the swatch row in InventoryUI shows
    /// for that index. Picks a color ~60% along the ramp (past shadow,
    /// before specular highlight) so the swatch reads as the "true" tone.</summary>
    public static Color DominantHairColor(int idx)
    {
        EnsureLoaded();
        if (idx < 0 || idx >= _hairColors.Count) return Colors.Magenta;
        return DominantColorOfRamp(_hairColors[idx]);
    }

    /// <summary>Single representative color for the skin ramp at
    /// <paramref name="idx"/>. Filters near-white highlights (S &lt; 0.1
    /// reads as a glint, not a skin tone), sorts the remaining ramp
    /// entries by HSV value descending, and picks the 2nd-brightest.
    /// Skin tones sit at S ≈ 0.15–0.5 even for pale Caucasian, so the
    /// saturation filter cleanly skips specular highlights while letting
    /// every actual tone through.</summary>
    public static Color DominantSkinColor(int idx)
    {
        EnsureLoaded();
        if (idx < 0 || idx >= _skins.Count) return Colors.Magenta;
        var ramp = _skins[idx];
        if (ramp.Length == 0) return Colors.Magenta;
        var saturated = ramp.Where(c => c.S >= 0.1f)
                            .OrderByDescending(c => c.V)
                            .ToArray();
        if (saturated.Length >= 2) return saturated[1];
        if (saturated.Length == 1) return saturated[0];
        // Every entry was washed out — fall back to brightest in the raw ramp.
        return ramp.OrderByDescending(c => c.V).First();
    }

    private static Color DominantColorOfRamp(Color[] ramp)
    {
        if (ramp.Length == 0) return Colors.Magenta;
        return ramp[Mathf.Min(ramp.Length - 1, (int)(ramp.Length * 0.6f))];
    }

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
        (_hairStyles, _hairStyleNames) = LoadHairRoster();
        (_hairColors, _hairColorBase, _hairColorNames) = LoadPaletteRoster(HairColorBaseRampPath, HairColorRampSheetPath, isSkin: false);
        (_skins,      _skinBase,      _skinNames)      = LoadPaletteRoster(SkinBaseRampPath,      SkinRampSheetPath,      isSkin: true);
    }

    /// <summary>Curated pretty names for known Mana Seed hair sheets.
    /// Falls back to a title-cased version of the file stem for any
    /// styles added later that aren't in this map.</summary>
    private static readonly Dictionary<string, string> StylePrettyNames = new()
    {
        ["afro"]              = "Afro",
        ["afropuffs"]         = "Afro Puffs",
        ["bob1"]              = "Bob",
        ["bob2"]              = "Long Bob",
        ["bushy"]             = "Bushy",
        ["dapper"]            = "Dapper",
        ["flattop"]           = "Flat Top",
        ["longbound"]         = "Long Bound",
        ["longboundclasped"]  = "Long Tied",
        ["longwavy"]          = "Long Wavy",
        ["mohawk"]            = "Mohawk",
        ["ponytail1"]         = "Ponytail",
        ["spiky1"]            = "Spiky",
        ["spiky2"]            = "Flowhawk",
        ["topknot"]           = "Top Knot",
        ["twintail"]          = "Twin Tails",
        ["twists"]            = "Twists",
    };

    private static (List<Texture2D>, List<string>) LoadHairRoster()
    {
        var list = new List<Texture2D>();
        var names = new List<string>();
        using var dir = DirAccess.Open(HairSheetDir);
        if (dir == null) return (list, names);
        var files = dir.GetFiles();
        Array.Sort(files);
        foreach (var f in files)
        {
            if (!f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
            var tex = GD.Load<Texture2D>(HairSheetDir + f);
            if (tex == null) continue;
            list.Add(tex);
            names.Add(ExtractStyleName(f));
        }
        return (list, names);
    }

    /// <summary>Pull a display name out of a hair sheet filename.
    /// "fbas_13hair_afro_00.png" → "Afro" via StylePrettyNames; falls
    /// back to title-casing the stem ("longbound" → "Longbound") for
    /// styles not in the map.</summary>
    private static string ExtractStyleName(string filename)
    {
        var stem = filename;
        // Strip extension.
        var dot = stem.LastIndexOf('.');
        if (dot >= 0) stem = stem.Substring(0, dot);
        // Strip "fbas_13hair_" prefix.
        const string prefix = "fbas_13hair_";
        if (stem.StartsWith(prefix)) stem = stem.Substring(prefix.Length);
        // Strip trailing variant code: any "_NN" or "_NNx" or "_NN_x"
        // segment (00, 00f, 00_e, etc). The variant follows the style key.
        int variantStart = stem.IndexOf("_0", StringComparison.Ordinal);
        if (variantStart > 0) stem = stem.Substring(0, variantStart);

        if (StylePrettyNames.TryGetValue(stem, out var pretty)) return pretty;
        // Fallback — capitalize first letter so unknown styles still
        // look like names rather than raw filenames.
        if (stem.Length == 0) return "—";
        return char.ToUpperInvariant(stem[0]) + stem.Substring(1);
    }

    private static (List<Color[]>, Color[], List<string>) LoadPaletteRoster(string baseRampPath, string sheetPath, bool isSkin)
    {
        var roster = new List<Color[]>();
        var names = new List<string>();
        Color[] baseRamp = Array.Empty<Color>();

        var baseTex = GD.Load<Texture2D>(baseRampPath);
        if (baseTex == null) { GD.PushWarning($"[Customization] base ramp missing: {baseRampPath}"); return (roster, baseRamp, names); }
        baseRamp = PaletteSwapper.ReadRampFromTexture(baseTex);

        var sheetTex = GD.Load<Texture2D>(sheetPath);
        if (sheetTex == null) { GD.PushWarning($"[Customization] ramp sheet missing: {sheetPath}"); return (roster, baseRamp, names); }
        var img = sheetTex.GetImage();
        if (img == null) return (roster, baseRamp, names);

        // Track per-name occurrences so duplicates get suffixed ("Brown",
        // "Brown 2", "Brown 3") instead of all collapsing to one label.
        var nameCounts = new Dictionary<string, int>();
        Color[] previous = null;
        for (int row = 0; row < img.GetHeight(); row++)
        {
            var raw = PaletteSwapper.ReadRampRow(sheetTex, row, maxColors: 16);
            if (raw.Length == 0) continue;
            var ramp = DedupeInOrder(raw);
            if (previous != null && RampsEqual(previous, ramp)) continue;
            roster.Add(ramp);
            previous = ramp;

            var name = NameForRamp(ramp, isSkin);
            int count = nameCounts.TryGetValue(name, out var c) ? c + 1 : 1;
            nameCounts[name] = count;
            names.Add(count == 1 ? name : $"{name} {count}");
        }
        return (roster, baseRamp, names);
    }

    /// <summary>Heuristic name for a Mana Seed palette ramp. Picks a
    /// representative color from the ramp (skews to the higher end where
    /// the "true" tone lives — early indices are shadows, late indices
    /// are highlights), classifies hue → bucket. Approximate; not
    /// guaranteed unique across the roster (the dedup-suffix in
    /// <see cref="LoadPaletteRoster"/> handles ties).</summary>
    private static string NameForRamp(Color[] ramp, bool isSkin)
    {
        if (ramp.Length == 0) return "—";
        // Roughly 60% along the ramp — past the shadow band, before the
        // brightest specular highlight. Lands on the dominant tone.
        var sample = ramp[Mathf.Min(ramp.Length - 1, (int)(ramp.Length * 0.6f))];
        float h = sample.H, s = sample.S, v = sample.V;

        if (isSkin)
        {
            // Skin tones are all warm — classify purely by lightness.
            if (v > 0.88f) return "Pale";
            if (v > 0.78f) return "Fair";
            if (v > 0.66f) return "Tan";
            if (v > 0.52f) return "Olive";
            if (v > 0.38f) return "Brown";
            return "Dark";
        }

        // Hair: low saturation = grayscale family.
        if (s < 0.18f)
        {
            if (v > 0.85f) return "White";
            if (v > 0.6f)  return "Silver";
            if (v > 0.32f) return "Gray";
            return "Black";
        }

        float hueDeg = h * 360f;
        // Reds wrap around 0; check both ends.
        if (hueDeg >= 350f || hueDeg < 12f)  return v > 0.55f ? "Red" : "Crimson";
        if (hueDeg < 28f)  return v > 0.62f ? "Ginger" : "Auburn";
        if (hueDeg < 48f)  return v > 0.72f ? "Blonde" : "Brown";
        if (hueDeg < 70f)  return v > 0.7f  ? "Blonde" : "Honey";
        if (hueDeg < 165f) return "Green";
        if (hueDeg < 200f) return "Teal";
        if (hueDeg < 255f) return "Blue";
        if (hueDeg < 295f) return "Purple";
        if (hueDeg < 335f) return "Pink";
        return "Red";
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
