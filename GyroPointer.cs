using System;
using System.Drawing;
using System.Windows.Forms;

public sealed class GyroStreamWatchdog {
 ulong lastTimestamp;long lastFresh=-1,lastNonzero=-1,lastRestart=-2000;
 // A stationary controller may legitimately report exact zero angular rates.
 // Fresh timestamps prove that the IMU stream is alive; non-zero samples are
 // used only to decide when to gently re-enable a possibly idle sensor.
 public bool IsFresh(long now){return lastFresh>=0&&now-lastFresh<500;}
 public bool ShouldRestart(long now,bool opened){return now-lastRestart>=1500&&(!opened||!IsFresh(now)||lastNonzero<0||now-lastNonzero>=1500);}
 public void Restarted(long now){lastRestart=now;lastFresh=lastNonzero=now;lastTimestamp=0;}
 public void Observe(long now,ulong timestamp,float[] rate){
  if(timestamp==0||timestamp==lastTimestamp)return;lastTimestamp=timestamp;lastFresh=now;
  if(rate[0]!=0||rate[1]!=0||rate[2]!=0)lastNonzero=now;
 }
}

// Integrate only fresh sensor samples. Stable readings learn the stationary bias.
public sealed class GyroPointerFilter {
 readonly float[] mean=new float[3],bias=new float[3];
 ulong last;double stable;int samples;
 public bool Ready {get;private set;}
 public GyroPointerFilter(){}
 public GyroPointerFilter(float[] calibration){if(ValidCalibration(calibration)){Array.Copy(calibration,bias,3);Ready=true;}}
 static bool ValidCalibration(float[] value){if(value==null||value.Length!=3)return false;for(int i=0;i<3;i++)if(float.IsNaN(value[i])||float.IsInfinity(value[i])||Math.Abs(value[i])>20)return false;return true;}
 public float[] Calibration {get{return Ready?(float[])bias.Clone():null;}}
 public float DeltaX,DeltaY;
 public void Update(float[] rate,ulong timestamp){
  DeltaX=DeltaY=0;if(timestamp==0||timestamp==last)return;
  double dt=last==0||timestamp<last?0:(timestamp-last)/1000000.0;last=timestamp;
  if(dt<=0||dt>.1){stable=0;samples=0;return;}
  bool steady=samples>0;
  for(int i=0;i<3;i++)if(Math.Abs(rate[i]-mean[i])>.035f)steady=false;
  if(!steady){stable=0;samples=0;}
  samples++;stable+=dt;for(int i=0;i<3;i++)mean[i]+=(rate[i]-mean[i])/samples;
  if(stable>=.8){Array.Copy(mean,bias,3);Ready=true;}
  if(!Ready)return;
  float yaw=rate[1]-bias[1],pitch=rate[0]-bias[0];
  DeltaX=Math.Abs(yaw)>.012f?(float)(-yaw*dt*1800):0;
  DeltaY=Math.Abs(pitch)>.012f?(float)(-pitch*dt*1400):0;
 }
}

public partial class DepthWindow {
 bool gyroPointerActive;
 long lastLibraryForeground=-1000;string lastGyroDiagnostic="";
 bool MenuMayOwnGyro(bool foreground){
  if(foreground)lastLibraryForeground=frameClock.ElapsedMilliseconds;
  if(!Visible||activeGame!=null||ExternalDolphinRunning())return false;
  // Sensor ownership follows the menu/game lifetime, not foreground changes.
  // Hub takes foreground during conversion; reopening on every focus change
  // disables the IMU again and discards its calibration on emulator return.
  return true;
 }
 void RecordGyroStatus(bool enabled){
  string status="motion="+enabled+"; "+controllerInput.MotionStatus;
  if(status==lastGyroDiagnostic)return;lastGyroDiagnostic=status;
  try{System.IO.File.WriteAllText(System.IO.Path.Combine(Storage.Folder,"gyro-status.txt"),DateTime.Now.ToString("s")+" "+status);}catch{}
 }
 long nextDolphinCheck;bool externalDolphin;
 bool ExternalDolphinRunning(){
  if(frameClock.ElapsedMilliseconds<nextDolphinCheck)return externalDolphin;
  nextDolphinCheck=frameClock.ElapsedMilliseconds+1000;externalDolphin=false;
  foreach(var process in System.Diagnostics.Process.GetProcessesByName("Dolphin")){using(process){try{if(!process.HasExited)externalDolphin=true;}catch(InvalidOperationException){}}}
  return externalDolphin;
 }
 void UpdateGyroPointer(){
  bool recenter=(controllerInput.Pressed&(1u<<10))!=0;
  if(!controllerInput.MotionReady||controllerInput.Move!=0)return;
  float dx=controllerInput.GyroX,dy=controllerInput.GyroY;
  if(!recenter&&Math.Abs(dx)+Math.Abs(dy)<.01f)return;
  if(keyboardTileFocus){keyboardTileFocus=false;Invalidate();}
  InvalidatePointer();InvalidateCard(over);
  pointer=recenter?new PointF(720,470):new PointF(Math.Max(24,Math.Min(1416,pointer.X+dx)),Math.Max(24,Math.Min(916,pointer.Y+dy)));
  gyroPointerActive=true;pointerInside=true;lastMouseMotion=frameClock.ElapsedMilliseconds;if(platformFocused){platformFocused=false;Invalidate();}
  if(ScrollAnimating){over=null;InvalidatePointer();return;}
  if(panel!=""){over=null;UpdatePanelHover();InvalidatePointer();return;}
  RebuildHits();Game next=GameAt(pointer);if(next!=over){over=next;InvalidateLogical(new RectangleF(40,140,1360,90));if(next!=null)UiSound(false);}
  InvalidateCard(over);InvalidatePointer();
 }
 bool ActivateGyroPointer(uint pressed){
  if(!gyroPointerActive||!pointerInside||(pressed&1)==0)return false;
  lastMouseMotion=frameClock.ElapsedMilliseconds;InvalidatePointer();
  ClickScene(this,new MouseEventArgs(MouseButtons.Left,1,(int)(pointer.X*ClientSize.Width/1440),(int)(pointer.Y*ClientSize.Height/940),0));return true;
 }
}



