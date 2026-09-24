// Ciallo's articulated line shader!
shader_type canvas_item;
render_mode unshaded;
render_mode skip_vertex_transform;
// Godot doesn't support changing blend mode at runtime.
// VARIANT_DEFINES
#if defined(BLEND_ADD)
render_mode blend_add;
#elif defined(BLEND_MUL)
render_mode blend_mul;
#elif defined(BLEND_ERASE)
render_mode blend_mul;
#endif

// Warning: BLEND_MUL & BLEND_ADD here are implementing very wrong algorithm
// multiply's alpha need individual blend channel

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
    int Pressure2Radius;
};
const BrushFlag BrushFlags = BrushFlag(1 << 0, 1 << 1, 1 << 2);

const int Vanilla = 0, Stamp = 1, Airbrush = 2;
uniform int StrokeType : hint_enum("Vanilla", "Stamp", "Airbrush") = 0;
uniform int ActiveBrushFlags = 0;
uniform vec4 MaterialColor : source_color = vec4(0.0, 0.0, 0.0, 1.0);
instance uniform vec4 OverridingColor = vec4(0.0, 0.0, 0.0, -1.0);
// Canvas mode is used for drawing wireframe
uniform int RadiusMode : hint_enum("World", "Canvas") = 0;
// Diameters and padding are measured in render-target pixels.
uniform float CullScreenDiameter : hint_range(0.0, 0.25, 0.005) = 0.01;
const float ThinDiameter = 1.75;
const float UnfilteredDiameter = 2.5;
const float CenterlineDiameter = 0.75;
// Includes the filter support and MSAA sample locations around a pixel center.
const float CarrierPaddingPixels = 1.5;
// Dash
uniform float DashLength = -1.0; // radius-normalized length
uniform float GapLength = -1.0;
uniform float DashForwardSpeed = 0.0;
// Pen pressure map curve
uniform sampler2D Pressure2FlowCurve;

// ------stamp parameters-----------
//// Fail to compile:
// hint_range(intervalMin, 10.0, intervalStep) .
// const float intervalMin = 1.0/64.0;
// const float intervalStep = 1.0/128.0;
uniform int ActiveStampFlags;
uniform float StampInterval : hint_range(0.015625, 10.0, 0.0078125) = 0.2;
uniform sampler2D StampTexture;
uniform mat2 CoordinateTransform;
uniform sampler2D DiskOpacityCurve;
uniform sampler2D MultiplyTexture;
uniform float RotationNoiseAmplitude = 0; // `= PI` don't compile// ---------------------------------

uniform sampler2D FalloffCurve;
uniform float AlphaDensity;

varying flat vec2 p0;
varying flat vec2 p1;
varying flat float r0;
varying flat float r1;
varying flat float n0;
varying flat float n1;
varying flat float pp0; // pen pressure
varying flat float pp1;
varying vec2 p;
varying flat float filterWeight;
varying flat float carrierPadding;

varying flat vec2 tangent;
varying flat vec2 normal;
varying flat float segmentLength;
varying flat float sinTheta;
varying flat float cosTheta;
varying flat float scale;

bool hasFlag(int flags, int flag) {
	return (flags & flag) != 0;
}

