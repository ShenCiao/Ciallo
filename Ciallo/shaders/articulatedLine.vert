#version 460

layout(location = 0) in vec2 inPos;
layout(location = 1) in float inHalfThicknessOffset;
layout(location = 2) in float inSummedLength;

layout(location = 0) out float outHalfThicknessOffset;
layout(location = 1) out float outSummedLength;

void main() {
    gl_Position = vec4(inPos, 0.0, 1.0);
    outHalfThicknessOffset = inHalfThicknessOffset;
    outSummedLength = inSummedLength;
}
