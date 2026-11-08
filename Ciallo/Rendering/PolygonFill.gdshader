shader_type canvas_item;
render_mode unshaded, blend_mix;

#include "PolygonFillAA.gdshaderinc"

uniform vec4 FillColor : source_color = vec4(1.0);
instance uniform vec4 ShapeColor : source_color = vec4(0.0, 0.0, 0.0, 1.0);

void fragment() {
	COLOR *= FillColor.a < 0.0 ? ShapeColor : FillColor;
	COLOR.a = polygon_aa_alpha(COLOR.a, FRAGCOORD.xy);
}