void vertex() {
	mat3 canvas_linear = mat3(CANVAS_MATRIX);  // discard translation
    scale = length(canvas_linear * vec3(1.0, 0.0, 0.0));  // X-scale
	p0 = INSTANCE_TRANSFORM[0].xy;
	p1 = INSTANCE_TRANSFORM[1].xy;
	r0 = max(INSTANCE_TRANSFORM[3].x, 1e-7);
	r1 = max(INSTANCE_TRANSFORM[3].y, 1e-7);
	n0 = INSTANCE_CUSTOM.x;
	n1 = INSTANCE_CUSTOM.y;
	pp0 = INSTANCE_CUSTOM.z;
	pp1 = INSTANCE_CUSTOM.w;

	if(RadiusMode == 1){
		r0 /= scale;
		r1 /= scale;
		n0 *= scale;
		n1 *= scale;
	}
	segmentLength = distance(p1, p0);
	cosTheta = (r0 - r1)/distance(p0, p1);
	sinTheta = sqrt(1.0 - cosTheta * cosTheta);

	tangent = normalize(p1 - p0);
	normal = vec2(-tangent.y, tangent.x);

	mat2 localToScreen = mat2((CANVAS_MATRIX * MODEL_MATRIX)[0].xy,
		(CANVAS_MATRIX * MODEL_MATRIX)[1].xy);
	mat2 screenToLocal = inverse(localToScreen);
	float pixelsPerLocalUnit = 1.0 / length(transpose(screenToLocal) * normal);
	float endpointDiameter0Px = 2.0 * r0 * pixelsPerLocalUnit;
	float endpointDiameter1Px = 2.0 * r1 * pixelsPerLocalUnit;
	float minimumEndpointDiameterPx = min(endpointDiameter0Px, endpointDiameter1Px);

	filterWeight = 1.0 - smoothstep(ThinDiameter, UnfilteredDiameter, minimumEndpointDiameterPx);
	bool tooSmall = max(endpointDiameter0Px, endpointDiameter1Px) < CullScreenDiameter;

	vec2 position = vec2[](p0, p0, p1, p1)[VERTEX_ID];
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
	carrierPadding = CarrierPaddingPixels * filterWeight * maximumUnitsPerPixel;
	float geometryRadius = endpointRadius + carrierPadding;

	float tanHalfTheta = sqrt((1.0+cosTheta) / (1.0-cosTheta));
    float cotHalfTheta = 1.0 / tanHalfTheta;
    float normalTanValue = vec4(tanHalfTheta, tanHalfTheta, cotHalfTheta, cotHalfTheta)[VERTEX_ID];
    bool isIll = normalTanValue > 10.0 || normalTanValue < 0.1;

	if(!isIll && !tooSmall)
	{
		vec2 offset = offsetSign.x * geometryRadius * tangent
			+ offsetSign.y * geometryRadius * normal * normalTanValue;
		p = position + offset;
	} else {
		p = vec2(0.0);
	}

	VERTEX = (MODEL_MATRIX * vec4(p, 0.0, 1.0)).xy;
}

float x2n(float x) {
	if(abs(cosTheta) < 1e-7) return x/r0;
	return -1.0/cosTheta * log(1.0 - cosTheta*x/r0);
}
float n2x(float n) {
	if(abs(cosTheta) < 1e-7) return n*r0;
	return r0/cosTheta * (1.0-exp(-cosTheta*n));
}

float sampleCurve(sampler2D curve, float t){
	// Evaluate the curve at the fragment's brush coordinate.
	return textureLod(curve, vec2(t, 0), 0.0).r;
}

// ---------------------rotation noise--------------------
float hash1(float i) {
    return fract(sin(i * 127.1 + 311.7) * 43758.5453123) * 2.0 - 1.0;
}
float noise1D(float x) {
    return hash1(floor(x)) * RotationNoiseAmplitude;
}

// Find centerline positions t whose corresponding brush disk contains sampleLocal:
// (sampleLocal.x - t)^2 + sampleLocal.y^2 <= (r0 - cosTheta * t)^2.
// centerlineXInterval contains ordered segment-local x positions, not stamp indices.
// The interval is not clamped to [0, segmentLength]; Airbrush integrates it continuously.
// Return true when the interval overlaps the segment with nonzero length.
bool findCoveringCenterlineInterval(vec2 sampleLocal, out vec2 centerlineXInterval){
	float a = sinTheta * sinTheta;
	float radiusAtSampleX = r0 - cosTheta * sampleLocal.x;
	// Solve around the sample's x coordinate.  b*b - 4*a*c loses the
	// subpixel radius when x*x is large, even for a constant-width line.
	float discriminant = radiusAtSampleX * radiusAtSampleX - a * sampleLocal.y * sampleLocal.y;
	if(discriminant <= 0.0) return false;
	float intervalMidpointX = sampleLocal.x - cosTheta * radiusAtSampleX / a;
	float intervalHalfLength = sqrt(discriminant) / a;
	centerlineXInterval = vec2(intervalMidpointX - intervalHalfLength, intervalMidpointX + intervalHalfLength);
	return centerlineXInterval.y > 0.0 && centerlineXInterval.x < segmentLength;
}

