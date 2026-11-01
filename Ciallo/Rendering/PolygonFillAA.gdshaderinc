global uniform bool PolygonFillAaEnabled;
global uniform float PolygonFillAaEpsilon;
global uniform float PolygonFillAaMaxVertexOffsetPixels;

varying flat vec2 aa_triangle_a;
varying flat vec2 aa_triangle_b;
varying flat vec2 aa_triangle_c;

float aa_cross(vec2 a, vec2 b) {
	return a.x * b.y - a.y * b.x;
}

vec2 aa_vertex_offset(vec2 previous, vec2 current, vec2 next, float winding) {
	vec2 incoming = current - previous;
	vec2 outgoing = next - current;
	float incoming_length = length(incoming);
	float outgoing_length = length(outgoing);
	if (incoming_length == 0.0 || outgoing_length == 0.0 || winding == 0.0) {
		return vec2(0.0);
	}
	vec2 n0 = winding * vec2(incoming.y, -incoming.x) / incoming_length;
	vec2 n1 = winding * vec2(outgoing.y, -outgoing.x) / outgoing_length;
	// Support of a half-pixel square, with a small rasterizer rounding allowance.
	float h0 = 0.501 * (abs(n0.x) + abs(n0.y));
	float h1 = 0.501 * (abs(n1.x) + abs(n1.y));
	vec2 numerator = vec2(h0 * n1.y - h1 * n0.y, n0.x * h1 - n1.x * h0) * winding;
	float numerator_length = length(numerator);
	if (numerator_length == 0.0 || PolygonFillAaMaxVertexOffsetPixels <= 0.0) {
		return vec2(0.0);
	}
	// Bound the division itself, so an acute tip never creates an infinite offset.
	float denominator = max(abs(aa_cross(n0, n1)), numerator_length / PolygonFillAaMaxVertexOffsetPixels);
	return numerator / denominator;
}

void vertex() {
	mat4 local_to_screen = CANVAS_MATRIX * MODEL_MATRIX;
	aa_triangle_a = (local_to_screen * vec4(CUSTOM0.xy, 0.0, 1.0)).xy;
	aa_triangle_b = (local_to_screen * vec4(CUSTOM0.zw, 0.0, 1.0)).xy;
	aa_triangle_c = (local_to_screen * vec4(CUSTOM1.xy, 0.0, 1.0)).xy;
	mat2 linear = mat2(local_to_screen[0].xy, local_to_screen[1].xy);
	if (PolygonFillAaEnabled && determinant(linear) != 0.0) {
		float winding = sign(aa_cross(aa_triangle_b - aa_triangle_a, aa_triangle_c - aa_triangle_a));
		int corner = VERTEX_ID % 3;
		vec2 offset;
		if (corner == 0) {
			offset = aa_vertex_offset(aa_triangle_c, aa_triangle_a, aa_triangle_b, winding);
		} else if (corner == 1) {
			offset = aa_vertex_offset(aa_triangle_a, aa_triangle_b, aa_triangle_c, winding);
		} else {
			offset = aa_vertex_offset(aa_triangle_b, aa_triangle_c, aa_triangle_a, winding);
		}
		VERTEX += inverse(linear) * offset;
	}
#ifdef POLYGON_FILL_HINT
	// Preserve the hint's local-space, zoom-compensated pattern across shared edges.
	UV = VERTEX * (length(CANVAS_MATRIX[0].xy) / 256.0);
#endif
}

// Signed integral of clamp(y, 0, 1) dx inside the pixel's vertical strip.
// Closing edges at x=0 or x=1 have dx=0 and add no area.
float aa_edge_area(vec2 a, vec2 b) {
	float dx = b.x - a.x;
	if (dx == 0.0) return 0.0;
	// Canonical ordering makes reversed shared edges numerically symmetric.
	vec2 p = dx > 0.0 ? a : b;
	vec2 q = dx > 0.0 ? b : a;
	float left = max(p.x, 0.0);
	float right = min(q.x, 1.0);
	if (right <= left) return 0.0;
	float slope = (q.y - p.y) / (q.x - p.x);
	// Evaluate from the nearer endpoint to limit cancellation when zoomed in.
	vec2 origin = abs(q.x - 0.5) < abs(p.x - 0.5) ? q : p;
	float y0 = fma(left - origin.x, slope, origin.y);
	float y1 = fma(right - origin.x, slope, origin.y);
	float low = min(y0, y1);
	float high = max(y0, y1);
	float average;
	if (low >= 1.0) {
		average = 1.0;
	} else if (high <= 0.0) {
		return 0.0;
	} else if (low >= 0.0 && high <= 1.0) {
		average = 0.5 * (low + high);
	} else {
		float bottom = clamp(low, 0.0, 1.0);
		float top = clamp(high, 0.0, 1.0);
		average = (0.5 * (bottom + top) * (top - bottom) + max(high - 1.0, 0.0)) / (high - low);
	}
	return sign(dx) * (right - left) * average;
}

float polygon_aa_coverage(vec2 pixel_center) {
	vec2 a = aa_triangle_a - pixel_center + vec2(0.5);
	vec2 b = aa_triangle_b - pixel_center + vec2(0.5);
	vec2 c = aa_triangle_c - pixel_center + vec2(0.5);
	if (aa_cross(b - a, c - a) == 0.0) return 0.0;
	// Green's theorem: the clipped boundary integral equals triangle/pixel area.
	return clamp(abs(aa_edge_area(a, b) + aa_edge_area(b, c) + aa_edge_area(c, a)), 0.0, 1.0);
}

float polygon_aa_alpha(float target_alpha, vec2 pixel_center) {
	if (!PolygonFillAaEnabled) return target_alpha;
	float coverage = polygon_aa_coverage(pixel_center);
	float depth = -log2(max(1.0 - clamp(target_alpha, 0.0, 1.0), PolygonFillAaEpsilon));
	float x = coverage * depth;
	// Avoid cancellation of tiny triangle contributions in 1 - exp2(-x).
	if (x < 0.001) {
		float y = x * 0.69314718056;
		return y * (1.0 - 0.5 * y + y * y / 6.0);
	}
	return 1.0 - exp2(-x);
}
