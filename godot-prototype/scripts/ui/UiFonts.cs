using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Pixel-font pairing for the UI.
///
/// <para><b>Title</b> — alagard (native 16px). Use for item names, NPC names,
/// world banners. Pixel-perfect at 16, acceptable at 14.</para>
///
/// <para><b>Body</b> — Jersey 15 if installed at
/// <c>assets/fonts/Jersey15-Regular.ttf</c>, otherwise romulus as a
/// fallback. Jersey 15 is the design-system spec secondary face
/// (handoff/design-spec.md §3); Romulus is the pre-handoff stand-in.
/// Use for stat lines, descriptions, key hints, prompts, kbd chips. To
/// install Jersey 15, download from
/// <c>https://fonts.google.com/specimen/Jersey+15</c> and place the
/// regular .ttf in <c>assets/fonts/</c>.</para>
///
/// Global Theme (assets/fonts/UiTheme.tres) sets alagard as default_font, so
/// any label that isn't explicitly overridden picks up a title-style font.
/// Call <c>label.AddThemeFontOverride("font", UiFonts.Body)</c> to opt into
/// the secondary face for body text.
/// </summary>
public static class UiFonts
{
    private const string JerseyPath = "res://assets/fonts/Jersey15-Regular.ttf";
    private const string RomulusPath = "res://assets/fonts/romulus_by_pix3m-d6aokem.ttf";

    private static Font _body;
    private static Font _title;
    private static Font _pixel;

    public static Font Body
    {
        get
        {
            if (_body != null) return _body;
            _body = ResourceLoader.Exists(JerseyPath)
                ? GD.Load<Font>(JerseyPath)
                : GD.Load<Font>(RomulusPath);
            return _body;
        }
    }

    public static Font Title =>
        _title ??= GD.Load<Font>("res://assets/fonts/alagard_by_pix3m-d6awiwp.ttf");

    /// <summary>Chunky retro pixel font — for menu options on title / game over
    /// where we want the "Final Fantasy menu" look rather than the ornate
    /// alagard title face.</summary>
    public static Font Pixel =>
        _pixel ??= GD.Load<Font>("res://assets/fonts/Font_Fantasy.ttf");
}