vec4 emptySample() {
#if defined(BLEND_MUL) || defined(BLEND_ERASE)
	return vec4(1.0); // Multiplication by one leaves the destination unchanged.
#else
	return vec4(0.0);
#endif
}

float centerlineRadiusAt(float x) {
	float clampedX = clamp(x, 0.0, segmentLength);
	return max(r0 - cosTheta * clampedX, 1e-7);
}

// Center of the generating disk nearest to a point on the tapered boundary.
// Clamping selects the circular caps, including their slanted tangent joins.
float nearestDiskX(vec2 localPosition) {
	return clamp(localPosition.x - abs(localPosition.y) * cosTheta / sinTheta,
		0.0, segmentLength);
}

// Area CDF of a pixel square projected onto a local direction.  Unlike a
// single linear ramp, this retains the energy of oblique subpixel lines.
float pixelProjectionCDF(float z, vec2 projectedPixel) {
	vec2 dimensions = abs(projectedPixel);
	float a = max(dimensions.x, dimensions.y);
	float b = min(dimensions.x, dimensions.y);
	if(b < a * 1e-4) return clamp(z / a + 0.5, 0.0, 1.0);
	float h = 0.5 * (a + b);
	float x = abs(z);
	float tail;
	if(x >= h) tail = 0.0;
	else if(x <= 0.5 * (a - b)) tail = 0.5 - x / a;
	else tail = (h - x) * (h - x) / (2.0 * a * b);
	return z >= 0.0 ? 1.0 - tail : tail;
}

float realStrokeCoverage(vec2 localPosition, vec2 localDx, vec2 localDy) {
	float centerX = nearestDiskX(localPosition);
	float radius = centerlineRadiusAt(centerX);
	vec2 offset = localPosition - vec2(centerX, 0.0);
	float distanceToCenter = length(offset);
	vec2 direction = distanceToCenter > 1e-7 ? offset / distanceToCenter : vec2(0.0, 1.0);
	vec2 projectedPixel = vec2(dot(direction, localDx), dot(direction, localDy));
	// Integrate BOTH edges of a finite-width cross section.  A single smoothed
	// SDF edge would leave ~50% opacity when the true radius tends to zero.
	float coverage = pixelProjectionCDF(radius - distanceToCenter, projectedPixel)
		- pixelProjectionCDF(-radius - distanceToCenter, projectedPixel);
	// Short segments (including single-point strokes) have finite support in x too.
	float longitudinalFootprint = abs(localDx.x) + abs(localDy.x);
	return coverage * min(1.0, (segmentLength + r0 + r1) / longitudinalFootprint);
}

float carrierCoverage(vec2 localPosition, vec2 localDx, vec2 localDy) {
	vec2 upperNormal = vec2(cosTheta, sinTheta);
	vec2 lowerNormal = vec2(cosTheta, -sinTheta);
	float upper = pixelProjectionCDF(r0 + carrierPadding - dot(upperNormal, localPosition),
		vec2(dot(upperNormal, localDx), dot(upperNormal, localDy)));
	float lower = pixelProjectionCDF(r0 + carrierPadding - dot(lowerNormal, localPosition),
		vec2(dot(lowerNormal, localDx), dot(lowerNormal, localDy)));
	vec2 xFootprint = vec2(localDx.x, localDy.x);
	float ends = pixelProjectionCDF(localPosition.x + r0 + carrierPadding, xFootprint)
		- pixelProjectionCDF(localPosition.x - segmentLength - r1 - carrierPadding, xFootprint);
	return max(upper + lower - 1.0, 0.0) * ends;
}

