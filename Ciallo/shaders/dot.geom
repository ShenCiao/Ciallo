#version 460

layout(points) in;
layout(triangle_strip, max_vertices = 4) out;

layout(location = 0) in float[] inHalfThicknessOffset;
layout(location = 0) out vec2 uv;

layout(location = 0) uniform mat4 MVP;
layout(location = 2) uniform float size;

void main(){
    vec2 p = gl_in[0].gl_Position.xy;

    gl_Position = MVP*vec4(p-size, 0.0, 1.0);
    uv = vec2(0.0, 0.0);
    EmitVertex();

    gl_Position = MVP*vec4(p.x-size, p.y+size, 0.0, 1.0);
    uv = vec2(0.0, 1.0);
    EmitVertex();

    gl_Position = MVP*vec4(p+size, 0.0, 1.0);
    uv = vec2(1.0, 1.0);
    EmitVertex();

    gl_Position = MVP*vec4(p.x+size, p.y-size, 0.0, 1.0);
    uv = vec2(1.0, 0.0);
    EmitVertex();

    EndPrimitive();
}