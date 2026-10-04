// JRPG-style item-shine shader. Draws the item texture normally, plus a
// diagonal bright band that sweeps top-left → bottom-right. The shine is
// additively blended on top of the base RGB and multiplied by the base's
// alpha, so it's naturally clipped to the sprite's silhouette (no halo).
//
// The band has two regions for a pixel-art feel (hard edges, no smoothstep):
//   - Core:   `core_width` UV units wide, full shine strength
//   - Fringe: `fringe_width` UV units on each side of the core, 50% strength
//
// Defaults assume a 16×16 sprite (1/16 ≈ 0.0625 UV per pixel): core = 1 px,
// fringe = 1 px each side, total band = 3 px. For larger sprites, scale the
// widths up proportionally or just leave them — still reads fine.
//
// A cycle runs for `cycle_period` seconds. The active sweep only plays in
// the first `sweep_fraction` of that cycle; the rest is a rest so the shine
// reads as a recurring glint rather than a constant shimmer.
shader_type canvas_item;

uniform float core_width : hint_range(0.02, 0.3) = 0.08;
uniform float fringe_width : hint_range(0.0, 0.3) = 0.07;
uniform float fringe_strength : hint_range(0.0, 1.0) = 0.5;
uniform float shine_strength : hint_range(0.0, 4.0) = 1.3;
uniform float cycle_period : hint_range(0.4, 6.0) = 1.6;
uniform float sweep_fraction : hint_range(0.1, 1.0) = 0.55;
uniform vec3 shine_color : source_color = vec3(1.0, 0.95, 0.7);
// Per-instance phase offset (seconds). C# sets this from a random value at
// _Ready so two pickups side-by-side don't sweep in lockstep.
uniform float phase_offset : hint_range(0.0, 10.0) = 0.0;

void fragment() {
    vec4 base = texture(TEXTURE, UV);

    // Diagonal coordinate: 0 at top-left, 2 at bottom-right.
    float diag = UV.x + UV.y;

    // Normalize time within the cycle, sweep only during the active window.
    float t = mod(TIME + phase_offset, cycle_period) / cycle_period;
    float sweep_t = clamp(t / sweep_fraction, 0.0, 1.0);
    float active = step(t, sweep_fraction);
    float band_center = mix(-0.5, 2.5, sweep_t);

    float dist = abs(diag - band_center);

    // Stepped intensity: 1.0 inside the core, fringe_strength in the fringe
    // ring just outside it, 0 beyond. step() gives pixel-sharp edges which
    // reads better on 16×16 pixel art than a soft smoothstep gradient.
    float in_core   = step(dist, core_width);
    float in_fringe = step(dist, core_width + fringe_width) - in_core;
    float shine = (in_core + in_fringe * fringe_strength) * active;

    // Additive highlight, clipped by the sprite's alpha.
    vec3 rgb = base.rgb + shine_color * shine * shine_strength * base.a;
    COLOR = vec4(rgb, base.a);
}
