using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Reflection;
using UnityEngine;
using Verse;

namespace RimTouch
{
    [StaticConstructorOnStartup]
    public static class RimTouchBootstrap
    {
        static RimTouchBootstrap()
        {
            TryUnlockUiScales();

            Harmony harmony = new Harmony("tatsuki.rimtouch");
            harmony.PatchAll();
            Log.Message("[RimTouch] Loaded touch input MVP.");
        }

        private static void TryUnlockUiScales()
        {
            FieldInfo uiScalesField = ReflectionGuard.Field(typeof(Dialog_Options), "UIScales");
            if (!ReflectionGuard.TrySetField(uiScalesField, null, new[]
            {
                0.75f, 0.85f, 1.0f, 1.1f, 1.25f, 1.35f, 1.5f, 1.75f, 2.0f, 2.25f, 2.5f, 3.0f
            }))
            {
                Log.Warning("[RimTouch] UI scale unlock skipped; continuing without it.");
            }
        }

        public static void PostprocessButtonTap(Rect rect, ref bool result)
        {
            if (TouchInputDriver.ShouldSuppressUiClickInRect(rect))
            {
                result = false;
                Event current = Event.current;
                if (current != null && current.isMouse)
                {
                    current.Use();
                }
                return;
            }

            if (!result && TouchTapRepair.TryConsumeTap(rect))
            {
                result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Game), "InitNewGame")]
    public static class GameInitNewGamePatch
    {
        public static void Postfix()
        {
            RimTouchMod.ApplyExtendedZoomRange();
        }
    }

    [HarmonyPatch(typeof(Game), "LoadGame")]
    public static class GameLoadGamePatch
    {
        public static void Postfix()
        {
            RimTouchMod.ApplyExtendedZoomRange();
        }
    }

    [HarmonyPatch(typeof(Root_Play), "Update")]
    public static class RootPlayUpdatePatch
    {
        public static void Prefix()
        {
            TouchInputDriver.Update();
        }
    }

    [HarmonyPatch(typeof(Root_Entry), "Update")]
    public static class RootEntryUpdatePatch
    {
        public static void Postfix()
        {
            TouchInputDriver.Update();
        }
    }

    [HarmonyPatch(typeof(Root), "OnGUI")]
    public static class RootOnGUIPatch
    {
        public static void Postfix()
        {
            TouchInputDriver.OnGUI();
        }
    }

    [HarmonyPatch(typeof(Selector), "HandleMapClicks")]
    public static class SelectorHandleMapClicksPatch
    {
        public static bool Prefix()
        {
            return !TouchInputDriver.ShouldSuppressVanillaMapInput;
        }
    }

    [HarmonyPatch(typeof(DesignatorManager), "ProcessInputEvents")]
    public static class DesignatorManagerProcessInputEventsPatch
    {
        public static bool Prefix()
        {
            return !TouchInputDriver.ShouldSuppressVanillaMapToolInput;
        }
    }

    [HarmonyPatch(typeof(Targeter), "ProcessInputEvents")]
    public static class TargeterProcessInputEventsPatch
    {
        public static bool Prefix()
        {
            return !TouchInputDriver.ShouldSuppressVanillaMapToolInput;
        }
    }

    [HarmonyPatch(typeof(DragBox), "DragBoxOnGUI")]
    public static class DragBoxOnGUIPatch
    {
        public static bool Prefix()
        {
            return !TouchInputDriver.ShouldSuppressVanillaDragBoxDrawing;
        }
    }

    [HarmonyPatch(typeof(WorldSelector), "HandleWorldClicks")]
    public static class WorldSelectorHandleWorldClicksPatch
    {
        public static bool Prefix()
        {
            return !TouchInputDriver.ShouldSuppressVanillaWorldInput;
        }
    }

    [HarmonyPatch(typeof(WorldDragBox), "DragBoxOnGUI")]
    public static class WorldDragBoxOnGUIPatch
    {
        public static bool Prefix()
        {
            return !TouchInputDriver.ShouldSuppressVanillaWorldDragBoxDrawing;
        }
    }

    [HarmonyPatch(typeof(WorldDrawLayer_MouseTile), "get_Tile")]
    public static class WorldDrawLayerMouseTilePatch
    {
        public static bool Prefix(ref PlanetTile __result)
        {
            if (!TouchInputDriver.ShouldSuppressWorldHover)
            {
                return true;
            }

            __result = PlanetTile.Invalid;
            return false;
        }
    }

