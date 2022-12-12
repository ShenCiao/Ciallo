#version 460

layout(location = 0) in vec2 uv;
layout(location = 0) out vec4 outColor;

layout(location = 1) uniform vec4 color = vec4(0.9, 0.0, 0.0, 1.0); // Red to warn if value is not set
layout(location = 3, binding = 0) uniform sampler2D tex;
void main() {
    vec4 c = texture(tex, uv);
    outColor = vec4(color.rgb, c.a);
}