uniform bool AaEnabled = true;
uniform float AaEpsilon : hint_range(0.00001, 0.5) = 0.01;
uniform float AaMaxVertexOffsetPixels : hint_range(0.0, 32.0) = 10.0;

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
	if (numerator_length == 0.0 || AaMaxVertexOffsetPixels <= 0.0) {
		return vec2(0.0);
	}
	// Bound the division itself, so an acute tip never creates an infinite offset.
	float denominator = max(abs(aa_cross(n0, n1)), numerator_length / AaMaxVertexOffsetPixels);
	return numerator / denominator;
}

void vertex() {
	mat4 local_to_screen = CANVAS_MATRIX * MODEL_MATRIX;
	aa_triangle_a = (local_to_screen * vec4(CUSTOM0.xy, 0.0, 1.0)).xy;
	aa_triangle_b = (local_to_screen * vec4(CUSTOM0.zw, 0.0, 1.0)).xy;
	aa_triangle_c = (local_to_screen * vec4(CUSTOM1.xy, 0.0, 1.0)).xy;
	mat2 linear = mat2(local_to_screen[0].xy, local_to_screen[1].xy);
	if (AaEnabled && determinant(linear) != 0.0) {
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

float polygon_aa_coverage(vec2 pixel_center) {
	vec2 triangle[3] = vec2[](aa_triangle_a - pixel_center, aa_triangle_b - pixel_center, aa_triangle_c - pixel_center);
	float winding = sign(aa_cross(triangle[1] - triangle[0], triangle[2] - triangle[0]));
	if (winding == 0.0) return 0.0;
	vec2 normals[3];
	float distances[3];
	bool fully_inside = true;
	for (int edge = 0; edge < 3; edge++) {
		vec2 delta = triangle[(edge + 1) % 3] - triangle[edge];
		vec2 normal = winding * vec2(-delta.y, delta.x);
		float distance = -dot(normal, triangle[edge]);
		float radius = 0.5 * (abs(normal.x) + abs(normal.y));
		if (distance + radius <= 0.0) return 0.0;
		fully_inside = fully_inside && distance >= radius;
		normals[edge] = normal;
		distances[edge] = distance;
	}
	if (fully_inside) return 1.0;

	// Clip the unit pixel by the three triangle half-planes. Intersection has <= 7 vertices.
	vec2 polygon[8];
	vec2 clipped[8];
	polygon[0] = vec2(-0.5, -0.5);
	polygon[1] = vec2(0.5, -0.5);
	polygon[2] = vec2(0.5, 0.5);
	polygon[3] = vec2(-0.5, 0.5);
	int count = 4;
	for (int edge = 0; edge < 3; edge++) {
		int output_count = 0;
		vec2 previous = polygon[count - 1];
		float previous_distance = dot(normals[edge], previous) + distances[edge];
		for (int i = 0; i < count; i++) {
			vec2 current = polygon[i];
			float distance = dot(normals[edge], current) + distances[edge];
			// Vertices exactly on the clipping plane are emitted only once.
			if ((distance > 0.0 && previous_distance < 0.0) || (distance < 0.0 && previous_distance > 0.0)) {
				float t = previous_distance / (previous_distance - distance);
				clipped[output_count++] = mix(previous, current, t);
			}
			if (distance >= 0.0) clipped[output_count++] = current;
			previous = current;
			previous_distance = distance;
		}
		if (output_count < 3) return 0.0;
		count = output_count;
		for (int i = 0; i < count; i++) polygon[i] = clipped[i];
	}
	float twice_area = 0.0;
	vec2 previous = polygon[count - 1];
	for (int i = 0; i < count; i++) {
		twice_area += aa_cross(previous, polygon[i]);
		previous = polygon[i];
	}
	return clamp(abs(twice_area) * 0.5, 0.0, 1.0);
}

float polygon_aa_alpha(float target_alpha, vec2 pixel_center) {
	if (!AaEnabled) return target_alpha;
	float coverage = polygon_aa_coverage(pixel_center);
	float depth = -log2(max(1.0 - clamp(target_alpha, 0.0, 1.0), AaEpsilon));
	float x = coverage * depth;
	// Avoid cancellation of tiny triangle contributions in 1 - exp2(-x).
	if (x < 0.001) {
		float y = x * 0.69314718056;
		return y * (1.0 - 0.5 * y + y * y / 6.0);
	}
	return 1.0 - exp2(-x);
}
