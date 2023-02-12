#version 460

layout(lines) in;
layout(triangle_strip, max_vertices = 4) out;

layout(location = 0) in float[] inHalfThicknessOffset;
layout(location = 1) in float[] inSummedLength;

layout(location = 0) out vec4 fragColor;
layout(location = 1) out flat vec2 p0;
layout(location = 2) out flat vec2 p1;
layout(location = 3) out vec2 p;
layout(location = 4) out float halfThickness;
layout(location = 5) out flat float summedLength;
layout(location = 6) out flat float hthickness[2];

layout(std140, binding=0) uniform _MVP{ mat4 MVP; };
layout(location = 1) uniform vec4 color = vec4(0.9, 0.0, 0.0, 1.0); // Red to warn if value is not set
layout(location = 2) uniform float uniThickness;

void main(){
    vec4 v01 = gl_in[1].gl_Position - gl_in[0].gl_Position;
    vec2 nv = normalize(v01.xy);
    vec2 n = vec2(-nv.y, nv.x);

    // Vertex at p0 left(v01 direction)
    halfThickness = uniThickness+inHalfThicknessOffset[0];
    p0 = gl_in[0].gl_Position.xy;
    p1 = gl_in[1].gl_Position.xy;
    p = p0 + n*halfThickness - nv*halfThickness;
    gl_Position = MVP*vec4(p, 0.0, 1.0);
    fragColor = color;
    summedLength = inSummedLength[0];
    hthickness[0] = uniThickness+inHalfThicknessOffset[0];
    hthickness[1] = uniThickness+inHalfThicknessOffset[1];
    EmitVertex();

    // Vertex at p0 right
    halfThickness = uniThickness+inHalfThicknessOffset[0];
    p0 = gl_in[0].gl_Position.xy;
    p1 = gl_in[1].gl_Position.xy;
    p = p0 - n*halfThickness - nv*halfThickness;
    gl_Position = MVP*vec4(p, 0.0, 1.0);
    fragColor = color;
    summedLength = inSummedLength[0];
    hthickness[0] = uniThickness+inHalfThicknessOffset[0];
    hthickness[1] = uniThickness+inHalfThicknessOffset[1];
    EmitVertex();

    // Vertex at p1 left
    halfThickness = uniThickness+inHalfThicknessOffset[1];
    p0 = gl_in[0].gl_Position.xy;
    p1 = gl_in[1].gl_Position.xy;
    p = p1 + n*halfThickness + nv*halfThickness;
    gl_Position = MVP*vec4(p, 0.0, 1.0);
    fragColor = color;
    summedLength = inSummedLength[0];
    hthickness[0] = uniThickness+inHalfThicknessOffset[0];
    hthickness[1] = uniThickness+inHalfThicknessOffset[1];
    EmitVertex();

    // Vertex at p1 right
    halfThickness = uniThickness+inHalfThicknessOffset[1];
    p0 = gl_in[0].gl_Position.xy;
    p1 = gl_in[1].gl_Position.xy;
    p = p1 - n*halfThickness + nv*halfThickness;
    gl_Position = MVP*vec4(p, 0.0, 1.0);
    fragColor = color;
    summedLength = inSummedLength[0];
    hthickness[0] = uniThickness+inHalfThicknessOffset[0];
    hthickness[1] = uniThickness+inHalfThicknessOffset[1];
    EmitVertex();

    EndPrimitive();
}