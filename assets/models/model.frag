#version 120
uniform sampler2D baseMap,normalMap,roughMap,aoMap,emissionMap;
uniform float baseMapPresent,normalMapPresent,roughMapPresent,aoMapPresent,emissionMapPresent;
uniform float title,eye,aspect,depth;
uniform vec4 baseFactor,material,emission;
varying vec3 position,normal;
varying vec2 uv;
varying vec4 color;
const float PI=3.14159265;
vec3 light(vec3 n,vec3 v,vec3 l,vec3 base,float metal,float rough,vec3 strength){
 vec3 h=normalize(v+l);float nl=max(dot(n,l),0.0),nv=max(dot(n,v),0.001),nh=max(dot(n,h),0.0),vh=max(dot(v,h),0.0);
 float a=rough*rough,a2=a*a,d=a2/(PI*pow(nh*nh*(a2-1.0)+1.0,2.0));float k=pow(rough+1.0,2.0)/8.0;
 float g=(nv/(nv*(1.0-k)+k))*(nl/(nl*(1.0-k)+k));vec3 f=mix(vec3(.04),base,metal);f=f+(1.0-f)*pow(1.0-vh,5.0);
 return ((1.0-f)*(1.0-metal)*base/PI+d*g*f/max(.004,4.0*nl*nv))*strength*nl;
}
void main(){
 vec3 n=normalize(normal);if(!gl_FrontFacing)n=-n;
 if(title>.5){float front=max(dot(n,vec3(0,0,1)),0.0);gl_FragColor=vec4(mix(vec3(.30,.48,.56),vec3(1),front),1);return;}
 vec4 base=baseFactor*color;if(baseMapPresent>.5){vec4 t=texture2D(baseMap,uv);base*=vec4(pow(t.rgb,vec3(2.2)),t.a);}
 if(base.a<max(.02,material.w))discard;
 float normalVariance=0.0;
 if(normalMapPresent>.5){vec3 q1=dFdx(position),q2=dFdy(position);vec2 s1=dFdx(uv),s2=dFdy(uv);vec3 t=q1*s2.y-q2*s1.y,b=-q1*s2.x+q2*s1.x;float inv=inversesqrt(max(dot(t,t),dot(b,b)));vec3 bump=texture2D(normalMap,uv).xyz*2.0-1.0;normalVariance=max(0.0,1.0-length(bump));bump.xy*=material.z;if(dot(t,t)>.0000001)n=normalize(mat3(t*inv,b*inv,n)*bump);}
 float metal=material.x,rough=material.y;if(roughMapPresent>.5){vec4 mr=texture2D(roughMap,uv);metal*=mr.b;rough*=mr.g;}rough=clamp(rough,.12,1.0);
 // Filter subpixel normal variation into the specular lobe instead of sparkling highlights.
 vec3 dnX=dFdx(n),dnY=dFdy(n);float variance=min(.35,normalVariance+0.5*(dot(dnX,dnX)+dot(dnY,dnY)));rough=sqrt(min(1.0,rough*rough+variance));
 vec3 v=normalize(vec3(eye*aspect,0,900.0-depth)-position);float ao=aoMapPresent>.5?texture2D(aoMap,uv).r:1.0;
 vec3 radiance=base.rgb*(.24+.13*max(n.y,0.0))*ao;
 radiance+=light(n,v,normalize(vec3(-.6,.9,1)),base.rgb,metal,rough,vec3(2.8,2.7,2.6));
 radiance+=light(n,v,normalize(vec3(.8,.3,.4)),base.rgb,metal,rough,vec3(.75,.95,1.2));
 radiance+=light(n,v,normalize(vec3(.1,.65,-1)),base.rgb,metal,rough,vec3(1.2,1.5,1.8));
 vec3 emit=emission.xyz;if(emissionMapPresent>.5)emit*=pow(texture2D(emissionMap,uv).rgb,vec3(2.2));radiance+=emit;
 vec3 mapped=radiance/(radiance+vec3(.65));mapped=pow(mapped,vec3(1.0/2.2));gl_FragColor=vec4(mapped*base.a,base.a);
}
