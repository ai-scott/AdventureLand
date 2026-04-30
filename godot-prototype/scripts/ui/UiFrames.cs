using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Design-system Frame helpers — produce StyleBoxFlat panels per
/// <c>handoff/design-spec.md</c> §4.1 (mossy / grass / wood variants, 3px
/// ink border, content padding).
///
/// User override: frames are <b>opaque</b>, not the spec's translucent
/// rgba(31,48,38,0.82) variant. See feedback memory.
///
/// Bevel: the spec asks for a 3px asymmetric inset bevel
/// (top/left = field_hi, bottom/right = field_lo). Godot's StyleBoxFlat
/// only supports a single border color, so the proper bevel needs a
/// 3-panel composite (or a custom 9-slice). Deferred — these helpers
/// land the fill + ink border now; the bevel can be added later as a
/// reusable Control wrapper without breaking call sites.
/// </summary>
public static class UiFrames
{
    /// <summary>Default content padding inside any Frame body. Pulled from
    /// <c>handoff/tokens.json</c> spacing.panel_padding_px.</summary>
    public const int DefaultPadding = 16;

    /// <summary>Opaque mossy frame with full asymmetric bevel — the project
    /// default per user override. Use for menu screens, in-world dialogs,
    /// save-slot wrappers, etc.</summary>
    public static BevelStyleBox MossyPanel(int padding = DefaultPadding)
        => MakeBevelPanel(DesignTokens.MossyField, DesignTokens.MossyFieldHi, DesignTokens.MossyFieldLo, DesignTokens.Ink, padding);

    /// <summary>Opaque grass frame with bevel — for daytime menu backdrops
    /// where the surrounding canvas is grass rather than painted art.</summary>
    public static BevelStyleBox GrassPanel(int padding = DefaultPadding)
        => MakeBevelPanel(DesignTokens.GrassField, DesignTokens.GrassFieldHi, DesignTokens.GrassFieldLo, DesignTokens.InkGrass, padding);

    /// <summary>Deep-wood ribbon with bevel — title banner only, never a
    /// content body.</summary>
    public static BevelStyleBox DeepWoodBanner(int padding = 8)
        => MakeBevelPanel(DesignTokens.DeepWood, DesignTokens.DeepWoodHi, DesignTokens.DeepWoodLo, DesignTokens.Ink, padding);

    /// <summary>Apply the standard mossy frame to a PanelContainer in one
    /// call. Mirrors the existing UiStyles.ApplyBtnActionStyle ergonomics.</summary>
    public static void ApplyMossyPanel(PanelContainer panel, int padding = DefaultPadding)
        => panel.AddThemeStyleboxOverride("panel", MossyPanel(padding));

    /// <summary>Apply the grass-field frame to a PanelContainer.</summary>
    public static void ApplyGrassPanel(PanelContainer panel, int padding = DefaultPadding)
        => panel.AddThemeStyleboxOverride("panel", GrassPanel(padding));

    /// <summary>Save-slot-row chip stylebox — mossy fill with bevel + 3px
    /// border. Pass <c>DesignTokens.Ink</c> for rest, <c>DesignTokens.Gold</c>
    /// for selected/focused.</summary>
    public static BevelStyleBox SaveSlotChip(Color borderColor)
        => MakeBevelPanel(DesignTokens.MossyField, DesignTokens.MossyFieldHi, DesignTokens.MossyFieldLo, borderColor, padding: 8);

    /// <summary>Action-button stylebox per spec §4.2 — variant fill, bevel,
    /// 3px border. Pass <c>DesignTokens.Ink</c> for rest, <c>DesignTokens.Gold</c>
    /// for focus.</summary>
    public static BevelStyleBox ActionButton(Color fill, Color borderColor)
    {
        var (hi, lo) = ButtonBevelColors(fill);
        return MakeBevelPanel(fill, hi, lo, borderColor, padding: 6);
    }

