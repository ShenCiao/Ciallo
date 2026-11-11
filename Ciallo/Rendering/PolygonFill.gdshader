shader_type canvas_item;
render_mode unshaded, blend_mix;

#include "PolygonFillAA.gdshaderinc"

uniform vec4 FillColor : source_color = vec4(1.0);
uniform int ActiveBrushFlags = 0;
const int UseShapeRgb = 1 << 0;
const int UseShapeAlpha = 1 << 1;
instance uniform vec4 ShapeColor : source_color = vec4(0.0, 0.0, 0.0, 1.0);

void fragment() {
	COLOR *= vec4(
		(ActiveBrushFlags & UseShapeRgb) != 0 ? ShapeColor.rgb : FillColor.rgb,
		(ActiveBrushFlags & UseShapeAlpha) != 0 ? ShapeColor.a : FillColor.a);
	COLOR.a = polygon_aa_alpha(COLOR.a, FRAGCOORD.xy);
}
