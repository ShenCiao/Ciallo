#version 460

layout(location = 0) in vec4 fragColor;
layout(location = 1) in flat vec2 p0;
layout(location = 2) in flat vec2 p1;
layout(location = 3) in vec2 p;
layout(location = 4) in float radius; // radius value across the current pixel
layout(location = 5) in flat vec2 summedLength;
layout(location = 6) in flat vec2 flatRadius;

layout(location = 0) out vec4 outColor;

// #define STAMP
// #define AIRBRUSH

#ifdef STAMP
layout(location = 2) uniform float uniRadius;
layout(location = 3, binding = 0) uniform sampler2D stamp;
layout(location = 4) uniform float stampIntervalRatio;
layout(location = 5) uniform float noiseFactor;
layout(location = 6) uniform float rotationRand;
layout(location = 7) uniform int stampMode;
const int EquiDistance = 0;
const int RatioDistance = 1;
#endif

#ifdef AIRBRUSH
layout(location = 3, binding = 0) uniform sampler1D gradientSampler;
float sampleGraident(float distance){ return texture(gradientSampler, distance).r; }
#endif

mat2 rotate(float angle){
    return mat2(cos(angle), -sin(angle), sin(angle), cos(angle));
}

// ---------------- Noise helper functions, ignore them ------------------
float random (in vec2 st) {
    return fract(sin(dot(st.xy,
                         vec2(12.9898,78.233)))*
        43758.5453123);
}

float noise (in vec2 st) {
    vec2 i = floor(st);
    vec2 f = fract(st);

    // Four corners in 2D of a tile
    float a = random(i);
    float b = random(i + vec2(1.0, 0.0));
    float c = random(i + vec2(0.0, 1.0));
    float d = random(i + vec2(1.0, 1.0));

    vec2 u = f * f * (3.0 - 2.0 * f);

    return mix(a, b, u.x) +
            (c - a)* u.y * (1.0 - u.x) +
            (d - b) * u.x * u.y;
}

#define OCTAVES 6
float fbm (in vec2 st) {
    // Initial values
    float value = 0.0;
    float amplitude = .5;
    float frequency = 0.;
    //
    // Loop of octaves
    for (int i = 0; i < OCTAVES; i++) {
        value += amplitude * noise(st);
        st *= 2.;
        amplitude *= .5;
    }
    return value;
}
// ---------------- Noise end ------------------

#ifdef STAMP
float x2n(float x, float r, float t1, float t2, float L){
    // I screw the code up.
    if(stampMode == RatioDistance){
        const float tolerance = 1e-5;
        if(t1 <= 0 || t1/t2 < tolerance){
            t1 = tolerance*t2;
        }
        else if(t2 <= 0 || t2/t1 < tolerance){
            t2 = tolerance*t1;
        }
        t1 = 2.0*t1; t2 = 2.0*t2; 
        if(t1 == t2){
            return x/(r*t1);
        }
        else{
            return -L / r / (t1 - t2) * log(1.0 - (1.0 - t2/t1)/L * x);
        }
    }
    else{
        return x / (uniRadius * 2.0 * r);
    }
    
}

float n2x(float n, float r, float t1, float t2, float L){
    if(stampMode == RatioDistance){
        const float tolerance = 1e-5;
        if(t1 <= 0 || t1/t2 < tolerance){
            t1 = tolerance*t2;
        }
        else if(t2 <= 0 || t2/t1 < tolerance){
            t2 = tolerance*t1;
        }
        t1 = 2.0*t1; t2 = 2.0*t2;
        if(t1 == t2){
            return n * r * t1;
        }
        else{
            return L * (1.0-exp(-(t1-t2)*n*r/L)) / (1.0-t2/t1);
        }
    }
    else{
        return n * uniRadius * 2.0 * r;
    }
}
#endif

