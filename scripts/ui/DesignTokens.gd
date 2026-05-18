extends Node

# Autoload -- no class_name (collides with the autoload singleton name).
#
# Adventure Land design-system tokens -- palette + typography sizes
# mirrored from handoff/tokens.json. Single source of truth so call
# sites pull from semantic names (DesignTokens.GOLD) rather than
# hand-typed hex.
#
# When a token changes in the handoff, update it here too. Colors are
# named in SCREAMING_SNAKE for GDScript const idiom; the C# facade
# (DesignTokens.cs) preserves the prior PascalCase names (Gold, Ink,
# etc.) for unchanged C# call sites.
#
# Register in Project -> Autoload as:
#   Path: res://scripts/ui/DesignTokens.gd
#   Name: DesignTokens

# === Surface ===
const MOSSY_FIELD: Color = Color("#3F5A47")
const MOSSY_FIELD_HI: Color = Color("#5A7A60")
const MOSSY_FIELD_LO: Color = Color("#1F3026")
const GRASS_FIELD: Color = Color("#CDB246")
const GRASS_FIELD_HI: Color = Color("#E3CB6E")
const GRASS_FIELD_LO: Color = Color("#8A7628")
const DEEP_WOOD: Color = Color("#2A3A2A")
const DEEP_WOOD_HI: Color = Color("#445A45")
const DEEP_WOOD_LO: Color = Color("#101A11")
const STONE: Color = Color("#94A09F")
const STONE_HI: Color = Color("#BFC9C7")
const STONE_LO: Color = Color("#5A6463")

# Translucent variant of the mossy field, used over painted backgrounds.
const MOSSY_FIELD_TRANSLUCENT: Color = Color(0.122, 0.188, 0.149, 0.82)  # rgba(31,48,38,0.82)

# === Ink / text ===
const INK: Color = Color("#10180F")
const INK_GRASS: Color = Color("#3A2F0E")
const PAPER: Color = Color("#E8E4C8")
const INK_TEXT: Color = Color("#0F1A14")

# === Action ===
const TEAL: Color = Color("#3FA3A8")
const TEAL_HI: Color = Color("#6BC8CC")
const TEAL_LO: Color = Color("#1F5C60")
const GOLD: Color = Color("#F2C84B")  # Selection / focus border swap.
const GOLD_DEEP: Color = Color("#B58A1C")

# === Status ===
const HP_RED: Color = Color("#D03A3A")
const GEM_GREEN: Color = Color("#4FC774")
const DANGER: Color = Color("#C8412A")

# === Typography sizes ===
# Display sizes (Alagard family).
const DISPLAY_FAMILY: String = "Alagard"
const DISPLAY_H1: int = 56
const DISPLAY_H2: int = 32
const DISPLAY_H3: int = 24
const DISPLAY_LABEL: int = 18

# UI sizes (Jersey 15 family).
const UI_FAMILY: String = "Jersey 15"
const UI_BODY: int = 20
const UI_BUTTON: int = 22
const UI_SMALL: int = 16
const UI_CAPTION: int = 14
const UI_KBD_CHIP: int = 14

# === Spacing ===
const BORDER_WEIGHT_PX: int = 3
const BEVEL_INSET_PX: int = 3
const SECTION_GAP_PX: int = 24
const PANEL_PADDING_PX: int = 16
