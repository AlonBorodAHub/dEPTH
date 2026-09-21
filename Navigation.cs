using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public partial class DepthWindow {
 Timer navigationTimer;
 string panel="",panelMessage="",editText="",editLabel="";
 int menuIndex=0,keyIndex=0;bool upper=false;
 Action<string> acceptText;string keyboardReturnPanel="";
 Game editingGame;
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 void InitializeNavigation(){navigationTimer=new Timer{Interval=16};navigationTimer.Tick+=(s,e)=>PollNavigation();navigationTimer.Start();}
 void PollNavigation(){
  long inputStamp=FrameTiming.Start();bool exit=controllerInput.Poll();FrameTiming.End("controller poll",inputStamp);
    HandleStereoButtons();
    if(activeGame!=null){if(exit&&closingSince<0)RequestExit();else HandleSaveStateButtons();return;}
  if(returnSince>=0||libraryMasked||introSince>=0||revealSince>=0||!Enabled)return;
  IntPtr foreground=GetForegroundWindow();if(foreground!=Handle){uint pid;GetWindowThreadProcessId(foreground,out pid);try{using(var p=Process.GetProcessById((int)pid)){if(p.ProcessName!="Odyssey3DPlayer")return;}}catch{return;}}
  if(exit){Close();return;}HandleNavigation(controllerInput.Move,controllerInput.Pressed);
 }
 string[] Tabs(){return new[]{"Library","Favorites"}.Concat(VisibleGames.Select(g=>g.Platform).Distinct()).ToArray();}
 void SelectTab(int delta){var tabs=Tabs();int i=Array.IndexOf(tabs,filter);filter=tabs[(Math.Max(0,i)+delta+tabs.Length)%tabs.Length];page=0;selected=null;RefreshGames();FocusGame();}
 void FocusGame(){over=selected;pointerInside=false;Invalidate();}
 void HandleNavigation(int move,uint pressed){
  if(move==0&&pressed==0)return;UiSound((pressed&1)!=0);
  pointerInside=false;
  bool confirm=(pressed&1)!=0,back=(pressed&2)!=0;
  if(panel=="keyboard"){
   var keys=KeyboardKeys();if(move!=0)keyIndex=NavigationIndex(keyIndex,move,keys.Length,10);
   if((pressed&(1u<<2))!=0&&editText.Length>0)editText=editText.Substring(0,editText.Length-1);
   if((pressed&(1u<<3))!=0)upper=!upper;
   if(back){panel=keyboardReturnPanel;acceptText=null;}
   else if((pressed&(1u<<6))!=0)FinishText();else if(confirm)KeyboardSelect();
   Invalidate();return;
  }
  if(panel!=""){
   var items=MenuItems();if(move!=0){int delta=move<0?-1:1;menuIndex=(menuIndex+delta+items.Length)%items.Length;}
   if(back){panel="";panelMessage="";}
   else if(confirm)ActivateMenu();Invalidate();return;
  }
  if((pressed&(1u<<9))!=0)SelectTab(-1);else if((pressed&(1u<<10))!=0)SelectTab(1);
  if(move!=0&&shown.Count>0){int i=Math.Max(0,shown.IndexOf(selected));i=NavigationIndex(i,move,shown.Count,5);selected=shown[i];page=i/10;FocusGame();}
  if(back&&query!=""){query="";RefreshGames();FocusGame();}
  if((pressed&(1u<<2))!=0)ToggleFavorite();
  if((pressed&(1u<<3))!=0)OpenSearch();
  if((pressed&(1u<<6))!=0){panel="menu";menuIndex=0;}
  else if(confirm&&selected!=null)Launch(selected);
  Invalidate();
 }
 public static int NavigationIndex(int index,int direction,int count,int columns){
  if(count<=0)return 0;
  if(direction==-1)return index%columns==0?index:index-1;
  if(direction==1)return index%columns==columns-1?index:Math.Min(count-1,index+1);
  return Math.Max(0,Math.Min(count-1,index+(direction<0?-columns:columns)));
 }
 void ToggleFavorite(){if(selected==null)return;var game=selected;game.Favorite=!game.Favorite;Storage.Save(lib);ClearRenderCache();RefreshGames();FocusGame();}
 void OpenSearch(){OpenKeyboard("Search",query,value=>{query=value;page=0;RefreshGames();FocusGame();});}
 void OpenKeyboard(string label,string value,Action<string> apply){keyboardReturnPanel=panel;editLabel=label;editText=value??"";acceptText=apply;keyIndex=0;panel="keyboard";panelMessage="";}
 string[] KeyboardKeys(){string chars=upper?"ABCDEFGHIJKLMNOPQRSTUVWXYZ":"abcdefghijklmnopqrstuvwxyz";return (chars+"0123456789 ._-:/\\{}\"()").Select(c=>c.ToString()).Concat(new[]{"Space","Delete","Case","Done"}).ToArray();}
 void KeyboardSelect(){var keys=KeyboardKeys();string key=keys[keyIndex];if(key=="Done"){FinishText();return;}if(key=="Case")upper=!upper;else if(key=="Delete"){if(editText.Length>0)editText=editText.Substring(0,editText.Length-1);}else editText+=key=="Space"?" ":key;}
 void FinishText(){var apply=acceptText;panel="";acceptText=null;if(apply!=null)apply(editText);}
 string[] MenuItems(){
  if(panel=="settings")return new[]{"Depth  −","Depth  +","Swap eyes: "+(lib.SwapEyes?"On":"Off"),"Library folder","Add game","Fullscreen","Head tracking: "+(lib.HeadTracking?"On":"Off"),"Back"};
  if(panel=="edit")return new[]{"Title","Platform","Executable","Arguments","ROM","Steam ID","Cover","Notes","Save","Cancel"};
  if(panel=="quit")return new[]{"Cancel","Quit dEPTH"};
  return new[]{"Play","Favorite","Search","Scan","Settings","Edit","Quit","Back"};
 }
 void ActivateMenu(){
  panelMessage="";
  if(panel=="quit"){if(menuIndex==1)Close();else panel="";return;}
  if(panel=="settings"){
   switch(menuIndex){case 0:lib.Depth=Math.Max(0,lib.Depth-.1f);break;case 1:lib.Depth=Math.Min(2,lib.Depth+.1f);break;case 2:lib.SwapEyes=!lib.SwapEyes;break;case 3:OpenKeyboard("Library folder",lib.Root,value=>{lib.Root=value;Storage.Save(lib);panel="settings";});return;case 4:editingOriginal=null;editingGame=new Game{Platform="Custom",Arguments="{rom}"};panel="edit";menuIndex=0;return;case 5:Fullscreen();return;case 6:lib.HeadTracking=!lib.HeadTracking;headCentered=false;break;default:panel="";return;}Storage.Save(lib);return;
  }
  if(panel=="edit"){
   string[] fields={"Title","Platform","Executable","Arguments","Rom","SteamId","Cover","Note"};
   if(menuIndex<8){string field=fields[menuIndex];OpenKeyboard(MenuItems()[menuIndex],(string)typeof(Game).GetField(field).GetValue(editingGame),value=>{typeof(Game).GetField(field).SetValue(editingGame,value);panel="edit";});return;}
   if(menuIndex==8){if(string.IsNullOrWhiteSpace(editingGame.Title)){panelMessage="Title required";return;}try{Storage.StartInfo(editingGame);}catch(Exception ex){panelMessage=ex.Message;return;}
    if(editingOriginal!=null){int i=lib.Games.IndexOf(editingOriginal);if(i>=0)lib.Games[i]=editingGame;}else lib.Games.Add(editingGame);Storage.Save(lib);selected=editingGame;ClearRenderCache();RefreshGames();FocusGame();}
   panel="";editingOriginal=null;return;
  }
  switch(menuIndex){case 0:panel="";if(selected!=null)Launch(selected);break;case 1:ToggleFavorite();break;case 2:OpenSearch();break;case 3:Scan();break;case 4:panel="settings";menuIndex=0;break;case 5:if(selected!=null){editingOriginal=selected;editingGame=Storage.Json.Deserialize<Game>(Storage.Json.Serialize(selected));panel="edit";menuIndex=0;}break;case 6:panel="quit";menuIndex=0;break;default:panel="";break;}
 }
 Game editingOriginal;
 void DrawControllerPanel(Graphics g){
  Fill(g,Color.FromArgb(250,12,25,40),new RectangleF(240,125,960,690));
  TextAt(g,panel=="keyboard"?editLabel:panel=="settings"?"Settings":panel=="edit"?"Edit":panel=="quit"?"Quit":"Menu",28,Color.White,280,153,880,FontStyle.Bold);
  if(panel=="keyboard"){
   TextAt(g,editText,21,Color.FromArgb(150,245,225),280,213,870);var keys=KeyboardKeys();for(int i=0;i<keys.Length;i++){RectangleF r=new RectangleF(280+(i%10)*88,275+(i/10)*66,80,54);Fill(g,i==keyIndex?Color.FromArgb(70,140,150):Color.FromArgb(28,52,67),r);TextAt(g,keys[i],16,Color.White,r.X+9,r.Y+15,r.Width-12);}
   TextAt(g,"A  Select    B  Back    X  Delete    Y  Case    +  Done",15,Color.LightGray,280,752,880);
  }else{var items=MenuItems();for(int i=0;i<items.Length;i++){RectangleF r=new RectangleF(280,204+i*48,880,42);if(i==menuIndex)Fill(g,Color.FromArgb(46,102,119),r);TextAt(g,items[i],19,Color.White,r.X+14,r.Y+7,850);}TextAt(g,panelMessage!=""?panelMessage:panel=="settings"?"Depth: "+lib.Depth.ToString("0.0")+"     A  Select    B  Back":"A  Select    B  Back",14,Color.LightGray,280,752,880);}
 }
}
