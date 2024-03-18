#version 460

layout(location = 0) in vec2 inPos;
layout(location = 1) in float inRadius;

layout(location = 0) out float outRadius;

void main() {
    gl_Position = vec4(inPos, 0.0, 1.0);
    outRadius = inRadius;
}