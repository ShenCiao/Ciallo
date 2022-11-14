#version 460

// Beginner friendly triangle rendering. Used for rendering convex polygon.
layout(location = 0) in vec2 inPos;

layout(location = 0) uniform mat4 MVP;

void main() {
    gl_Position = MVP * vec4(inPos, 0.0, 1.0);
}