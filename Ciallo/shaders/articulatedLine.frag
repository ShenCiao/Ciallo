#version 460

layout(location = 0) in vec4 fragColor;
layout(location = 1) in flat vec2 p0;
layout(location = 2) in flat vec2 p1;
layout(location = 3) in vec2 p;
layout(location = 4) in float halfThickness;
layout(location = 5) in flat float[2] summedLength;

layout(location = 0) out vec4 outColor;

#ifdef STAMP
layout(location = 3, binding = 0) uniform sampler2D stamp;
layout(location = 4) uniform float stampInterval = 0.03;
#endif

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
    
    vec2 lHat = normalize(p1 - p0);
    vec2 hHat = vec2(-lHat.y, lHat.x);
    float len = length(p1-p0);

    // In LH coordinate
    vec2 pLH = vec2(dot(p-p0, lHat), dot(p-p0, hHat));
    vec2 p0LH = vec2(0, 0);
    vec2 p1LH = vec2(len, 0);

    float d0 = distance(pLH, p0LH);
    float d1 = distance(pLH, p1LH);

#if !defined(STAMP) && !defined(AIRBRUSH)
    // Vanilla 
    if((pLH.x < 0 && d0 > halfThickness)){
        discard;
    }
    if((pLH.x > p1LH.x && d1 > halfThickness)){
        discard;
    }
    outColor = fragColor;
    return;
#endif

#ifdef STAMP
    float stampStarting = mod(stampInterval - mod(summedLength[0], stampInterval), stampInterval);

    if(stampStarting > len) discard; // There are no stamps in this segment.

    float innerStampStarting;
    if(pLH.x-halfThickness <= stampStarting){
        innerStampStarting = stampStarting;
    }
    else{
        innerStampStarting = stampStarting + stampInterval * (1.0+floor((pLH.x-halfThickness-stampStarting)/stampInterval));
    }
    float innerStampEnding = (pLH.x+halfThickness < len) ? pLH.x+halfThickness:len;
    if(innerStampStarting > innerStampEnding) discard; // There are no stamps in this rect.

    float currStamp = innerStampStarting;
    float A = 0;
    int san_i = 0, MAX_i = 32; // sanity check to avoid infinite loop
    do{
        san_i += 1;
        if(san_i > MAX_i) break;
        // Sample on stamp and manually blend alpha
        vec2 uv = ((vec2(pLH.x, pLH.y) - vec2(currStamp, 0))/halfThickness + vec2(1.0, 1.0))/2.0;
        vec4 color = texture(stamp, uv);
        A = A * (1.0-color.a) + color.a;
        currStamp += stampInterval;
    }while(currStamp < innerStampEnding);
    if(A < 1e-4){
        discard;
    }
    outColor = vec4(fragColor.rgb, A*fragColor.a);
    return;
#endif

#ifdef AIRBRUSH
    float A = fragColor.a;

    if((pLH.x < 0 && d0 > halfThickness)){
        discard;
    }
    if((pLH.x > p1LH.x && d1 > halfThickness)){
        discard;
    }

    float reverse_falloff_stroke = reverse_falloff(pLH.y, A);

    float exceed1, exceed2;
    exceed1 = exceed2 = 1.0;
    
    if(d0 < 1.0) {
      exceed1 = pow(reverse_falloff(d0, A), 
        sign(pLH.x) * 1.0/2.0 * (1.0-abs(pLH.x))) * 
        pow(reverse_falloff_stroke, step(0.0, -pLH.x));
    }
    if(d1 < 1.0) {
      exceed2 = pow(reverse_falloff(d1, A), 
        sign(len - pLH.x) * 1.0/2.0 * (1.0-abs(len-pLH.x))) * 
        pow(reverse_falloff_stroke, step(0.0, pLH.x - len));
    }
    A = clamp(1 - reverse_falloff_stroke/exceed1/exceed2, 0.0, 1.0);
    outColor = vec4(fragColor.rgb, A);
    return;
#endif
    
}
