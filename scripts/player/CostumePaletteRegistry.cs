using Godot;
using System.Collections.Generic;

namespace AdventureLandPrototype;

/// <summary>
/// Read-only lookup of per-item costume color ramps, baked offline by
/// <c>tools/bake_costume_palettes.py</c> from the C3 variant frame PNGs.
/// CostumeController calls <see cref="Get"/> when equipping a clothing item
/// and applies the resulting (base, variant) ramp pair as a palette-swap
/// ShaderMaterial via <see cref="PaletteSwapper.CreateMaterial"/>.
///
/// File format (assets/data/costume_palettes.json):
/// <code>
/// {
///   "51": { "layer": "14head", "base": "fbas_14head_boaterhat_00d",
///           "variant_suffix": "straw_boat",
///           "base_colors":    ["#181818", ...],
///           "variant_colors": ["#181818", ...] },
///   ...
/// }
/// </code>
/// Numeric keys map to <see cref="ItemData.Id"/>; non-numeric keys are
/// reserved for the post-C3 "variants/" authoring path documented in the
/// bake script.
/// </summary>
public static class CostumePaletteRegistry
{
    private const string JsonPath = "res://assets/data/costume_palettes.json";

    private static Dictionary<int, (Color[] BaseRamp, Color[] VariantRamp)> _byId;
    private static Dictionary<string, (Color[] BaseRamp, Color[] VariantRamp)> _byKey;
    private static bool _loaded;

    public static (Color[] BaseRamp, Color[] VariantRamp)? Get(int itemId)
    {
        EnsureLoaded();
        return _byId.TryGetValue(itemId, out var p) ? p : null;
    }

    public static (Color[] BaseRamp, Color[] VariantRamp)? GetByKey(string key)
    {
        EnsureLoaded();
        return _byKey.TryGetValue(key, out var p) ? p : null;
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _byId = new Dictionary<int, (Color[], Color[])>();
        _byKey = new Dictionary<string, (Color[], Color[])>();

        if (!FileAccess.FileExists(JsonPath))
        {
            GD.PushWarning($"[CostumePalette] {JsonPath} missing — run tools/bake_costume_palettes.py");
            return;
        }
        using var file = FileAccess.Open(JsonPath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushError($"[CostumePalette] Failed to open {JsonPath}");
            return;
        }

        var raw = Json.ParseString(file.GetAsText());
        if (raw.VariantType != Variant.Type.Dictionary)
        {
            GD.PushError("[CostumePalette] JSON root is not a dictionary");
            return;
        }

        var dict = raw.AsGodotDictionary();
        foreach (var key in dict.Keys)
        {
            var keyStr = key.AsString();
            var entry = dict[key].AsGodotDictionary();
            if (!entry.ContainsKey("base_colors") || !entry.ContainsKey("variant_colors")) continue;

            var baseArr = entry["base_colors"].AsGodotArray();
            var variantArr = entry["variant_colors"].AsGodotArray();
            int n = Mathf.Min(baseArr.Count, variantArr.Count);
            if (n == 0) continue;

            var baseRamp = new Color[n];
            var variantRamp = new Color[n];
            for (int i = 0; i < n; i++)
            {
                baseRamp[i] = new Color(baseArr[i].AsString());
                variantRamp[i] = new Color(variantArr[i].AsString());
            }

            _byKey[keyStr] = (baseRamp, variantRamp);
            if (int.TryParse(keyStr, out int id)) _byId[id] = (baseRamp, variantRamp);
        }

        GD.Print($"[CostumePalette] Loaded {_byId.Count} palettes (by item id), " +
                 $"{_byKey.Count - _byId.Count} variant-keyed");
    }
}
