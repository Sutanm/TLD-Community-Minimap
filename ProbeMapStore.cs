// 社区HUD地图 · sutanm — 可热重载的室内/洞穴探针绑定
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CommunityMinimap;

internal static class ProbeMapStore
{
    private const int SupportedFormatVersion = 1;
    private static readonly Dictionary<string, MapDefinition> ByScene =
        new(StringComparer.OrdinalIgnoreCase);

    public static MapDefinition Find(string sceneName) =>
        sceneName != null && ByScene.TryGetValue(sceneName, out MapDefinition definition)
            ? definition
            : null;

    public static bool Load(string path, Action<string> log, Action<string> warn)
    {
        try
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("The probe map file does not exist.", path);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            ProbeMapFile file = JsonSerializer.Deserialize<ProbeMapFile>(
                File.ReadAllText(path), options);
            if (file == null)
                throw new InvalidDataException("The probe map file is empty.");
            if (file.FormatVersion != SupportedFormatVersion)
                throw new InvalidDataException(
                    $"Unsupported probe map format {file.FormatVersion}; expected {SupportedFormatVersion}.");

            var loaded = new Dictionary<string, MapDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (ProbeMapEntry entry in file.Maps ?? Array.Empty<ProbeMapEntry>())
            {
                if (string.IsNullOrWhiteSpace(entry.Scene) ||
                    string.IsNullOrWhiteSpace(entry.MapId) ||
                    string.IsNullOrWhiteSpace(entry.FileName))
                {
                    warn("Ignored a probe map entry missing scene, mapId, or fileName.");
                    continue;
                }

                // Probe images must stay inside CommunityMinimap/maps. Rejecting path components
                // also makes a typo fail visibly instead of reading an unrelated file.
                if (!string.Equals(Path.GetFileName(entry.FileName), entry.FileName,
                        StringComparison.Ordinal) ||
                    (!entry.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
                     !entry.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
                {
                    warn($"Ignored probe map '{entry.MapId}': invalid fileName '{entry.FileName}'.");
                    continue;
                }

                string displayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                    ? entry.MapId
                    : entry.DisplayName;
                loaded[entry.Scene] = new MapDefinition(entry.MapId, displayName,
                    entry.FileName, CalibrationProfile.None, true, entry.Scene);
            }

            // Editors may briefly leave a partial file on disk. Commit only after the complete
            // replacement has parsed and validated, preserving the last working binding on error.
            ByScene.Clear();
            foreach (KeyValuePair<string, MapDefinition> pair in loaded)
                ByScene[pair.Key] = pair.Value;
            log($"Loaded {loaded.Count} hot-reloadable probe map bindings.");
            return true;
        }
        catch (Exception ex)
        {
            warn($"Failed loading probe-maps.json; keeping the last valid bindings. {ex.Message}");
            return false;
        }
    }

    private sealed class ProbeMapFile
    {
        public int FormatVersion { get; set; }
        public ProbeMapEntry[] Maps { get; set; } = Array.Empty<ProbeMapEntry>();
    }

    private sealed class ProbeMapEntry
    {
        public string Scene { get; set; } = "";
        public string MapId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string FileName { get; set; } = "";
    }
}
