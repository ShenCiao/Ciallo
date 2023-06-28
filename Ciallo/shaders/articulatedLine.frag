#version 460

layout(location = 0) in vec4 fragColor;
layout(location = 1) in flat vec2 p0;
layout(location = 2) in flat vec2 p1;
layout(location = 3) in vec2 p;
layout(location = 4) in float radius;
layout(location = 5) in flat vec2 summedLength;
layout(location = 6) in flat float hthickness[2]; // used by transparent vanilla and stamp

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
    vec2 lHat = normalize(p1 - p0);
    vec2 hHat = vec2(-lHat.y, lHat.x);
    float len = length(p1-p0);

    // In LH coordinate
    vec2 pLH = vec2(dot(p-p0, lHat), dot(p-p0, hHat));
    vec2 p0LH = vec2(0, 0);
    vec2 p1LH = vec2(len, 0);

    float d0 = distance(p, p0);
    float d1 = distance(p, p1);

    // // - Naive vanilla (OK if stroke is opaque)
    // if(pLH.x < 0 && d0 > radius){
    //     discard;
    // }
    // if(pLH.x > p1LH.x && d1 > radius){
    //     discard;
    // }
    // outColor = fragColor;
    // return;

    // - Transparent vanilla (perfectly handle transparency and self overlapping)
    //  use uninterpolated(flat) thickness avoid the joint mismatch.
    if(pLH.x < 0 && d0 > hthickness[0]){
        discard;
    }
    if(pLH.x > p1LH.x && d1 > hthickness[1]){
        discard;
    }
    if(d0 < hthickness[0] && d1 < hthickness[1]){
        discard;
    }
    float A = fragColor.a;
    if(d0 < hthickness[0] || d1 < hthickness[1]){
        A = 1.0 - sqrt(1.0 - fragColor.a);
    }
    outColor = vec4(fragColor.rgb, A);
    return;

// #ifdef STAMP
//     float frontEdge = pLH.x-radius, backEdge = pLH.x+radius;
//     float summedIndex = summedLength[0]/stampIntervalRatio;
//     float startIndex, endIndex;
//     if (frontEdge <= 0){
//         startIndex = ceil(summedIndex) - summedIndex;
//     }
//     else{
//         startIndex = ceil(summedIndex + x2n(frontEdge, stampIntervalRatio, hthickness[0], hthickness[1], len)) - summedIndex;
//     }
//     endIndex = summedLength[1]/stampIntervalRatio-summedIndex;
//     float backIndex = x2n(backEdge, stampIntervalRatio, hthickness[0], hthickness[1], len);
//     endIndex = endIndex < backIndex ? endIndex : backIndex;
//     if(startIndex > endIndex) discard;

//     int MAX_i = 32; float currIndex = startIndex;
//     float A = 0.0;
//     for(int i = 0; i < MAX_i; i++){
//         float currStamp = n2x(currIndex, stampIntervalRatio, hthickness[0], hthickness[1], len);
//         vec2 vStamp = pLH - vec2(currStamp, 0);
//         float angle = rotationRand*radians(360*fract(sin(summedIndex+currIndex)*1.0));
//         vStamp *= rotate(angle);
//         vec2 uv = (vStamp/radius + 1.0)/2.0;
//         vec4 color = texture(stamp, uv);
//         float alpha = clamp(color.a - noiseFactor*fbm(uv*50.0), 0.0, 1.0) * fragColor.a;
//         A = A * (1.0-alpha) + alpha;

//         currIndex += 1.0;
//         if(currIndex > endIndex) break;
//     }
//     if(A < 1e-4) discard;
//     outColor = vec4(fragColor.rgb, A);
//     return;
// #endif

// #ifdef AIRBRUSH
//     float A = fragColor.a;

//     if((pLH.x < 0 && d0 > radius)){
//         discard;
//     }
//     if((pLH.x > p1LH.x && d1 > radius)){
//         discard;
//     }

//     // normalize
//     pLH = pLH / radius;
//     d0 /= radius;
//     d1 /= radius;
//     len /= radius;

//     float reversedGradBone = 1.0-A*sampleGraident(pLH.y);

//     float exceed0, exceed1;
//     exceed0 = exceed1 = 1.0;
    
//     if(d0 < 1.0) {
//       exceed0 = pow(1.0-A*sampleGraident(d0), 
//         sign(pLH.x) * 1.0/2.0 * (1.0-abs(pLH.x))) * 
//         pow(reversedGradBone, step(0.0, -pLH.x));
//     }
//     if(d1 < 1.0) {
//       exceed1 = pow(1.0-A*sampleGraident(d1), 
//         sign(len - pLH.x) * 1.0/2.0 * (1.0-abs(len-pLH.x))) * 
//         pow(reversedGradBone, step(0.0, pLH.x - len));
//     }
//     A = clamp(1.0 - reversedGradBone/exceed0/exceed1, 0.0, 1.0-1e-3);
//     outColor = vec4(fragColor.rgb, A);
//     return;
// #endif
    
}
