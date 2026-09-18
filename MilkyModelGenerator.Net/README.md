# MilkyModelGenerator.Net

IR to C# models generator for the Milky IR schema.

## ABI compatibility

Before deleting the existing output, the generator parses positional records in `Generated` with
Roslyn. When the new IR adds fields it keeps every previously emitted constructor overload and
`Deconstruct` overload, supplying `default!` or the IR default for newly required arguments.
This applies to common models and generated API request/response records.

Generation fails with an explicit major-breaking-change error when an existing positional field is
removed, renamed, reordered, or changes type, or when a new shape collides with an old signature but
would map arguments to different properties. Existing compatibility overloads are parsed and carried
forward on later generations.

This is an ABI guarantee, not a protocol-semantic guarantee. Existing callers remain loadable when
an API request gains a field, but an old constructor can only supply the IR default or `default!`.
If the server requires a meaningful new value, the adapter call site must be updated. APIs that
previously had no request record and gain their first request field also require manual adapter
wiring.

The default official Milky 1.3 IR URL is locked to SHA-256
`94783956629f2cff29fa0a7c38e9bce6f5329870cd525c715b2d9c4166425dbd`. The generated
`_GeneratedInfo.cs` records source label, URL, SHA-256, Milky version and package version. Review ABI
changes before updating the lock; do not silently regenerate from a changed mutable URL.

Output layout:

- `Common`: shared structs, unions, event metadata and generated `event_type`/`message_scene` registries
- `System/Requests`, `System/Responses`
- `Message/Requests`, `Message/Responses`
- `Friend/Requests`, `Friend/Responses`
- `Group/Requests`, `Group/Responses`
- `File/Requests`, `File/Responses`

Default output:

```powershell
.\..\ShiroBot.Model\Generated
```

Default namespace:

```powershell
ShiroBot.Adapter.Milky.Model
```

Run directly:

```powershell
dotnet run --project .\MilkyModelGenerator.Net.csproj
```

Run the local compatibility fixture without downloading or replacing the current models:

```powershell
dotnet run --project .\MilkyModelGenerator.Net.csproj -- --self-test
```

Interactive wrapper:

```powershell
.\generate-models.ps1
```

Default wrapper usage:

```powershell
.\generate-models.ps1 -Preset Default
```

Custom target example:

```powershell
dotnet run --project .\MilkyModelGenerator.Net.csproj -- `
  --output C:\path\to\Generated `
  --namespace My.Models `
  --ir-url https://milky.ntqqrev.org/raw/milky-ir/ir.json `
  --ir-source milky-ir/ir.json `
  --expected-sha256 94783956629f2cff29fa0a7c38e9bce6f5329870cd525c715b2d9c4166425dbd
```

Equivalent wrapper usage:

```powershell
.\generate-models.ps1 -Preset Custom `
  -Output C:\path\to\Generated `
  -Namespace My.Models `
  -IrUrl https://milky.ntqqrev.org/raw/milky-ir/ir.json `
  -IrSource milky-ir/ir.json `
  -IrSha256 94783956629f2cff29fa0a7c38e9bce6f5329870cd525c715b2d9c4166425dbd
```
