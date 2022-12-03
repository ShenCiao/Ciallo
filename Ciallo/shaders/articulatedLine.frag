#version 460

layout(location = 0) in vec4 fragColor;
layout(location = 1) in flat vec2 p0;
layout(location = 2) in flat vec2 p1;
layout(location = 3) in vec2 p;
layout(location = 4) in float width;
layout(location = 5) in flat float[2] summedLength;

layout(location = 0) out vec4 outColor;

// For airbrush. Arbritry alpha falloff function. Will be user editable with bezier curve mapping
float falloff_modulate(float h, float hardfac) {
  /* Modulate the falloff profile */
  float mod_val = pow(1.0 - abs(h), mix(0.01, 10.0, 1.0 - hardfac));
  return smoothstep(0.0, 1.0, mod_val);
}
float reverse_falloff(float v, float A) {
    return 1.0 - A * falloff_modulate(v, 0.98);
}

void main() {
    
    vec2 L = normalize(p1 - p0);
    vec2 H = vec2(-L.y, L.x);
    float lenL = length(p1-p0)/width;

    vec2 pLH = vec2(dot(p-p0, L), dot(p-p0, H))/width;
    vec2 p0LH = vec2(0, 0);
    vec2 p1LH = vec2(lenL, 0);

    float d0LH = distance(pLH, p0LH);
    float d1LH = distance(pLH, p1LH);

    // // Vanilla begin
    // if((pLH.x < 0 && d0LH > 1.0)){
    //     discard;
    // }
    // if((pLH.x > p1LH.x && d1LH > 1.0)){
    //     discard;
    // }
    // float A = fragColor.a;

    // Stamp begin
    float px = dot(p-p0, L);
    float len = summedLength[1] - summedLength[0];
    float stampDist = 1/200.0;
    float stampStarting = mod(stampDist - mod(summedLength[0], stampDist), stampDist);

    if(stampStarting > len) discard; // There are no stamps in this segment.

    float innerStampStarting;
    if(px-width <= stampStarting){
        innerStampStarting = stampStarting;
    }
    else{
        innerStampStarting = stampStarting + stampDist * (1.0+floor((px-width-stampStarting)/stampDist));
    }
    float innerStampEnding = (px+width < len) ? px+width:len;
    if(innerStampStarting > innerStampEnding) discard; // There are no stamps in this rect.

    float currStamp = innerStampStarting;
    do{
        // Sample on stamp and manually blend color
        outColor = fragColor;
        currStamp += stampDist;
    }while(currStamp < innerStampEnding);


    // // -----Airbrush begin. Can be discard.
    // // Sadly Shen Ciao already forget about some details in this implementation.
    // // And He just copy and paste old implementation. May Muse bless the poor boy.
    // float reverse_falloff_stroke = reverse_falloff(pLH.y, A);

    // float exceed1, exceed2;
    // exceed1 = exceed2 = 1.0;
    
    // if(d0LH < 1.0) {
    //   exceed1 = pow(reverse_falloff(d0LH, A), 
    //     sign(pLH.x) * 1.0/2.0 * (1.0-abs(pLH.x))) * 
    //     pow(reverse_falloff_stroke, step(0.0, -pLH.x));
    // }
    // if(d1LH < 1.0) {
    //   exceed2 = pow(reverse_falloff(d1LH, A), 
    //     sign(lenL - pLH.x) * 1.0/2.0 * (1.0-abs(lenL-pLH.x))) * 
    //     pow(reverse_falloff_stroke, step(0.0, pLH.x - lenL));
    // }
    // A = clamp(1 - reverse_falloff_stroke/exceed1/exceed2, 0.0, 1.0);
    // // -------Airbrush end. 

    // outColor = vec4(fragColor.rgb, A);
}
