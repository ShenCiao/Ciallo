// Articulated strokes: shared Gaussian coverage, per-brush contribution, canvas blending.
shader_type canvas_item;
render_mode unshaded, skip_vertex_transform;
// Godot doesn't support changing blend mode at runtime.
// VARIANT_DEFINES
#if defined(BLEND_ADD)
render_mode blend_add;
#elif defined(BLEND_MUL) || defined(BLEND_ERASE)
render_mode blend_mul;
#endif

// Brush configuration. Uniform names are bound by StrokeBrushMaterial.
struct StampFlag {
	int StampTexture;
	int MaskTexture;
	int RotationNoise;
	int MaskDisk;
};
const StampFlag StampFlags = StampFlag(1 << 0, 1 << 1, 1 << 2, 1 << 3);

struct BrushFlag {
	int Pressure2FlowCurve;
	int Dash;
};
const BrushFlag BrushFlags = BrushFlag(1 << 0, 1 << 1);

const int Vanilla = 0, Stamp = 1, Airbrush = 2;
const int MaxStampsPerFragment = 128;
uniform int StrokeType : hint_enum("Vanilla", "Stamp", "Airbrush") = 0;
uniform int ActiveBrushFlags = 0;
uniform vec4 MaterialColor : source_color = vec4(0.0, 0.0, 0.0, 1.0);
instance uniform vec4 OverridingColor = vec4(0.0, 0.0, 0.0, -1.0);
// Canvas radii are fixed in screen space (e.g. wireframes).
uniform int RadiusMode : hint_enum("World", "Canvas") = 0;
// Diameters and padding are measured in render-target pixels.
uniform float CullScreenDiameter : hint_range(0.0, 0.25, 0.005) = 0.01;
// All brushes share the same screen-space Gaussian and carrier extent.
const float FilterSigmaPixels = 0.6;
const float FilterSupportPixels = 2.0;
// Normalization for the Gaussian truncated at Support / Sigma = 10/3.
const float GaussianTail = 0.0004290603;
const float GaussianMass = 1.0 - 2.0 * GaussianTail;

uniform sampler2D Pressure2FlowCurve;

// Dash
uniform float DashLength = -1.0; // radius-normalized length
uniform float GapLength = -1.0;
uniform float DashForwardSpeed = 0.0;

// Stamp
uniform int ActiveStampFlags;
uniform float StampInterval : hint_range(0.015625, 10.0, 0.0078125) = 0.2;
uniform sampler2D StampTexture;
uniform mat2 CoordinateTransform;
uniform sampler2D DiskOpacityCurve;
uniform sampler2D MultiplyTexture;
uniform float RotationNoiseAmplitude = 0.0;

// Airbrush
uniform sampler2D FalloffCurve;
uniform float AlphaDensity;

// Per-segment state. Radii remain real brush radii; only vertex geometry is padded.
// n0/n1 are cumulative radius-normalized distance; x/y below are tangent/normal.
varying flat vec2 segmentStart;
varying flat float r0;
varying flat float r1;
varying flat float n0;
varying flat float n1;
varying flat float pressure0;
varying flat float pressure1;
varying vec2 meshPosition;

varying flat vec2 tangent;
varying flat vec2 normal;
varying flat float segmentLength;
varying flat float sinTheta;
varying flat float cosTheta;

struct FilteredCrossSection {
	float coverage;
	float sampleOffset;
};

// Segment geometry and radius-normalized stamp coordinates.
float x2n(float x) {
	if (abs(cosTheta) < 1e-7) return x / r0;
	return -1.0 / cosTheta * log(1.0 - cosTheta * x / r0);
}

float n2x(float n) {
	if (abs(cosTheta) < 1e-7) return n * r0;
	return r0 / cosTheta * (1.0 - exp(-cosTheta * n));
}

