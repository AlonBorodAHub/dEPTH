using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

public partial class DepthWindow {
 Game previewDwellGame;long previewDwellSince=-1;
 const int HoverDwellMilliseconds=350,ScrollDuration=380;
 long scrollSince=-1;int scrollOldPage;float scrollProgress;Game scrollTarget;
 bool ScrollAnimating {get{return scrollSince>=0;}}
 void CancelScrollAnimation(){scrollSince=-1;scrollTarget=null;ResetPreviewDwell();}
 RectangleF PreviewBounds(int index,bool above,float eye){
  SizeF size=CoverSize(shown[index]);float k=900f/888;
  return new RectangleF(186+(index%5)*260-size.Width*k/2+StereoMath.Parallax(eye-headX*.45f,12),above?290+UpperRowOffset:808,size.Width*k,ScrollPreviewHeight);
 }
 Game PreviewAt(PointF point){
  for(int side=0;side<2;side++){bool above=side==0;int start=above?page*10-5:page*10+10;if(start<0||start>=shown.Count)continue;
   for(int i=start;i<Math.Min(shown.Count,start+5);i++)if(PreviewBounds(i,above,headX).Contains(point))return shown[i];
  }return null;
 }
 void ResetPreviewDwell(){previewDwellGame=null;previewDwellSince=-1;}
 void UpdateHoverScroll(long now){
  if(ScrollAnimating){AdvanceScroll(now);return;}
  if(!PointerVisible||panel!=""||!Enabled||activeGame!=null||libraryMasked||introSince>=0||revealSince>=0||returnSince>=0){ResetPreviewDwell();return;}
  Game target=PreviewAt(pointer);if(target==null){ResetPreviewDwell();return;}
  if(target!=previewDwellGame){previewDwellGame=target;previewDwellSince=now;return;}
  if(now-previewDwellSince<HoverDwellMilliseconds)return;
  int index=shown.IndexOf(target);if(index<0){ResetPreviewDwell();return;}
  scrollOldPage=page;scrollSince=now;scrollProgress=0;scrollTarget=target;
  page=index/10;selected=target;over=null;keyboardTileFocus=false;platformFocused=false;ResetPreviewDwell();Invalidate();
 }
 void AdvanceScroll(long now){
  if(panel!=""||activeGame!=null||libraryMasked){CancelScrollAnimation();return;}
  scrollProgress=Smooth((now-scrollSince)/(float)ScrollDuration);
  int index=shown.IndexOf(scrollTarget);if(index<0){CancelScrollAnimation();return;}
  Invalidate();
  if(now-scrollSince<ScrollDuration)return;
  scrollSince=-1;scrollTarget=null;RebuildHits();over=pointerInside?GameAt(pointer):null;
 }
 RectangleF ScrollingCardBounds(int index,float eye){
  int owner=index/10;RectangleF r=CardBoundsFor(shown[index],index%10,0,eye);
  r.Y+=(owner-scrollOldPage)*454-(page-scrollOldPage)*454*scrollProgress;return r;
 }
 void DrawAnimatedGrid(Graphics g,int eye,int count,float width){
  RectangleF viewport=new RectangleF(0,306,1440,534);GraphicsState state=null;
  if(gpu!=null){Rectangle clip=Rectangle.Ceiling(Pixels(viewport,eye,width));gpu.Clip(clip.X,ClientSize.Height-clip.Bottom,clip.Width,clip.Height);}else{state=g.Save();g.SetClip(viewport,CombineMode.Intersect);}
  int start=Math.Max(0,(Math.Min(page,scrollOldPage)-1)*10),end=Math.Min(shown.Count,(Math.Max(page,scrollOldPage)+2)*10);
  for(int i=start;i<end;i++){
   RectangleF r=ScrollingCardBounds(i,Eye(eye,count));RectangleF visible=RectangleF.Intersect(r,viewport);if(visible.Height<=0)continue;
   float alpha=Math.Min(1,visible.Height/70);Bitmap image=Sprite(shown[i]);
   if(gpu!=null)gpu.Image(image,Pixels(r,eye,width),alpha);
   else using(var attributes=new ImageAttributes()){var matrix=new ColorMatrix();matrix.Matrix33=alpha;attributes.SetColorMatrix(matrix);g.DrawImage(image,Rectangle.Round(r),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);}
   if(alpha>=1){if(gpu!=null){RectangleF pixels=Pixels(r,eye,width);gpu.Outline(pixels,Color.FromArgb(125,207,234),1,pixels.Width/CoverSize(shown[i]).Width*8,pixels.Height/CoverSize(shown[i]).Height*8);}else using(var path=Rounded(r,r.Height*8/CoverSize(shown[i]).Height))using(var pen=new Pen(Color.FromArgb(125,207,234,237),1))g.DrawPath(pen,path);}
  }
  if(gpu!=null)gpu.Clip((int)(eye*width),0,(int)width,ClientSize.Height);else g.Restore(state);
 }
}
