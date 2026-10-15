using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public partial class DepthWindow {
 bool omitPlatformTabs;
 readonly Dictionary<string,float> tabHover=new Dictionary<string,float>();
 readonly Dictionary<string,Bitmap> tabSprites=new Dictionary<string,Bitmap>();

 float TabHover(string tab){float amount;return tabHover.TryGetValue(tab,out amount)?amount:0;}
 RectangleF TabBounds(string tab,float eye){
  float x=52;foreach(string label in Tabs()){
   float width=label.Length*8.5f+32;
   if(label==tab){float z=80*TabHover(tab),k=900/(900-z);return new RectangleF(x+width*(1-k)/2+StereoMath.Parallax(eye-headX,z),TabTop+TabHeight/2*(1-k),width*k,TabHeight*k);}
   x+=width+8;
  }return RectangleF.Empty;
 }
 string PointedTab(){if(!pointerInside)return null;foreach(string tab in Tabs())if(TabBounds(tab,headX).Contains(pointer))return tab;return null;}
 void AnimateTabs(float blend){
  string pointed=PointedTab();bool changed=false;
  foreach(string tab in Tabs()){float value=TabHover(tab),target=(pointed==tab||platformFocused&&filter==tab)?1:0;
   if(Math.Abs(value-target)<.002f){if(value==target)continue;value=target;}else value+=(target-value)*blend;
   tabHover[tab]=value;changed=true;
  }
  if(changed)InvalidateLogical(new RectangleF(25,143,1390,60));
 }
 void DrawPlatformTabs(Graphics g,int eye,int count,float width){
  foreach(string tab in Tabs()){
   string key=tab+"|"+(filter==tab)+"|"+(platformFocused&&filter==tab);Bitmap image;
   if(!tabSprites.TryGetValue(key,out image)){
    int tw=(int)Math.Ceiling(tab.Length*8.5f+32);image=new Bitmap(tw*2,(int)TabHeight*2,PixelFormat.Format32bppPArgb);
    using(var bg=Graphics.FromImage(image)){bg.ScaleTransform(2,2);bg.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;Button(bg,"tab:"+tab,tab,new RectangleF(0,0,tw,TabHeight),filter==tab);if(platformFocused&&filter==tab)using(var pen=new Pen(Color.FromArgb(160,255,232),2))bg.DrawRectangle(pen,1,1,tw-2,TabHeight-2);}
    tabSprites[key]=image;
   }
   buttons["tab:"+tab]=TabBounds(tab,headX);RectangleF r=TabBounds(tab,Eye(eye,count));
   if(gpu!=null)gpu.Image(image,Pixels(r,eye,width));else g.DrawImage(image,r);
  }
 }
 float HoveredSurfaceDepth {get{if(over!=null)return 12+Hover(over)*180;string tab=PointedTab();return tab==null?0:80*TabHover(tab);}}
 void DisposePointerPresentation(){foreach(var image in tabSprites.Values){if(gpu!=null)gpu.Forget(image);image.Dispose();}tabSprites.Clear();}
}



