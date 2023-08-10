#version 300 es
precision mediump float;
precision mediump int;

// Author: Shen Ciao
// 
// This is the web version of articulated line rendering, imlementing instanced and batch rendering.
// Geometry shader is missing on WebGL, we have to mimic the behavior of "line" input of geometry shader here.
// Push two vertices' info (or an edge's info) into a single vertex and do instanced rendering. Each instance is a trapzoid. 
// Two vertices' attributes are labelled with 0 and 1, 0 is on the left and 1 is on the right.
//
// p is position, r is radius
// l (summedLength) is the distance from the vertex to the first vertex of the stroke along the polyline.
// l value is only needed for the stamp brush.

uniform mat4 modelViewMatrix;
uniform mat4 projectionMatrix;

in vec2 position0;
in float radius0;
in float summedLength0;
in int isEndPoint0; // bool value for the batch rendering.
in vec2 position1;
in float radius1;
in float summedLength1;

out vec2 p; // position of the current pixel
flat out vec2 p0;
flat out float r0;
flat out float l0;
flat out vec2 p1;
flat out float r1;
flat out float l1;

void main()	{
    // Batch rendering: 
    // We push all polylines' vertices into a single vertex buffer.
    // If the vertex0 is the end point of a polyline, don't connect it to the next point. Then batch rendering done!
    if(bool(isEndPoint0)) return;

    // Pass flat(non-interpolated) values to the fragment shader
    r0 = radius0;
    r1 = radius1;
    p0 = position0;
    p1 = position1;
    l0 = summedLength0;
    l1 = summedLength1;

    vec2 tangentDirection = normalize(position1 - position0);
    vec2 normalDirection = vec2(-tangentDirection.y, tangentDirection.x);
    float cosTheta = (r0 - r1)/distance(p0, p1); // theta is the angle stroke tilt, there is a diagram in README to explain this.
    // the vertex1 with radius is fully inside the vertex0.
    if(abs(cosTheta) >= 1.0) return; 
    
    // Each instance is a trapzoid, whose vertices' positions are determined here. 
    // Use gl_VertexID {0, 1, 2, 3} to index and get the desired parameters.
    // Be careful with the backface culling! We are ignoring it here.
    vec2[] offsetSigns = vec2[](
        vec2(-1.0,-1.0),
        vec2(-1.0, 1.0), 
        vec2( 1.0, 1.0),
        vec2( 1.0,-1.0));
    vec2 offsetSign = offsetSigns[gl_VertexID];

    vec2[] polylineVertexPositions = vec2[](position0, position0, position1, position1);
    vec2 pos = polylineVertexPositions[gl_VertexID];
    
    float radius = vec4(radius0, radius0, radius1, radius1)[gl_VertexID];

    float tanHalfTheta = sqrt((1.0+cosTheta) / (1.0-cosTheta));
    float cotHalfTheta = 1.0 / tanHalfTheta;
    float normalTanValue = vec4(tanHalfTheta, tanHalfTheta, cotHalfTheta, cotHalfTheta)[gl_VertexID];
    if(normalTanValue > 10.0 || normalTanValue < 0.1) return;
    
    vec2 trapzoidVertexPosition = pos + 
        offsetSign.x * radius * tangentDirection + 
        offsetSign.y * radius * normalDirection * normalTanValue;
    p = trapzoidVertexPosition;
    
    gl_Position = projectionMatrix * modelViewMatrix * vec4(trapzoidVertexPosition, 0.0, 1.0);
}