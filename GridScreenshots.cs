using System;
using System.Drawing;

public partial class DepthWindow {
 void UpdateGridVideoPlayback(){
  bool allowed=Visible&&WindowState!=System.Windows.Forms.FormWindowState.Minimized&&panel==""&&activeGame==null&&!CompanionOpeningBlocked&&secondaryReturnCurtain==null;
  UpdateCompanionVideo(FocusedCover,allowed,1);
 }
 void DisposeGridScreenshots(){DisposeGridVideoFrame();}
 void DrawGridScreenshot(Graphics g,int eye,int count,float width){
  if(companionVideo==null||!companionVideo.Playing||previewOpacity<.001f)return;
  Bitmap image=eye==0?GridVideoFrame():gridVideoFrame;if(image==null)return;float alpha=.52f*previewOpacity;
  if(gpu!=null){gpu.CurvedScreenshot(image,Eye(eye,count),headY,new RectangleF(eye*width,0,width,ClientSize.Height),alpha);return;}
  var state=g.Save();g.SetClip(new RectangleF(0,200,1440,688),System.Drawing.Drawing2D.CombineMode.Intersect);
  var points=new PointF[4];var uv=new PointF[4];for(int y=0;y<8;y++)for(int x=0;x<12;x++){for(int n=0;n<4;n++){float u=(x+(n==1||n==2?1:0))/12f,v=(y+(n>=2?1:0))/8f;uv[n]=new PointF(u,v);float wx=(u-.5f)*3000,wy=(v-.5f)*1900,z=GridDepth(wx,wy,1),k=900/(900-z);points[n]=new PointF(720+wx*k+Eye(eye,count)*(1-k),470+wy*k+headY*(1-k));}Color tint=Color.FromArgb((int)(alpha*255),255,255,255);CaseArtTriangle(g,image,points,uv,0,1,2,tint);CaseArtTriangle(g,image,points,uv,0,2,3,tint);}g.Restore(state);
 }
}
public sealed partial class GpuRenderer {
 uint screenshotSurface;
 readonly float[] screenshotMatrix={1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1};
 public void CurvedScreenshot(Bitmap image,float eye,float headY,RectangleF pane,float alpha){
  if(screenshotSurface==0){screenshotSurface=glGenLists(1);glNewList(screenshotSurface,0x1300);glBegin(7);for(int y=0;y<24;y++)for(int x=0;x<32;x++)for(int n=0;n<4;n++){float u=(x+(n==1||n==2?1:0))/32f,v=(y+(n>=2?1:0))/24f;float wx=(u-.5f)*3000,wy=(v-.5f)*1900,z=DepthWindow.GridDepth(wx,wy,1),k=900/(900-z);glTexCoord2f(u,v);glVertex3f(720+wx*k,470+wy*k,1-k);}glEnd();glEndList();}
  glEnable(0x0DE1);glBindTexture(0x0DE1,Texture(image));glColor4f(alpha,alpha,alpha,alpha);glPushMatrix();glTranslatef(pane.X,pane.Y,0);glScalef(pane.Width/1440,pane.Height/940,1);screenshotMatrix[8]=eye;screenshotMatrix[9]=headY;glMultMatrixf(screenshotMatrix);glCallList(screenshotSurface);glPopMatrix();
 }
}