    [HarmonyPatch(typeof(CameraDriver), "CalculateCurInputDollyVect")]
    public static class CameraDriverCalculateCurInputDollyVectPatch
    {
        public static void Postfix(CameraDriver __instance, ref Vector2 __result)
        {
            if (TouchInputDriver.ShouldSuppressEdgeScroll)
            {
                __result = Vector2.zero;
                TouchInputDriver.SuppressCameraEdgeScrollState(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(WorldCameraDriver), "CalculateCurInputDollyVect")]
    public static class WorldCameraDriverCalculateCurInputDollyVectPatch
    {
        public static void Postfix(WorldCameraDriver __instance, ref Vector2 __result)
        {
            __result = TouchInputDriver.ModifyWorldCameraInput(__instance, __result);
        }
    }

    [HarmonyPatch(typeof(ResolutionUtility), "UIScaleSafeWithResolution")]
    public static class ResolutionUtilityUIScaleSafeWithResolutionPatch
    {
        public static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(WindowStack), "CloseWindowsBecauseClicked")]
    public static class WindowStackCloseWindowsBecauseClickedPatch
    {
        public static bool Prefix(ref bool __result)
        {
            if (!TouchTapRepair.ShouldSuppressWindowClose && !TouchInputDriver.ShouldSuppressWindowCloseFromGesture)
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(WindowStack), "NotifyOutsideClicks")]
    public static class WindowStackNotifyOutsideClicksPatch
    {
        public static bool Prefix()
        {
            return !TouchTapRepair.ShouldSuppressWindowClose && !TouchInputDriver.ShouldSuppressWindowCloseFromGesture;
        }
    }

    [HarmonyPatch(typeof(WindowStack), "Add")]
    public static class WindowStackAddPatch
    {
        public static void Prefix(Window window)
        {
            if (!TouchInputDriver.RecentTouchInput)
            {
                return;
            }

            FloatMenu floatMenu = window as FloatMenu;
            if (floatMenu != null)
            {
                floatMenu.vanishIfMouseDistant = false;
            }
        }
    }

    [HarmonyPatch(typeof(MainTabsRoot), "ToggleTab")]
    public static class MainTabsRootToggleTabPatch
    {
        public static bool Prefix()
        {
            return !TouchInputDriver.ShouldSuppressMainTabsFromGesture;
        }
    }

    [HarmonyPatch(typeof(MainTabsRoot), "SetCurrentTab")]
    public static class MainTabsRootSetCurrentTabPatch
    {
        public static bool Prefix()
        {
            return !TouchInputDriver.ShouldSuppressMainTabsFromGesture;
        }
    }

    [HarmonyPatch(typeof(ColonistBarColonistDrawer), "HandleClicks")]
    public static class ColonistBarColonistDrawerHandleClicksPatch
    {
        public static void Postfix(Rect rect, Pawn colonist)
        {
            if (colonist == null || !TouchTapRepair.TryConsumeDoubleTap(rect))
            {
                return;
            }

            CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(colonist), CameraJumper.MovementMode.Pan);
        }
    }

    [HarmonyPatch(typeof(Widgets), "ButtonInvisible", new[] { typeof(Rect), typeof(bool) })]
    public static class WidgetsButtonInvisiblePatch
    {
        public static void Postfix(Rect butRect, ref bool __result)
        {
            RimTouchBootstrap.PostprocessButtonTap(butRect, ref __result);
        }
    }

    [HarmonyPatch(typeof(Widgets), "ButtonText", new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(System.Nullable<TextAnchor>) })]
    public static class WidgetsButtonTextPatch
    {
        public static void Postfix(Rect rect, ref bool __result)
        {
            RimTouchBootstrap.PostprocessButtonTap(rect, ref __result);
        }
    }

    [HarmonyPatch(typeof(Widgets), "ButtonText", new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(Color), typeof(bool), typeof(System.Nullable<TextAnchor>) })]
    public static class WidgetsButtonTextColorPatch
    {
        public static void Postfix(Rect rect, ref bool __result)
        {
            RimTouchBootstrap.PostprocessButtonTap(rect, ref __result);
        }
    }

    [HarmonyPatch(typeof(Widgets), "ButtonImage", new[] { typeof(Rect), typeof(Texture2D), typeof(bool), typeof(string) })]
    public static class WidgetsButtonImagePatch
    {
        public static void Postfix(Rect butRect, ref bool __result)
        {
            RimTouchBootstrap.PostprocessButtonTap(butRect, ref __result);
        }
    }

    [HarmonyPatch(typeof(Widgets), "ButtonImage", new[] { typeof(Rect), typeof(Texture2D), typeof(Color), typeof(bool), typeof(string) })]
    public static class WidgetsButtonImageColorPatch
    {
        public static void Postfix(Rect butRect, ref bool __result)
        {
            RimTouchBootstrap.PostprocessButtonTap(butRect, ref __result);
        }
    }

    [HarmonyPatch(typeof(Widgets), "ButtonImage", new[] { typeof(Rect), typeof(Texture2D), typeof(Color), typeof(Color), typeof(bool), typeof(string) })]
    public static class WidgetsButtonImageTwoColorPatch
    {
        public static void Postfix(Rect butRect, ref bool __result)
        {
            RimTouchBootstrap.PostprocessButtonTap(butRect, ref __result);
        }
    }
}
