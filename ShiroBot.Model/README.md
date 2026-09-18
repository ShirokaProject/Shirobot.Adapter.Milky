# ShiroBot.Adapter.Milky.Model

Shared Milky protocol models used by ShiroBot plugins, adapters and the host.

## Install

Most projects receive this package transitively from `ShiroBot.SDK`. Reference it directly only when
building model-only tooling:

```xml
<PackageReference Include="ShiroBot.Adapter.Milky.Model" Version="1.3.0" />
```

Models are grouped into these namespaces:

- `ShiroBot.Adapter.Milky.Model.Common`
- `ShiroBot.Adapter.Milky.Model.System.Requests` and `Responses`
- `ShiroBot.Adapter.Milky.Model.Message.Requests` and `Responses`
- `ShiroBot.Adapter.Milky.Model.Friend.Requests` and `Responses`
- `ShiroBot.Adapter.Milky.Model.Group.Requests` and `Responses`
- `ShiroBot.Adapter.Milky.Model.File.Requests` and `Responses`

## Compatibility

Version 1.3.0 is generated from the locked official Milky 1.3.0 IR. Additive fields
preserve previously emitted constructors and `Deconstruct` overloads so existing plugin binaries can
continue loading. Removed, renamed, reordered or type-changed fields are rejected by the generator and
require a deliberate major model release.

`EventMetadataRegistry` includes generated Milky `event_type` and `message_scene` discriminator maps.
Adapters should use it instead of maintaining handwritten event type dictionaries.

## Regeneration

`Generated/` may be overwritten; hand-written extensions belong in `Manual/`. From the repository
root, run:

```bash
dotnet run --project ./MilkyModelGenerator.Net/MilkyModelGenerator.Net.csproj -- --self-test
dotnet run --project ./MilkyModelGenerator.Net/MilkyModelGenerator.Net.csproj -- \
  --output ./ShiroBot.Model/Generated \
  --namespace ShiroBot.Adapter.Milky.Model \
  --expected-sha256 94783956629f2cff29fa0a7c38e9bce6f5329870cd525c715b2d9c4166425dbd
```
