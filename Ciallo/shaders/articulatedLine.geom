#version 460

layout(lines) in;
layout(triangle_strip, max_vertices = 4) out;

layout(location = 0) in float[] inWidth;


layout(location = 0) out vec4 fragColor;
layout(location = 1) out flat vec2 p0;
layout(location = 2) out flat vec2 p1;
layout(location = 3) out vec2 p;
layout(location = 4) out float width;

layout(location = 0) uniform mat4 MVP;
layout(location = 1) uniform vec4 color;

void main(){
    vec4 v01 = gl_in[1].gl_Position - gl_in[0].gl_Position;
    vec2 nv = normalize(v01.xy);
    vec2 n = vec2(-nv.y, nv.x);

    // Vertex at p0 left(v01 direction)
    width = inWidth[0];
    p0 = gl_in[0].gl_Position.xy;
    p1 = gl_in[1].gl_Position.xy;
    p = p0 + n*width - nv*width;
    gl_Position = MVP*vec4(p, 0.0, 1.0);
    fragColor = color;
    EmitVertex();

    // Vertex at p0 right
    width = inWidth[0];
    p0 = gl_in[0].gl_Position.xy;
    p1 = gl_in[1].gl_Position.xy;
    p = p0 - n*width - nv*width;
    gl_Position = MVP*vec4(p, 0.0, 1.0);
    fragColor = color;
    EmitVertex();

    // Vertex at p1 left
    width = inWidth[1];
    p0 = gl_in[0].gl_Position.xy;
    p1 = gl_in[1].gl_Position.xy;
    p = p1 + n*width + nv*width;
    gl_Position = MVP*vec4(p, 0.0, 1.0);
    fragColor = color;
    EmitVertex();

    // Vertex at p1 right
    width = inWidth[1];
    p0 = gl_in[0].gl_Position.xy;
    p1 = gl_in[1].gl_Position.xy;
    p = p1 - n*width + nv*width;
    gl_Position = MVP*vec4(p, 0.0, 1.0);
    fragColor = color;
    EmitVertex();

    EndPrimitive();
}