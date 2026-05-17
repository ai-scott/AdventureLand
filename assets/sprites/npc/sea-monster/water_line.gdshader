shader_type canvas_item;

// UV-space cutoff (0 = nothing visible, 1 = whole sprite visible). Pixels with
// UV.y > local_water_y are clipped (alpha 0). The C# controller writes this
// every tween frame from the sprite's world transform vs. the lake's surface.
uniform float local_water_y : hint_range(0.0, 1.0) = 1.0;

// Soft edge in UV units so the cut isn't a hard pixel line — fakes a wet
// meniscus and avoids stair-stepping on the rising silhouette.
uniform float edge_softness : hint_range(0.0, 0.2) = 0.04;

void fragment() {
    vec4 src = texture(TEXTURE, UV);
    float fade = smoothstep(local_water_y + edge_softness, local_water_y, UV.y);
    COLOR = vec4(src.rgb, src.a * fade);
}