void main() {
    vec2 tangent = normalize(p1 - p0);
    vec2 normal = vec2(-tangent.y, tangent.x);
    float len = distance(p1, p0);

    // edge's locate coordinate, origin at the starting point, x axis along the tangent 
    vec2 pLocal = vec2(dot(p-p0, tangent), dot(p-p0, normal));
    vec2 p0Local = vec2(0, 0);
    vec2 p1Local = vec2(len, 0);

    float cosTheta = (flatRadius[0] - flatRadius[1])/len; // theta is the angle stroke tilt.
    float d0 = distance(p, p0);
    float d0cos = pLocal.x / d0;
    float d1 = distance(p, p1);
    float d1cos = (pLocal.x - len) / d1;

    // remove four corners
    if(d0cos < cosTheta && d0 > flatRadius[0]){
        discard;
    }
    if(d1cos > cosTheta && d1 > flatRadius[1]){
        discard;
    }

#if !defined(AIRBRUSH) && !defined(STAMP)
    if(d0 < flatRadius[0] && d1 < flatRadius[1]){
        discard;
    }
    float A = fragColor.a;
    if(d0 < flatRadius[0] || d1 < flatRadius[1]){
        A = 1.0 - sqrt(1.0 - fragColor.a);
    }
    outColor = vec4(fragColor.rgb, A);
    return;
#endif

#ifdef STAMP
    // roots of the quadratic polynomial are frontedge and backedge
    // formulas from SIGGRAPH 2022 Talk - A Fast & Robust Solution for Cubic & Higher-Order Polynomials
    float a, b, c, delta;
    a = 1.0 - pow(cosTheta, 2.0);
    b = 2.0 * (flatRadius[0] * cosTheta - pLocal.x);
    c = pow(pLocal.x, 2.0) + pow(pLocal.y, 2.0) - pow(flatRadius[0], 2.0);
    delta = pow(b, 2.0) - 4.0*a*c;
    if(delta <= 0.0) discard; // This should never happen.
    
    float tempMathBlock = b + sign(b) * sqrt(delta);
    float x1 = -2 * c / tempMathBlock;
    float x2 = -tempMathBlock / (2*a);
    float frontEdge = x1 <= x2 ? x1 : x2;
    float backEdge = x1 > x2 ? x1 : x2;

    // float summedIndex = summedLength[0]/stampIntervalRatio;
    // float startIndex, endIndex;
    // if (frontEdge <= 0){
    //     startIndex = ceil(summedIndex) - summedIndex;
    // }
    // else{
    //     startIndex = ceil(summedIndex + x2n(frontEdge, stampIntervalRatio, flatRadius[0], flatRadius[1], len)) - summedIndex;
    // }
    // endIndex = summedLength[1]/stampIntervalRatio-summedIndex;
    // float backIndex = x2n(backEdge, stampIntervalRatio, flatRadius[0], flatRadius[1], len);
    // endIndex = endIndex < backIndex ? endIndex : backIndex;
    // if(startIndex > endIndex) discard;

    // int MAX_i = 32; float currIndex = startIndex;
    // float A = 0.0;
    // for(int i = 0; i < MAX_i; i++){
    //     float currStampLocalX = n2x(currIndex, stampIntervalRatio, flatRadius[0], flatRadius[1], len);
    //     vec2 vStamp = pLocal - vec2(currStampLocalX, 0);
    //     float angle = rotationRand*radians(360*fract(sin(summedIndex+currIndex)*1.0));
    //     vStamp *= rotate(angle);
    //     vec2 uv = (vStamp/radius + 1.0)/2.0;
    //     vec4 color = texture(stamp, uv);
    //     float alpha = clamp(color.a - noiseFactor*fbm(uv*50.0), 0.0, 1.0) * fragColor.a;
    //     A = A * (1.0-alpha) + alpha;

    //     currIndex += 1.0;
    //     if(currIndex > endIndex) break;
    // }
    // if(A < 1e-4) discard;
    // outColor = vec4(fragColor.rgb, A);
    outColor = vec4(temp, fragColor.gba);
    return;
#endif

#ifdef AIRBRUSH
    // normalize
    pLocal = pLocal / radius;
    d0 /= radius;
    d1 /= radius;
    len /= radius;

    float A = fragColor.a;
    float reversedGradBone = 1.0-A*sampleGraident(pLocal.y);

    float exceed0, exceed1;
    exceed0 = exceed1 = 1.0;
    
    if(d0 < 1.0) {
      exceed0 = pow(1.0-A*sampleGraident(d0), 
        sign(pLocal.x) * 1.0/2.0 * (1.0-abs(pLocal.x))) * 
        pow(reversedGradBone, step(0.0, -pLocal.x));
    }
    if(d1 < 1.0) {
      exceed1 = pow(1.0-A*sampleGraident(d1), 
        sign(len - pLocal.x) * 1.0/2.0 * (1.0-abs(len-pLocal.x))) * 
        pow(reversedGradBone, step(0.0, pLocal.x - len));
    }
    A = clamp(1.0 - reversedGradBone/exceed0/exceed1, 0.0, 1.0-1e-3);
    outColor = vec4(fragColor.rgb, A);
    return;
#endif
    
}
