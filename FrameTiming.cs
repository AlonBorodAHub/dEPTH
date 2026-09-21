using System;using System.Collections.Generic;using System.Diagnostics;using System.IO;using System.Linq;
public static class FrameTiming {
 static readonly object gate=new object();static readonly Dictionary<string,List<double>> samples=new Dictionary<string,List<double>>();
 static long previousFrame;static readonly System.Threading.Timer writer=new System.Threading.Timer(Flush,null,5000,5000);
 public static long Start(){return Stopwatch.GetTimestamp();}
 public static void End(string name,long start){Record(name,(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency);}
 public static void Frame(){long now=Start(),prior=System.Threading.Interlocked.Exchange(ref previousFrame,now);if(prior!=0)Record("frame interval",(now-prior)*1000.0/Stopwatch.Frequency);}
 static void Record(string name,double value){lock(gate){List<double> list;if(!samples.TryGetValue(name,out list)){list=new List<double>();samples[name]=list;}if(list.Count<10000)list.Add(value);}}
 static void Flush(object unused){var lines=new List<string>{DateTime.Now.ToString("O")};lock(gate){foreach(var pair in samples){var a=pair.Value.OrderBy(x=>x).ToArray();if(a.Length==0)continue;lines.Add(string.Format("{0}: count={1}, median={2:F3}ms, p95={3:F3}ms, max={4:F3}ms",pair.Key,a.Length,a[a.Length/2],a[Math.Min(a.Length-1,(int)(a.Length*.95))],a[a.Length-1]));pair.Value.Clear();}}try{File.AppendAllLines(Path.Combine(Storage.Folder,"frame-timing.txt"),lines);}catch{}}
}
