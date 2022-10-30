#version 460

layout(location = 0) in vec2 inPos;
layout(location = 1) in float inWidth;

layout(location = 0) out float outWidth;
layout(location = 1) out vec4 outColor;

void main() {
    gl_Position = vec4(inPos, 0.0, 1.0);
    outWidth = inWidth;
    outColor = vec4(0.0, 0.0, 0.0, 1.0);
}
