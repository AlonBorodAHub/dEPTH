using System;
using System.Drawing;
using System.Runtime.InteropServices;

public sealed partial class GpuRenderer {
 [DllImport("opengl32.dll")]static extern void glMultMatrixf(float[] matrix);
 uint trackedGrid;
 public void OpeningGrid(float eye,float curvature,RectangleF pane,float alpha,float headY=0){
  glDisable(0x0DE1);float a=48/255f*alpha;glColor4f(193/255f*a,240/255f*a,244/255f*a,a);glLineWidth(Math.Max(1,pane.Height/940));
  if(curvature==1){
   if(trackedGrid==0){trackedGrid=glGenLists(1);glNewList(trackedGrid,0x1300);for(int axis=0;axis<2;axis++)for(int n=-20;n<=20;n++){glBegin(3);for(int step=-40;step<=40;step++){float x=axis==0?n*130:step*65,y=axis==0?step*50:n*110,z=DepthWindow.GridDepth(x,y,1),k=900/(900-z);glVertex3f(720+x*k,470+y*k,1-k);}glEnd();}glEndList();}
   glPushMatrix();glTranslatef(pane.X,pane.Y,0);glScalef(pane.Width/1440,pane.Height/940,1);glMultMatrixf(new float[]{1,0,0,0,0,1,0,0,eye,headY,1,0,0,0,0,1});glCallList(trackedGrid);glPopMatrix();return;
  }
  for(int axis=0;axis<2;axis++)for(int n=-20;n<=20;n++){glBegin(3);for(int step=-40;step<=40;step++){float x=axis==0?n*130:step*65,y=axis==0?step*50:n*110,z=DepthWindow.GridDepth(x,y,curvature),k=900/(900-z);glVertex2f(pane.X+(720+x*k+eye*(1-k))*pane.Width/1440,pane.Y+(470+y*k)*pane.Height/940);}glEnd();}
 }
 uint titleLists;float titleWidth;
 [StructLayout(LayoutKind.Sequential)] struct GlyphMetric {public float width,height,x,y,advanceX,advanceY;}
 [DllImport("opengl32.dll")] static extern uint glGenLists(int count);
 [DllImport("opengl32.dll")] static extern void glDeleteLists(uint first,int count);
 [DllImport("opengl32.dll")] static extern void glCallList(uint id);
 [DllImport("opengl32.dll")] static extern void glPushMatrix();
 [DllImport("opengl32.dll")] static extern void glPopMatrix();
 [DllImport("opengl32.dll")] static extern void glTranslatef(float x,float y,float z);
 [DllImport("opengl32.dll")] static extern void glScalef(float x,float y,float z);
 [DllImport("opengl32.dll",CharSet=CharSet.Unicode)] static extern bool wglUseFontOutlinesW(IntPtr dc,uint first,uint count,uint lists,float deviation,float extrusion,int format,[Out] GlyphMetric[] metrics);
 [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
 [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
 public void PrepareTitle(){
  if(titleLists==0){
   using(var font=new Font("Segoe UI",256,FontStyle.Bold,GraphicsUnit.Pixel)){
    IntPtr native=font.ToHfont(),old=SelectObject(dc,native);uint lists=glGenLists(96);
    var metrics=new GlyphMetric[96];bool ok;
    try{ok=wglUseFontOutlinesW(dc,32,96,lists,.00002f,.12f,1,metrics);}finally{SelectObject(dc,old);DeleteObject(native);}
    if(!ok){glDeleteLists(lists,96);throw new InvalidOperationException("Unable to create vector title outlines");}
    titleLists=lists;foreach(char c in "dEPTH")titleWidth+=metrics[c-32].advanceX;
   }
  }
 }
 public void VectorTitle(float centerX,float centerY,float em,float horizontalScale){
  PrepareTitle();glDisable(0x0DE1);glColor4f(1,1,1,1);glPushMatrix();
  glTranslatef(centerX,centerY,0);glScalef(em*horizontalScale,-em,1);glTranslatef(-titleWidth/2,-.36f,0);
  foreach(char c in "dEPTH")glCallList(titleLists+c-32);
  glPopMatrix();
 }
 public void WhiteOverlay(int width,int height,float alpha){
  alpha=Math.Max(0,Math.Min(1,alpha));glDisable(0x0DE1);glColor4f(alpha,alpha,alpha,alpha);
  glBegin(7);glVertex2f(0,0);glVertex2f(width,0);glVertex2f(width,height);glVertex2f(0,height);glEnd();
 }
}