    /// <summary>Icon variant of <see cref="BuildKbdChip(string)"/> — same
    /// ink-fill / gold-border chip but with a TextureRect inside instead
    /// of a text Label. Used where the keypress wants to read as a glyph
    /// (e.g. the spc-key icon on the HUD attack button).</summary>
    public static PanelContainer BuildKbdChip(Texture2D icon, int scale = 2)
    {
        var panel = new PanelContainer();
        var sb = new BevelStyleBox
        {
            Fill = DesignTokens.Ink,
            Border = DesignTokens.Gold,
            BorderWidth = 1,
            BevelWidth = 0,
            CornerGap = 1,
            Padding = 0,
        };
        sb.ContentMarginLeft = 4;
        sb.ContentMarginRight = 4;
        sb.ContentMarginTop = 2;
        sb.ContentMarginBottom = 2;
        panel.AddThemeStyleboxOverride("panel", sb);
        panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

        var rect = new TextureRect
        {
            Texture = icon,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (icon != null) rect.CustomMinimumSize = icon.GetSize() * scale;
        panel.AddChild(rect);
        return panel;
    }

    /// <summary>Compact keyboard-hint chip ([SPC], [↵], [ESC]) per spec §4.3
    /// — ink fill, gold text + 1px gold border. Returned as a PanelContainer
    /// with the label already attached; just add it to a parent.</summary>
    public static PanelContainer BuildKbdChip(string text)
    {
        var panel = new PanelContainer();
        var sb = new BevelStyleBox
        {
            Fill = DesignTokens.Ink,
            Border = DesignTokens.Gold,
            BorderWidth = 1,
            BevelWidth = 0,
            CornerGap = 1,
            Padding = 0,
        };
        sb.ContentMarginLeft = 6;
        sb.ContentMarginRight = 6;
        sb.ContentMarginTop = 2;
        sb.ContentMarginBottom = 2;
        panel.AddThemeStyleboxOverride("panel", sb);
        panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        // Secondary UI font (Jersey 15 / Romulus fallback) at body size.
        // 14–16px renders too small/rough on the chip; 20px gives Jersey 15
        // enough vertical pixels to read crisply even at integer scale.
        label.AddThemeFontOverride("font", UiFonts.Body);
        label.AddThemeFontSizeOverride("font_size", 20);
        label.AddThemeColorOverride("font_color", DesignTokens.Gold);
        panel.AddChild(label);
        return panel;
    }

    /// <summary>Build an action button with an inline keyboard-hint chip
    /// (Back + esc, Continue + ↵, etc.). The chip lives inside an HBox
    /// anchored to the button's full rect so style swaps to the underlying
    /// stylebox don't disturb the layout. <paramref name="applyStyle"/>
    /// picks the variant: <see cref="ApplyPrimaryButton"/>,
    /// <see cref="ApplySecondaryButton"/>, <see cref="ApplyDangerButton"/>.</summary>
    public static Button BuildChipButton(string text, string kbdHint, System.Action<Button> applyStyle)
    {
        var btn = new Button { Text = "" };
        btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        applyStyle(btn);

        var hbox = new HBoxContainer();
        hbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        hbox.OffsetLeft = 12;
        hbox.OffsetRight = -12;
        hbox.OffsetTop = 4;
        hbox.OffsetBottom = -4;
        hbox.Alignment = BoxContainer.AlignmentMode.Center;
        hbox.AddThemeConstantOverride("separation", 8);
        hbox.MouseFilter = Control.MouseFilterEnum.Ignore;
        btn.AddChild(hbox);

        var label = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", DesignTokens.Paper);
        hbox.AddChild(label);

        if (!string.IsNullOrEmpty(kbdHint))
        {
            hbox.AddChild(BuildKbdChip(kbdHint));
        }

        return btn;
    }

    /// <summary>Compact stat chip used inside item dialogs — mossy panel
    /// with text followed by an optional icon (e.g. "+3" + sword,
    /// "11" + gem). Bumped 50% over the original 16px icon spec to give
    /// the value-and-glyph pair more presence inside the item card.</summary>
    public static PanelContainer BuildStatChip(string text, Texture2D icon = null, Color? textColor = null)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new BevelStyleBox
        {
            Fill = DesignTokens.MossyFieldLo,
            BevelHi = DesignTokens.MossyField,
            BevelLo = new Color(0.05f, 0.08f, 0.06f, 1),
            Border = DesignTokens.Ink,
            BorderWidth = 2,
            BevelWidth = 1,
            Padding = 6,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        });
        panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.AddChild(row);

        // Text first (per design spec) — value reads before the glyph.
        var label = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontOverride("font", UiFonts.Body);
        label.AddThemeFontSizeOverride("font_size", 26);
        label.AddThemeColorOverride("font_color", textColor ?? DesignTokens.Paper);
        row.AddChild(label);

