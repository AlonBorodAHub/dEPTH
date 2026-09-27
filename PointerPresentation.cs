using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public partial class DepthWindow {
 bool omitPlatformTabs;
 readonly Dictionary<string,float> tabHover=new Dictionary<string,float>();
 readonly Dictionary<string,Bitmap> tabSprites=new Dictionary<string,Bitmap>();
 Bitmap pointerShadow;
 float TabHover(string tab){float amount;return tabHover.TryGetValue(tab,out amount)?amount:0;}
 RectangleF TabBounds(string tab,float eye){
  float x=52;foreach(string label in Tabs()){
   float width=label.Length*8.5f+32;
   if(label==tab){float z=80*TabHover(tab),k=900/(900-z);return new RectangleF(x+width*(1-k)/2+StereoMath.Parallax(eye-headX,z),246+20*(1-k),width*k,40*k);}
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
  if(changed)InvalidateLogical(new RectangleF(25,227,1390,77));
 }
 void DrawPlatformTabs(Graphics g,int eye,int count,float width){
  foreach(string tab in Tabs()){
   string key=tab+"|"+(filter==tab)+"|"+(platformFocused&&filter==tab);Bitmap image;
   if(!tabSprites.TryGetValue(key,out image)){
    int tw=(int)Math.Ceiling(tab.Length*8.5f+32);image=new Bitmap(tw*2,80,PixelFormat.Format32bppPArgb);
    using(var bg=Graphics.FromImage(image)){bg.ScaleTransform(2,2);bg.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;Button(bg,"tab:"+tab,tab,new RectangleF(0,0,tw,40),filter==tab);if(platformFocused&&filter==tab)using(var pen=new Pen(Color.FromArgb(160,255,232),2))bg.DrawRectangle(pen,1,1,tw-2,38);}
    tabSprites[key]=image;
   }
   buttons["tab:"+tab]=TabBounds(tab,headX);RectangleF r=TabBounds(tab,Eye(eye,count));
   if(gpu!=null)gpu.Image(image,Pixels(r,eye,width));else g.DrawImage(image,r);
  }
 }
 float HoveredSurfaceDepth {get{if(over!=null)return 12+Hover(over)*180;string tab=PointedTab();return tab==null?0:80*TabHover(tab);}}
 bool ShadowTarget(float eye,out RectangleF bounds){
  if(over!=null){int slot=shown.IndexOf(over)-page*10;if(slot>=0&&slot<10){bounds=CardBounds(slot,Hover(over),eye);return true;}}
  string tab=PointedTab();if(tab!=null){bounds=TabBounds(tab,eye);return true;}
  foreach(var button in buttons)if(button.Value.Contains(pointer)){bounds=button.Value;return true;}
  bounds=RectangleF.Empty;return false;
 }
 void DrawPointerShadow(Graphics g,int eye,int count,float width){
  RectangleF target;if(!PointerVisible||!ShadowTarget(Eye(eye,count),out target))return;
  if(pointerShadow==null){
   pointerShadow=new Bitmap(80,80,PixelFormat.Format32bppPArgb);
   using(var bg=Graphics.FromImage(pointerShadow)){bg.SmoothingMode=SmoothingMode.AntiAlias;bg.TranslateTransform(40,40);using(var star=SoftStarPath()){
    for(int blur=16;blur>=2;blur-=2)using(var pen=new Pen(Color.FromArgb(5,0,8,20),blur)){pen.LineJoin=LineJoin.Round;bg.DrawPath(pen,star);}
    using(var brush=new SolidBrush(Color.FromArgb(52,0,8,20)))bg.FillPath(brush,star);
   }}
  }
  float x=pointer.X+StereoMath.Parallax(Eye(eye,count)-headX,HoveredSurfaceDepth)+6,y=pointer.Y+10;
  RectangleF destination=new RectangleF(x-40,y-40,80,80);
  if(gpu!=null){Rectangle clip=Rectangle.Intersect(Rectangle.Ceiling(Pixels(target,eye,width)),new Rectangle((int)(eye*width),0,(int)width,ClientSize.Height));if(clip.Width<=0||clip.Height<=0)return;gpu.Clip(clip.X,ClientSize.Height-clip.Bottom,clip.Width,clip.Height);gpu.Image(pointerShadow,Pixels(destination,eye,width));gpu.Clip((int)(eye*width),0,(int)width,ClientSize.Height);}
  else{var state=g.Save();if(over==null)g.SetClip(target,CombineMode.Intersect);else using(var path=Rounded(target,8))g.SetClip(path,CombineMode.Intersect);g.DrawImage(pointerShadow,destination);g.Restore(state);}
 }
 void DisposePointerPresentation(){foreach(var image in tabSprites.Values){if(gpu!=null)gpu.Forget(image);image.Dispose();}tabSprites.Clear();if(pointerShadow!=null){if(gpu!=null)gpu.Forget(pointerShadow);pointerShadow.Dispose();pointerShadow=null;}}
}

