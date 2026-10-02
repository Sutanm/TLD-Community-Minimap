// 社区HUD地图 · sutanm — 抓取的底图：按 P 抓取、持久化、加载
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTLD.Scenes;
using MelonLoader;
using MelonLoader.Utils;
using ModSettings;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace CommunityMinimap;

public sealed partial class ModEntry : MelonMod
{

    // Takes the game's own map image and makes the HUD use it, right now.
    //
    // The region's own texture is 1024x1024 while the panel draws a 2048x2048 image, so the panel is
    // the better source whenever the player has it on screen. It carries the reveal state, which is
    // why this is a key press rather than something automatic: only the player knows whether they
    // have lit the region yet.
    //
    // The framing is shared with the panel path deliberately. The panel reports its bounds and uvRect
    // through the same objects CaptureVanillaMap already reads, so the captured image and the
    // projection that places the player pointer and the markers come from one source. Anything else
    // risks reintroducing the mismatch that made markers land off the map.
    private void CaptureGameMapImage()
    {
        Texture2D owned = null;
        Texture2D forFile = null;
        try
        {
            string captureKey = _settings.CaptureMapKey.ToString();
            Panel_Map panel = InterfaceManager.GetPanel<Panel_Map>();
            if (panel == null || !panel.gameObject.activeInHierarchy)
            {
                LoggerInstance.Msg(
                    $"{captureKey}: the game map is not open, so there is nothing to capture.");
                return;
            }

            Transform regionMap = FindActiveRegionMap(panel.transform);
            if (regionMap == null)
            {
                LoggerInstance.Msg($"{captureKey}: no active region map in the panel.");
                return;
            }

            UITexture main = regionMap.GetComponent<UITexture>();
            if (main == null || ReferenceEquals(main.mainTexture, null))
            {
                LoggerInstance.Msg($"{captureKey}: the region map widget has no texture yet.");
                return;
            }

            string sceneName = _observedSceneName;
            if (MapCatalog.Find(sceneName) == null)
            {
                LoggerInstance.Msg(
                    $"{captureKey}: {sceneName} has no independent map definition; " +
                    "the panel is only showing a parent-region fallback, so it was not captured.");
                return;
            }
            var bounds = new Rect(main.drawingDimensions.x, main.drawingDimensions.y,
                main.drawingDimensions.z - main.drawingDimensions.x,
                main.drawingDimensions.w - main.drawingDimensions.y);
            Rect textureUv = main.uvRect;

            owned = CaptureTexture(main.mainTexture);
            LoggerInstance.Msg(
                $"{captureKey} captured the game map for {sceneName}: {owned.width}x{owned.height}, " +
                $"bounds={bounds}, uv={textureUv}.");

            // Keep a separate copy for the file, so the one handed to a layer can be destroyed by
            // that layer later without the export depending on its lifetime.
            forFile = CaptureTexture(main.mainTexture);

            // Hand it to every layer that is showing the vanilla source: the capture is region-wide,
            // so a layer that wants vanilla wants this image. Each layer takes its own texture, and
            // UseVanillaBaseMap retires the previous one safely.
            int applied = 0;
            for (int i = 0; i < _layers.Length; i++)
            {
                MapLayer layer = _layers[i];
                if (!layer.UsingVanilla || layer.Definition == null)
                    continue;
                Texture2D copy = applied == 0 ? owned : CaptureTexture(main.mainTexture);
                UseVanillaBaseMap(copy, sceneName, layer, textureUv, bounds);
                if (applied == 0)
                    owned = null; // ownership transferred to the layer
                applied++;
            }

            if (applied == 0)
            {
                UnityEngine.Object.Destroy(owned);
                owned = null;
            }
            else
                LoggerInstance.Msg(
                    $"{captureKey} applied the game map to {applied} vanilla layer(s).");

            // The projection fields the panel path would have set, so the two routes agree.
            for (int i = 0; i < _layers.Length; i++)
            {
                MapLayer target = _layers[i];
                target.VanillaProjectionScene = sceneName;
                target.VanillaMapLocalBounds = bounds;
                target.VanillaTextureUv = textureUv;
            }
            _panelTextureMeasuredForScene = sceneName;

            string path = Path.Combine(_modDirectory,
                $"panelmap_{SanitizeFileName(sceneName)}.png");
            WriteTextureToPng(forFile, path);
            // The copy the loader reads next session, so this capture only has to be made once.
            SaveCapturedMap(forFile, sceneName);
            WriteFramingSidecar(sceneName, bounds, textureUv);
            UnityEngine.Object.Destroy(forFile);
            forFile = null;
            LoggerInstance.Msg($"{captureKey} wrote {path} for calibration.");
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Map capture failed: {ex.Message}");
        }
        finally
        {
            if (!ReferenceEquals(owned, null))
                UnityEngine.Object.Destroy(owned);
            if (!ReferenceEquals(forFile, null))
                UnityEngine.Object.Destroy(forFile);
        }
    }


