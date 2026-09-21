#version 120
uniform float angle, eye, aspect, depth, centerY, modelScale, title, headY;
varying vec3 position, normal;
varying vec2 uv;
varying vec4 color;
vec3 rotate(vec3 v){float c=cos(angle),s=sin(angle);vec3 p=vec3(v.x*c+v.z*s,v.y,-v.x*s+v.z*c);float tilt=title>0.5?0.0:0.26;return vec3(p.x,p.y*cos(tilt)-p.z*sin(tilt),p.y*sin(tilt)+p.z*cos(tilt));}
void main(){
 position=rotate((gl_ModelViewMatrix*gl_Vertex).xyz*modelScale);
 normal=rotate(normalize(gl_NormalMatrix*gl_Normal));uv=gl_MultiTexCoord0.xy;color=gl_Color;
 float w=(900.0-depth-position.z)/900.0;
 gl_Position=vec4(position.x/(aspect*720.0)+eye*(w-1.0)/720.0,((470.0-centerY)*w+position.y-headY*(w-1.0))/470.0,w-0.1,w);
}
