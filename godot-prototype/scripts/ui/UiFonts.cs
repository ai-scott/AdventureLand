using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Pixel-font pairing for the UI.
///
/// <para><b>Title</b> — alagard (native 16px). Use for item names, NPC names,
/// world banners. Pixel-perfect at 16, acceptable at 14.</para>
///
/// <para><b>Body</b> — romulus (native 8px). Use for stat lines, descriptions,
/// key hints, prompts. Pixel-perfect at 8 / 16 / 24 only — intermediate sizes
/// render mushy because pixel TTFs need integer multiples of their native size.
/// Prefer <b>16</b> everywhere (2× native) until we add a 10px-native font.</para>
///
/// Global Theme (assets/fonts/UiTheme.tres) sets alagard as default_font, so
/// any label that isn't explicitly overridden picks up a title-style font.
/// Call <c>label.AddThemeFontOverride("font", UiFonts.Body)</c> to opt into
/// romulus for body text.
/// </summary>
public static class UiFonts
{
    private static Font _body;
    private static Font _title;

    public static Font Body =>
        _body ??= GD.Load<Font>("res://assets/fonts/romulus_by_pix3m-d6aokem.ttf");

    public static Font Title =>
        _title ??= GD.Load<Font>("res://assets/fonts/alagard_by_pix3m-d6awiwp.ttf");
}
