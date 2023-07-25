#version 300 es
precision mediump float;
precision mediump int;

uniform mat4 modelViewMatrix;
uniform mat4 projectionMatrix;

in vec2 position0;
in float radius0;
in vec2 position1;
in float radius1;

out vec2 p;
out float r;
flat out vec2 p0;
flat out float r0;
flat out vec2 p1;
flat out float r1;

void main()	{
    vec2 tangent = normalize(position1 - position0);
    vec2 normal = vec2(-tangent.y, tangent.x);
    

    vec2[] offsetSigns = vec2[](
        vec2(-1.0, -1.0),
        vec2(-1.0, 1.0), 
        vec2(1.0,  1.0),
        vec2(1.0,  -1.0));
    vec2 offsetSign = offsetSigns[gl_VertexID];

    vec2[] polylineVertexPositions = vec2[](
        position0,
        position0,
        position1,
        position1);
    vec2 pos = polylineVertexPositions[gl_VertexID];
    
    vec4 radiuses = vec4(
        radius0, 
        radius0, 
        radius1, 
        radius1);
    float radius = radiuses[gl_VertexID];
    r = radius;
    r0 = radius0;
    r1 = radius1;

    vec2 trapzoidVertexPosition = pos + 
        offsetSign.x * radius * tangent + 
        offsetSign.y * radius * normal;
    p = pos;
    p0 = position0;
    p1 = position1;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(trapzoidVertexPosition, 0.0, 1.0);
}
