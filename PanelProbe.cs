using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace CommunityMinimap;

// TEMPORARY DIAGNOSTIC. This logs and changes nothing else.
//
// Patching InputManager.ExecuteOpenMapActionFromObjective was a wrong guess: a whole session in
// which a charcoal survey did open the built-in map produced no "Survey popup:" line at all, so
// that method is not on the survey's path. Its name says "FromObjective" - an objective prompt -
// which is probably what it really is.
//
// The survey must be reaching Panel_Map without going through InputManager. Rather than guess a
// second time, watch every plausible entry point at once and let a single survey say which one
// actually fires, and in what order.
internal static class PanelProbe
{
    private static readonly Dictionary<string, DateTime> s_lastLog = new(StringComparer.Ordinal);

    internal static void Install(HarmonyLib.Harmony harmony)
    {
        // Registered by hand rather than through PatchAll: a patch whose target signature is
        // wrong throws, and one throw during PatchAll would take the whole mod down with it.
        Watch(harmony, typeof(Panel_Map), "Enable", new[] { typeof(bool) });
        Watch(harmony, typeof(Panel_Map), "Enable", new[] { typeof(bool), typeof(bool) });
        Watch(harmony, typeof(Panel_Map), "DoDetailSurvey", new[] { typeof(SurveyType) });
        Watch(harmony, typeof(Panel_Map), "RevealFogForScene", new[] { typeof(string) });
        Watch(harmony, typeof(Panel_Map), "RevealCurrentScene", Type.EmptyTypes);
        Watch(harmony, typeof(Panel_ActionsRadial), "DoOpenMap", Type.EmptyTypes);
    }

    private static void Watch(HarmonyLib.Harmony harmony, Type type, string name, Type[] parameters)
    {
        try
        {
            MethodInfo target = AccessTools.Method(type, name, parameters);
            if (target == null)
            {
                MelonLogger.Warning($"[CommunityHUD/probe] {type.Name}.{name}: not found");
                return;
            }

            harmony.Patch(target, prefix: new HarmonyMethod(
                typeof(PanelProbe).GetMethod(nameof(Before), BindingFlags.Static | BindingFlags.NonPublic)));
            MelonLogger.Msg($"[CommunityHUD/probe] watching {type.Name}.{name}");
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"[CommunityHUD/probe] {type.Name}.{name}: {ex.Message}");
        }
    }

    private static void Before(MethodBase __originalMethod, object[] __args)
    {
        string name = $"{__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}";
        string detail = __args == null || __args.Length == 0
            ? ""
            : string.Join(", ", Array.ConvertAll(__args, a => a?.ToString() ?? "null"));
        Report($"{name}({detail})");
    }

    internal static void Report(string call)
    {
        // Some of these are plausibly called every frame while the panel is alive; one line per
        // quarter second per method is enough to read the sequence without flooding the log.
        DateTime now = DateTime.UtcNow;
        string key = call.Split('(')[0];
        if (s_lastLog.TryGetValue(key, out DateTime last) &&
            (now - last).TotalMilliseconds < 250.0)
            return;
        s_lastLog[key] = now;

        MelonLogger.Msg($"[CommunityHUD/probe] {call}   <- {Caller()}");
    }

    // Patching the panel was the wrong guess once already, so record who called it. If the managed
    // stack is not available under IL2CPP this says so instead of pretending.
    private static string Caller()
    {
        try
        {
            var trace = new StackTrace(2, false);
            var parts = new List<string>();
            for (int i = 0; i < trace.FrameCount && parts.Count < 4; i++)
            {
                MethodBase method = trace.GetFrame(i)?.GetMethod();
                if (method == null)
                    continue;
                parts.Add($"{method.DeclaringType?.Name}.{method.Name}");
            }
            return parts.Count > 0 ? string.Join(" <- ", parts) : "?";
        }
        catch (Exception ex)
        {
            return $"<no stack: {ex.GetType().Name}>";
        }
    }
}
