using HarmonyLib;
using Il2Cpp;

namespace CommunityMinimap;

[HarmonyPatch]
internal static class InputPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetEscapePressed))]
    private static bool SuppressEscapePressed(ref bool __result)
    {
        return MaybeSuppress(ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetPauseMenuTogglePressed))]
    private static bool SuppressPauseToggle(ref bool __result)
    {
        return MaybeSuppress(ref __result);
    }

    private static bool MaybeSuppress(ref bool result)
    {
        if (!ModEntry.ShouldSuppressGameEscape())
            return true;

        result = false;
        return false;
    }

    // Every "open the map" path funnels through this one action: the map key, objective prompts
    // and anything else that asks for the map. Returning false skips the original, so the game's
    // map panel is never opened - we show our own full map instead. Panel_Map stays untouched.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.ExecuteOpenMapAction))]
    private static bool RedirectOpenMap()
    {
        return !ModEntry.TryRedirectGameMap();
    }

    // There is a second way in: the objective prompt path, which is what cartography uses when
    // charcoal reveals an area. Without this the game's own map still appears after surveying,
    // which both breaks the takeover and rebuilds the panel behind our back.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.ExecuteOpenMapActionFromObjective))]
    private static bool RedirectOpenMapFromObjective()
    {
        return !ModEntry.TryRedirectGameMap();
    }}
