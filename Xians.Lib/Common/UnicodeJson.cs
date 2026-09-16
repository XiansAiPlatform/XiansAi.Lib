using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Xians.Lib.Common;

/// <summary>
/// JSON options that pass Unicode through as UTF-8 rather than <c>\\uXXXX</c> escapes,
/// so agent names like <c>Kjøpsassistent</c> are stored the same way on lib and server.
/// HTML-sensitive characters (<c>&lt; &gt; &amp;</c>) remain escaped.
/// </summary>
internal static class UnicodeJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };
}