float centerlineRadiusAt(float x) {
	float clampedX = clamp(x, 0.0, segmentLength);
	return max(r0 - cosTheta * clampedX, 1e-7);
}

// Center of the generating disk nearest to the tapered boundary, clamped to the caps.
float nearestDiskX(vec2 localPosition) {
	return clamp(localPosition.x - abs(localPosition.y) * cosTheta / sinTheta,
		0.0, segmentLength);
}

// Centerline positions t whose disks contain sampleLocal:
// (sampleLocal.x - t)^2 + sampleLocal.y^2 <= (r0 - cosTheta * t)^2.
// Returns an unclamped x interval, not stamp indices, and whether it overlaps the segment.
bool findCoveringCenterlineInterval(vec2 sampleLocal, out vec2 centerlineXInterval) {
	float a = sinTheta * sinTheta;
	float radiusAtSampleX = r0 - cosTheta * sampleLocal.x;
	// Solve around the sample's x coordinate.  b*b - 4*a*c loses the
	// subpixel radius when x*x is large, even for a constant-width line.
	float discriminant = radiusAtSampleX * radiusAtSampleX - a * sampleLocal.y * sampleLocal.y;
	if (discriminant <= 0.0) return false;
	float intervalMidpointX = sampleLocal.x - cosTheta * radiusAtSampleX / a;
	float intervalHalfLength = sqrt(discriminant) / a;
	centerlineXInterval = vec2(intervalMidpointX - intervalHalfLength, intervalMidpointX + intervalHalfLength);
	return centerlineXInterval.y > 0.0 && centerlineXInterval.x < segmentLength;
}

// Shared brush inputs.
bool hasFlag(int flags, int flag) {
	return (flags & flag) != 0;
}

float sampleCurve(sampler2D curve, float t) {
	return textureLod(curve, vec2(t, 0), 0.0).r;
}

float pressureFlowRatio(float centerlineX) {
	float pressure = clamp(mix(pressure0, pressure1, centerlineX / segmentLength), 0.0, 1.0);
	return hasFlag(ActiveBrushFlags, BrushFlags.Pressure2FlowCurve)
		? sampleCurve(Pressure2FlowCurve, pressure) : 1.0;
}

bool isDashGap(float sampleX) {
	if (!hasFlag(ActiveBrushFlags, BrushFlags.Dash)) return false;
	float unitLength = DashLength + (GapLength > 0.0 ? GapLength : DashLength);
	float pixelN = x2n(sampleX) + n0;
	return mod(pixelN - TIME * DashForwardSpeed, unitLength) > DashLength;
}

// Gaussian filter, coverage and representative brush positions.
// Standard normal CDF approximation.
float normalCDF(float x) {
	float t = 1.0 / (1.0 + 0.2316419 * abs(x));
	float tail = 0.3989422804 * exp(-0.5 * x * x) * t
		* (0.319381530 + t * (-0.356563782 + t * (1.781477937
		+ t * (-1.821255978 + t * 1.330274429))));
	return x >= 0.0 ? 1.0 - tail : tail;
}

float gaussianFilterCDF(float z, float unitsPerPixel) {
	float support = FilterSupportPixels * unitsPerPixel;
	if (z <= -support) return 0.0;
	if (z >= support) return 1.0;
	return clamp((normalCDF(z / (FilterSigmaPixels * unitsPerPixel))
		- GaussianTail) / GaussianMass, 0.0, 1.0);
}

float gaussianFilterIntegral(float z, float unitsPerPixel) {
	float support = FilterSupportPixels * unitsPerPixel;
	if (z <= -support) return 0.0;
	if (z >= support) return z;
	float sigma = FilterSigmaPixels * unitsPerPixel;
	float x = z / sigma;
	float boundaryDensity = 0.001542278996; // phi(2 / 0.6)
	return (z * (normalCDF(x) - GaussianTail)
		+ sigma * (0.3989422804 * exp(-0.5 * x * x) - boundaryDensity)) / GaussianMass;
}

