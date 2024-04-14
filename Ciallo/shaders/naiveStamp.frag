// #version 460

// layout(location = 0) in vec2 uv;
// layout(location = 0) out vec4 outColor;

// layout(location = 1) uniform vec4 color = vec4(0.9, 0.0, 0.0, 0.0); // Red to warn if value is not set
// layout(location = 3, binding = 0) uniform sampler2D tex;
// void main() {
//     vec4 c = texture(tex, uv);
//     outColor = (color.a < 1e-5) ? c:vec4(color.rgb, c.a);
// }

#version 460

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

layout(location = 0) in vec2 uv;
layout(location = 0) out vec4 outColor;

layout(location = 1) uniform vec4 color = vec4(0.9, 0.0, 0.0, 0.0); // Red to warn if value is not set
layout(location = 3, binding = 0) uniform sampler2D tex;
void main() {
    vec4 c = texture(tex, uv);
    float alpha = clamp(c.a - 0.5*fbm(uv*50.0), 0.0, 1.0) * 1.0;
    outColor = (color.a < 1e-5) ? c:vec4(color.rgb, alpha);
    // outColor = (color.a < 1e-5) ? c:vec4(color.rgb, c.a);
}