    // Captures the panel's own map texture, measures where its opaque content actually sits, and
    // writes one PNG per region for offline comparison against the base map the HUD is given.
    //
    // The panel's uvRect is applied to this image, so the content box here is in the SAME space the
    // projection's output uv indexes. Comparing it with the base map's content box settles whether
    // the two are the same picture; nothing so far has tested that, and every explanation attempted
    // for the misplaced markers assumed it was true.
    private void MeasureAndExportPanelTexture(UITexture main, string sceneName)
    {
        Texture2D owned = null;
        try
        {
            // Once per session per region, not once ever: the file is now overwritten every time,
            // but re-exporting on every panel refresh would write a 2048x2048 PNG repeatedly for no
            // gain. A new session re-captures, which is what makes a freshly revealed map replace a
            // stale one.
            if (string.Equals(_panelTextureMeasuredForScene, sceneName, StringComparison.Ordinal))
                return;

            Texture source = main.mainTexture;
            if (ReferenceEquals(source, null))
            {
                LoggerInstance.Warning($"Panel texture [{sceneName}]: mainTexture is null.");
                return;
            }

            LoggerInstance.Msg(
                $"Panel texture [{sceneName}]: {source.width}x{source.height}, " +
                $"uvRect={main.uvRect}, drawing={main.drawingDimensions}, " +
                $"widget={main.width}x{main.height}.");

            // The asset itself is not CPU-readable and belongs to Addressables, so measure a copy.
            owned = CaptureTexture(source);
            Rect content = MeasureOpaqueUv(owned);
            LoggerInstance.Msg(
                $"Panel texture content [{sceneName}]: {owned.width}x{owned.height} " +
                $"uv=({content.x:F4},{content.y:F4},{content.width:F4},{content.height:F4}) " +
                $"pixels={content.width * owned.width:F0}x{content.height * owned.height:F0}.");

            string path = Path.Combine(_modDirectory,
                $"panelmap_{SanitizeFileName(sceneName)}.png");
            // Always overwrite. Skipping when the file exists meant the export kept the FIRST panel
            // state it ever saw, which is the pre-reveal fog, and that stale image was then read as
            // evidence about the map's extent for a whole round of investigation. The panel texture
            // is a snapshot of a reveal state, so the newest one is the only one worth keeping.
            WriteTextureToPng(owned, path);
            LoggerInstance.Msg($"Exported panel map texture: {path}");

            _panelTextureMeasuredForScene = sceneName;
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Panel texture measurement [{sceneName}] failed: {ex.Message}");
        }
        finally
        {
            // Diagnostic exports used to leak one persistent 2048x2048 RGBA texture per scene
            // (about 16 MiB before driver overhead). The PNG is the only lasting artifact needed.
            if (!ReferenceEquals(owned, null))
                UnityEngine.Object.Destroy(owned);
        }
    }


