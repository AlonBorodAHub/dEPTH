using System;
class NavigationChecks {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Main(){
  Check(DepthWindow.NavigationIndex(4,1,59,5)==4,"Right must not wrap rows");
  Check(DepthWindow.NavigationIndex(5,-1,59,5)==5,"Left must not wrap rows");
  Check(DepthWindow.NavigationIndex(7,5,59,5)==12,"Down must cross pages in the same column");
  Check(DepthWindow.NavigationIndex(54,5,58,5)==57,"Partial last row must remain selectable");
  Check(DepthWindow.NavigationIndex(0,-5,59,5)==0,"Top boundary");
  Check(DepthWindow.NavigationIndex(9,5,54,10)==19,"Keyboard has ten columns");
  Check(DepthWindow.NavigationIndex(0,1,0,5)==0,"Empty collection");
  Console.WriteLine("PASS: grid edges, page transitions, partial rows, keyboard navigation, empty collections.");
 }
}
