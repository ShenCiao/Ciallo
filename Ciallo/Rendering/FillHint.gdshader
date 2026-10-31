shader_type canvas_item;

render_mode unshaded, blend_mix;

#define POLYGON_FILL_HINT
#include "PolygonFillAA.gdshaderinc"

uniform vec4 strip_color : source_color = vec4(1.0);
uniform float variation = 0.0;
float dot2(vec2 x) {
	return dot(x, x);
}
float rand(vec2 x) {
    return fract(cos(mod(dot(x, vec2(13.9898, 8.141)), 3.14)) * 43758.5);
}
vec2 rand2(vec2 x) {
    return fract(cos(mod(vec2(dot(x, vec2(13.9898, 8.141)),
						      dot(x, vec2(3.4562, 17.398))), vec2(3.14))) * 43758.5);
}
vec3 rand3(vec2 x) {
    return fract(cos(mod(vec3(dot(x, vec2(13.9898, 8.141)),
							  dot(x, vec2(3.4562, 17.398)),
                              dot(x, vec2(13.254, 5.867))), vec3(3.14))) * 43758.5);
}
float param_rnd(float minimum, float maximum, float seed) {
	return minimum+(maximum-minimum)*rand(vec2(seed));
}
float param_rndi(float minimum, float maximum, float seed) {
	return floor(param_rnd(minimum, maximum + 1.0, seed));
}
vec3 rgb2hsv(vec3 c) {
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = c.g < c.b ? vec4(c.bg, K.wz) : vec4(c.gb, K.xy);
	vec4 q = c.r < p.x ? vec4(p.xyw, c.r) : vec4(c.r, p.yzx);
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}
vec3 hsv2rgb(vec3 c) {
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}
const float seed_o516855702874 = 0.369070798;
const float p_o516855702874_tx = 4.000000000;
const float p_o516855702874_ty = 4.000000000;
const float p_o516855702874_overlap = 1.000000000;
const float p_o516855702874_scale_x = 1.000000000;
const float p_o516855702874_scale_y = 1.000000000;
const float p_o516855702874_fixed_offset = 0.000000000;
const float p_o516855702874_offset = 0.000000000;
const float p_o516855702874_rotate = 0.000000000;
const float p_o516855702874_scale = 0.000000000;
const float p_o516855702874_value = 0.000000000;
const float p_o516939590164_cx = 0.000000000;
const float p_o516939590164_cy = 0.000000000;
const float p_o516939590164_rotate = 45.000000000;
const float p_o516922812931_cx = 0.000000000;
const float p_o516922812931_cy = 0.000000000;
const float p_o516922812931_scale_x = 0.033000000;
const float p_o516922812931_scale_y = 0.400000000;
const float p_o516973144581_sides = 2.000000000;
const float p_o516973144581_radius = 0.100000000;
const float p_o516973144581_edge = 0.000000000;
// #globals: translate (o516956367345)
// #globals: rotate (o516939590164)
vec2 rotate(vec2 uv, float rotate) {
 	vec2 rv;
	rv.x = cos(rotate)*uv.x + sin(rotate)*uv.y;
	rv.y = -sin(rotate)*uv.x + cos(rotate)*uv.y;
	return rv;	
}
// #globals: scale (o516922812931)
vec2 scale(vec2 uv, vec2 center, vec2 scale) {
	uv -= center;
	uv /= scale;
	uv += center;
	return uv;
}
// #globals: shape_2 (o516973144581)
float shape_circle(vec2 uv, float sides, float size, float edge) {
	uv = 2.0*uv-1.0;
	edge = max(edge, 1.0e-8);
	float distance = length(uv);
	return clamp((1.0-distance/size)/edge, 0.0, 1.0);
}
float shape_polygon(vec2 uv, float sides, float size, float edge) {
	uv = 2.0*uv-1.0;
	edge = max(edge, 1.0e-8);
	float angle = atan(uv.x, uv.y)+3.14159265359;
	float slice = 6.28318530718/sides;
	return clamp((1.0-(cos(floor(0.5+angle/slice)*slice-angle)*length(uv))/size)/edge, 0.0, 1.0);
}
float shape_star(vec2 uv, float sides, float size, float edge) {
	uv = 2.0*uv-1.0;
	edge = max(edge, 1.0e-8);
	float angle = atan(uv.x, uv.y);
	float slice = 6.28318530718/sides;
	return clamp((1.0-(cos(floor(angle*sides/6.28318530718-0.5+2.0*step(fract(angle*sides/6.28318530718), 0.5))*slice-angle)*length(uv))/size)/edge, 0.0, 1.0);
}
float shape_curved_star(vec2 uv, float sides, float size, float edge) {
	uv = 2.0*uv-1.0;
	edge = max(edge, 1.0e-8);
	float angle = 2.0*(atan(uv.x, uv.y)+3.14159265359);
	float slice = 6.28318530718/sides;
	return clamp((1.0-cos(floor(0.5+0.5*angle/slice)*2.0*slice-angle)*length(uv)/size)/edge, 0.0, 1.0);
}
float shape_rays(vec2 uv, float sides, float size, float edge) {
	uv = 2.0*uv-1.0;
	edge = 0.5*max(edge, 1.0e-8)*size;
	float slice = 6.28318530718/sides;
	float angle = mod(atan(uv.x, uv.y)+3.14159265359, slice)/slice;
	return clamp(min((size-angle)/edge, angle/edge), 0.0, 1.0);
}
// #globals: custom_uv
vec2 get_from_tileset(float count, float seed, vec2 uv) {
	return clamp((uv+floor(rand2(vec2(seed))*count))/count, vec2(0.0), vec2(1.0));
}
vec2 old_custom_uv_transform(vec2 uv, vec2 cst_scale, float rnd_rotate, float rnd_scale, vec2 seed) {
	seed = rand2(seed);
	uv -= vec2(0.5);
	float angle = (seed.x * 2.0 - 1.0) * rnd_rotate;
	float ca = cos(angle);
	float sa = sin(angle);
	uv = vec2(ca*uv.x+sa*uv.y, -sa*uv.x+ca*uv.y);
	uv *= (seed.y-0.5)*2.0*rnd_scale+1.0;
	uv /= cst_scale;
	uv += vec2(0.5);
	return uv;
}
float o516855702874_input_in(vec2 uv, float _seed_variation_) {
// #code: rotate (o516939590164)
vec2 o516939590164_0_c = vec2(0.5+p_o516939590164_cx, 0.5+p_o516939590164_cy);
// #output0: shape_2 (o516973144581)
float o516973144581_0_1_f = shape_polygon((scale((rotate(uv-o516939590164_0_c, p_o516939590164_rotate*0.01745329251)+o516939590164_0_c), vec2(0.5+p_o516922812931_cx, 0.5+p_o516922812931_cy), vec2(p_o516922812931_scale_x, p_o516922812931_scale_y))), p_o516973144581_sides+1.0e-5, p_o516973144581_radius*1.0, p_o516973144581_edge*1.0);
// #output0: scale (o516922812931)
vec4 o516922812931_0_1_rgba = vec4(vec3(o516973144581_0_1_f), 1.0);
// #output0: rotate (o516939590164)
vec4 o516939590164_0_1_rgba = o516922812931_0_1_rgba;
return (dot((o516939590164_0_1_rgba).rgb, vec3(1.0))/3.0);
}
// #instance: tiler (o516855702874)
vec4 tiler_o516855702874(vec2 uv, vec2 tile, int overlap, inout vec3 instance_uv, vec2 _seed, float _seed_variation_) {
	float c = 0.0;
	vec3 rc = vec3(0.0);
	vec3 rc1;
	for (int dx = -overlap; dx <= overlap; ++dx) {
		for (int dy = -overlap; dy <= overlap; ++dy) {
			vec2 pos = uv*tile+vec2(float(dx), float(dy)); pos = fract((floor(mod(pos, tile))+vec2(0.5))/tile)-vec2(0.5);
			vec2 seed = rand2(pos+_seed);
			rc1 = rand3(seed);
			vec2 tile_index = floor(uv * tile) + vec2(float(dx), float(dy));
			pos += vec2((p_o516855702874_fixed_offset/tile.x)*mod(mod(tile_index.y, tile.y), 2.0), 0.0)+p_o516855702874_offset*seed/tile;
			float mask = 1.0;
			if (mask > 0.01) {
				vec2 pv = fract(uv - pos)-vec2(0.5);
				seed = rand2(seed);
				float angle = (seed.x * 2.0 - 1.0) * p_o516855702874_rotate * 0.01745329251;
				float ca = cos(angle);
				float sa = sin(angle);
				pv = vec2(ca*pv.x+sa*pv.y, -sa*pv.x+ca*pv.y);
				pv *= (seed.y-0.5)*2.0*p_o516855702874_scale+1.0;
				pv /= vec2(p_o516855702874_scale_x, p_o516855702874_scale_y);
				pv += vec2(0.5);
				seed = rand2(seed);
				vec2 clamped_pv = clamp(pv, vec2(0.0), vec2(1.0));
				if (pv.x != clamped_pv.x || pv.y != clamped_pv.y) {
					continue;
				}
				vec2 full_uv = pv;
				pv = get_from_tileset( 1.0, seed.x, pv);
				float c1 = o516855702874_input_in(pv, false ? seed.x : 0.0)*mask*(1.0-p_o516855702874_value*seed.x);
				c = max(c, c1);
				rc = mix(rc, rc1, step(c, c1));
				instance_uv = mix(instance_uv, vec3(full_uv, seed.x), step(c, c1));
			}
		}
	}
	return vec4(rc, c);
}
void fragment() {
	float _seed_variation_ = variation;
	vec2 uv = fract(UV);

// #code: tiler (o516855702874)
vec3 o516855702874_0_instance_uv = vec3(0.0);
vec4 o516855702874_0_rch = tiler_o516855702874(((uv)-vec2((-TIME*0.1), (TIME*0.1))), vec2(p_o516855702874_tx, p_o516855702874_ty), int(p_o516855702874_overlap), o516855702874_0_instance_uv, vec2(float((seed_o516855702874+fract(_seed_variation_)))), _seed_variation_);
// #output0: tiler (o516855702874)
float o516855702874_0_1_f = o516855702874_0_rch.a;

// #output0: translate (o516956367345)
vec4 o516956367345_0_1_rgba = vec4(strip_color.rgb, o516855702874_0_1_f * strip_color.a);

	vec4 color_tex = o516956367345_0_1_rgba;
	color_tex = mix(pow((color_tex + vec4(0.055)) * (1.0 / (1.0 + 0.055)),vec4(2.4)),color_tex * (1.0 / 12.92),lessThan(color_tex,vec4(0.04045)));
	color_tex.a *= COLOR.a;
	color_tex.a = polygon_aa_alpha(color_tex.a, FRAGCOORD.xy);
	COLOR = color_tex;
}
