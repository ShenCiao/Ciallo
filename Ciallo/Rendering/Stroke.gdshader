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
// Viewport pixels, measured from the radius interpolated by the vertex shader.
// Widths between the two thresholds fade in.
uniform float MinimumScreenDiameter : hint_range(0.0, 4.0, 0.05) = 0.1;
uniform float FullOpacityScreenDiameter : hint_range(0.0, 8.0, 0.05) = 1.0;
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
varying float r;

varying flat vec2 tangent;
varying flat vec2 normal;
varying flat float segmentLength;
varying flat float sinTheta;
varying flat float cosTheta;
varying flat float tanTheta;
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
	tanTheta = abs(cosTheta) < 1e-5 ? 1e10 * sign(cosTheta) : sinTheta/cosTheta;
	if(abs(tanTheta) < 1e-5) tanTheta = 1e10;

	tangent = normalize(p1 - p0);
	normal = vec2(-tangent.y, tangent.x);

	vec2 position = vec2[](p0, p0, p1, p1)[VERTEX_ID];
	vec2 offsetSign = vec2[](
        vec2(-1.0, -1.0),
        vec2(-1.0, 1.0),
        vec2(1.0, -1.0),
        vec2(1.0, 1.0)
    )[VERTEX_ID];
	r = vec4(r0, r0, r1, r1)[VERTEX_ID];

	float tanHalfTheta = sqrt((1.0+cosTheta) / (1.0-cosTheta));
    float cotHalfTheta = 1.0 / tanHalfTheta;
    float normalTanValue = vec4(tanHalfTheta, tanHalfTheta, cotHalfTheta, cotHalfTheta)[VERTEX_ID];
    bool isIll = normalTanValue > 10.0 || normalTanValue < 0.1;

	if(!isIll)
	{
		vec2 offset = offsetSign.x * r * tangent + offsetSign.y * r * normal * normalTanValue;
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

bool solveTheQuadraticEq(vec2 pLocal, out vec2 result){
	float a, b, c, delta;
    a = 1.0 - cosTheta * cosTheta;
    b = 2.0 * (r0 * cosTheta - pLocal.x);
    c = pLocal.x * pLocal.x + pLocal.y * pLocal.y - r0 * r0;
    delta = b * b - 4.0*a*c;
    if(delta <= 0.0) return false;

	float tempMathBlock = b + (b < 0.0 ? -1.0 : 1.0) * sqrt(delta);
    float x1 = -2.0 * c / tempMathBlock;
    float x2 = -tempMathBlock / (2.0*a);
    result = vec2(min(x1, x2), max(x1, x2));
	return result.y > 0.0 && result.x < segmentLength;
}

vec4 emptySample() {
#if defined(BLEND_MUL) || defined(BLEND_ERASE)
	return vec4(1.0); // Multiplication by one leaves the destination unchanged.
#else
	return vec4(0.0);
#endif
}

// Return the contribution before framebuffer blending: premultiplied color for
// Normal/Add, or a destination multiplier for Multiply/Erase.
vec4 evaluateBrush(vec2 samplePosition, float sampleRadius,
		vec2 localDx, vec2 localDy, vec2 radiusGradient, float visibility) {
	vec2 pLocal = vec2(dot(samplePosition-p0, tangent), dot(samplePosition-p0, normal));
	float d0 = distance(samplePosition, p0);
    float d1 = distance(samplePosition, p1);
	float cosPhi0 = pLocal.x / d0;
    float cosPhi1 = (pLocal.x - segmentLength) / d1;
	vec2 p_r = pLocal - vec2(abs(pLocal.y)/tanTheta, 0);
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
		if(cosPhi0 < cosTheta) {
			if(d0 > r0) return emptySample();
		} else if(cosPhi1 > cosTheta) {
			if(d1 > r1) return emptySample();
		}

		vec4 brush = OverridingColor.a >= 0.0 ? OverridingColor : MaterialColor;
		float targetFlow = brush.a * flowRatio;
		// Fade before the joint correction so two overlapping caps still reach
		// the faded target opacity. Keep the brush's branch selection unchanged.
		float coverage = targetFlow * visibility;
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
 		// The quadratic equation
	    vec2 result;
		if(!solveTheQuadraticEq(pLocal, result)) return emptySample();
		float x1 = result.x;
		float x2 = result.y;

 		float startN = x1 < 0.0 ? n0 : x2n(x1) + n0;
		float startStampIdx = startN / StampInterval;
 		float endN = x2 > segmentLength ? n1 : x2n(x2) + n0;
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
 			vec2 pToCurrStamp = pLocal - vec2(currStampLocalX, 0.0);
			// Rotation noise
			float rotationNoise = hasFlag(ActiveStampFlags, StampFlags.RotationNoise) ? noise1D(currStampIdx) : 0.0;
			float cosRot = cos(rotationNoise);
			float sinRot = sin(rotationNoise);
			mat2 stampTransform = CoordinateTransform * mat2(vec2(cosRot, sinRot), vec2(-sinRot, cosRot));
			pToCurrStamp *= stampTransform;

			vec2 uv = (pToCurrStamp/sampleRadius + 1.0)/2.0;
			// Explicit gradients keep mip selection defined across the varying stamp loop.
			vec2 uvDx = 0.5 * (localDx * stampTransform - pToCurrStamp * radiusGradient.x / sampleRadius) / sampleRadius;
			vec2 uvDy = 0.5 * (localDy * stampTransform - pToCurrStamp * radiusGradient.y / sampleRadius) / sampleRadius;
			float textureMask = hasFlag(ActiveStampFlags, StampFlags.MaskTexture) ? textureGrad(MultiplyTexture, uv, uvDx, uvDy).r : 1.0;
			float nr = length(pToCurrStamp/sampleRadius); // normalized r
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
		return mix(vec4(1.0), currMul, visibility);
#elif defined(BLEND_ADD)
		float addAmount = max(currA, 0.0) * visibility;
		return vec4(MaterialColor.rgb * addAmount, addAmount);
#elif defined(BLEND_ERASE)
		float keep = 1.0 - clamp(currA, 0.0, 1.0) * visibility;
		return vec4(vec3(keep), keep);
#else
		float alpha = clamp(currA, 0.0, 1.0) * visibility;
		return vec4(MaterialColor.rgb * alpha, alpha);
#endif
 	}
	/*----------------------------------------------------------*/
	if(StrokeType == Airbrush){
		if(cosPhi0 < cosTheta && d0 > r0) return emptySample();
		if(cosPhi1 > cosTheta && d1 > r1) return emptySample();
		float flow = MaterialColor.a * flowRatio;
		vec2 result;
		if(!solveTheQuadraticEq(pLocal, result)) return emptySample();
		float x1 = result.x;
		float x2 = result.y;
		float startN = x1 < 0.0 ? 0.0 : x2n(x1);
		float endN = x2 > segmentLength ? (n1 - n0) : x2n(x2);
		float deltaN = endN - startN;

		float lambda = abs(pLocal.y) / sinTheta / r_p;
		float curveFall = sampleCurve(FalloffCurve, lambda);
		float tau = deltaN * AlphaDensity * flow * curveFall;
#if defined(BLEND_ADD)
		float addAmount = max(tau, 0.0) * visibility;
		return vec4(MaterialColor.rgb * addAmount, addAmount);
#elif defined(BLEND_MUL)
		return vec4(mix(vec3(1.0), exp(-tau * (vec3(1.0) - MaterialColor.rgb)), visibility), 1.0);
#elif defined(BLEND_ERASE)
		float keep = 1.0 - (1.0 - exp(-tau)) * visibility;
		return vec4(vec3(keep), keep);
#else
		float keep = exp(-tau);
		float A = (1.0 - keep) * visibility;
		return vec4(MaterialColor.rgb * A, A);
#endif
	}
	return emptySample();
}