// Return the contribution before framebuffer blending: premultiplied color for
// Normal/Add, or a destination multiplier for Multiply/Erase.
vec4 evaluateBrush(vec2 pLocal, vec2 localDx, vec2 localDy, float geometricCoverage) {
	float d0 = length(pLocal);
    float d1 = length(pLocal - vec2(segmentLength, 0.0));
	vec2 p_r = pLocal - vec2(abs(pLocal.y) * cosTheta / sinTheta, 0);
	float r_p = r0 - cosTheta * p_r.x;

	float t = mix(pp0, pp1, p_r.x/segmentLength);
	t = clamp(t, 0.0, 1.0);
	float flowRatio = hasFlag(ActiveBrushFlags, BrushFlags.Pressure2FlowCurve) ? sampleCurve(Pressure2FlowCurve, t) : 1.0;

	if(hasFlag(ActiveBrushFlags, BrushFlags.Dash)){
		float unitLength = DashLength + (GapLength > 0.0 ? GapLength : DashLength);
		float pixelN = x2n(pLocal.x) + n0;
		if(mod(pixelN - TIME * DashForwardSpeed, unitLength) > DashLength) return emptySample();
	}

	/*----------------------------------------------------------*/
	if(StrokeType == Vanilla){
		if(pLocal.x < cosTheta * d0) {
			if(d0 > r0) return emptySample();
		} else if(pLocal.x - segmentLength > cosTheta * d1) {
			if(d1 > r1) return emptySample();
		}

		vec4 brush = OverridingColor.a >= 0.0 ? OverridingColor : MaterialColor;
		float targetFlow = brush.a * flowRatio;
		float coverage = targetFlow * geometricCoverage;
		if(targetFlow < 0.999){ // Can avoid little white dot
			if(d0 < r0 && d1 < r1){
				return emptySample();
    		}
		}

		if(d0 < r0 || d1 < r1){
			coverage = 1.0 - sqrt(1.0 - coverage);
    	}
		coverage = clamp(coverage, 0.0, 1.0);
#if defined(BLEND_ADD)
		float addAmount = max(coverage, 0.0);
		return vec4(brush.rgb * addAmount, addAmount);
#elif defined(BLEND_MUL)
		return vec4(mix(vec3(1.0), brush.rgb, coverage), 1.0);
#elif defined(BLEND_ERASE)
		float keep = 1.0 - coverage;
		return vec4(vec3(keep), keep);
#else
		return vec4(brush.rgb * coverage, coverage);
#endif
	}
	/*----------------------------------------------------------*/
  	if(StrokeType == Stamp){
		vec2 centerlineXInterval;
		if(!findCoveringCenterlineInterval(pLocal, centerlineXInterval)) return emptySample();

		float startN = centerlineXInterval.x < 0.0 ? n0 : x2n(centerlineXInterval.x) + n0;
		float startStampIdx = startN / StampInterval;
		float endN = centerlineXInterval.y > segmentLength ? n1 : x2n(centerlineXInterval.y) + n0;
		float endStampIdx = endN / StampInterval;

 		int i = 0;
 		int MAX_i = 128;
 		float currStampIdx = ceil(startStampIdx);
#if defined(BLEND_MUL)
		vec4 currMul = vec4(1.0);
#else
		float currA = 0.0;
#endif
		float flow = MaterialColor.a * flowRatio;
		// Main loop
 		while(i < MAX_i && currStampIdx < endStampIdx){
			float currN = currStampIdx * StampInterval;
			float currStampLocalX = n2x(currN - n0);
			float stampRadius = r0 - cosTheta * currStampLocalX;
 			vec2 pToCurrStamp = pLocal - vec2(currStampLocalX, 0.0);
			// Rotation noise
			float rotationNoise = hasFlag(ActiveStampFlags, StampFlags.RotationNoise) ? noise1D(currStampIdx) : 0.0;
			float cosRot = cos(rotationNoise);
			float sinRot = sin(rotationNoise);
			mat2 stampTransform = CoordinateTransform * mat2(vec2(cosRot, sinRot), vec2(-sinRot, cosRot));
			pToCurrStamp *= stampTransform;

			vec2 uv = (pToCurrStamp/stampRadius + 1.0)/2.0;
			// Explicit gradients keep mip selection defined across the varying stamp loop.
			// Each stamp has one radius, independent of the fragment sampling it.
			vec2 uvDx = 0.5 * (localDx * stampTransform) / stampRadius;
			vec2 uvDy = 0.5 * (localDy * stampTransform) / stampRadius;
			float textureMask = hasFlag(ActiveStampFlags, StampFlags.MaskTexture) ? textureGrad(MultiplyTexture, uv, uvDx, uvDy).r : 1.0;
			float nr = length(pToCurrStamp/stampRadius); // normalized r
			float dotMask = hasFlag(ActiveStampFlags, StampFlags.MaskDisk) ? sampleCurve(DiskOpacityCurve, nr) : 1.0;
			float stampSample = hasFlag(ActiveStampFlags, StampFlags.StampTexture) ? textureGrad(StampTexture, uv, uvDx, uvDy).r : 1.0;
			bool isOutDisk = uv.x > 1.0 || uv.y > 1.0 || nr > 1.0;
			float sampledA = isOutDisk ? 0.0 : clamp(textureMask * dotMask * flow * stampSample, 0.0, 1.0);

#if defined(BLEND_ADD)
			currA += sampledA;
#elif defined(BLEND_MUL)
			currMul *= vec4(1.0 - MaterialColor.rgb, sampledA);
#else
			currA = sampledA + currA * (1.0 - sampledA);
#endif
			i++;
			currStampIdx += 1.0;
 		}
#if defined(BLEND_MUL)
		return mix(vec4(1.0), currMul, geometricCoverage);
#elif defined(BLEND_ADD)
		float addAmount = max(currA, 0.0) * geometricCoverage;
		return vec4(MaterialColor.rgb * addAmount, addAmount);
#elif defined(BLEND_ERASE)
		float keep = 1.0 - clamp(currA, 0.0, 1.0) * geometricCoverage;
		return vec4(vec3(keep), keep);
#else
		float alpha = clamp(currA, 0.0, 1.0) * geometricCoverage;
		return vec4(MaterialColor.rgb * alpha, alpha);
#endif
 	}
	/*----------------------------------------------------------*/
	if(StrokeType == Airbrush){
		if(pLocal.x < cosTheta * d0 && d0 > r0) return emptySample();
		if(pLocal.x - segmentLength > cosTheta * d1 && d1 > r1) return emptySample();
		float flow = MaterialColor.a * flowRatio;
		vec2 centerlineXInterval;
		if(!findCoveringCenterlineInterval(pLocal, centerlineXInterval)) return emptySample();
		float startN = centerlineXInterval.x < 0.0 ? 0.0 : x2n(centerlineXInterval.x);
		float endN = centerlineXInterval.y > segmentLength ? (n1 - n0) : x2n(centerlineXInterval.y);
		float deltaN = endN - startN;

		float lambda = abs(pLocal.y) / sinTheta / r_p;
		float curveFall = sampleCurve(FalloffCurve, lambda);
		float tau = deltaN * AlphaDensity * flow * curveFall;
#if defined(BLEND_ADD)
		float addAmount = max(tau, 0.0) * geometricCoverage;
		return vec4(MaterialColor.rgb * addAmount, addAmount);
#elif defined(BLEND_MUL)
		return vec4(mix(vec3(1.0), exp(-tau * (vec3(1.0) - MaterialColor.rgb)), geometricCoverage), 1.0);
#elif defined(BLEND_ERASE)
		float keep = 1.0 - (1.0 - exp(-tau)) * geometricCoverage;
		return vec4(vec3(keep), keep);
#else
		float keep = exp(-tau);
		float A = (1.0 - keep) * geometricCoverage;
		return vec4(MaterialColor.rgb * A, A);
#endif
	}
	return emptySample();
}

