#version 460

layout(points) in;
layout(triangle_strip, max_vertices = 4) out;

layout(location = 0) in float[] inRadius;

layout(location = 0) out vec2 uv;

layout(std140, binding=0) uniform _MVP{ mat4 MVP; };

void main(){
    vec2 p = gl_in[0].gl_Position.xy;
    float radius = inRadius[0];

    gl_Position = MVP*vec4(p.x-radius, p.y-radius, 0.0, 1.0);
    uv = vec2(0.0, 0.0);
    EmitVertex();

    gl_Position = MVP*vec4(p.x-radius, p.y+radius, 0.0, 1.0);
    uv = vec2(0.0, 1.0);
    EmitVertex();

    gl_Position = MVP*vec4(p.x+radius, p.y-radius, 0.0, 1.0);
    uv = vec2(1.0, 0.0);
    EmitVertex();

    gl_Position = MVP*vec4(p.x+radius, p.y+radius, 0.0, 1.0);
    uv = vec2(1.0, 1.0);
    EmitVertex();

    EndPrimitive();
}