float gaussianFilterQuantile(float weight, float unitsPerPixel) {
	float e = 2.0 * (GaussianTail + GaussianMass * weight) - 1.0;
	// Winitzki inverse-erf approximation; rationalization avoids cancellation
	// around the center. Truncated tails keep log's argument strictly positive.
	float l = log(1.0 - e * e);
	float a = 2.0 / (PI * 0.147) + 0.5 * l;
	float inverseErf = sign(e) * sqrt((-l / 0.147) / (sqrt(a * a - l / 0.147) + a));
	return sqrt(2.0) * FilterSigmaPixels * unitsPerPixel * inverseErf;
}

FilteredCrossSection gaussianCrossSection(float offset, float radius, float unitsPerPixel) {
	float beginCDF = gaussianFilterCDF(-radius - offset, unitsPerPixel);
	float endCDF = gaussianFilterCDF(radius - offset, unitsPerPixel);
	float sampleOffset = offset + gaussianFilterQuantile(0.5 * (beginCDF + endCDF), unitsPerPixel);
	return FilteredCrossSection(endCDF - beginCDF, clamp(sampleOffset, -radius, radius));
}

FilteredCrossSection diskCrossSection(vec2 offset, float radius, vec2 localDx, vec2 localDy) {
	float distanceToCenter = length(offset);
	vec2 direction = distanceToCenter > 1e-7 ? offset / distanceToCenter : vec2(0.0, 1.0);
	float unitsPerPixel = length(vec2(dot(direction, localDx), dot(direction, localDy)));
	return gaussianCrossSection(distanceToCenter, radius, unitsPerPixel);
}

float filteredDiskCoverage(vec2 offset, float radius, vec2 localDx, vec2 localDy) {
	FilteredCrossSection crossSection = diskCrossSection(offset, radius, localDx, localDy);
	float distanceToCenter = length(offset);
	// A subpixel disk also shrinks along the perpendicular direction.
	vec2 perpendicular = distanceToCenter > 1e-7 ? vec2(-offset.y, offset.x) / distanceToCenter : vec2(1.0, 0.0);
	float unitsPerPixel = length(vec2(dot(perpendicular, localDx), dot(perpendicular, localDy)));
	return crossSection.coverage * (gaussianFilterCDF(radius, unitsPerPixel) - gaussianFilterCDF(-radius, unitsPerPixel));
}

float realStrokeCoverage(vec2 pixelLocal, vec2 localDx, vec2 localDy, out vec2 brushLocal) {
	// The nearest generating disk selects the tangent boundary or a circular cap.
	float centerX = nearestDiskX(pixelLocal);
	float radius = centerlineRadiusAt(centerX);
	vec2 offset = pixelLocal - vec2(centerX, 0.0);
	FilteredCrossSection crossSection = diskCrossSection(offset, radius, localDx, localDy);
	float distanceToCenter = length(offset);
	brushLocal = vec2(centerX, 0.0) + offset * (crossSection.sampleOffset / max(distanceToCenter, 1e-7));
	// Short dots have finite support in the longitudinal direction too.
	float unitsPerPixel = length(vec2(localDx.x, localDy.x));
	float longitudinalCoverage = gaussianFilterCDF(segmentLength + r1 - pixelLocal.x, unitsPerPixel)
		- gaussianFilterCDF(-r0 - pixelLocal.x, unitsPerPixel);
	return crossSection.coverage * longitudinalCoverage;
}

float filteredSegmentFraction(vec2 interval, float fullLength, float unitsPerPixel) {
	// fullLength can be supplied directly as twice the root extent, retaining
	// tiny intervals whose absolute endpoint coordinates are much larger.
	float beforeStart = gaussianFilterIntegral(-interval.x, unitsPerPixel)
		- gaussianFilterIntegral(-interval.y, unitsPerPixel);
	float afterEnd = gaussianFilterIntegral(interval.y - segmentLength, unitsPerPixel)
		- gaussianFilterIntegral(interval.x - segmentLength, unitsPerPixel);
	return clamp((fullLength - beforeStart - afterEnd) / max(fullLength, 1e-7), 0.0, 1.0);
}