void fragment() {
	vec2 positionDx = dFdx(p);
	vec2 positionDy = dFdy(p);
	vec2 localDx = vec2(dot(positionDx, tangent), dot(positionDx, normal));
	vec2 localDy = vec2(dot(positionDy, tangent), dot(positionDy, normal));
	float unitsPerPixel = length(vec2(localDx.y, localDy.y));

	// Full filtering integrates around the pixel center.  As padding shrinks,
	// return continuously to the covered MSAA location of the unexpanded path.
	vec2 samplePosition = mix(interpolateAtCentroid(p), p, filterWeight);
	vec2 brushLocal = vec2(dot(samplePosition - p0, tangent), dot(samplePosition - p0, normal));
	float geometricCoverage = 1.0;
	if(filterWeight > 0.0) {
		vec2 pixelLocal = vec2(dot(p - p0, tangent), dot(p - p0, normal));
		geometricCoverage = realStrokeCoverage(pixelLocal, localDx, localDy);
		// When padding shrinks, MSAA already supplies part of the coverage.
		// Normalize by the carrier's coverage to avoid applying it twice.
		if(filterWeight < 1.0) {
			geometricCoverage = min(geometricCoverage / max(carrierCoverage(pixelLocal, localDx, localDy), 1e-7), 1.0);
		}
		if(geometricCoverage <= 0.0) discard;

		float screenDiameter = 2.0 * centerlineRadiusAt(brushLocal.x) / unitsPerPixel;
		float yWeight = smoothstep(CenterlineDiameter, ThinDiameter, screenDiameter);
		// Only brush evaluation is moved; coverage always uses the true position.
		brushLocal.y *= yWeight;

		// A filtered pixel may have its center outside the real shape.  Sample
		// a representative point inside its nearest disk, keeping x in the body.
		float diskX = nearestDiskX(brushLocal);
		float diskRadius = centerlineRadiusAt(diskX);
		vec2 diskOffset = brushLocal - vec2(diskX, 0.0);
		float distanceToDisk = length(diskOffset);
		if(distanceToDisk > diskRadius * 0.999) {
			brushLocal = vec2(diskX, 0.0) + diskOffset * (diskRadius * 0.999 / distanceToDisk);
		}
	}

	// Keep the original pixel footprint for texture LOD, even when evaluating
	// at the centerline.  Collapsing texture gradients would alias tiny stamps.
	vec4 contribution = evaluateBrush(brushLocal, localDx, localDy, geometricCoverage);

#if defined(BLEND_MUL) || defined(BLEND_ERASE)
	COLOR = contribution;
#elif defined(BLEND_ADD)
	float addAlpha = sqrt(max(contribution.a, 0.0));
	COLOR = addAlpha > 0.0 ? vec4(contribution.rgb / addAlpha, addAlpha) : vec4(0.0);
#else
	COLOR = contribution.a > 0.0 ? vec4(contribution.rgb / contribution.a, contribution.a) : vec4(0.0);
#endif

	// Be mindful with premultiply alpha
	// See: https://github.com/godotengine/godot/issues/99715
	//
	// Multiply and eraser variants use blend_mul: result = SRC * DST.
	// Godot's canvas framebuffer stores premultiplied alpha: a pixel with
	// straight color C and alpha a is stored as (C*a, C*a, C*a, a).
	// Eraser therefore multiplies both RGB and alpha by the same keep factor.
}
