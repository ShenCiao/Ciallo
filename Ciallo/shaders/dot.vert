#version 460
// use geometry shader to render dots, strokes and dots can share the same buffer

layout(location = 0) in vec2 inPos;


void main() {
    gl_Position = vec4(inPos, 0.0, 1.0);
}