// Contributions use premultiplied RGB for Normal/Add, destination multipliers
// for Multiply/Erase. Encoding for Godot's canvas blend state happens only at output.
vec4 emptySample() {
#if defined(BLEND_MUL) || defined(BLEND_ERASE)
	return vec4(1.0); // Multiplication by one leaves the destination unchanged.
#else
	return vec4(0.0);
#endif
}

vec4 opacityContribution(vec3 color, float opacity) {
#if defined(BLEND_MUL)
	return vec4(mix(vec3(1.0), color, opacity), 1.0);
#elif defined(BLEND_ERASE)
	float keep = 1.0 - opacity;
	return vec4(vec3(keep), keep);
#else
	return vec4(color * opacity, opacity);
#endif
}

float filteredKeep(float depth, float coverage, float fraction) {
	// Split fully covered dense contributions before exp() underflows.
	return coverage >= 1.0 ? exp(-depth * fraction)
		: pow(mix(1.0, exp(-depth), coverage), fraction);
}

vec3 filteredColorKeep(vec3 depth, float coverage, vec3 fraction) {
	return coverage >= 1.0 ? exp(-depth * fraction)
		: pow(mix(vec3(1.0), exp(-depth), coverage), fraction);
}

vec4 canvasColor(vec4 contribution) {
#if defined(BLEND_MUL) || defined(BLEND_ERASE)
	return contribution;
#elif defined(BLEND_ADD)
	float addAlpha = sqrt(max(contribution.a, 0.0));
	return addAlpha > 0.0 ? vec4(contribution.rgb / addAlpha, addAlpha) : vec4(0.0);
#else
	return contribution.a > 0.0 ? vec4(contribution.rgb / contribution.a, contribution.a) : vec4(0.0);
#endif
}

// Brush evaluation.
vec4 evaluateVanilla(vec2 pixelLocal, vec2 localDx, vec2 localDy, float geometricCoverage, float flowRatio) {
	vec4 brush = OverridingColor.a >= 0.0 ? OverridingColor : MaterialColor;
	float targetFlow = brush.a * flowRatio;
	float coverage = targetFlow * geometricCoverage;
	// Adjacent segments share endpoint disks. Split their transmittance
	// continuously across both the interior and the Gaussian fringe.
	float startCoverage = min(geometricCoverage, filteredDiskCoverage(pixelLocal, r0, localDx, localDy));
	float endCoverage = min(geometricCoverage, filteredDiskCoverage(pixelLocal - vec2(segmentLength, 0.0), r1, localDx, localDy));
	float jointKeep = sqrt((1.0 - targetFlow * startCoverage) * (1.0 - targetFlow * endCoverage));
	if (targetFlow >= 0.999 && segmentLength < r0 + r1) {
		jointKeep /= max(1.0 - targetFlow * min(startCoverage, endCoverage), 1e-7);
	}
	coverage = 1.0 - (1.0 - coverage) / max(jointKeep, 1e-7);
	coverage = clamp(coverage, 0.0, 1.0);
	return opacityContribution(brush.rgb, coverage);
}

float stampRotationNoise(float stampIndex) {
	float seed = floor(stampIndex);
	float noise = fract(sin(seed * 127.1 + 311.7) * 43758.5453123) * 2.0 - 1.0;
	return noise * RotationNoiseAmplitude;
}

