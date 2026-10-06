using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace TokNotch.UI.Glass;

// Original native implementation; the React experiment is a visual reference.
public sealed class LiquidGlassEffect : ShaderEffect
{
 public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(LiquidGlassEffect), 0);
 public static readonly DependencyProperty DimensionsProperty = DependencyProperty.Register("Dimensions", typeof(Point), typeof(LiquidGlassEffect), new UIPropertyMetadata(new Point(380,220), PixelShaderConstantCallback(0)));
 public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register("Radius", typeof(double), typeof(LiquidGlassEffect), new UIPropertyMetadata(28d, PixelShaderConstantCallback(1)));
 public static readonly DependencyProperty TintProperty = DependencyProperty.Register("Tint",typeof(Color),typeof(LiquidGlassEffect),new UIPropertyMetadata(Color.FromRgb(19,18,24),PixelShaderConstantCallback(2)));
 public Color Tint {get=>(Color)GetValue(TintProperty);set=>SetValue(TintProperty,value);}
 public Brush Input { get => (Brush)GetValue(InputProperty); set => SetValue(InputProperty,value); }
 public Point Dimensions { get => (Point)GetValue(DimensionsProperty); set => SetValue(DimensionsProperty,value); }
 public double Radius { get => (double)GetValue(RadiusProperty); set => SetValue(RadiusProperty,value); }
 public LiquidGlassEffect()
 {
  if (!RenderCapability.IsPixelShaderVersionSupported(3,0)) throw new NotSupportedException("Pixel shader 3.0 unavailable.");
  var shader = new PixelShader(); using var stream = new MemoryStream(Compile()); shader.SetStreamSource(stream); shader.Freeze(); PixelShader=shader;
  UpdateShaderValue(InputProperty); UpdateShaderValue(DimensionsProperty); UpdateShaderValue(RadiusProperty);UpdateShaderValue(TintProperty);
 }
 private const string Source = """
 sampler2D background : register(s0);
 float2 dimensions : register(c0);
 float radius : register(c1);
 float4 tint : register(c2);
 float4 main(float2 uv : TEXCOORD) : COLOR {
   float2 p = (uv-.5)*dimensions;
   float2 q = abs(p)-(dimensions*.5-radius);
   float d = length(max(q,0))+min(max(q.x,q.y),0)-radius;
   float edge = 1-smoothstep(0,20,-d);
   float2 n = normalize(p/(dimensions*.5)+float2(.0001,.0001));
   float2 warped = clamp(uv - n*edge*9/dimensions, .015,.985);
   float2 stepUV = 3/dimensions;
   float3 color = tex2D(background,warped).rgb*.28;
   color += (tex2D(background,warped+float2(stepUV.x,0)).rgb+tex2D(background,warped-float2(stepUV.x,0)).rgb)*.12;
   color += (tex2D(background,warped+float2(0,stepUV.y)).rgb+tex2D(background,warped-float2(0,stepUV.y)).rgb)*.12;
   color += (tex2D(background,warped+stepUV).rgb+tex2D(background,warped-stepUV).rgb)*.12;
   float2 dispersion = n*edge*1.6/dimensions;
   color.r = lerp(color.r, tex2D(background,warped+dispersion).r,.38);
   color.b = lerp(color.b, tex2D(background,warped-dispersion).b,.38);
   float luminance=dot(color,float3(.2126,.7152,.0722));
   color=lerp(luminance.xxx,color,1.15);
   color=lerp(color,tint.rgb,.63);
   float rim=pow(edge,4)*(.055+.085*saturate(-n.y-n.x*.35));
   return float4(saturate(color+rim),1);
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

