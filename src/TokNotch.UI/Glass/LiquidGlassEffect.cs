using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using TokNotch.Core.Models;
namespace TokNotch.UI.Glass;

// Native port of rdev/liquid-glass-react maps, RGB displacement and shader-utils.
// See Licenses/liquid-glass-react-MIT.txt and shuding-liquid-glass-LICENSE.txt.
public sealed class LiquidGlassEffect : ShaderEffect
{
 public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(LiquidGlassEffect), 0);
 public static readonly DependencyProperty DimensionsProperty = DependencyProperty.Register("Dimensions", typeof(Point), typeof(LiquidGlassEffect), new UIPropertyMetadata(new Point(380,220), PixelShaderConstantCallback(0)));
 public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register("Radius", typeof(double), typeof(LiquidGlassEffect), new UIPropertyMetadata(28d, PixelShaderConstantCallback(1)));
 public static readonly DependencyProperty TintProperty = DependencyProperty.Register("Tint",typeof(Color),typeof(LiquidGlassEffect),new UIPropertyMetadata(Color.FromRgb(19,18,24),PixelShaderConstantCallback(2)));
 private static readonly DependencyProperty MapProperty=RegisterPixelShaderSamplerProperty("Map",typeof(LiquidGlassEffect),1,SamplingMode.Bilinear);
 private static DependencyProperty Constant(string name,double value,int register)=>DependencyProperty.Register(name,typeof(double),typeof(LiquidGlassEffect),new UIPropertyMetadata(value,PixelShaderConstantCallback(register)));
 private static readonly DependencyProperty DisplacementProperty=Constant("Displacement",70,3),BlurProperty=Constant("Blur",6,4),SaturationProperty=Constant("Saturation",1.4,5),AberrationProperty=Constant("Aberration",2,6),ModeProperty=Constant("Mode",0,7),OverLightProperty=Constant("OverLight",0,8),HoverProperty=Constant("Hover",0,10),PressProperty=Constant("Press",0,11),NormalizationProperty=Constant("Normalization",1,12),TintOpacityProperty=Constant("TintOpacity",.08,13);
 private static readonly DependencyProperty PointerProperty=DependencyProperty.Register("Pointer",typeof(Point),typeof(LiquidGlassEffect),new UIPropertyMetadata(new Point(),PixelShaderConstantCallback(9)));
 private static readonly Lazy<PixelShader> SharedShader=new(()=>{var shader=new PixelShader();using var stream=new MemoryStream(Compile());shader.SetStreamSource(stream);shader.Freeze();return shader;});
 private static readonly Lazy<Brush[]> Maps=new(()=>new[]{LoadMap("standard.jpg"),LoadMap("polar.jpg"),LoadMap("prominent.png")});
 private static Brush LoadMap(string name){var bitmap=new BitmapImage(new Uri("pack://application:,,,/Assets/GlassMaps/"+name));bitmap.Freeze();var brush=new ImageBrush(bitmap){Stretch=Stretch.Fill};brush.Freeze();return brush;}
 private static readonly Lazy<Point> ShaderFactors=new(()=>{
  double maxX=0,maxY=0;
  for(int y=0;y<=512;y++)for(int x=0;x<=512;x++){
   double ix=x/512d-.5,iy=y/512d-.5,qx=Math.Abs(ix)+.3,qy=Math.Abs(iy)+.4;
   double d=Math.Sqrt(qx*qx+qy*qy)-.6;
   double t=Math.Clamp(1-(d-.15)/.8,0,1);double displacement=t*t*(3-2*t);double scale=displacement*displacement*(3-2*displacement);
   maxX=Math.Max(maxX,Math.Abs(ix*(scale-1)));maxY=Math.Max(maxY,Math.Abs(iy*(scale-1)));
  }
  return new(maxX,maxY);
 });
 public Color Tint {get=>(Color)GetValue(TintProperty);set=>SetValue(TintProperty,value);}
 public Brush Input { get => (Brush)GetValue(InputProperty); set => SetValue(InputProperty,value); }
 public Point Dimensions { get => (Point)GetValue(DimensionsProperty); set {SetValue(DimensionsProperty,value);var factors=ShaderFactors.Value;SetValue(NormalizationProperty,Math.Max(1,Math.Max(factors.X*value.X,factors.Y*value.Y)));} }
 public double Radius { get => (double)GetValue(RadiusProperty); set => SetValue(RadiusProperty,value); }
 public LiquidGlassEffect()
 {
  if (!RenderCapability.IsPixelShaderVersionSupported(3,0)) throw new NotSupportedException("Pixel shader 3.0 unavailable.");
  PixelShader=SharedShader.Value;
  UpdateShaderValue(InputProperty); UpdateShaderValue(DimensionsProperty); UpdateShaderValue(RadiusProperty);UpdateShaderValue(TintProperty);
  foreach(var property in new[]{MapProperty,DisplacementProperty,BlurProperty,SaturationProperty,AberrationProperty,ModeProperty,OverLightProperty,PointerProperty,HoverProperty,PressProperty,NormalizationProperty,TintOpacityProperty})UpdateShaderValue(property);
  Configure(new());
 }
 internal void Configure(GlassMaterial material)
 {
  material.Validate();SetValue(MapProperty,Maps.Value[Math.Min(2,(int)material.Mode)]);
  SetValue(DisplacementProperty,material.Displacement);SetValue(BlurProperty,(material.OverLight?12:4)+material.Blur*32);SetValue(SaturationProperty,material.Saturation/100);SetValue(AberrationProperty,material.Aberration);SetValue(ModeProperty,(double)material.Mode);SetValue(OverLightProperty,material.OverLight?1d:0d);SetValue(TintOpacityProperty,material.TintOpacity);
 }
 internal void Interaction(Point offset,double hover,double press){SetValue(PointerProperty,offset);SetValue(HoverProperty,hover);SetValue(PressProperty,press);}
 private const string Source = """
 sampler2D background : register(s0);
 sampler2D displacementMap : register(s1);
 float2 dimensions : register(c0);
 float radius : register(c1);
 float4 tint : register(c2);
 float displacementScale : register(c3);
 float blurRadius : register(c4);
 float saturationAmount : register(c5);
 float aberration : register(c6);
 float mode : register(c7);
 float overLight : register(c8);
 float2 pointer : register(c9);
 float hovered : register(c10);
 float pressed : register(c11);
 float normalization : register(c12);
 float tintOpacity : register(c13);
 float3 blurred(float2 uv) {
   float softness=max(.1,.5-aberration*.1);
   float2 stepUV=sqrt(blurRadius*blurRadius+softness*softness)*.5/dimensions;
   float3 c=tex2D(background,saturate(uv)).rgb*.25;
   c+=(tex2D(background,saturate(uv+float2(stepUV.x,0))).rgb+tex2D(background,saturate(uv-float2(stepUV.x,0))).rgb+tex2D(background,saturate(uv+float2(0,stepUV.y))).rgb+tex2D(background,saturate(uv-float2(0,stepUV.y))).rgb)*.125;
   c+=(tex2D(background,saturate(uv+stepUV)).rgb+tex2D(background,saturate(uv-stepUV)).rgb+tex2D(background,saturate(uv+float2(stepUV.x,-stepUV.y))).rgb+tex2D(background,saturate(uv+float2(-stepUV.x,stepUV.y))).rgb)*.0625;
   return lerp(dot(c,float3(.2126,.7152,.0722)).xxx,c,saturationAmount);
 }
 float4 main(float2 uv : TEXCOORD) : COLOR {
   float2 p = (uv-.5)*dimensions;
   float2 q = abs(p)-(dimensions*.5-radius);
   float d = length(max(q,0))+min(max(q.x,q.y),0)-radius;
   float2 mapUV=(uv-.5)*dimensions/max(dimensions.x,dimensions.y)+.5;
   float4 map=tex2D(displacementMap,mapUV);
   if(mode>2.5){
     float2 ij=uv-.5;float2 sq=abs(ij)+float2(.3,.4);
     float dist=length(sq)-.6;
     float disp=1-smoothstep(0,.8,dist-.15);float scaled=smoothstep(0,1,disp);
     float edgeFade=saturate(min(min(uv.x*dimensions.x,uv.y*dimensions.y),min((1-uv.x)*dimensions.x,(1-uv.y)*dimensions.y))/2);
     float2 generated=saturate(ij*(scaled-1)*dimensions*edgeFade/normalization+.5);
     map=float4(generated.x,generated.y,generated.y,1);
   }
   float2 offset=(map.rb-.5)*displacementScale*(overLight>.5?.5:1)/dimensions;
   float sign=mode>2.5?1:-1;
   float3 refracted;
   refracted.r=blurred(uv+offset*sign).r;
   refracted.g=blurred(uv+offset*(sign-aberration*.05)).g;
   refracted.b=blurred(uv+offset*(sign-aberration*.1)).b;
   float mask=map.a>=.666667?1:map.a>=.333333?aberration*.05:0;
   float3 color=lerp(blurred(uv),refracted,mask);
   color=lerp(color,max(0,2*color*.8-1),overLight);
   color=lerp(color,tint.rgb,tintOpacity);
   float angle=(135+pointer.x*1.2)*.0174532925;
   float2 direction=float2(sin(angle),-cos(angle));
   float gradient=saturate(dot(p,direction)/dot(abs(direction),dimensions)+.5);
   float stopA=clamp((33+pointer.y*.3)/100,.1,.9),stopB=clamp((66+pointer.y*.4)/100,stopA+.01,.95);
   float rimMask=1-smoothstep(0,1.5,-d);
   float a1=saturate(.12+abs(pointer.x)*.008),b1=saturate(.4+abs(pointer.x)*.012);
   float a2=saturate(.32+abs(pointer.x)*.008),b2=saturate(.6+abs(pointer.x)*.012);
   float screenGradient=gradient<stopA?a1*gradient/stopA:gradient<stopB?lerp(a1,b1,(gradient-stopA)/(stopB-stopA)):b1*(1-gradient)/(1-stopB);
   float overlayGradient=gradient<stopA?a2*gradient/stopA:gradient<stopB?lerp(a2,b2,(gradient-stopA)/(stopB-stopA)):b2*(1-gradient)/(1-stopB);
   float screenGlow=screenGradient*.2*rimMask;
   color=1-(1-color)*(1-screenGlow);
   color=lerp(color,saturate(color*2),overlayGradient*rimMask);
   float radial=length(float2(p.x,uv.y*dimensions.y))/length(float2(dimensions.x*.5,dimensions.y));
   color=lerp(color,saturate(color*2),saturate(1-radial/.5)*.25*max(hovered,pressed));
   color=lerp(color,saturate(color*2),saturate(1-radial/.8)*.5*pressed);
   color=lerp(color,saturate(color*2),saturate(1-radial)*(hovered>.01?.4*hovered:.8*pressed));
   float inset=(1-smoothstep(0,.5,-d))*.5+(1-smoothstep(.5,3,-d))*.08;
   color=1-(1-color)*(1-inset);
   return float4(saturate(color),1);
 }
 """;
 internal static byte[] Compile()
 {
  var source=Encoding.UTF8.GetBytes(Source); IntPtr code=IntPtr.Zero, errors=IntPtr.Zero;
  try {
   int hr=D3DCompile(source,(nuint)source.Length,"TokNotch-native-glass",IntPtr.Zero,IntPtr.Zero,"main","ps_3_0",1<<15,0,out code,out errors);
   if(hr<0) { string message=errors==IntPtr.Zero ? $"Shader compiler: 0x{hr:X8}" : Marshal.PtrToStringAnsi(Call<BlobPointer>(errors,3)(errors))!; throw new InvalidOperationException(message); }
   int size=checked((int)Call<BlobSize>(code,4)(code)); var bytes=new byte[size]; Marshal.Copy(Call<BlobPointer>(code,3)(code),bytes,0,size); return bytes;
  } finally { if(code!=IntPtr.Zero) Marshal.Release(code); if(errors!=IntPtr.Zero) Marshal.Release(errors); }
 }
 private static T Call<T>(IntPtr obj,int slot) where T:Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj),slot*IntPtr.Size));
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr BlobPointer(IntPtr self);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate nuint BlobSize(IntPtr self);
 [DllImport("d3dcompiler_47.dll",CallingConvention=CallingConvention.StdCall)] private static extern int D3DCompile(byte[] data,nuint size,[MarshalAs(UnmanagedType.LPStr)]string name,IntPtr defines,IntPtr include,[MarshalAs(UnmanagedType.LPStr)]string entry,[MarshalAs(UnmanagedType.LPStr)]string target,uint flags,uint effects,out IntPtr code,out IntPtr errors);
}
