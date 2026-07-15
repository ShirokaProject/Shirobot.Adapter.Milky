# ShiroBot Milky Adapter

ShiroBot 的 Milky 协议适配器，支持 HTTP API 调用以及 WebSocket、SSE、Webhook 事件接收。

## 版本含义

- Adapter 版本 `1.3.0-rc2`：本仓库适配器实现自身的发布版本，写入 `BotAdapterAttribute`。
- ShiroBot SDK `0.7.1`：适配器与 ShiroBot 宿主之间的接口契约版本，不代表 Milky 协议版本。
- 服务端 `MilkyVersion`：由 `get_impl_info` 返回的 Milky 协议版本，启动时会单独输出。

## Milky 协议兼容策略

当前明确支持 Milky `1.2.x - 1.3.x`，模型能力对齐 `1.3.0-rc.1`：

- 服务端版本低于 `1.2.0` 时拒绝启动，避免以不完整契约运行。
- 服务端版本为 `1.2.x` 或 `1.3.x` 时正常启动；`group_disband`、`markdown` 等能力取决于服务端版本。
- 服务端版本达到 `1.4.0` 或更高时输出警告，但按 Milky 的应用端向前兼容规则继续启动。
- 预发布版本会明确告警，并按低于同核心稳定版本的保守策略评估；例如 `1.2.0-rc.1` 低于最低支持版本。
- 含负数版本分量或其他非法格式的版本字符串不会传入 `System.Version`，而是按无法解析处理。
- 无法解析版本字符串时输出警告并继续启动，兼容性视为未经确认。
- 未知事件会记录并跳过；未知或缺少 `type` 的消息段会记录并转换为可见的诊断 `TextIncomingSegment`，不会泄漏适配器内部占位类型。
- 已知事件和消息段必须包含对象型 `data`；缺失或类型错误时拒绝当前事件，不构造字段为 `0`/`null` 的伪对象。
- 已知事件中的未知枚举值会输出包含原始值和目标枚举类型的诊断，并仅跳过当前事件。
- 群通知支持当前模型中的 `join_request`、`admin_change`、`kick`、`quit`、`invited_join_request`；未知通知类型会记录并从返回列表安全跳过。
- 响应对象允许增加字段；未知字段由 `System.Text.Json` 忽略。
- `event_type` 与 `message_scene` 映射由 Model 生成器从 Milky IR 自动生成；新增事件无需手写适配器字典或 typed event。

Milky API 的 HTTP `200` 不代表业务成功。适配器始终检查响应 envelope 的 `status` 和 `retcode`，业务失败会抛出 `HttpRequestException`；成功响应只向调用方返回 `data`。无返回值操作同样执行业务错误检查。

Webhook 与 WebSocket/SSE 使用相同的安全事件反序列化。坏 JSON、缺失必填 `data` 或未知枚举会对当前请求返回带诊断信息的 HTTP `400`，监听器继续接受后续请求；停止适配器时 cancellation 会中断等待中的 `GetContextAsync`。

## JSON 请求

请求使用 `snake_case` 字段名，值为 `null` 的属性不会写入 JSON，以符合 Milky 对可选请求字段的约定。

## SDK 依赖

项目始终通过 NuGet 使用正式 SDK：

```xml
<PackageReference Include="ShiroBot.SDK" Version="0.7.1" />
```

## 构建与测试

```bash
dotnet test tests/ShiroBot.MilkyAdapter.Tests/ShiroBot.MilkyAdapter.Tests.csproj \
  -p:CopyAdapterToHost=false

dotnet publish ShiroBot.MilkyAdapter.csproj -c Release \
  -p:CopyAdapterToHost=false
```

Release workflow 和 PR CI 都使用显式项目文件运行包引用模式测试和发布，并校验发布目录中的 DLL 仅有 `ShiroBot.MilkyAdapter.dll`。

测试覆盖 Markdown 消息段、生成的事件注册表、临时消息统一事件流、群解散事件、群通知多态、Webhook 错误隔离与取消、API 错误 envelope、有额外字段的新响应、请求 null 忽略、版本策略、multicast 事件等待，以及未知事件、消息段和枚举的容错行为。
