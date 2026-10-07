# ShiroBot Milky Adapter

ShiroBot 的 Milky 协议适配器，支持 HTTP API 调用以及 WebSocket、SSE、Webhook 事件接收。

- Milky 协议版本：**1.3.0**（兼容 1.2.x - 1.3.x）
- 需要 ShiroBot 0.9.4 及以上

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

构建需要宿主源码，默认路径为 `../../ShiroBot`；其他布局可指定：

```sh
dotnet build -c Release -p:ShiroBotSourceRoot=/path/to/ShiroBot -p:CopyAdapterToHost=false
```
