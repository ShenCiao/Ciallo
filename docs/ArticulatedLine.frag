#version 300 es
precision mediump float;
precision mediump int;

in vec2 p;
in float r;
flat in vec2 p0;
flat in float r0;
flat in vec2 p1;
flat in float r1;

// Common
uniform int type;
const int Vanilla = 0, Stamp = 1, Airbrush = 2;
uniform vec3 color;
uniform float alpha;
// Airbrush
uniform mediump sampler2D gradient;
float sampleGraident(float distance){ return texture(gradient, vec2(distance, 0.0)).r; }

out vec4 outColor;

void main()	{
    vec2 tangent = normalize(p1 - p0);
    vec2 normal = vec2(-tangent.y, tangent.x);

    float len = distance(p1, p0);
    vec2 pLocal = vec2(dot(p-p0, tangent), dot(p-p0, normal));
    vec2 p0Local = vec2(0, 0);
    vec2 p1Local = vec2(len, 0);

    float cosTheta = (r0 - r1)/len;
    float d0 = distance(p, p0);
    float d0cos = pLocal.x / d0;
    float d1 = distance(p, p1);
    float d1cos = (pLocal.x - len) / d1;

    // remove corners
    if(d0cos < cosTheta && d0 > r0) discard;
    if(d1cos > cosTheta && d1 > r1) discard;
    
    if(type == Vanilla){
        if(d0 < r0 && d1 < r1) discard;
        float A = (d0 < r0 || d1 < r1) ? 1.0 - sqrt(1.0 - alpha) : alpha;
        outColor = vec4(color, A);
    }

    if(type == Airbrush){
        float tanTheta = sqrt(1.0 - cosTheta*cosTheta)/cosTheta;
        float mid = pLocal.x - abs(pLocal.y)/tanTheta;
        float A = alpha;
        float transparency0 = d0 > r0 ? 1.0:sqrt(1.0 - A*sampleGraident(d0/r0));
        float transparency1 = d1 > r1 ? 1.0:sqrt(1.0 - A*sampleGraident(d1/r1));
        float transparency;

        if(mid <= 0.0){
            transparency = transparency0/transparency1;
        }
        if(mid > 0.0 && mid < len){
            float r = (mid * r1 + (len - mid) * r0)/len;
            float dr = distance(pLocal, vec2(mid, 0))/r;
            transparency = (1.0 - A*sampleGraident(dr))/transparency0/transparency1;
        }
        if(mid >= len){
            transparency = transparency1/transparency0;
        }

        outColor = vec4(color, 1.0 - transparency);
    }
    return;
}