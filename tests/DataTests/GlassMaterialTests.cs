using TokNotch.Core.Models;
using TokNotch.Infrastructure.Settings;
internal static class GlassMaterialTests
{
 internal static void Run(string root)
 {
  int checks=0;void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
  Check(new WindowPreferences().Glass==new GlassMaterial(),"old preferences receive full glass defaults");
  foreach(var mode in Enum.GetValues<RefractionMode>()){
   var material=new GlassMaterial(mode,65,.12,145,1.5,.2,24,true,.1);
   var store=new DisplayPreferencesStore(Path.Combine(root,"full-glass-"+mode));store.Save(DisplayPreferences.Default.WithWindow(new(Material:material)));
   Check(store.Load().Window.Glass==material,"all glass controls survive restart: "+mode);
  }
  bool Invalid(GlassMaterial value){try{value.Validate();return false;}catch(ArgumentException){return true;}}
  Check(Invalid(new(Mode:(RefractionMode)99))&&Invalid(new(Displacement:double.NaN))&&Invalid(new(Blur:2))&&Invalid(new(Elasticity:-1)),"invalid glass parameters rejected");
  var m=new GlassMaterial(Elasticity:.2);var stretch=m.Interaction(150,0,380,220,false);
  Check(stretch.ScaleX>1&&stretch.ScaleY<1&&stretch.X>0,"directional stretch follows pointer");
  Check(m.Interaction(150,0,380,220,true).ScaleX==.96&&m.Interaction(1000,0,380,220,false).ScaleX==1,"press shrinks and distant pointer fades elasticity");
  Console.WriteLine($"{checks} full glass material checks passed");
 }
}
