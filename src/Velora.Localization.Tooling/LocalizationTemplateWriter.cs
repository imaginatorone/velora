using System.Text.Json;

namespace Velora.Localization.Tooling;

public sealed record LocalizationTemplateFiles(
    byte[] TemplateJson,
    byte[] ReferenceJson,
    byte[] KeyMapJson);

/// <summary>Creates deterministic UTF-8 JSON artifacts without reflection serialization.</summary>
public static class LocalizationTemplateWriter
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
    };

    public static LocalizationTemplateFiles Write(LocalizationExtraction extraction)
    {
        ArgumentNullException.ThrowIfNull(extraction);

        ExtractedLocalizationEntry[] distinctEntries = extraction.Entries
            .GroupBy(static entry => entry.Key, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
            .ToArray();

        return new LocalizationTemplateFiles(
            WriteTemplate(distinctEntries),
            WriteReference(distinctEntries),
            WriteKeyMap(extraction.Entries));
    }

    private static byte[] WriteTemplate(IEnumerable<ExtractedLocalizationEntry> entries) =>
        WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WritePropertyName("__meta");
            writer.WriteStartObject();
            writer.WriteString("generated_by", "Velora Localization Tooling");
            writer.WriteString("language_name", "FILL_IN_LANGUAGE_NAME");
            writer.WriteString("language_code", "FILL_IN_CODE");
            writer.WriteEndObject();
            foreach (ExtractedLocalizationEntry entry in entries)
            {
                writer.WriteString(entry.Key, string.Empty);
            }

            writer.WriteEndObject();
        });

    private static byte[] WriteReference(IEnumerable<ExtractedLocalizationEntry> entries) =>
        WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WritePropertyName("__meta");
            writer.WriteStartObject();
            writer.WriteString("description", "Reference text for generated localization keys");
            writer.WriteEndObject();
            foreach (ExtractedLocalizationEntry entry in entries)
            {
                writer.WriteString(entry.Key, entry.OriginalText);
            }

            writer.WriteEndObject();
        });

    private static byte[] WriteKeyMap(IEnumerable<ExtractedLocalizationEntry> entries) =>
        WriteJson(writer =>
        {
            writer.WriteStartObject();
            foreach (IGrouping<string, ExtractedLocalizationEntry> group in entries
                         .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
                         .GroupBy(static entry => entry.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(group.Key);
                writer.WriteStartArray();
                foreach (ExtractedLocalizationEntry entry in group)
                {
                    writer.WriteStartObject();
                    writer.WriteString("text", entry.OriginalText);
                    writer.WriteString("context", entry.Context);
                    writer.WriteString("speaker", entry.Speaker);
                    writer.WriteString("file", entry.SourceFile);
                    writer.WriteNumber("line", entry.Line);
                    writer.WriteNumber("span_start", entry.Span.Start);
                    writer.WriteNumber("span_length", entry.Span.Length);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        });

    private static byte[] WriteJson(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            write(writer);
        }

        return stream.ToArray();
    }
}