float sampleStamp(vec2 brushLocal, vec2 localDx, vec2 localDy, float stampIndex, float flow) {
	float stampN = stampIndex * StampInterval;
	float stampX = n2x(stampN - n0);
	float stampRadius = r0 - cosTheta * stampX;
	vec2 stampOffset = brushLocal - vec2(stampX, 0.0);
	float rotationNoise = hasFlag(ActiveStampFlags, StampFlags.RotationNoise) ? stampRotationNoise(stampIndex) : 0.0;
	float cosRot = cos(rotationNoise);
	float sinRot = sin(rotationNoise);
	mat2 stampTransform = CoordinateTransform * mat2(vec2(cosRot, sinRot), vec2(-sinRot, cosRot));
	stampOffset *= stampTransform;

	vec2 uv = (stampOffset / stampRadius + 1.0) / 2.0;
	// Explicit gradients keep mip selection defined across the varying stamp loop.
	// Each stamp has one radius, independent of the fragment sampling it.
	vec2 uvDx = 0.5 * (localDx * stampTransform) / stampRadius;
	vec2 uvDy = 0.5 * (localDy * stampTransform) / stampRadius;
	float textureMask = hasFlag(ActiveStampFlags, StampFlags.MaskTexture) ? textureGrad(MultiplyTexture, uv, uvDx, uvDy).r : 1.0;
	float normalizedRadius = length(stampOffset / stampRadius);
	float dotMask = hasFlag(ActiveStampFlags, StampFlags.MaskDisk) ? sampleCurve(DiskOpacityCurve, normalizedRadius) : 1.0;
	float stampSample = hasFlag(ActiveStampFlags, StampFlags.StampTexture) ? textureGrad(StampTexture, uv, uvDx, uvDy).r : 1.0;
	bool outsideDisk = normalizedRadius > 1.0;
	return outsideDisk ? 0.0 : clamp(textureMask * dotMask * flow * stampSample, 0.0, 1.0);
}

vec4 evaluateStamp(vec2 brushLocal, vec2 localDx, vec2 localDy, float geometricCoverage, float flowRatio) {
	// Query only real stamps covering the representative point.
	// The shared stroke filter supplies coverage; gaps remain empty.
	vec2 centerlineXInterval;
	if (!findCoveringCenterlineInterval(brushLocal, centerlineXInterval)) return emptySample();

	float startN = centerlineXInterval.x < 0.0 ? n0 : x2n(centerlineXInterval.x) + n0;
	float startStampIdx = startN / StampInterval;
	float endN = centerlineXInterval.y > segmentLength ? n1 : x2n(centerlineXInterval.y) + n0;
	float endStampIdx = endN / StampInterval;

	int stampCount = 0;
	float stampIndex = ceil(startStampIdx);
#if defined(BLEND_MUL)
	vec3 accumulatedMultiplier = vec3(1.0);
#else
	float accumulatedAlpha = 0.0;
#endif
	float flow = MaterialColor.a * flowRatio;
	while (stampCount < MaxStampsPerFragment && stampIndex < endStampIdx) {
		float sampledAlpha = sampleStamp(brushLocal, localDx, localDy, stampIndex, flow);
#if defined(BLEND_ADD)
		accumulatedAlpha += sampledAlpha;
#elif defined(BLEND_MUL)
		accumulatedMultiplier *= mix(vec3(1.0), MaterialColor.rgb, sampledAlpha);
#else
		accumulatedAlpha = sampledAlpha + accumulatedAlpha * (1.0 - sampledAlpha);
#endif
		stampCount++;
		stampIndex += 1.0;
	}
#if defined(BLEND_MUL)
	return vec4(mix(vec3(1.0), accumulatedMultiplier, geometricCoverage), 1.0);
#elif defined(BLEND_ADD)
	return opacityContribution(MaterialColor.rgb, max(accumulatedAlpha, 0.0) * geometricCoverage);
#else
	return opacityContribution(MaterialColor.rgb, clamp(accumulatedAlpha, 0.0, 1.0) * geometricCoverage);
#endif
}

