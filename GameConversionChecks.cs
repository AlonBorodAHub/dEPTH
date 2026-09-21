using System;using System.Reflection;
class GameConversionChecks {
 static MethodInfo gate=typeof(DepthWindow).GetMethod("StableConversion",BindingFlags.NonPublic|BindingFlags.Static);
 static long since=-1;
 static void Check(bool evidence,long now,bool expected){object[] args={evidence,now,since};bool result=(bool)gate.Invoke(null,args);since=(long)args[2];if(result!=expected)throw new Exception("Incorrect conversion gate at "+now);}
 static void Main(){Check(false,0,false);Check(false,3500,false);Check(false,60000,false);Check(true,60100,false);Check(true,61099,false);Check(false,61100,false);Check(true,61200,false);Check(true,62199,false);Check(true,62200,true);Check(false,62250,false);Console.WriteLine("PASS: missing evidence never reveals SBS; conversion must remain stable for one second; interrupted evidence restarts settling.");}
}
