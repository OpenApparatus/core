using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenApparatus.IO;

/// <summary>
/// Studio project persistence — round-trips the editor's authored state to a
/// single JSON file. Distinct from <see cref="Exporters.JsonExporter"/>, which
/// produces a downstream-consumer schema; this format is editor-internal and is
/// preserved exactly across save / open so reopening reconstructs the canvas as
/// the user left it.
///
/// Versioned so future schema changes can migrate older saves. v1 covers grid
/// dimensions, defaults, room ownership, passages, all colour palettes (per-room
/// + per-wall), object types + instances, project title, placement constraints,
/// and the camera state for both 2D and 3D views.
///
/// This class is the UI-agnostic half of project I/O — it knows nothing about
/// any particular editor (Avalonia VM, React state, etc.). The editor is
/// responsible for converting its own state into a <see cref="ProjectFile"/>
/// before calling <see cref="Save"/>, and applying a loaded file back to its
/// state after calling <see cref="Load"/>.
/// </summary>
public static class ProjectIO
{
    public const string CurrentVersion = "1.0";
    public const string FileExtension = ".oapp";

    static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serializes <paramref name="file"/> to <paramref name="path"/>.</summary>
    public static void Save(string path, ProjectFile file)
    {
        var json = JsonSerializer.Serialize(file, s_options);
        File.WriteAllText(path, json);
    }

    /// <summary>Reads and parses a project file from <paramref name="path"/>.
    /// Throws <see cref="InvalidDataException"/> for empty files or unsupported
    /// versions.</summary>
    public static ProjectFile Load(string path)
    {
        var json = File.ReadAllText(path);
        return Parse(json);
    }

    /// <summary>Parses a project file from a raw JSON string. Useful when the
    /// JSON comes from somewhere other than a local file (e.g. a browser File
    /// API upload).</summary>
    public static ProjectFile Parse(string json)
    {
        var doc = JsonSerializer.Deserialize<ProjectFile>(json, s_options)
            ?? throw new InvalidDataException("Project file is empty.");
        if (doc.Version is null || !doc.Version.StartsWith("1."))
            throw new InvalidDataException(
                $"Unsupported project version '{doc.Version}'. This studio reads v1.x.");
        return doc;
    }

    /// <summary>Serializes a project file to a JSON string. Mirror of
    /// <see cref="Parse"/> for in-memory roundtrips.</summary>
    public static string Stringify(ProjectFile file)
        => JsonSerializer.Serialize(file, s_options);
}
