using System;
using System.Runtime.InteropServices;

// Media Foundation supplies decoded CPU frames; dEPTH owns their presentation.
// No embedded WPF/Direct3D video surface participates in the library compositor.
sealed class PreviewFrameReader:IDisposable {
 [DllImport("ole32.dll")]static extern int CoInitializeEx(IntPtr reserved,uint flags);
 [DllImport("ole32.dll")]static extern void CoUninitialize();
 [DllImport("mfplat.dll")]static extern int MFStartup(int version,int flags);
 [DllImport("mfplat.dll")]static extern int MFShutdown();
 [DllImport("mfplat.dll")]static extern int MFCreateAttributes(out IntPtr attributes,int count);
 [DllImport("mfplat.dll")]static extern int MFCreateMediaType(out IntPtr type);
 [DllImport("mfreadwrite.dll",CharSet=CharSet.Unicode)]static extern int MFCreateSourceReaderFromURL(string path,IntPtr attributes,out IntPtr reader);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int SetUInt(IntPtr self,ref Guid key,uint value);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int GetUInt(IntPtr self,ref Guid key,out uint value);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int GetLong(IntPtr self,ref Guid key,out ulong value);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int SetGuid(IntPtr self,ref Guid key,ref Guid value);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int SelectStream(IntPtr self,uint index,int selected);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int SetType(IntPtr self,uint index,IntPtr reserved,IntPtr type);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int GetTypeFn(IntPtr self,uint index,out IntPtr type);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int ReadFn(IntPtr self,uint index,uint control,out uint actual,out uint flags,out long time,out IntPtr sample);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int ConvertFn(IntPtr sample,out IntPtr buffer);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int LockFn(IntPtr buffer,out IntPtr data,out uint maximum,out uint length);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int Lock2DFn(IntPtr buffer,out IntPtr scanline,out int pitch);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int UnlockFn(IntPtr buffer);
 [StructLayout(LayoutKind.Explicit,Size=24)]struct Variant {[FieldOffset(0)]public ushort Type;[FieldOffset(8)]public long Value;}
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int SeekFn(IntPtr self,ref Guid format,ref Variant position);
 static T Fn<T>(IntPtr p,int slot) where T:class{return (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(p),slot*IntPtr.Size),typeof(T));}
 static void Check(int hr){if(hr<0)Marshal.ThrowExceptionForHR(hr);}
 static void Release(ref IntPtr p){if(p!=IntPtr.Zero){Marshal.Release(p);p=IntPtr.Zero;}}
 const uint Video=0xFFFFFFFC;
 IntPtr reader;bool com,mf;ReadFn read;SeekFn seek;ConvertFn convert;
 public int Width,Height,Stride,ActualStride;
 public PreviewFrameReader(string path){
  IntPtr attributes=IntPtr.Zero,type=IntPtr.Zero,current=IntPtr.Zero;
  try{
   Check(CoInitializeEx(IntPtr.Zero,0));com=true;Check(MFStartup(0x20070,0));mf=true;
   Check(MFCreateAttributes(out attributes,1));Guid processing=new Guid("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d");Check(Fn<SetUInt>(attributes,21)(attributes,ref processing,1));
   Check(MFCreateSourceReaderFromURL(path,attributes,out reader));var select=Fn<SelectStream>(reader,4);Check(select(reader,0xFFFFFFFE,0));Check(select(reader,Video,1));
   Check(MFCreateMediaType(out type));Guid majorKey=new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f"),video=new Guid("73646976-0000-0010-8000-00aa00389b71"),subKey=new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5"),rgb=new Guid("00000016-0000-0010-8000-00aa00389b71");var set=Fn<SetGuid>(type,24);Check(set(type,ref majorKey,ref video));Check(set(type,ref subKey,ref rgb));Check(Fn<SetType>(reader,7)(reader,Video,IntPtr.Zero,type));
   Check(Fn<GetTypeFn>(reader,6)(reader,Video,out current));Guid size=new Guid("1652c33d-d6b2-4012-b834-72030849a37d"),strideKey=new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");ulong packed;Check(Fn<GetLong>(current,8)(current,ref size,out packed));Width=(int)(packed>>32);Height=(int)(packed&0xffffffff);uint stride;Stride=Fn<GetUInt>(current,7)(current,ref strideKey,out stride)>=0?unchecked((int)stride):Width*4;
   if(Width<=0||Height<=0||Width>4096||Height>2160)throw new InvalidOperationException("Invalid preview dimensions");read=Fn<ReadFn>(reader,9);seek=Fn<SeekFn>(reader,8);
  }catch{Dispose();throw;}finally{Release(ref current);Release(ref type);Release(ref attributes);}
 }
 public bool Read(byte[] frame,out long milliseconds){
  while(true){IntPtr sample=IntPtr.Zero,buffer=IntPtr.Zero;uint actual,flags;long stamp;Check(read(reader,Video,0,out actual,out flags,out stamp,out sample));milliseconds=stamp/10000;
   // The decoder can renegotiate row alignment when its first sample arrives.
   if((flags&32)!=0){IntPtr current;Check(Fn<GetTypeFn>(reader,6)(reader,Video,out current));try{Guid key=new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");uint pitch;if(Fn<GetUInt>(current,7)(current,ref key,out pitch)>=0)Stride=unchecked((int)pitch);}finally{Release(ref current);}}
   if((flags&2)!=0){Release(ref sample);return false;}if(sample==IntPtr.Zero)continue;
   try{if(convert==null)convert=Fn<ConvertFn>(sample,41);Check(convert(sample,out buffer));
    IntPtr surface=IntPtr.Zero;Guid iid=new Guid("7dc9d5f9-9ed9-44ec-9bbf-0600bb589fbb");
    try{
     if(Marshal.QueryInterface(buffer,ref iid,out surface)>=0){
      IntPtr scanline;int pitch;Check(Fn<Lock2DFn>(surface,3)(surface,out scanline,out pitch));
      try{CopyRows(scanline,pitch,frame);}finally{Check(Fn<UnlockFn>(surface,4)(surface));}
     }else{
      // Ordinary buffers use the negotiated layout; 2-D surfaces above supply
      // their own signed pitch and a pointer to the displayed top scanline.
      IntPtr data;uint maximum,length;Check(Fn<LockFn>(buffer,3)(buffer,out data,out maximum,out length));
      try{int pitch=Math.Abs(Stride);if(length<(uint)(pitch*Height))throw new InvalidOperationException("Truncated decoded preview frame");CopyRows(Stride<0?IntPtr.Add(data,(Height-1)*pitch):data,Stride,frame);}finally{Check(Fn<UnlockFn>(buffer,4)(buffer));}
     }
    }finally{Release(ref surface);}
   }finally{Release(ref buffer);Release(ref sample);}return true;
  }
 }
 void CopyRows(IntPtr top,int pitch,byte[] frame){
  int row=checked(Width*4);if(Math.Abs((long)pitch)<row||frame.Length!=checked(row*Height))throw new InvalidOperationException("Invalid decoded preview layout");
  ActualStride=pitch;for(int y=0;y<Height;y++)Marshal.Copy(IntPtr.Add(top,checked(y*pitch)),frame,y*row,row);
  for(int i=3;i<frame.Length;i+=4)frame[i]=255;
 }
 public void Rewind(){Guid format=Guid.Empty;var position=new Variant{Type=20,Value=0};Check(seek(reader,ref format,ref position));}
 public void Dispose(){Release(ref reader);if(mf){MFShutdown();mf=false;}if(com){CoUninitialize();com=false;}}
}
