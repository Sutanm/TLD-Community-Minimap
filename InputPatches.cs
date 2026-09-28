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

    // The survey popup does not go through InputManager at all.
    //
    // A probe over one session settled it: a charcoal survey produced Panel_Map.Enable(true, true)
    // with no InputManager method anywhere on the path, while the radial menu produced
    // Panel_ActionsRadial.DoOpenMap() followed by the one-argument Panel_Map.Enable(true). The two
    // overloads are told apart here, so suppressing the survey popup cannot also suppress the
    // radial menu.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Panel_Map), nameof(Panel_Map.Enable), new[] { typeof(bool), typeof(bool) })]
    private static bool RedirectSurveyMapPopup(bool enable)
    {
        if (!enable)
            return true;                      // letting the panel close is always right
        return !ModEntry.HandleSurveyMapPopup("Panel_Map.Enable(bool, bool)");
    }

    // Kept from an earlier, wrong guess. The probe showed this method is not on the survey's path
    // at all - its name says "FromObjective", an objective prompt - but objective prompts do exist
    // in story mode, so it stays wired to the same three-way answer rather than being dropped.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.ExecuteOpenMapActionFromObjective))]
    private static bool RedirectOpenMapFromObjective()
    {
        return !ModEntry.HandleSurveyMapPopup("ExecuteOpenMapActionFromObjective");
    }
}
