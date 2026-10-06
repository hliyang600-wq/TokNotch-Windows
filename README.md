# TokNotch Windows

Windows 原生 AI 用量悬浮岛，使用 WPF、DXGI 和 HLSL 实现液态玻璃背景。项目仍处于预览阶段。

## 功能

- 单实例运行：重复启动展开已有悬浮岛，设置入口会转交给已有实例。

- Codex 与 DSH 本地日志用量统计；Codex 可显示日志中记录的五小时、每周剩余额度。
- DeepSeek API 余额查询；DSH 的余额圆环可绑定 DeepSeek 账户。DeepSeek token 用量来自本机日志，余额接口不提供完整账户 token 用量。
- Kimi API 余额、MiMo 登录会话用量查询，具体可用字段以服务返回为准。
- 选择并排序三个显示来源，自定义内外圆环的指标和满圈金额。
- 主题、四边停靠、拖动位置、展开方式、弹性动画和减少动态效果设置。

这是独立项目，与 OpenAI、DeepSeek、Moonshot、小米及原 TokNotch 项目没有官方关联。额度与用量可能因日志缺失、延迟或平台接口变化而不完整；付款和账户额度以平台页面为准。

## 使用

下载 Windows x64 便携包，解压到可写目录后运行 `TokNotchWindows.exe`。便携包包含 .NET 运行时。MiMo 登录需要 [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)；通常 Windows 已安装。

若包名带 `requires-dotnet10`，这是精简包，不包含 .NET 运行时；请先安装 Windows x64 的 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)。此版本仍需保留 ZIP 中所有 DLL、资源和许可证，不能只复制 EXE。

右键悬浮岛或托盘图标进入设置，配置显示来源与账户连接。Codex 和 DSH 默认只读当前用户的本地日志；无需填入它们的登录令牌。MiMo 使用应用自己的登录窗口，不读取其他浏览器的 cookie 数据库。

设置、加密 API key、加密 MiMo 会话及 WebView2 配置保存在程序旁的 `data/` 目录。凭据由 Windows DPAPI 绑定当前用户。不要分享 `data/` 或将其提交到 Git，也不要把应用安装到不可写的 Program Files 目录。

## 从源码构建

需要 Windows x64 和 [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) 10.0.401 或较新的 10.0.4xx 补丁，依赖从 NuGet.org 自动还原。

```powershell
./Build.ps1
./Test.ps1
./Test.ps1 -Native  # 需要已解锁的交互桌面
./Publish.ps1      # 输出 artifacts/TokNotch-Windows-win-x64.zip
./Publish.ps1 -FrameworkDependent  # 精简包，需要用户安装 .NET 10 Desktop Runtime
```

开发运行：

```powershell
dotnet run --project src/TokNotch.UI -c Release
```

GitHub Actions 在 Windows 上构建、检查测试夹具并生成便携包。屏幕采样检查需要本地交互桌面，未纳入无交互 CI。测试所生成的报告与本地账户信息均不属于公开源码。

## 当前限制

- 仅验证 Windows x64；锁屏、切换桌面或显卡限制可能让 DXGI 采样暂时失效。
- 动态背景采样速度受系统和图形管线限制，尚未保证与高刷新率显示器一致；本机动态测试约 60 FPS。
- MiMo 等账户接口可能变化；应用不会主动调用模型来制造 token 用量。
- DSH 的日志统计与 DeepSeek 的余额是不同数据源，不代表整个 DeepSeek 账户的历史消费明细。

## 许可证与鸣谢

项目代码使用 MIT；依赖、字体和设计参考的说明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)，完整许可证在 `LICENSES/`。感谢 [liquid-glass-react](https://github.com/rdev/liquid-glass-react) 的视觉参考，以及 [TokNotch](https://github.com/ReffWu/toknotch) 的悬浮岛思路。

提交问题时请遮盖账户、余额、日志路径和凭据，不要上传 `data/`、浏览器配置或原始会话日志。

玻璃采样帧率可在「设置 → 外观」中分别设置展开静止与收起状态：15 / 30 / 60 FPS / 自定义（1–360）/ 跟随屏幕，默认均为 30 FPS。动画和拖动期间解除静止限速；锁屏、熄屏和休眠时暂停并释放采样资源。档位是采样上限，实际更新还受显示器、桌面变化和渲染开销影响。

液态玻璃效果在「设置 → 外观」中配置：Standard、Polar、Prominent、Shader 四种折射模式，折射强度、磨砂、饱和度、色散、鼠标弹性、圆角、染色浓度和浅色背景增强。「恢复演示效果」载入原 React 演示参数，点击「保存并应用」生效。材质具有双层边缘高光、三层悬停光、按压缩放和阴影；文字保持固定，减少动态效果设置仍然生效。

这是原库效果的原生 GPU 移植。模糊使用固定九点 GPU 核，阴影受透明窗口边界限制，浏览器与 WPF 的合成和抗锯齿也不同，因此不承诺逐像素一致。无需 React/WebView 渲染或新增运行依赖。真实材质检查可在已解锁的 Windows 桌面运行 `dotnet src/TokNotch.UI/bin/Release/net10.0-windows/TokNotchWindows.dll --full-glass-validate`。
