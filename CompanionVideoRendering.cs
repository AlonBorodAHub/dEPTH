using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public partial class DepthWindow {
 Bitmap gridVideoFrame;byte[] gridVideoPixels,gridVideoFeather;long gridVideoSerial=-1;CompanionVideoView gridVideoOwner;
 void DisposeGridVideoFrame(){if(gridVideoFrame!=null){if(gpu!=null)gpu.Forget(gridVideoFrame);gridVideoFrame.Dispose();gridVideoFrame=null;}gridVideoPixels=gridVideoFeather=null;gridVideoSerial=-1;gridVideoOwner=null;}
 Bitmap GridVideoFrame(){
  long serial=companionVideo.FrameSerial;
  if(gridVideoFrame!=null&&gridVideoOwner==companionVideo&&gridVideoSerial==serial&&gridVideoPath==companionVideo.PathName)return gridVideoFrame;
  bool changed;Bitmap source=companionVideo.Frame(out changed);if(source==null)return null;
  if(gridVideoFrame==null||gridVideoFrame.Size!=source.Size){
   DisposeGridVideoFrame();int w=source.Width,h=source.Height;gridVideoFrame=new Bitmap(w,h,PixelFormat.Format32bppPArgb);gridVideoPixels=new byte[w*h*4];gridVideoFeather=new byte[w*h];
   for(int y=0;y<h;y++)for(int x=0;x<w;x++){float edge=Math.Min(Math.Min(x/(w*.143f),(w-1-x)/(w*.143f)),Math.Min(y/(h*.166f),(h-1-y)/(h*.166f)));edge=Math.Max(0,Math.Min(1,edge));gridVideoFeather[y*w+x]=(byte)(255*edge*edge*(3-2*edge));}
  }
  var rect=new Rectangle(0,0,source.Width,source.Height);var data=source.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
  try{Marshal.Copy(data.Scan0,gridVideoPixels,0,gridVideoPixels.Length);}finally{source.UnlockBits(data);}
  for(int i=0,p=0;i<gridVideoFeather.Length;i++,p+=4){int edge=gridVideoFeather[i];gridVideoPixels[p]=(byte)(gridVideoPixels[p]*edge/255);gridVideoPixels[p+1]=(byte)(gridVideoPixels[p+1]*edge/255);gridVideoPixels[p+2]=(byte)(gridVideoPixels[p+2]*edge/255);gridVideoPixels[p+3]=(byte)edge;}
  data=gridVideoFrame.LockBits(rect,ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);try{Marshal.Copy(gridVideoPixels,0,data.Scan0,gridVideoPixels.Length);}finally{gridVideoFrame.UnlockBits(data);}
  gridVideoOwner=companionVideo;gridVideoPath=companionVideo.PathName;gridVideoSerial=serial;if(gpu!=null)gpu.RefreshImage(gridVideoFrame);return gridVideoFrame;
 }
 string gridVideoPath="";
}
