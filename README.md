# ShiroBot Milky Adapter

源码使用 SDK `1.0.0`，需要 ShiroBot `1.0.0` 或更高版本。SDK 版本集中在 `Directory.Build.props` 的 `ShiroBotSdkVersion`。

ShiroBot 的 Milky 协议适配器，支持 HTTP API 调用以及 WebSocket、SSE、Webhook 事件接收。

- Milky 协议版本：**1.3.0**（兼容 1.2.x - 1.3.x）
- 需要 ShiroBot 1.0.0 及以上

## 使用

默认使用 Webhook：适配器在 `127.0.0.1`（Windows 上为 `localhost`）的随机端口监听，不主动连接 Milky。首次启动会生成监听地址和密钥，日志会打印监听地址。

在 Milky 实现端中：

1. 把事件推送地址设为适配器配置里的 `webhook_url`；
2. 把 `webhook_token` 配置为推送时携带的 Bearer 令牌；
3. 确认适配器的 `base_url` 指向实现端的 HTTP API 地址。

所有配置都可以在 Dashboard 的适配器配置页修改，保存后自动重启连接；新配置启动失败（如端口被占用）时会回滚到原配置。想让适配器主动连接 Milky 时，把事件接收方式改为 `ws` 或 `sse`。

## Milky 与 ShiroBot 不在同一台机器

默认的 `127.0.0.1` 只接受本机的推送。Milky 实现端在另一台机器、或者两者之一运行在 Docker 里时：

1. 把 `webhook_url` 改为 `http://+:端口/`，监听所有网卡。不要写 `0.0.0.0`，.NET 的 `HttpListener` 不支持这种写法。
2. 在 Milky 实现端填写 ShiroBot 所在机器可访问的地址，例如 `http://192.168.1.10:端口/`；ShiroBot 在 Docker 中时，需要在 `compose.yaml` 中映射该端口。
3. 同时把 `base_url` 改为 ShiroBot 能访问到的 Milky API 地址。
4. 保留 `webhook_token`，并通过防火墙限制来源，不要把端口直接暴露到公网。

Windows 上，普通用户只能监听 `localhost`（因此在 Windows 上会默认生成 `localhost`）。监听 `http://+:端口/` 需要以管理员身份运行一次：

```powershell
netsh http add urlacl url=http://+:端口/ user=Everyone
```

## QQ Model ABI 1.0

插件侧 ID 统一为字符串，Milky 的数值转换只发生在协议边界。群信息和成员查询统一在 `IQGroupApi`，
入群申请使用 `QGroupJoinRequest`，审批直接传入该对象。引用旧 QQ Model 的插件需重新编译。

## Rider 一键运行与调试

打开 `ShiroBot.MilkyAdapter.slnx`，选择 **Debug** 构建配置，在运行配置中选择 **ShiroBot**。
首次 Debug 构建自动下载与 SDK 匹配的开发宿主，并把适配器、便携 PDB 和开发测试插件复制到
`.shirobot-dev/host/<RID>/`。点击 Run 或 Debug 会直接启动这个宿主；Debug 可命中适配器源码断点，
并支持 C# Hot Reload。保留 Rider 的运行前构建步骤。

先构建一次即可生成本机的 `Properties/launchSettings.json`；Windows、macOS 和 Linux 使用相应宿主可执行文件。
开发宿主默认 Dashboard 地址为 `http://127.0.0.1:7002/dashboard/`，登录令牌在开发目录的 `config.toml` 中。
首次运行自动加载开发目录中的 Milky 实例；在 Dashboard 填写 Milky API 地址和事件接收配置。
开发测试插件提供 `ping`、`adapter-info` 和 `adapter-echo ` 命令，供收到真实消息后验证收发。

命令行也可以使用 `sh scripts/dev.sh`，Windows 使用
`powershell -ExecutionPolicy Bypass -File scripts/dev.ps1`。
加 `--prepare` 只准备文件、不启动宿主；已有构建输出时可追加 `--no-build`。
开发目录由 Git 忽略，SDK 升级保留已有配置、实例和插件数据。

Release 仍采用 SDK 单 DLL 打包；无需宿主源码：

```sh
dotnet publish ShiroBot.Adapter.Milky.csproj -c Release -p:CopyAdapterToHost=false
```

## 通用文件服务

通过 `GetAdapterExtension<IFileService>()` 探测上传能力。Milky 同时保留 `IQFileApi`；普通文件发送使用单独的 `FileSegment`，支持群聊及私聊。上传即发布文件，但不返回消息 ID；发送结果用 `UploadedFile.FileId` 表示文件 ID，不能当作消息 ID。暂不支持文本/引用与文件混合发送。

当前源码使用 SDK 1.0.0 / SDK ABI 1.2.0.0，要求宿主 1.0.0 或更高版本；QQ Model ABI 仍为 1.0.0.0。