// Split filtered transmittance by centerline contribution so straight segments
// with constant radius and flow stay consistent under subdivision.
vec4 evaluateAirbrush(vec2 pixelLocal, vec2 localDx, vec2 localDy) {
	if (isDashGap(pixelLocal.x)) return emptySample();
	float sampleX = clamp(pixelLocal.x, 0.0, segmentLength);
	float radius = centerlineRadiusAt(sampleX);
	float flow = MaterialColor.a * pressureFlowRatio(sampleX);
	float a = sinTheta * sinTheta;
	float halfWidth = radius / sinTheta;
	float normalFootprint = length(vec2(localDx.y, localDy.y));
	float tangentFootprint = length(vec2(localDx.x, localDy.x));
	FilteredCrossSection crossSection = gaussianCrossSection(pixelLocal.y, halfWidth, normalFootprint);
	float coveredWeight = crossSection.coverage;
	if (coveredWeight <= 0.0) return emptySample();
	float y = crossSection.sampleOffset;
	float extent = sqrt(max(radius * radius - a * y * y, 0.0)) / a;
	float midpoint = pixelLocal.x - cosTheta * radius / a;
	float left = midpoint - extent;
	float right = midpoint + extent;
	float fullLength = 2.0 * extent;
	float footprintHalfLength = FilterSupportPixels * tangentFootprint;
	if (right <= -footprintHalfLength || left >= segmentLength + footprintHalfLength) return emptySample();
	float segmentFraction = filteredSegmentFraction(vec2(left, right), fullLength, tangentFootprint);
	float fullN = fullLength / radius;
	if (abs(cosTheta) >= 1e-3) {
		float leftRadius = radius - cosTheta * (-cosTheta * radius / a - extent);
		float rightRadius = radius - cosTheta * (-cosTheta * radius / a + extent);
		fullN = log(leftRadius / rightRadius) / cosTheta;
	}
	float depth = fullN * AlphaDensity * flow * sampleCurve(FalloffCurve, abs(y) / halfWidth);
#if defined(BLEND_ADD)
	float additive = coveredWeight * depth * segmentFraction;
	return vec4(MaterialColor.rgb * additive, additive);
#elif defined(BLEND_MUL)
	vec3 colorDepth = depth * (vec3(1.0) - MaterialColor.rgb);
	vec3 keep = filteredColorKeep(colorDepth, coveredWeight, vec3(segmentFraction));
	return vec4(keep, 1.0);
#else
	float keep = filteredKeep(depth, coveredWeight, segmentFraction);
#if defined(BLEND_ERASE)
	return vec4(vec3(keep), keep);
#else
	float alpha = 1.0 - keep;
	return vec4(MaterialColor.rgb * alpha, alpha);
#endif
#endif
}

