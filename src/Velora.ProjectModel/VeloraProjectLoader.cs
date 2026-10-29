using System.Text.Json;
using Velora.ProjectModel.Serialization;

namespace Velora.ProjectModel;

/// <summary>Loads bounded, versioned, reflection-free <c>.velproj</c> JSON.</summary>
public static class VeloraProjectLoader
{
    public const int MaximumFileSize = 1024 * 1024;
    public const int MaximumNameLength = 128;

    public static ProjectLoadResult Parse(ReadOnlySpan<byte> utf8Json, string? source = null)
    {
        if (utf8Json.Length > MaximumFileSize)
        {
            return Failure(
                ProjectDiagnosticIds.FileTooLarge,
                $"Project metadata exceeds the {MaximumFileSize}-byte limit.",
                source);
        }

        if (utf8Json.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            utf8Json = utf8Json[3..];
        }

        try
        {
            var duplicateProperty = FindDuplicateTopLevelProperty(utf8Json);
            if (duplicateProperty is not null)
            {
                return Failure(
                    ProjectDiagnosticIds.DuplicateProperty,
                    $"Project property '{duplicateProperty}' is specified more than once.",
                    source);
            }

            var document = JsonSerializer.Deserialize(
                utf8Json,
                VeloraProjectJsonContext.Default.VeloraProjectDocument);
            if (document is null)
            {
                return Failure(
                    ProjectDiagnosticIds.MalformedJson,
                    "Project metadata must be a JSON object.",
                    source);
            }

            return Validate(document, source);
        }
        catch (JsonException exception)
        {
            return Failure(
                ProjectDiagnosticIds.MalformedJson,
                $"Project metadata is malformed: {exception.Message}",
                source);
        }
    }

    public static ProjectLoadResult LoadFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);

            if (stream.Length > MaximumFileSize)
            {
                return Failure(
                    ProjectDiagnosticIds.FileTooLarge,
                    $"Project metadata exceeds the {MaximumFileSize}-byte limit.",
                    filePath);
            }

            var buffer = new byte[MaximumFileSize + 1];
            var bytesRead = 0;
            while (bytesRead < buffer.Length)
            {
                var read = stream.Read(buffer, bytesRead, buffer.Length - bytesRead);
                if (read == 0)
                {
                    break;
                }

                bytesRead += read;
            }

            if (bytesRead > MaximumFileSize)
            {
                return Failure(
                    ProjectDiagnosticIds.FileTooLarge,
                    $"Project metadata exceeds the {MaximumFileSize}-byte limit.",
                    filePath);
            }

            return Parse(buffer.AsSpan(0, bytesRead), filePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure(
                ProjectDiagnosticIds.IoFailure,
                $"Project metadata could not be read: {exception.Message}",
                filePath);
        }
    }

    private static ProjectLoadResult Validate(VeloraProjectDocument document, string? source)
    {
        if (document.FormatVersion != VeloraProject.CurrentFormatVersion)
        {
            return Failure(
                ProjectDiagnosticIds.UnsupportedVersion,
                $"Unsupported project format version '{document.FormatVersion?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "missing"}'. " +
                $"Expected version {VeloraProject.CurrentFormatVersion}.",
                source);
        }

        if (string.IsNullOrWhiteSpace(document.Name) || document.Name.Length > MaximumNameLength ||
            document.Name.Any(char.IsControl))
        {
            return Failure(
                ProjectDiagnosticIds.InvalidName,
                $"Project name must contain 1 to {MaximumNameLength} printable characters.",
                source);
        }

        if (!ProjectRelativePath.TryCreate(document.EntryScript, out var entryScript, out var entryError) ||
            !entryScript.Value.EndsWith(".vel", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                ProjectDiagnosticIds.InvalidEntryScript,
                $"Entry script must be a relative .vel path ({entryError}).",
                source);
        }

        if (!ProjectRelativePath.TryCreate(
                document.AssetsDirectory,
                out var assetsDirectory,
                out var assetsError))
        {
            return Failure(
                ProjectDiagnosticIds.InvalidAssetsDirectory,
                $"Assets directory must be a relative project path ({assetsError}).",
                source);
        }

        var project = new VeloraProject(
            document.FormatVersion.Value,
            document.Name,
            entryScript,
            assetsDirectory);
        return new ProjectLoadResult(project, Array.Empty<ProjectDiagnostic>());
    }

    private static string? FindDuplicateTopLevelProperty(ReadOnlySpan<byte> utf8Json)
    {
        var reader = new Utf8JsonReader(utf8Json);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("The JSON root must be an object.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected a property name.");
            }

            var name = reader.GetString() ?? throw new JsonException("A property name is null.");
            if (!names.Add(name))
            {
                return name;
            }

            if (!reader.Read())
            {
                throw new JsonException("A property value is missing.");
            }

            reader.Skip();
        }

        throw new JsonException("The JSON object is not closed.");
    }

    private static ProjectLoadResult Failure(string id, string message, string? source) =>
        new(
            null,
            new[]
            {
                new ProjectDiagnostic(id, ProjectDiagnosticSeverity.Error, message, source),
            });
}