    // The captured map, kept on disk and reloaded automatically.
    //
    // Capturing happens once per region by hand, because only the player can light a region up. That
    // work must not be thrown away when the session ends, and it must not depend on the capture
    // being applied live - the first attempt at that did not show up on screen, and a saved image
    // sidesteps the question entirely: the next time the region loads, the file is simply read like
    // any other map image.
    private string CapturedMapPath(string sceneName) =>
        Path.Combine(_modDirectory, "captured", SanitizeFileName(sceneName) + ".png");


    private void SaveCapturedMap(Texture2D texture, string sceneName)
    {
        try
        {
            string path = CapturedMapPath(sceneName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            WriteTextureToPng(texture, path);
            LoggerInstance.Msg($"Saved the captured map for {sceneName}: {path}");
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Saving the captured map for {sceneName} failed: {ex.Message}");
        }
    }


    // Writes the panel's framing next to the captured image. Kept as text rather than folded into
    // the image so a framing that ever needs adjusting can be corrected without re-capturing.
    private void WriteFramingSidecar(string sceneName, Rect bounds, Rect textureUv)
    {
        try
        {
            string sidecar = CapturedMapPath(sceneName) + ".framing";
            File.WriteAllText(sidecar, string.Join(",",
                bounds.x.ToString("R", CultureInfo.InvariantCulture),
                bounds.y.ToString("R", CultureInfo.InvariantCulture),
                bounds.width.ToString("R", CultureInfo.InvariantCulture),
                bounds.height.ToString("R", CultureInfo.InvariantCulture),
                textureUv.x.ToString("R", CultureInfo.InvariantCulture),
                textureUv.y.ToString("R", CultureInfo.InvariantCulture),
                textureUv.width.ToString("R", CultureInfo.InvariantCulture),
                textureUv.height.ToString("R", CultureInfo.InvariantCulture)));
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Writing framing for {sceneName} failed: {ex.Message}");
        }
    }


    // Reads a previously captured map, if one exists, into the layer.
    //
    // The framing recorded with it is the panel's own bounds and uvRect, because that is what the
    // game lays its markers out with. The sidecar is a plain text file so it can be inspected and
    // corrected by hand if a capture ever needs adjusting.
    private bool TryLoadCapturedMap(string sceneName, MapLayer layer)
    {
        Texture2D texture = null;
        try
        {
            string path = CapturedMapPath(sceneName);
            if (!File.Exists(path))
                return false;

            Rect bounds = new(-325f, -325f, 650f, 650f);
            Rect textureUv = new(0f, 0f, 1f, 1f);
            string sidecar = path + ".framing";
            if (File.Exists(sidecar))
            {
                string[] parts = File.ReadAllText(sidecar).Split(',');
                if (parts.Length >= 8 &&
                    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float bx) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float by) &&
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float bw) &&
                    float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float bh) &&
                    float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float ux) &&
                    float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float uy) &&
                    float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float uw) &&
                    float.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out float uh))
                {
                    bounds = new Rect(bx, by, bw, bh);
                    textureUv = new Rect(ux, uy, uw, uh);
                }
            }

            byte[] bytes = File.ReadAllBytes(path);
            var il2CppBytes = new Il2CppStructArray<byte>(bytes);
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, il2CppBytes, true))
            {
                LoggerInstance.Warning($"Captured map for {sceneName} failed to decode.");
                UnityEngine.Object.Destroy(texture);
                texture = null;
                return false;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
            UnityEngine.Object.DontDestroyOnLoad(texture);

            UseVanillaBaseMap(texture, sceneName, layer, textureUv, bounds);
            Texture2D loadedTexture = texture;
            texture = null; // ownership transferred to the layer
            LoggerInstance.Msg(
                $"Using the captured map for {sceneName}: {loadedTexture.width}x{loadedTexture.height}, " +
                $"bounds={bounds}, uv={textureUv}.");
            return true;
        }
        catch (Exception ex)
        {
            if (!ReferenceEquals(texture, null))
                UnityEngine.Object.Destroy(texture);
            LoggerInstance.Warning($"Loading the captured map for {sceneName} failed: {ex.Message}");
            return false;
        }
    }
}
// — sutanm · 社区HUD地图