        if (icon != null)
        {
            // 24px tall (50% larger than the previous 16px) — aspect
            // preserved on the X axis based on the source texture.
            var nativeSize = icon.GetSize();
            float scale = nativeSize.Y > 0 ? 24f / nativeSize.Y : 1f;
            var iconRect = new TextureRect
            {
                Texture = icon,
                CustomMinimumSize = new Vector2(nativeSize.X * scale, 24),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            row.AddChild(iconRect);
        }
        return panel;
    }

    /// <summary>Apply the design-system primary (teal) action button styling
    /// — rest = teal+ink border, focus = teal+gold border, disabled = stone.
    /// Replaces the legacy UiStyles.ApplyBtnActionStyle for screens migrated
    /// to the new design system.</summary>
    public static void ApplyPrimaryButton(Button btn)
        => ApplyActionButton(btn, DesignTokens.Teal);

    /// <summary>Apply the design-system secondary (stone) action button.</summary>
    public static void ApplySecondaryButton(Button btn)
        => ApplyActionButton(btn, DesignTokens.Stone);

    /// <summary>Apply the design-system danger (red) action button.</summary>
    public static void ApplyDangerButton(Button btn)
        => ApplyActionButton(btn, DesignTokens.Danger);

    private static void ApplyActionButton(Button btn, Color fill)
    {
        var rest = ActionButton(fill, DesignTokens.Ink);
        var focus = ActionButton(fill, DesignTokens.Gold);
        var disabled = ActionButton(DesignTokens.Stone, DesignTokens.Ink);
        btn.AddThemeStyleboxOverride("normal", rest);
        btn.AddThemeStyleboxOverride("hover", rest);
        btn.AddThemeStyleboxOverride("pressed", rest);
        btn.AddThemeStyleboxOverride("focus", focus);
        btn.AddThemeStyleboxOverride("disabled", disabled);
        btn.AddThemeColorOverride("font_color", DesignTokens.Paper);
        btn.AddThemeColorOverride("font_hover_color", DesignTokens.Paper);
        btn.AddThemeColorOverride("font_focus_color", DesignTokens.Paper);
        btn.AddThemeColorOverride("font_pressed_color", DesignTokens.Paper);
        btn.AddThemeColorOverride("font_disabled_color", new Color(DesignTokens.Paper.R, DesignTokens.Paper.G, DesignTokens.Paper.B, 0.5f));
    }

    /// <summary>Pick a sensible (hi, lo) bevel pair for an action-button
    /// fill. Mirrors the surface-level (hi/lo) pairs in the design tokens.</summary>
    private static (Color hi, Color lo) ButtonBevelColors(Color fill)
    {
        if (fill == DesignTokens.Teal)   return (DesignTokens.TealHi, DesignTokens.TealLo);
        if (fill == DesignTokens.Stone)  return (DesignTokens.StoneHi, DesignTokens.StoneLo);
        if (fill == DesignTokens.Danger) return (Lighten(fill, 0.18f), Darken(fill, 0.30f));
        if (fill == DesignTokens.Gold)   return (Lighten(fill, 0.18f), DesignTokens.GoldDeep);
        return (Lighten(fill, 0.18f), Darken(fill, 0.30f));
    }

    private static Color Lighten(Color c, float amount)
        => new(System.Math.Min(c.R + amount, 1f), System.Math.Min(c.G + amount, 1f), System.Math.Min(c.B + amount, 1f), c.A);

    private static Color Darken(Color c, float amount)
        => new(System.Math.Max(c.R - amount, 0f), System.Math.Max(c.G - amount, 0f), System.Math.Max(c.B - amount, 0f), c.A);

    private static BevelStyleBox MakeBevelPanel(Color fill, Color hi, Color lo, Color border, int padding)
    {
        var sb = new BevelStyleBox
        {
            Fill = fill,
            BevelHi = hi,
            BevelLo = lo,
            Border = border,
            BorderWidth = DesignTokens.BorderWeightPx,
            BevelWidth = DesignTokens.BevelInsetPx,
            Padding = padding,
        };
        // Push the same "border + bevel + padding" total into the inherited
        // ContentMargin properties so PanelContainer / Button reserve the
        // right inner space for their content.
        float total = sb.BorderWidth + sb.BevelWidth + sb.Padding;
        sb.ContentMarginLeft = total;
        sb.ContentMarginRight = total;
        sb.ContentMarginTop = total;
        sb.ContentMarginBottom = total;
        return sb;
    }
}
