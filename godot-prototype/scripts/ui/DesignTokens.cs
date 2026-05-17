using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Adventure Land design-system tokens — palette + typography sizes mirrored
/// from <c>handoff/tokens.json</c>. Single source of truth so call sites pull
/// from semantic names (DesignTokens.Gold) rather than hand-typed hex.
///
/// Foundation only — Phase B of the design-system rollout. Per-screen
/// adoption (mossy frames, gold-border-swap buttons, etc.) lands later as
/// each screen is individually confirmed.
///
/// When a token changes in the handoff, update it here too. Keep colors
/// authored as float triplets — Godot's Color ctor takes [0..1], so the
/// conversion happens once here.
/// </summary>
public static class DesignTokens
{
    // === Surface ===
    public static readonly Color MossyField   = Hex("#3F5A47");
    public static readonly Color MossyFieldHi = Hex("#5A7A60");
    public static readonly Color MossyFieldLo = Hex("#1F3026");
    public static readonly Color GrassField   = Hex("#CDB246");
    public static readonly Color GrassFieldHi = Hex("#E3CB6E");
    public static readonly Color GrassFieldLo = Hex("#8A7628");
    public static readonly Color DeepWood     = Hex("#2A3A2A");
    public static readonly Color DeepWoodHi   = Hex("#445A45");
    public static readonly Color DeepWoodLo   = Hex("#101A11");
    public static readonly Color Stone        = Hex("#94A09F");
    public static readonly Color StoneHi      = Hex("#BFC9C7");
    public static readonly Color StoneLo      = Hex("#5A6463");

    // Translucent variant of the mossy field, used over painted backgrounds.
    public static readonly Color MossyFieldTranslucent = new(0.122f, 0.188f, 0.149f, 0.82f); // rgba(31,48,38,0.82)

    // === Ink / text ===
    public static readonly Color Ink      = Hex("#10180F");
    public static readonly Color InkGrass = Hex("#3A2F0E");
    public static readonly Color Paper    = Hex("#E8E4C8");
    public static readonly Color InkText  = Hex("#0F1A14");

    // === Action ===
    public static readonly Color Teal     = Hex("#3FA3A8");
    public static readonly Color TealHi   = Hex("#6BC8CC");
    public static readonly Color TealLo   = Hex("#1F5C60");
    public static readonly Color Gold     = Hex("#F2C84B"); // Selection / focus border swap.
    public static readonly Color GoldDeep = Hex("#B58A1C");

    // === Status ===
    public static readonly Color HpRed    = Hex("#D03A3A");
    public static readonly Color GemGreen = Hex("#4FC774");
    public static readonly Color Danger   = Hex("#C8412A");

    // === Typography sizes ===
    public static class Display
    {
        public const string Family = "Alagard";
        public const int H1    = 56;
        public const int H2    = 32;
        public const int H3    = 24;
        public const int Label = 18;
    }

    public static class Ui
    {
        public const string Family = "Jersey 15"; // Pending: download Jersey15.ttf into assets/fonts/.
        public const int Body    = 20;
        public const int Button  = 22;
        public const int Small   = 16;
        public const int Caption = 14;
        public const int KbdChip = 14;
    }

    // === Spacing ===
    public const int BorderWeightPx = 3;
    public const int BevelInsetPx   = 3;
    public const int SectionGapPx   = 24;
    public const int PanelPaddingPx = 16;

    /// <summary>Parse a #RRGGBB string into a Color. Token files are authored
    /// in hex; this keeps the constants above readable and 1:1 with the spec.</summary>
    private static Color Hex(string rgb) => new(rgb);
}
