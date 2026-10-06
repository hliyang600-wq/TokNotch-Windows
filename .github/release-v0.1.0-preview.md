Windows 原生 AI 用量悬浮岛预览版，支持 Codex、DSH、DeepSeek、Kimi 和 MiMo 数据来源，提供液态玻璃背景、可排序的来源、自定义内外圆环、主题、位置与弹性动画设置。

## 下载与启动

下载 `TokNotch-Windows-v0.1.0-preview-win-x64-requires-dotnet10.zip`。

1. 安装 Windows x64 的 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)。
2. 将 ZIP 解压到可写目录，运行 `TokNotchWindows.exe`，保留所有配套文件。
3. 右键悬浮岛或托盘图标进入设置，选择显示来源并配置账户。MiMo 登录需要 [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)。

## 数据与隐私

Codex、DSH 用量来自当前用户本地日志。DeepSeek 接口查询余额，其 token 用量来源于本机日志，不代表整个账户历史。Kimi 查询 API 余额，MiMo 在应用自己的登录窗口中连接。

API key 与 MiMo 会话使用 Windows DPAPI 加密，配置位于程序旁的 `data/`。请不要分享这个目录，也不要上传原始会话日志。

## 已知限制

- 目前验证平台为 Windows x64。
- 背景采样尚不保证达到高刷新率显示器帧率；锁屏、切换桌面或显卡限制可能导致采样暂时失效。
- 账户接口、日志格式或服务字段变化可能影响数据显示；余额和额度以平台页面为准。
- 这是独立预览项目，与 AI 服务及上游项目没有官方关联。

## 许可证

项目代码使用 MIT。依赖与字体保留各自许可证；液态玻璃视觉参考来自 liquid-glass-react。完整说明见源码中的 `THIRD_PARTY_NOTICES.md` 和 `LICENSES/`，应用包也保留对应许可证。
