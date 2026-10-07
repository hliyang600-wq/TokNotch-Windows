# Third-party notices

TokNotch Windows project code is provided under the root MIT LICENSE. Dependencies retain their own copyrights and licenses; the project license does not replace them.

| Component | Version | License | Included notice |
| --- | --- | --- | --- |
| [ZstdSharp.Port](https://github.com/oleg-st/ZstdSharp) | 0.8.6 | MIT | LICENSES/ZstdSharp-LICENSE.txt |
| [Inter](https://github.com/rsms/inter) | 4.1, bundled OTF fonts | SIL Open Font License 1.1 | LICENSES/Inter-LICENSE.txt |
| [Microsoft.Web.WebView2](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4258.31) | 1.0.4258.31 | Microsoft SDK license supplied with this package | LICENSES/WebView2-LICENSE.txt |
| [.NET / WPF](https://github.com/dotnet) | .NET 10 | MIT and runtime third-party notices | Self-contained packages include runtime LICENSE and THIRD-PARTY-NOTICES under Licenses/ |

The WindowsDesktop runtime NuGet package does not always include its license text. The complete WPF license and notices from the official [.NET 10 release branch](https://github.com/dotnet/wpf/tree/release/10.0) are retained in `LICENSES/dotnet-WPF-LICENSE.txt` and `LICENSES/dotnet-WPF-THIRD-PARTY-NOTICES.txt`; the packager uses them only when the corresponding package notice is absent. Missing .NET Core runtime notices still fail packaging.

## Design references

The native glass material ports the displacement maps, RGB aberration, four refraction modes, directional elasticity and highlight algorithms from [liquid-glass-react](https://github.com/rdev/liquid-glass-react), reference revision `ac48eab18d1f7f444ae30002d240cae29c863a21`, copyright 2025 MAX ROVENSKY, MIT. Its complete license is retained in LICENSES/liquid-glass-react-MIT.txt. The original displacement images are bundled as WPF resources. Shader-mode mathematics also derive from [shuding/liquid-glass](https://github.com/shuding/liquid-glass), copyright 2025 Shu Ding, MIT; its complete license is included in LICENSES/shuding-liquid-glass-LICENSE.txt. This Windows application uses C#, DXGI capture and HLSL; it does not execute the React component or bundle its demo dependencies.

The floating island concept was also informed by [TokNotch](https://github.com/ReffWu/toknotch). TokNotch Windows is an independent implementation and is not an official release of that project or of the AI services it displays.

Redistributions must preserve the applicable notices, including the font license and notices for any bundled runtime. The WebView2 Runtime is separately installed and is subject to Microsoft's runtime terms.
