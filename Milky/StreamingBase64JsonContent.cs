using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShiroBot.Adapter.Milky.Milky;

/// <summary>Writes existing Base64 URIs without a second payload-sized JSON buffer.</summary>
internal sealed class StreamingBase64JsonContent : HttpContent
{
    private const int BufferSize = 16 * 1024;
    private readonly byte[] _json;
    private readonly List<MediaString> _media;
    private readonly long _length;

    public StreamingBase64JsonContent(object request, Type requestType, JsonSerializerOptions options)
    {
        var strings = new MediaStringConverter();
        var serialization = new JsonSerializerOptions(options);
        serialization.Converters.Insert(0, strings);
        _json = JsonSerializer.SerializeToUtf8Bytes(request, requestType, serialization);
        _media = strings.Media;
        _length = _json.LongLength;
        foreach (var media in _media)
            _length = checked(_length + media.Value.Length - media.Token.Length);
        Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
    }

    protected override bool TryComputeLength(out long length) { length = _length; return true; }
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => WriteAsync(stream, CancellationToken.None);
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) => WriteAsync(stream, cancellationToken);

    private async Task WriteAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Only the small JSON envelope is retained; the original strings are borrowed.
        byte[]? buffer = null;
        try
        {
            int position = 0;
            foreach (var media in _media)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int relative = _json.AsSpan(position).IndexOf(media.Token);
                if (relative < 0) throw new JsonException("Base64 placeholder was not serialized.");
                int start = position + relative;
                await stream.WriteAsync(_json.AsMemory(position, start - position), cancellationToken).ConfigureAwait(false);
                buffer ??= ArrayPool<byte>.Shared.Rent(BufferSize);
                for (int offset = 0; offset < media.Value.Length;)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int count = Math.Min(BufferSize, media.Value.Length - offset);
                    int written = Encoding.ASCII.GetBytes(media.Value.AsSpan(offset, count), buffer);
                    await stream.WriteAsync(buffer.AsMemory(0, written), cancellationToken).ConfigureAwait(false);
                    offset += count;
                }
                position = start + media.Token.Length;
            }
            await stream.WriteAsync(_json.AsMemory(position), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (buffer is not null) ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private sealed record MediaString(byte[] Token, string Value);

    private sealed class MediaStringConverter : JsonConverter<string>
    {
        public List<MediaString> Media { get; } = [];
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetString();
        public override string ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetString()!;
        public override void WriteAsPropertyName(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WritePropertyName(value);
        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            if (IsBase64Uri(value))
            {
                // A new unpredictable token per occurrence also handles duplicate URIs and nested forward messages.
                string token = "milky-media-" + Guid.NewGuid().ToString("N");
                Media.Add(new MediaString(Encoding.ASCII.GetBytes(token), value));
                writer.WriteStringValue(token);
            }
            else writer.WriteStringValue(value);
        }

        private static bool IsBase64Uri(string value)
        {
            if (!value.StartsWith("base64://", StringComparison.OrdinalIgnoreCase)) return false;
            // Only this ASCII alphabet can be copied between JSON quotes without escaping.
            // Unexpected characters go through the normal JSON writer.
            foreach (char ch in value.AsSpan("base64://".Length))
                if (!(ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '+' or '/' or '='))
                    return false;
            return true;
        }
    }
}
