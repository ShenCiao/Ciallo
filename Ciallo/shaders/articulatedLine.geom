#version 460

layout(lines) in;
layout(triangle_strip, max_vertices = 4) out;

layout(location = 0) in float[] inRadiusOffset;
layout(location = 1) in float[] inSummedLength;

layout(location = 0) out vec4 fragColor;
layout(location = 1) out flat vec2 p0;
layout(location = 2) out flat vec2 p1;
layout(location = 3) out vec2 p;
layout(location = 5) out flat vec2 summedLength;
layout(location = 6) out flat vec2 flatRadius;

layout(std140, binding=0) uniform _MVP{ mat4 MVP; };
layout(location = 1) uniform vec4 color = vec4(0.9, 0.0, 0.0, 1.0); // Red to warn if value is not set
layout(location = 2) uniform float uniRadius;

void main(){
    vec4 v01 = gl_in[1].gl_Position - gl_in[0].gl_Position;
    vec2 tangent = normalize(v01.xy);
    vec2 normal = vec2(-tangent.y, tangent.x);

    vec2 pos0 = gl_in[0].gl_Position.xy;
    vec2 pos1 = gl_in[1].gl_Position.xy;
    float r0 = uniRadius + inRadiusOffset[0];
    float r1 = uniRadius + inRadiusOffset[1];
    float len = distance(pos0, pos1);

    float cosTheta = (r0 - r1)/len; // theta is the angle stroke tilt.
    // apply trigonometric half angle formula
    float tanValue0 = sqrt((1.0+cosTheta) / (1.0-cosTheta));
    float tanValue1 = 1.0/tanValue0;

    // the corner case, Shen call it "Transit":
    // small disk is fully inside the big disk
    if(abs(cosTheta) >= 1.0) return;
    // center of the small disk is very near to the big disk
    const float offsetRatioTolerance = 10.0;
    if(tanValue0 > offsetRatioTolerance || tanValue1 > offsetRatioTolerance) return;

    // Vertex at p0 left(v01 direction)
    p0 = pos0;
    p1 = pos1;
    p = p0 + normal*radius*tanValue0 - tangent*radius;
    gl_Position = MVP*vec4(p, 0.0, 1.0);
    fragColor = color;
    summedLength = vec2(inSummedLength[0], inSummedLength[1]);
    flatRadius = vec2(r0, r1);
    EmitVertex();

    // Vertex at p0 right
    p0 = pos0;
    p1 = pos1;
    p = p0 - normal*radius*tanValue0 - tangent*radius;
    gl_Position = MVP*vec4(p, 0.0, 1.0);
    fragColor = color;
    summedLength = vec2(inSummedLength[0], inSummedLength[1]);
    flatRadius = vec2(r0, r1);
    EmitVertex();

    // Vertex at p1 left
    p0 = pos0;
    p1 = pos1;
    p = p1 + normal*radius*tanValue1 + tangent*radius;
    gl_Position = MVP*vec4(p, 0.0, 1.0);
    fragColor = color;
    summedLength = vec2(inSummedLength[0], inSummedLength[1]);
    flatRadius = vec2(r0, r1);
    EmitVertex();

    // Vertex at p1 right
    p0 = pos0;
    p1 = pos1;
    p = p1 - normal*radius*tanValue1 + tangent*radius;
    gl_Position = MVP*vec4(p, 0.0, 1.0);
    fragColor = color;
    summedLength = vec2(inSummedLength[0], inSummedLength[1]);
    flatRadius = vec2(r0, r1);
    EmitVertex();

    EndPrimitive();
}