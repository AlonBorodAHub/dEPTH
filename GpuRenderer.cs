using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// Windows OpenGL compositor. Artwork is rasterized once, uploaded once, then
// animated as GPU textures. No driver-specific SDK or package is required.
public sealed partial class GpuRenderer:IDisposable {
 IntPtr window,dc,context;Dictionary<Bitmap,uint> textures=new Dictionary<Bitmap,uint>();
 public string Device;
 public int AntialiasSamples {get;private set;}
 [DllImport("gdi32.dll")] static extern int DescribePixelFormat(IntPtr dc,int format,uint bytes,ref Pfd p);
 [DllImport("opengl32.dll")] static extern void glGetIntegerv(uint name,out int value);
 [DllImport("opengl32.dll")] static extern void glGetFloatv(uint name,out float value);
 [DllImport("opengl32.dll")] static extern void glTexParameterf(uint target,uint name,float value);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate bool ChooseAaFormatFn(IntPtr dc,int[] attributes,IntPtr floats,uint maximum,int[] formats,out uint count);
 // WGL extensions require a temporary context; a window's pixel format can only be set once.
 int MultisampleFormat(IntPtr target){
  using(var bootstrap=new System.Windows.Forms.Form()){
   IntPtr bootstrapDc=GetDC(bootstrap.Handle),bootstrapContext=IntPtr.Zero;
   try{var p=new Pfd{size=40,version=1,flags=0x25,colorBits=32,alphaBits=8,depthBits=24};int basic=ChoosePixelFormat(bootstrapDc,ref p);
    if(basic==0||!SetPixelFormat(bootstrapDc,basic,ref p))return 0;
    bootstrapContext=wglCreateContext(bootstrapDc);if(bootstrapContext==IntPtr.Zero||!wglMakeCurrent(bootstrapDc,bootstrapContext))return 0;
    IntPtr address=wglGetProcAddress("wglChoosePixelFormatARB");if(address==IntPtr.Zero||address.ToInt64()<=3||address.ToInt64()==-1)return 0;
    var choose=(ChooseAaFormatFn)Marshal.GetDelegateForFunctionPointer(address,typeof(ChooseAaFormatFn));
    foreach(int samples in new[]{8,4,2}){int[] attrs={0x2001,1,0x2010,1,0x2011,1,0x2013,0x202B,0x2014,32,0x201B,8,0x2022,24,0x2041,1,0x2042,samples,0};var formats=new int[1];uint count;if(choose(target,attrs,IntPtr.Zero,1,formats,out count)&&count>0)return formats[0];}
    return 0;
   }finally{wglMakeCurrent(IntPtr.Zero,IntPtr.Zero);if(bootstrapContext!=IntPtr.Zero)wglDeleteContext(bootstrapContext);ReleaseDC(bootstrap.Handle,bootstrapDc);}
  }
 }
 [StructLayout(LayoutKind.Sequential)] struct Pfd {
  public ushort size,version;public uint flags;public byte pixelType,colorBits,redBits,redShift,greenBits,greenShift,blueBits,blueShift,alphaBits,alphaShift,accumBits,accumRed,accumGreen,accumBlue,accumAlpha,depthBits,stencilBits,auxBuffers,layerType,reserved;public uint layerMask,visibleMask,damageMask;
 }
 [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr w);
 [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr w,IntPtr dc);
 [DllImport("gdi32.dll")] static extern int ChoosePixelFormat(IntPtr dc,ref Pfd p);
 [DllImport("gdi32.dll")] static extern bool SetPixelFormat(IntPtr dc,int i,ref Pfd p);
 [DllImport("gdi32.dll")] static extern bool SwapBuffers(IntPtr dc);
 [DllImport("opengl32.dll")] static extern IntPtr wglCreateContext(IntPtr dc);
 [DllImport("opengl32.dll")] static extern bool wglMakeCurrent(IntPtr dc,IntPtr rc);
 [DllImport("opengl32.dll")] static extern bool wglDeleteContext(IntPtr rc);
 [DllImport("opengl32.dll")] static extern IntPtr glGetString(uint n);
 [DllImport("opengl32.dll")] static extern void glViewport(int x,int y,int w,int h);
 [DllImport("opengl32.dll")] static extern void glMatrixMode(uint mode);
 [DllImport("opengl32.dll")] static extern void glLoadIdentity();
 [DllImport("opengl32.dll")] static extern void glOrtho(double l,double r,double b,double t,double n,double f);
 [DllImport("opengl32.dll")] static extern void glEnable(uint mode);
 [DllImport("opengl32.dll")] static extern void glDisable(uint mode);
 [DllImport("opengl32.dll")] static extern void glBlendFunc(uint s,uint d);
 [DllImport("opengl32.dll")] static extern void glGenTextures(int count,out uint texture);
 [DllImport("opengl32.dll")] static extern void glDeleteTextures(int count,ref uint texture);
 [DllImport("opengl32.dll")] static extern void glBindTexture(uint target,uint texture);
 [DllImport("opengl32.dll")] static extern void glTexParameteri(uint target,uint pname,int value);
 [DllImport("opengl32.dll")] static extern void glTexImage2D(uint target,int level,int internalFormat,int w,int h,int border,uint format,uint type,IntPtr pixels);
 [DllImport("opengl32.dll")] static extern void glBegin(uint mode);
 [DllImport("opengl32.dll")] static extern void glEnd();
 [DllImport("opengl32.dll")] static extern void glVertex2f(float x,float y);
 [DllImport("opengl32.dll")] static extern void glTexCoord2f(float x,float y);
 [DllImport("opengl32.dll")] static extern void glColor4f(float r,float g,float b,float a);
 [DllImport("opengl32.dll")] static extern void glLineWidth(float w);
 [DllImport("opengl32.dll")] static extern void glFinish();
 [DllImport("opengl32.dll")] static extern void glClear(uint mask);
 [DllImport("opengl32.dll")] static extern void glClearColor(float r,float g,float b,float a);
 [DllImport("opengl32.dll")] static extern void glScissor(int x,int y,int w,int h);
 public void Clip(int x,int y,int w,int h){glEnable(0x0C11);glScissor(x,y,w,h);}
 public void EndClip(){glDisable(0x0C11);}
 public void Black(){glClearColor(0,0,0,1);glClear(0x4000);}
 public GpuRenderer(IntPtr hwnd):this(hwnd,true){}
 public GpuRenderer(IntPtr hwnd,bool antialias){window=hwnd;dc=GetDC(hwnd);var p=new Pfd{size=40,version=1,flags=0x25,colorBits=32,alphaBits=8,depthBits=24,layerType=0};int format=antialias?MultisampleFormat(dc):0;if(format==0)format=ChoosePixelFormat(dc,ref p);else DescribePixelFormat(dc,format,40,ref p);if(format==0||!SetPixelFormat(dc,format,ref p))throw new Exception("OpenGL pixel format unavailable");context=wglCreateContext(dc);if(context==IntPtr.Zero||!wglMakeCurrent(dc,context))throw new Exception("OpenGL context unavailable");Device=Marshal.PtrToStringAnsi(glGetString(0x1F01));int samples;glGetIntegerv(0x80A9,out samples);AntialiasSamples=samples;if(samples>0)glEnable(0x809D);glEnable(0x0BE2);glBlendFunc(1,0x0303);}
 public void Begin(int width,int height){wglMakeCurrent(dc,context);glViewport(0,0,width,height);glMatrixMode(0x1701);glLoadIdentity();glOrtho(0,width,height,0,-1,1);glMatrixMode(0x1700);glLoadIdentity();}
 public void Forget(Bitmap b){uint id;if(b!=null&&textures.TryGetValue(b,out id)){wglMakeCurrent(dc,context);glDeleteTextures(1,ref id);textures.Remove(b);}}
 uint Texture(Bitmap b){uint id;if(textures.TryGetValue(b,out id))return id;glGenTextures(1,out id);glBindTexture(0x0DE1,id);glTexParameteri(0x0DE1,0x2801,0x2601);glTexParameteri(0x0DE1,0x2800,0x2601);glTexParameteri(0x0DE1,0x2802,0x812F);glTexParameteri(0x0DE1,0x2803,0x812F);var pixels=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);try{glTexImage2D(0x0DE1,0,0x1908,b.Width,b.Height,0,0x80E1,0x1401,pixels.Scan0);}finally{b.UnlockBits(pixels);}textures.Add(b,id);return id;}
 public void Image(Bitmap b,RectangleF r){Image(b,r,1);}
 public void PreloadImage(Bitmap b){Texture(b);}
 public void Image(Bitmap b,RectangleF r,float alpha){glEnable(0x0DE1);glBindTexture(0x0DE1,Texture(b));glColor4f(alpha,alpha,alpha,alpha);glBegin(7);glTexCoord2f(0,0);glVertex2f(r.Left,r.Top);glTexCoord2f(1,0);glVertex2f(r.Right,r.Top);glTexCoord2f(1,1);glVertex2f(r.Right,r.Bottom);glTexCoord2f(0,1);glVertex2f(r.Left,r.Bottom);glEnd();}
 public void Outline(RectangleF r,Color c,float width,float rx,float ry){glDisable(0x0DE1);glColor4f(c.R/255f,c.G/255f,c.B/255f,1);glLineWidth(width);glBegin(2);for(int corner=0;corner<4;corner++){float x=corner==0||corner==3?r.Left+rx:r.Right-rx,y=corner<2?r.Top+ry:r.Bottom-ry;for(int step=0;step<=8;step++){double angle=(180+corner*90+step*90/8.0)*Math.PI/180;glVertex2f(x+rx*(float)Math.Cos(angle),y+ry*(float)Math.Sin(angle));}}glEnd();}
 public void BlackOverlay(int width,int height,float alpha){glDisable(0x0DE1);glColor4f(0,0,0,alpha);glBegin(7);glVertex2f(0,0);glVertex2f(width,0);glVertex2f(width,height);glVertex2f(0,height);glEnd();}
 public void Finish(bool measure){if(measure)glFinish();SwapBuffers(dc);}
 public void Dispose(){if(context!=IntPtr.Zero){wglMakeCurrent(dc,context);DisposeImportedModels();if(trackedGrid!=0)glDeleteLists(trackedGrid,1);if(titleLists!=0)glDeleteLists(titleLists,96);foreach(uint v in textures.Values){uint id=v;glDeleteTextures(1,ref id);}textures.Clear();wglMakeCurrent(IntPtr.Zero,IntPtr.Zero);wglDeleteContext(context);context=IntPtr.Zero;}if(dc!=IntPtr.Zero){ReleaseDC(window,dc);dc=IntPtr.Zero;}}
}
