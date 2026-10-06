namespace TokNotch.Core.Models;

public enum RefractionMode { Standard, Polar, Prominent, Shader }

public sealed record GlassMaterial(RefractionMode Mode=RefractionMode.Standard,double Displacement=70,double Blur=.0625,double Saturation=140,double Aberration=2,double Elasticity=.15,double CornerRadius=28,bool OverLight=false,double TintOpacity=.08)
{
 public void Validate()
 {
  if(!Enum.IsDefined(Mode))throw new ArgumentException("折射模式无效。");
  Range(Displacement,0,120,"折射强度");Range(Blur,0,1,"磨砂程度");Range(Saturation,0,300,"饱和度");Range(Aberration,0,5,"色散");Range(Elasticity,0,1,"弹性");Range(CornerRadius,0,110,"圆角");Range(TintOpacity,0,1,"染色");
 }
 private static void Range(double value,double min,double max,string name){if(!double.IsFinite(value)||value<min||value>max)throw new ArgumentException($"{name}需在 {min}–{max} 之间。");}
 // rdev/liquid-glass-react directional stretch and translation; only the material moves.
 public (double X,double Y,double ScaleX,double ScaleY) Interaction(double x,double y,double width,double height,bool pressed)
 {
  var edgeX=Math.Max(0,Math.Abs(x)-width/2);var edgeY=Math.Max(0,Math.Abs(y)-height/2);var fade=Math.Max(0,1-Math.Sqrt(edgeX*edgeX+edgeY*edgeY)/200);
  var distance=Math.Sqrt(x*x+y*y);var nx=distance==0?0:Math.Abs(x)/distance;var ny=distance==0?0:Math.Abs(y)/distance;
  var strength=Math.Min(distance/300,1)*Elasticity*fade;
  return(x*Elasticity*.1*fade,y*Elasticity*.1*fade,pressed?.96:Math.Max(.8,1+nx*strength*.3-ny*strength*.15),pressed?.96:Math.Max(.8,1+ny*strength*.3-nx*strength*.15));
 }
}
