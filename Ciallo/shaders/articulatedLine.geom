#version 460

layout(lines) in;
layout(triangle_strip, max_vertices = 4) out;

layout(location = 0) in float[] inRadiusOffset;
layout(location = 1) in float[] inSummedLength;

layout(location = 0) out vec4 fragColor;
layout(location = 1) out flat vec2 p0;
layout(location = 2) out flat vec2 p1;
layout(location = 3) out vec2 p;
layout(location = 4) out float radius;
layout(location = 5) out flat vec2 summedLength;
layout(location = 6) out flat vec2 flatRadius;

layout(std140, binding=0) uniform _MVP{ mat4 MVP; };
layout(location = 1) uniform vec4 color = vec4(0.9, 0.0, 0.0, 1.0); // Red to warn if value is not set
layout(location = 2) uniform float uniRadius;

void main(){
    vec4 v01 = gl_in[1].gl_Position - gl_in[0].gl_Position;
    vec2 tangent = normalize(v01.xy);
    vec2 normal = vec2(-tangent.y, tangent.x);

    // Vertex at p0 left(v01 direction)
    radius = uniRadius+inRadiusOffset[0];
    p0 = gl_in[0].gl_Position.xy;
    p1 = gl_in[1].gl_Position.xy;
    p = p0 + normal*radius - tangent*radius;
    gl_Position = MVP*vec4(p, 0.0, 1.0);
    fragColor = color;
    summedLength.x = inSummedLength[0];
    summedLength.y = inSummedLength[1];
    flatRadius[0] = uniRadius+inRadiusOffset[0];
    flatRadius[1] = uniRadius+inRadiusOffset[1];
    EmitVertex();

    // Vertex at p0 right
    radius = uniRadius+inRadiusOffset[0];
    p0 = gl_in[0].gl_Position.xy;
    p1 = gl_in[1].gl_Position.xy;
    p = p0 - normal*radius - tangent*radius;
    gl_Position = MVP*vec4(p, 0.0, 1.0);
    fragColor = color;
    summedLength.x = inSummedLength[0];
    summedLength.y = inSummedLength[1];
    flatRadius[0] = uniRadius+inRadiusOffset[0];
    flatRadius[1] = uniRadius+inRadiusOffset[1];
    EmitVertex();

    // Vertex at p1 left
    radius = uniRadius+inRadiusOffset[1];
    p0 = gl_in[0].gl_Position.xy;
    p1 = gl_in[1].gl_Position.xy;
    p = p1 + normal*radius + tangent*radius;
    gl_Position = MVP*vec4(p, 0.0, 1.0);
    fragColor = color;
    summedLength.x = inSummedLength[0];
    summedLength.y = inSummedLength[1];
    flatRadius[0] = uniRadius+inRadiusOffset[0];
    flatRadius[1] = uniRadius+inRadiusOffset[1];
    EmitVertex();

    // Vertex at p1 right
    radius = uniRadius+inRadiusOffset[1];
    p0 = gl_in[0].gl_Position.xy;
    p1 = gl_in[1].gl_Position.xy;
    p = p1 - normal*radius + tangent*radius;
    gl_Position = MVP*vec4(p, 0.0, 1.0);
    fragColor = color;
    summedLength.x = inSummedLength[0];
    summedLength.y = inSummedLength[1];
    flatRadius[0] = uniRadius+inRadiusOffset[0];
    flatRadius[1] = uniRadius+inRadiusOffset[1];
    EmitVertex();

    EndPrimitive();
}