void fragment() {
	// Evaluate derivatives before the cutoff or any brush branches.
	vec2 positionDx = dFdx(p);
	vec2 positionDy = dFdy(p);
	vec2 radiusGradient = vec2(dFdx(r), dFdy(r));
	vec2 localDx = vec2(dot(positionDx, tangent), dot(positionDx, normal));
	vec2 localDy = vec2(dot(positionDy, tangent), dot(positionDy, normal));
	float unitsPerPixel = length(vec2(localDx.y, localDy.y));
	float screenDiameter = 2.0 * r / unitsPerPixel;
	if(screenDiameter <= MinimumScreenDiameter) discard;
	// Equal thresholds select a hard cutoff without undefined smoothstep edges.
	float visibility = FullOpacityScreenDiameter > MinimumScreenDiameter
		? smoothstep(MinimumScreenDiameter, FullOpacityScreenDiameter, screenDiameter)
		: 1.0;

	// Keep pixel-center derivatives above; use a covered location to evaluate the
	// brush so MSAA edge fragments do not sample outside the carrier geometry.
	vec2 brushPosition = interpolateAtCentroid(p);
	float brushRadius = interpolateAtCentroid(r);
	vec4 contribution = evaluateBrush(brushPosition, brushRadius, localDx, localDy, radiusGradient, visibility);

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
