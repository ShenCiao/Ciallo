#version 460

layout(location = 0) in vec2 inPos;
layout(location = 1) in float inWidth;
layout(location = 2) in float inSummedLength;

layout(location = 0) out float outWidth;
layout(location = 1) out float outSummedLength;

void main() {
    // I make it confusing by mistakenly have width in half width
    gl_Position = vec4(inPos, 0.0, 1.0);
    outWidth = inWidth;
    outSummedLength = inSummedLength;
}
