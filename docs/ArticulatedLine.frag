#version 300 es
precision mediump float;
precision mediump int;

in vec2 p;
in float r;
flat in vec2 p0;
flat in float r0;
flat in vec2 p1;
flat in float r1;

out vec4 outColor;

void main()	{
    vec2 tangent = normalize(p1 - p0);
    vec2 normal = vec2(-tangent.y, tangent.x);

    float len = distance(p1, p0);
    vec2 pLocal = vec2(dot(p-p0, tangent), dot(p-p0, normal));
    vec2 p0Local = vec2(0, 0);
    vec2 p1Local = vec2(len, 0);

    float d0 = distance(p, p0);
    float d1 = distance(p, p1);

    if(pLocal.x < 0.0 && d0 > r){
        discard;
    }
    if(pLocal.x > len && d1 > r){
        discard;
    }

    outColor = vec4(1.0, 1.0, 1.0, 1.0);
}