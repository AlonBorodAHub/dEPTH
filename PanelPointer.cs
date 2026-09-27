using System;
using System.Drawing;
using System.Drawing.Drawing2D;

public partial class DepthWindow {
 RectangleF PanelItemBounds(int index){return panel=="about"?new RectangleF(280,660,880,42):panel=="keyboard"?new RectangleF(280+(index%10)*88,275+(index/10)*66,80,54):new RectangleF(280,204+index*48,880,42);}
 int PanelItemAt(PointF point){int count=panel=="keyboard"?KeyboardKeys().Length:MenuItems().Length;for(int i=0;i<count;i++)if(PanelItemBounds(i).Contains(point))return i;return -1;}
 void UpdatePanelHover(){int index=PanelItemAt(pointer);if(index<0)return;if(panel=="keyboard"){if(keyIndex==index)return;keyIndex=index;}else{if(menuIndex==index)return;menuIndex=index;}Invalidate();}
 void ClickPanel(PointF point){
  if(panel=="keyboard"&&new RectangleF(1000,748,160,40).Contains(point)){panel=keyboardReturnPanel;acceptText=null;Invalidate();return;}
  int index=PanelItemAt(point);if(index<0)return;UiSound(true);
  if(panel=="keyboard"){keyIndex=index;KeyboardSelect();}else{menuIndex=index;ActivateMenu();}Invalidate();
 }
 void DrawPanelPointer(Graphics g,int count,float width){
  if(!PointerVisible)return;
  for(int eye=0;eye<count;eye++){
   if(gpu!=null){gpu.Clip((int)(eye*width),0,(int)width,ClientSize.Height);DrawPointerTrail(null,eye,count,width);gpu.Image(pointerSprite,Pixels(new RectangleF(pointer.X+StereoMath.Parallax(Eye(eye,count)-headX,48)-32,pointer.Y-32,64,64),eye,width));gpu.EndClip();}
   else{var state=g.Save();g.SetClip(new RectangleF(eye*width,0,width,ClientSize.Height),CombineMode.Intersect);g.TranslateTransform(eye*width,0);g.ScaleTransform(width/1440,ClientSize.Height/940f);DrawPointerTrail(g,eye,count,width);PaintPointer(g,Eye(eye,count));g.Restore(state);}
  }
 }
}
