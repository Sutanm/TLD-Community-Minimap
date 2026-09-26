using System;
using System.Collections.Generic;
using System.IO;
using Il2CppInterop.Runtime;
using Il2CppTLD.AddressableAssets;
using Il2CppTLD.Scenes;
using UnityEngine.ResourceManagement.ResourceLocations;
using Il2CppCollection = Il2CppSystem.Collections.Generic;

namespace CommunityMinimap;

internal static class SceneCatalogExporter
{
    // Scene discovery follows the public MIT-licensed DeveloperConsole implementation:
    // https://github.com/DigitalzombieTLD/TLD-Developer-Console
    public static bool TryExport(string path, out int sceneCount, out string error)
    {
        sceneCount = 0;
        error = "";
        try
        {
            Il2CppCollection.List<IResourceLocation> locations =
                AssetHelper.FindAllAssetsLocations<SceneSet>()
                    .Cast<Il2CppCollection.List<IResourceLocation>>();
            var sceneNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (IResourceLocation location in locations)
            {
                if (!string.IsNullOrWhiteSpace(location.PrimaryKey))
                    sceneNames.Add(location.PrimaryKey);
            }

            string temporaryPath = path + ".tmp";
            using (var writer = new StreamWriter(temporaryPath, false))
            {
                writer.WriteLine("scene,map_id,map_name,map_file,calibrated,status");
                foreach (string sceneName in sceneNames)
                {
                    MapDefinition definition = MapCatalog.Find(sceneName);
                    writer.WriteLine(string.Join(",",
                        EscapeCsv(sceneName),
                        EscapeCsv(definition?.Id ?? ""),
                        EscapeCsv(definition?.DisplayName ?? ""),
                        EscapeCsv(definition?.FileName ?? ""),
                        definition?.IsCalibrated == true ? "true" : "false",
                        definition == null ? "unmapped" : "mapped"));
                }
            }

            File.Move(temporaryPath, path, true);
            sceneCount = sceneNames.Count;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return false;
        }
    }

    private static string EscapeCsv(string value) =>
        '"' + value.Replace("\"", "\"\"") + '"';
}