// Shader entry points.
void vertex() {
	float canvasScale = length(mat3(CANVAS_MATRIX) * vec3(1.0, 0.0, 0.0));
	segmentStart = INSTANCE_TRANSFORM[0].xy;
	vec2 segmentEnd = INSTANCE_TRANSFORM[1].xy;
	r0 = max(INSTANCE_TRANSFORM[3].x, 1e-7);
	r1 = max(INSTANCE_TRANSFORM[3].y, 1e-7);
	n0 = INSTANCE_CUSTOM.x;
	n1 = INSTANCE_CUSTOM.y;
	pressure0 = INSTANCE_CUSTOM.z;
	pressure1 = INSTANCE_CUSTOM.w;

	if (RadiusMode == 1) {
		r0 /= canvasScale;
		r1 /= canvasScale;
		n0 *= canvasScale;
		n1 *= canvasScale;
	}
	segmentLength = distance(segmentEnd, segmentStart);
	cosTheta = (r0 - r1) / segmentLength;
	sinTheta = sqrt(1.0 - cosTheta * cosTheta);

	tangent = normalize(segmentEnd - segmentStart);
	normal = vec2(-tangent.y, tangent.x);

	mat2 localToScreen = mat2((CANVAS_MATRIX * MODEL_MATRIX)[0].xy,
		(CANVAS_MATRIX * MODEL_MATRIX)[1].xy);
	mat2 screenToLocal = inverse(localToScreen);
	float pixelsPerLocalUnit = 1.0 / length(transpose(screenToLocal) * normal);
	float endpointDiameter0Px = 2.0 * r0 * pixelsPerLocalUnit;
	float endpointDiameter1Px = 2.0 * r1 * pixelsPerLocalUnit;
	bool tooSmall = max(endpointDiameter0Px, endpointDiameter1Px) < CullScreenDiameter;

	vec2 position = vec2[](segmentStart, segmentStart, segmentEnd, segmentEnd)[VERTEX_ID];
	vec2 offsetSign = vec2[](
		vec2(-1.0, -1.0),
		vec2(-1.0, 1.0),
		vec2(1.0, -1.0),
		vec2(1.0, 1.0)
	)[VERTEX_ID];
	float endpointRadius = vec4(r0, r0, r1, r1)[VERTEX_ID];
	// The largest inverse scale covers the filter even under nonuniform scale.
	float trace = dot(screenToLocal[0], screenToLocal[0]) + dot(screenToLocal[1], screenToLocal[1]);
	float det = determinant(screenToLocal);
	float maximumUnitsPerPixel = sqrt(0.5 * (trace + sqrt(max(trace * trace - 4.0 * det * det, 0.0))));
	float geometryRadius = endpointRadius + FilterSupportPixels * maximumUnitsPerPixel;

	float tanHalfTheta = sqrt((1.0 + cosTheta) / (1.0 - cosTheta));
	float cotHalfTheta = 1.0 / tanHalfTheta;
	float normalExtentRatio = vec4(tanHalfTheta, tanHalfTheta, cotHalfTheta, cotHalfTheta)[VERTEX_ID];
	bool illConditioned = normalExtentRatio > 10.0 || normalExtentRatio < 0.1;

	if (!illConditioned && !tooSmall) {
		vec2 offset = offsetSign.x * geometryRadius * tangent
			+ offsetSign.y * geometryRadius * normal * normalExtentRatio;
		meshPosition = position + offset;
	} else {
		meshPosition = vec2(0.0);
	}

	VERTEX = (MODEL_MATRIX * vec4(meshPosition, 0.0, 1.0)).xy;
}

void fragment() {
	// Derivatives use the original position even when brush evaluation moves
	// inside the shape. Stamp texture LOD keeps this same screen footprint.
	vec2 positionDx = dFdx(meshPosition);
	vec2 positionDy = dFdy(meshPosition);
	vec2 localDx = vec2(dot(positionDx, tangent), dot(positionDx, normal));
	vec2 localDy = vec2(dot(positionDy, tangent), dot(positionDy, normal));
	vec2 pixelLocal = vec2(dot(meshPosition - segmentStart, tangent), dot(meshPosition - segmentStart, normal));

	vec4 contribution = emptySample();
	if (StrokeType == Airbrush) {
		contribution = evaluateAirbrush(pixelLocal, localDx, localDy);
	} else {
		vec2 brushLocal;
		float coverage = realStrokeCoverage(pixelLocal, localDx, localDy, brushLocal);
		if (coverage <= 0.0 || isDashGap(brushLocal.x)) discard;
		float centerlineX = brushLocal.x - abs(brushLocal.y) * cosTheta / sinTheta;
		float flowRatio = pressureFlowRatio(centerlineX);
		if (StrokeType == Vanilla) {
			contribution = evaluateVanilla(pixelLocal, localDx, localDy, coverage, flowRatio);
		} else if (StrokeType == Stamp) {
			contribution = evaluateStamp(brushLocal, localDx, localDy, coverage, flowRatio);
		}
	}
	COLOR = canvasColor(contribution);
}
