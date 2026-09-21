using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public partial class DepthWindow {
 const int ArrivalDuration=3100;
 readonly RectangleF[] arrivalRegions={new RectangleF(0,0,1440,102),new RectangleF(0,140,1440,94),new RectangleF(0,240,1440,50),new RectangleF(0,840,1440,100)};
 readonly System.Collections.Generic.Dictionary<string,Bitmap> selectionLayers=new System.Collections.Generic.Dictionary<string,Bitmap>();
 string[] arrivalKeys;Bitmap[] arrivalLayers;bool drawingArrivalChrome;
 float ArrivalTime {get{return revealSince<0?ArrivalDuration:frameClock.ElapsedMilliseconds-revealSince;}}
 public static float ArrivalProgress(float elapsed,int order){return Smooth((elapsed-order*115)/1400f);}
 // Perspective moves position, size and binocular disparity together. Elements
 // begin outside the view beside the viewer, then settle at their exact UI plane.
 public static RectangleF ArrivalBounds(RectangleF destination,float eye,float progress,int order){
  float z=760*(1-progress),k=900/(900-z),side=order%2==0?-1:1;
  float cx=destination.X+destination.Width/2-720+side*850*(1-progress);
  float cy=destination.Y+destination.Height/2-470;
  return new RectangleF(720+cx*k+StereoMath.Parallax(eye,z)-destination.Width*k/2,470+cy*k-destination.Height*k/2,destination.Width*k,destination.Height*k);
 }
 void DisposeArrival(){
  var all=new System.Collections.Generic.HashSet<Bitmap>(selectionLayers.Values);if(arrivalLayers!=null)foreach(var image in arrivalLayers)if(image!=null)all.Add(image);
  foreach(var image in all){if(gpu!=null)gpu.Forget(image);image.Dispose();}selectionLayers.Clear();arrivalLayers=null;arrivalKeys=null;
 }
 void PrepareArrival(){
  if(arrivalLayers==null){arrivalLayers=new Bitmap[arrivalRegions.Length];arrivalKeys=new string[arrivalRegions.Length];}
  string[] keys={query, (FocusedCover==null?"":FocusedCover.Title)+"|"+shown.Count, filter+"|"+string.Join("|",Tabs()), (selected==null?"":selected.Platform)+"|"+page+"|"+shown.Count};drawingArrivalChrome=true;
  try{for(int i=0;i<arrivalRegions.Length;i++){if(arrivalLayers[i]!=null&&arrivalKeys[i]==keys[i])continue;bool reusable=i==1||i==3;string cacheKey=i+"|"+keys[i];Bitmap cached;
   if(reusable&&selectionLayers.TryGetValue(cacheKey,out cached)){arrivalLayers[i]=cached;arrivalKeys[i]=keys[i];continue;}
   if(arrivalLayers[i]!=null&&!reusable){if(gpu!=null)gpu.Forget(arrivalLayers[i]);arrivalLayers[i].Dispose();}arrivalKeys[i]=keys[i];var r=arrivalRegions[i];var b=new Bitmap((int)r.Width*2,(int)r.Height*2,PixelFormat.Format32bppPArgb);arrivalLayers[i]=b;if(reusable){
    if(selectionLayers.Count>=32){string victim=null;foreach(var pair in selectionLayers){if(Array.IndexOf(arrivalLayers,pair.Value)<0){victim=pair.Key;break;}}if(victim!=null){var old=selectionLayers[victim];if(gpu!=null)gpu.Forget(old);old.Dispose();selectionLayers.Remove(victim);}}
    selectionLayers[cacheKey]=b;
   }using(var g=Graphics.FromImage(b)){g.ScaleTransform(2,2);g.TranslateTransform(-r.X,-r.Y);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;DrawScene(g,0);}}}finally{drawingArrivalChrome=false;}
 }
 void ArrivalImage(Graphics g,Bitmap image,RectangleF destination,int order,int eye,int count,float width){
  float progress=ArrivalProgress(ArrivalTime,order);if(progress<=0)return;
  RectangleF r=ArrivalBounds(destination,Eye(eye,count),progress,order);
  if(gpu!=null)gpu.Image(image,Pixels(r,eye,width));else g.DrawImage(image,r);
 }
 void PaintOpeningBars(Graphics g,float alpha){
  PrepareArrival();int count=lib.Stereo?2:1;float width=ClientSize.Width/(float)count;
  if(gpu!=null)gpu.Begin(ClientSize.Width,ClientSize.Height);
  for(int eye=0;eye<count;eye++)foreach(int index in new[]{0,3}){
   RectangleF r=Pixels(arrivalRegions[index],eye,width);
   if(gpu!=null)gpu.Image(arrivalLayers[index],r,alpha);
   else using(var attributes=new ImageAttributes()){var matrix=new ColorMatrix();matrix.Matrix33=alpha;attributes.SetColorMatrix(matrix);g.DrawImage(arrivalLayers[index],Rectangle.Round(r),0,0,arrivalLayers[index].Width,arrivalLayers[index].Height,GraphicsUnit.Pixel,attributes);}
  }
 }
 void RenderArrival(Graphics g){
  PrepareArrival();if(gpu!=null)gpu.Begin(ClientSize.Width,ClientSize.Height);PaintOpeningGrid(g,1,1);
  int count=lib.Stereo?2:1;float width=ClientSize.Width/(float)count;
  for(int eye=0;eye<count;eye++){
   GraphicsState state=null;if(gpu!=null)gpu.Clip((int)(eye*width),0,(int)width,ClientSize.Height);else{state=g.Save();g.SetClip(new RectangleF(eye*width,0,width,ClientSize.Height));g.TranslateTransform(eye*width,0);g.ScaleTransform(width/1440,ClientSize.Height/940f);g.InterpolationMode=InterpolationMode.HighQualityBicubic;}
   if(ArrivalProgress(ArrivalTime,1)>0)DrawConsole(gpu!=null?null:g,eye,count,width);
   for(int i=1;i<=2;i++)ArrivalImage(g,arrivalLayers[i],arrivalRegions[i],i,eye,count,width);
   for(int slot=0;slot<Math.Min(10,shown.Count-page*10);slot++){
    Game game=shown[page*10+slot];ArrivalImage(g,Sprite(game),CardBounds(slot,Hover(game),Eye(eye,count)),3+slot,eye,count,width);
   }
   if(gpu!=null)gpu.EndClip();else g.Restore(state);
  }
  PaintOpeningBars(g,1);if(gpu!=null)gpu.Finish(false);
 }
}
