using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public class EmulatorSource {
 public string Platform="",Executable="",GamesFolder="",Arguments="{rom}";
}
public static class PublicSetup {
 public static IEnumerable<string> Files(string root){
  if(string.IsNullOrWhiteSpace(root)||!Directory.Exists(root))yield break;
  var pending=new Stack<string>();pending.Push(root);
  while(pending.Count>0){string dir=pending.Pop();string[] files,dirs;
   try{files=Directory.GetFiles(dir);dirs=Directory.GetDirectories(dir);}catch(UnauthorizedAccessException){continue;}catch(IOException){continue;}
   foreach(string f in files)yield return f;
   foreach(string d in dirs){bool link;try{link=(File.GetAttributes(d)&FileAttributes.ReparsePoint)!=0;}catch{continue;}if(!link)pending.Push(d);}
  }
 }
 public static string Arguments(string platform){return DiscSystems.IsDolphin(platform)?"-b --config Dolphin.Display.Fullscreen=True -e {rom}":platform=="Dreamcast"?"-config window:fullscreen=yes {rom}":platform=="PS2"?"-fullscreen {rom}":platform=="PS3"?"--no-gui --fullscreen {rom}":platform=="Switch"?"-f -g {rom}":"{rom}";}
 public static List<Game> ScanSource(EmulatorSource source){
  var games=new List<Game>();
  string extensions=source.Platform=="3DS"?".3ds .cci .cxi .app":DiscSystems.IsDolphin(source.Platform)?".iso .gcm .rvz .wia .wbfs":source.Platform=="Dreamcast"?".chd .gdi .cdi":source.Platform=="PS2"?".iso .chd .cso .bin":source.Platform=="Switch"?".xci .nsp":"";
  foreach(string file in Files(source.GamesFolder)){
   bool ps3=source.Platform=="PS3"&&Path.GetFileName(file).Equals("EBOOT.BIN",StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(Path.GetDirectoryName(file)).Equals("USRDIR",StringComparison.OrdinalIgnoreCase);
   if(!ps3&&(extensions==""||!extensions.Split(' ').Contains(Path.GetExtension(file).ToLowerInvariant())))continue;
   string title=Path.GetFileNameWithoutExtension(file);
   if(ps3){string gameDir=Path.GetDirectoryName(Path.GetDirectoryName(file));string category=Storage.SfoField(Path.Combine(gameDir,"PARAM.SFO"),"CATEGORY");if(category!="HG"&&category!="DG")continue;title=Storage.SfoField(Path.Combine(gameDir,"PARAM.SFO"),"TITLE");if(title=="")title=Path.GetFileName(gameDir);}
   games.Add(new Game{Title=title,Platform=DiscSystems.IsDolphin(source.Platform)?DiscSystems.Detect(file,source.Platform):source.Platform,Executable=source.Executable,Rom=file,Arguments=source.Arguments,Note="Requires an existing working stereo configuration."});
  }return games;
 }
 public static bool Show(){
  bool fresh=!File.Exists(Storage.FileName);var library=Storage.Load();if(fresh){library.EnableStereoProfiles=false;library.ScanSteam=false;}
  using(var form=new Form{Text="dEPTH — Setup",ClientSize=new Size(890,610),StartPosition=FormStartPosition.CenterScreen,MinimumSize=new Size(906,649)}){
   var intro=new Label{Left=20,Top=16,Width=840,Height=86,Text="Welcome to dEPTH for Samsung Odyssey 3D.\nInstall and start Odyssey 3D Hub; enable automatic side-by-side conversion before opening the library.\nSelect your existing emulators and game folders below. No emulators, games, BIOS or keys are supplied.\nEmulator 3D settings and controller shortcuts must already be configured (see README)."};
   var table=new DataGridView{Left=20,Top=108,Width=850,Height=265,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,ReadOnly=true,RowHeadersVisible=false};
   table.Columns.Add("Platform","System");table.Columns.Add("Executable","Emulator");table.Columns.Add("Folder","Games folder");
   var sources=(library.Sources??new List<EmulatorSource>()).ToList();Action refresh=()=>{table.Rows.Clear();foreach(var s in sources)table.Rows.Add(s.Platform,s.Executable,s.GamesFolder);};refresh();
   var platform=new ComboBox{Left=20,Top=392,Width=135,DropDownStyle=ComboBoxStyle.DropDownList};platform.Items.AddRange(new object[]{"3DS","GameCube","Wii","Dreamcast","PS2","PS3","Switch"});platform.SelectedIndex=0;
   var add=new Button{Left=168,Top=390,Width=205,Text="Choose emulator + games…"};
   add.Click+=(s,e)=>{using(var exe=new OpenFileDialog{Filter="Emulator executable|*.exe",Title="Choose the existing emulator"}){if(exe.ShowDialog(form)!=DialogResult.OK)return;using(var folder=new FolderBrowserDialog{Description="Choose the folder containing this system's games (subfolders included). For RPCS3 digital games choose dev_hdd0/game."}){if(folder.ShowDialog(form)!=DialogResult.OK)return;string p=(string)platform.SelectedItem;sources.Add(new EmulatorSource{Platform=p,Executable=exe.FileName,GamesFolder=folder.SelectedPath,Arguments=Arguments(p)});refresh();}}};
   var remove=new Button{Left=387,Top=390,Width=150,Text="Remove selected"};remove.Click+=(s,e)=>{if(table.CurrentRow!=null){sources.RemoveAt(table.CurrentRow.Index);refresh();}};
   var steam=new CheckBox{Left=20,Top=438,Width=820,Text="Also scan installed Steam games (stereo compatibility varies by game)",Checked=library.ScanSteam};
   var info=new Label{Left=20,Top=470,Width=835,Height=53,Text="Keep this folder in a writable location. Your library and settings stay in its data folder.\nRun Setup.cmd to change folders later. Individual profiles and cover art can be edited inside dEPTH."};
   var status=new Label{Left=20,Top=535,Width=525,Height=45};
   var save=new Button{Left=560,Top=540,Width=180,Height=36,Text="Save and scan"};
   var cancel=new Button{Left=751,Top=540,Width=115,Height=36,Text="Cancel",DialogResult=DialogResult.Cancel};
   save.Click+=async(s,e)=>{save.Enabled=false;add.Enabled=false;remove.Enabled=false;cancel.Enabled=false;form.ControlBox=false;status.Text="Scanning your folders…";
    try{library.Sources=sources;library.Root="";library.ScanSteam=steam.Checked;await System.Threading.Tasks.Task.Run(()=>Storage.Scan(library));Storage.Save(library);form.DialogResult=DialogResult.OK;form.Close();}catch(Exception ex){status.Text=ex.Message;}finally{if(!form.IsDisposed){save.Enabled=add.Enabled=remove.Enabled=cancel.Enabled=true;form.ControlBox=true;}}};
   form.Controls.AddRange(new Control[]{intro,table,platform,add,remove,steam,info,status,save,cancel});return form.ShowDialog()==DialogResult.OK;
  }
 }
}
