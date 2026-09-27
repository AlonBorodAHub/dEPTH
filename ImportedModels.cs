using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

public sealed class ImportedModel {
 public sealed class Material {public string name,alpha;public float[] @base,emissive;public float metal,rough,cutoff,normalScale;public string[] textures;}
 public sealed class Batch {public int Material;public float[] Vertices;public uint[] Indices;public uint List,VertexBuffer,IndexBuffer;}
 public string Folder;public Material[] Materials;public List<Batch> Batches=new List<Batch>();
 public static ImportedModel Load(string folder){
  var model=new ImportedModel{Folder=folder};model.Materials=new JavaScriptSerializer().Deserialize<Material[]>(File.ReadAllText(Path.Combine(folder,"materials.json")));
  using(var r=new BinaryReader(File.OpenRead(Path.Combine(folder,"model.mesh")))){
   if(new string(r.ReadChars(4))!="DMD1")throw new InvalidDataException("Invalid console mesh");int count=r.ReadInt32();if(count<1||count>10000)throw new InvalidDataException("Invalid batch count");
   for(int i=0;i<count;i++){int material=r.ReadInt32(),vertices=r.ReadInt32(),indices=r.ReadInt32();if(material<0||material>=model.Materials.Length||vertices<1||vertices>2000000||indices<1||indices>6000000)throw new InvalidDataException("Invalid model bounds");var b=new Batch{Material=material,Vertices=new float[vertices*12],Indices=new uint[indices]};byte[] v=r.ReadBytes(vertices*48),ix=r.ReadBytes(indices*4);if(v.Length!=vertices*48||ix.Length!=indices*4)throw new InvalidDataException("Truncated model mesh");Buffer.BlockCopy(v,0,b.Vertices,0,v.Length);Buffer.BlockCopy(ix,0,b.Indices,0,ix.Length);for(int n=0;n<indices;n++)if(b.Indices[n]>=vertices)throw new InvalidDataException("Invalid model index");model.Batches.Add(b);}
  }return model;
 }
}
public sealed partial class GpuRenderer {
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void GenBuffersFn(int n,out uint buffer);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void BindBufferFn(uint target,uint buffer);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void BufferDataFn(uint target,IntPtr size,IntPtr data,uint usage);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void DeleteBuffersFn(int n,ref uint buffer);
 [DllImport("opengl32.dll")] static extern void glEnableClientState(uint array);
 [DllImport("opengl32.dll")] static extern void glDisableClientState(uint array);
 [DllImport("opengl32.dll")] static extern void glVertexPointer(int size,uint type,int stride,IntPtr pointer);
 [DllImport("opengl32.dll")] static extern void glNormalPointer(uint type,int stride,IntPtr pointer);
 [DllImport("opengl32.dll")] static extern void glTexCoordPointer(int size,uint type,int stride,IntPtr pointer);
 [DllImport("opengl32.dll")] static extern void glColorPointer(int size,uint type,int stride,IntPtr pointer);
 [DllImport("opengl32.dll")] static extern void glDrawElements(uint mode,int count,uint type,IntPtr indices);
 GenBuffersFn genBuffers;BindBufferFn bindBuffer;BufferDataFn bufferData;DeleteBuffersFn deleteBuffers;
 void UploadBatch(ImportedModel.Batch b){
  if(genBuffers==null){genBuffers=Proc<GenBuffersFn>("glGenBuffers");bindBuffer=Proc<BindBufferFn>("glBindBuffer");bufferData=Proc<BufferDataFn>("glBufferData");deleteBuffers=Proc<DeleteBuffersFn>("glDeleteBuffers");}
  genBuffers(1,out b.VertexBuffer);genBuffers(1,out b.IndexBuffer);
  var v=GCHandle.Alloc(b.Vertices,GCHandleType.Pinned);var ix=GCHandle.Alloc(b.Indices,GCHandleType.Pinned);
  try{bindBuffer(0x8892,b.VertexBuffer);bufferData(0x8892,new IntPtr(b.Vertices.Length*4),v.AddrOfPinnedObject(),0x88E4);bindBuffer(0x8893,b.IndexBuffer);bufferData(0x8893,new IntPtr(b.Indices.Length*4),ix.AddrOfPinnedObject(),0x88E4);}finally{v.Free();ix.Free();bindBuffer(0x8892,0);bindBuffer(0x8893,0);}
 }
 void DrawBatch(ImportedModel.Batch b){
  bindBuffer(0x8892,b.VertexBuffer);bindBuffer(0x8893,b.IndexBuffer);
  glEnableClientState(0x8074);glEnableClientState(0x8075);glEnableClientState(0x8078);glEnableClientState(0x8076);
  glVertexPointer(3,0x1406,48,IntPtr.Zero);glNormalPointer(0x1406,48,new IntPtr(12));glTexCoordPointer(2,0x1406,48,new IntPtr(24));glColorPointer(4,0x1406,48,new IntPtr(32));glDrawElements(4,b.Indices.Length,0x1405,IntPtr.Zero);
  glDisableClientState(0x8074);glDisableClientState(0x8075);glDisableClientState(0x8078);glDisableClientState(0x8076);bindBuffer(0x8892,0);bindBuffer(0x8893,0);
 }
 [DllImport("opengl32.dll")] static extern IntPtr wglGetProcAddress(string name);
 [DllImport("opengl32.dll")] static extern void glNewList(uint list,uint mode);
 [DllImport("opengl32.dll")] static extern void glEndList();
 [DllImport("opengl32.dll")] static extern void glNormal3f(float x,float y,float z);
 [DllImport("opengl32.dll")] static extern void glDepthMask(byte enabled);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint CreateShaderFn(uint type);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void ShaderSourceFn(uint shader,int count,IntPtr strings,IntPtr lengths);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void ShaderFn(uint shader);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint CreateProgramFn();
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void AttachFn(uint program,uint shader);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void GetParamFn(uint obj,uint name,out int value);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void InfoFn(uint obj,int length,out int written,StringBuilder log);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int LocationFn(uint program,string name);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void Uniform1Fn(int location,float x);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void UniformIFn(int location,int x);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void Uniform4Fn(int location,float x,float y,float z,float w);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void ActiveFn(uint unit);
 CreateShaderFn createShader;ShaderSourceFn shaderSource;ShaderFn compileShader,linkProgram,useProgram,deleteShader,deleteProgram;CreateProgramFn createProgram;AttachFn attachShader;GetParamFn shaderParam,programParam;InfoFn shaderInfo,programInfo;LocationFn location;Uniform1Fn uniform1;UniformIFn uniformI;Uniform4Fn uniform4;ActiveFn activeTexture;
 uint modelProgram;Dictionary<string,int> modelUniforms=new Dictionary<string,int>();
 ShaderFn generateModelMipmaps;HashSet<uint> filteredModelTextures=new HashSet<uint>();
 public float ModelAnisotropy {get;private set;}
 Dictionary<string,ImportedModel> importedModels=new Dictionary<string,ImportedModel>();Dictionary<string,Bitmap> modelImages=new Dictionary<string,Bitmap>();
 T Proc<T>(string name) where T:class{IntPtr p=wglGetProcAddress(name);if(p==IntPtr.Zero||p.ToInt64()==-1||p.ToInt64()<4)throw new InvalidOperationException("OpenGL function unavailable: "+name);return Marshal.GetDelegateForFunctionPointer(p,typeof(T)) as T;}
 uint Shader(uint type,string source){uint shader=createShader(type);IntPtr str=Marshal.StringToHGlobalAnsi(source),array=Marshal.AllocHGlobal(IntPtr.Size);try{Marshal.WriteIntPtr(array,str);shaderSource(shader,1,array,IntPtr.Zero);}finally{Marshal.FreeHGlobal(array);Marshal.FreeHGlobal(str);}compileShader(shader);int ok;shaderParam(shader,0x8B81,out ok);if(ok==0){int size;var log=new StringBuilder(4096);shaderInfo(shader,4096,out size,log);deleteShader(shader);throw new InvalidOperationException(log.ToString());}return shader;}
 void PrepareModelProgram(){if(modelProgram!=0)return;
  generateModelMipmaps=Proc<ShaderFn>("glGenerateMipmap");string extensions=Marshal.PtrToStringAnsi(glGetString(0x1F03))??"";ModelAnisotropy=1;
  if(extensions.Contains("GL_EXT_texture_filter_anisotropic")){float maximum;glGetFloatv(0x84FF,out maximum);ModelAnisotropy=Math.Min(16,maximum);}
  createShader=Proc<CreateShaderFn>("glCreateShader");shaderSource=Proc<ShaderSourceFn>("glShaderSource");compileShader=Proc<ShaderFn>("glCompileShader");createProgram=Proc<CreateProgramFn>("glCreateProgram");attachShader=Proc<AttachFn>("glAttachShader");linkProgram=Proc<ShaderFn>("glLinkProgram");useProgram=Proc<ShaderFn>("glUseProgram");deleteShader=Proc<ShaderFn>("glDeleteShader");deleteProgram=Proc<ShaderFn>("glDeleteProgram");shaderParam=Proc<GetParamFn>("glGetShaderiv");programParam=Proc<GetParamFn>("glGetProgramiv");shaderInfo=Proc<InfoFn>("glGetShaderInfoLog");programInfo=Proc<InfoFn>("glGetProgramInfoLog");location=Proc<LocationFn>("glGetUniformLocation");uniform1=Proc<Uniform1Fn>("glUniform1f");uniformI=Proc<UniformIFn>("glUniform1i");uniform4=Proc<Uniform4Fn>("glUniform4f");activeTexture=Proc<ActiveFn>("glActiveTexture");
  string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data","models");uint vs=Shader(0x8B31,File.ReadAllText(Path.Combine(folder,"model.vert"))),fs=Shader(0x8B30,File.ReadAllText(Path.Combine(folder,"model.frag")));
  modelProgram=createProgram();attachShader(modelProgram,vs);attachShader(modelProgram,fs);linkProgram(modelProgram);deleteShader(vs);deleteShader(fs);int ok;programParam(modelProgram,0x8B82,out ok);if(ok==0){int size;var log=new StringBuilder(4096);programInfo(modelProgram,4096,out size,log);deleteProgram(modelProgram);modelProgram=0;throw new InvalidOperationException(log.ToString());}
 }
 int U(string key){int value;if(!modelUniforms.TryGetValue(key,out value)){value=location(modelProgram,key);modelUniforms[key]=value;}return value;}
 void F(string key,float value){uniform1(U(key),value);}void Four(string key,float x,float y,float z,float w){uniform4(U(key),x,y,z,w);}
 void BindModelTexture(string name,string path,int slot){activeTexture(0x84C0u+(uint)slot);uniformI(U(name),slot);bool exists=!string.IsNullOrEmpty(path)&&(modelImages.ContainsKey(path)||File.Exists(path));F(name+"Present",exists?1:0);if(exists){Bitmap image;if(!modelImages.TryGetValue(path,out image)){using(var source=new Bitmap(path)){image=new Bitmap(source.Width,source.Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(image))g.DrawImageUnscaled(source,0,0);}modelImages[path]=image;}uint texture=Texture(image);glBindTexture(0x0DE1,texture);if(filteredModelTextures.Add(texture)){generateModelMipmaps(0x0DE1);glTexParameteri(0x0DE1,0x2801,0x2703);if(ModelAnisotropy>1)glTexParameterf(0x0DE1,0x84FE,ModelAnisotropy);}glTexParameteri(0x0DE1,0x2802,0x2901);glTexParameteri(0x0DE1,0x2803,0x2901);}else glBindTexture(0x0DE1,0);}
 public float HeadY;
 void ModelSettings(float angle,float eye,float aspect,float depth,float centerY,float scale,bool title){PrepareModelProgram();useProgram(modelProgram);F("angle",angle);F("eye",eye);F("headY",HeadY);F("aspect",aspect);F("depth",depth);F("centerY",centerY);F("modelScale",scale);F("title",title?1:0);}
 public bool ImportedConsole(string platform,double angle,float eye,float aspect,int x,int width,int height,float baseDepth=-520,float centerY=556){
  string key=ConsoleModels.Key(platform),folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data","models",key.Replace(' ','-'));if(!importedModels.ContainsKey(key)&&!File.Exists(Path.Combine(folder,"model.mesh")))return false;
  ImportedModel model;if(!importedModels.TryGetValue(key,out model)){model=ImportedModel.Load(folder);importedModels[key]=model;foreach(var batch in model.Batches)UploadBatch(batch);}
  glViewport(x,0,width,height);glMatrixMode(0x1700);glPushMatrix();glLoadIdentity();BeginConsole();ModelSettings((float)angle,eye,aspect,baseDepth,centerY,250,false);
  for(int pass=0;pass<2;pass++)foreach(var batch in model.Batches){var m=model.Materials[batch.Material];bool transparent=m.alpha=="BLEND"&&m.@base[3]<.99f;if((pass==1)!=transparent)continue;glDepthMask(transparent?(byte)0:(byte)1);Four("baseFactor",m.@base[0],m.@base[1],m.@base[2],m.@base[3]);Four("material",m.metal,m.rough,m.normalScale,m.alpha=="MASK"?m.cutoff:0);Four("emission",m.emissive[0],m.emissive[1],m.emissive[2],0);
   string[] names={"baseMap","normalMap","roughMap","aoMap","emissionMap"};for(int i=0;i<5;i++)BindModelTexture(names[i],m.textures[i]==""?null:Path.Combine(folder,m.textures[i]),i);DrawBatch(batch);
  }
  glDepthMask(1);useProgram(0);activeTexture(0x84C0);EndConsole();glPopMatrix();return true;
 }
 public void SpatialTitle(float eye,float aspect,float depth,int x,int width,int height){
  PrepareTitle();float em=Math.Min(110,1000*aspect*(900-depth-14)/900/Math.Max(.1f,titleWidth));glViewport(x,0,width,height);glMatrixMode(0x1700);glPushMatrix();glLoadIdentity();glScalef(em,em,em);glTranslatef(-titleWidth/2,-.36f,0);BeginConsole();ModelSettings(0,eye,aspect,depth,470,1,true);glColor4f(1,1,1,1);foreach(char c in "dEPTH")glCallList(titleLists+c-32);useProgram(0);EndConsole();glPopMatrix();
 }
 void DisposeImportedModels(){foreach(var model in importedModels.Values)foreach(var batch in model.Batches){if(batch.VertexBuffer!=0)deleteBuffers(1,ref batch.VertexBuffer);if(batch.IndexBuffer!=0)deleteBuffers(1,ref batch.IndexBuffer);}importedModels.Clear();foreach(var image in modelImages.Values)image.Dispose();modelImages.Clear();filteredModelTextures.Clear();if(modelProgram!=0){deleteProgram(modelProgram);modelProgram=0;}}
}
