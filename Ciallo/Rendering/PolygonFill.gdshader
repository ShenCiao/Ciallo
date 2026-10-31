shader_type canvas_item;
render_mode unshaded, blend_mix;

#include "PolygonFillAA.gdshaderinc"

instance uniform vec4 FillColor : source_color = vec4(1.0);

void fragment() {
	COLOR *= FillColor;
	COLOR.a = polygon_aa_alpha(COLOR.a, FRAGCOORD.xy);
}
