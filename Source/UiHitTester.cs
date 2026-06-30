using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimTouch
{
    public static partial class TouchInputDriver
    {
        private static bool IsLikelyUiPosition(Vector2 gui)
        {
            WindowStack stack = Find.WindowStack;
            if (stack != null && stack.GetWindowAt(gui) != null)
            {
                return true;
            }

            float width = UI.screenWidth;
            float height = UI.screenHeight;
            if (gui.y > height - 190f)
            {
                return true;
            }
            if (Find.CurrentMap != null && gui.x < 700f && gui.y > height - 380f)
            {
                return true;
            }
            if (gui.x > width - 170f)
            {
                return true;
            }
            if (gui.y < 90f)
            {
                return true;
            }

            if (Find.CurrentMap == null)
            {
                return !IsWorldMapActive();
            }

            return false;
        }

        private static bool IsVanillaMapToolActive()
        {
            if (Find.DesignatorManager != null && Find.DesignatorManager.SelectedDesignator != null)
            {
                return true;
            }
            if (Find.Targeter != null && Find.Targeter.IsTargeting)
            {
                return true;
            }
            return false;
        }

        private static bool HasCurrentOneFingerLocalMapTouch()
        {
            try
            {
                if (Input.touchCount != 1)
                {
                    return false;
                }

                if (Find.CurrentMap == null || IsWorldMapActive())
                {
                    return false;
                }

                Touch touch = Input.GetTouch(0);
                Vector2 gui = TouchToGui(touch.position);
                return !IsLikelyUiPosition(gui);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryCancelVanillaMapToolFromTwoFingerTap()
        {
            if (!twoFingerStartedWithVanillaMapTool || Find.CurrentMap == null || IsWorldMapActive())
            {
                return false;
            }

            if (!IsTwoFingerTapGesture())
            {
                return false;
            }

            if (!CancelVanillaMapTool())
            {
                return false;
            }

            suppressReleaseTap = true;
            SuppressUiClickFallout();
            TouchTapRepair.Clear();
            CancelRimWorldDragBox();
            return true;
        }

        private static bool CancelVanillaMapTool()
        {
            bool canceled = false;

            if (Find.DesignatorManager != null && Find.DesignatorManager.SelectedDesignator != null)
            {
                Find.DesignatorManager.Deselect();
                canceled = true;
            }

            if (Find.Targeter != null && Find.Targeter.IsTargeting)
            {
                Find.Targeter.StopTargeting();
                canceled = true;
            }

            return canceled;
        }

        private static void SuppressUiClickFallout()
        {
            suppressWindowCloseUntilFrame = Math.Max(suppressWindowCloseUntilFrame, Time.frameCount + 12);
            if (Input.touchCount >= 2 || mode == TouchMode.TwoFinger)
            {
                suppressUiClickUntilFrame = Math.Max(suppressUiClickUntilFrame, Time.frameCount + 12);
            }
        }

        private static void SuppressVanillaMapInputFallout()
        {
            suppressVanillaMapInputUntilFrame = Math.Max(suppressVanillaMapInputUntilFrame, Time.frameCount + 12);
        }

        private static void SuppressUiScrollFallout()
        {
            suppressUiClickUntilFrame = Math.Max(suppressUiClickUntilFrame, Time.frameCount + 12);
            suppressWindowCloseUntilFrame = Math.Max(suppressWindowCloseUntilFrame, Time.frameCount + 12);
        }

        private static Vector2 TouchToGui(Vector2 touchPosition)
        {
            float scaleX = Screen.width > 0 ? UI.screenWidth / (float)Screen.width : 1f;
            float scaleY = Screen.height > 0 ? UI.screenHeight / (float)Screen.height : 1f;
            return new Vector2(touchPosition.x * scaleX, (Screen.height - touchPosition.y) * scaleY);
        }

        private static Vector2 TouchDeltaToGui(Vector2 touchDelta)
        {
            float scaleX = Screen.width > 0 ? UI.screenWidth / (float)Screen.width : 1f;
            float scaleY = Screen.height > 0 ? UI.screenHeight / (float)Screen.height : 1f;
            return new Vector2(touchDelta.x * scaleX, -touchDelta.y * scaleY);
        }

        private static Vector2 GuiToLocalGuiPoint(Vector2 gui)
        {
            try
            {
                return GUIUtility.ScreenToGUIPoint(GuiToScreenGuiPoint(gui));
            }
            catch
            {
                return gui;
            }
        }

        private static Vector2 GuiToScreenGuiPoint(Vector2 gui)
        {
            float scaleX = UI.screenWidth > 0 ? Screen.width / (float)UI.screenWidth : 1f;
            float scaleY = UI.screenHeight > 0 ? Screen.height / (float)UI.screenHeight : 1f;
            return new Vector2(gui.x * scaleX, gui.y * scaleY);
        }

        private static Vector2 CurrentEventMousePosition(Vector2 fallbackGui)
        {
            Event current = Event.current;
            return current != null ? current.mousePosition : fallbackGui;
        }

        private static bool SameRect(Rect a, Rect b)
        {
            return Mathf.Abs(a.x - b.x) <= 0.5f
                && Mathf.Abs(a.y - b.y) <= 0.5f
                && Mathf.Abs(a.width - b.width) <= 0.5f
                && Mathf.Abs(a.height - b.height) <= 0.5f;
        }

        private static bool SameScrollRect(Rect a, Rect b)
        {
            if (SameRect(a, b))
            {
                return true;
            }

            float widthDelta = Mathf.Abs(a.width - b.width);
            float heightDelta = Mathf.Abs(a.height - b.height);
            if (widthDelta > Mathf.Max(32f, Mathf.Min(a.width, b.width) * 0.2f)
                || heightDelta > Mathf.Max(32f, Mathf.Min(a.height, b.height) * 0.2f))
            {
                return false;
            }

            float xMin = Mathf.Max(a.xMin, b.xMin);
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            float yMax = Mathf.Min(a.yMax, b.yMax);
            float overlapWidth = Mathf.Max(0f, xMax - xMin);
            float overlapHeight = Mathf.Max(0f, yMax - yMin);
            float overlapArea = overlapWidth * overlapHeight;
            float minArea = Mathf.Min(Mathf.Max(1f, a.width * a.height), Mathf.Max(1f, b.width * b.height));
            return overlapArea / minArea >= 0.65f;
        }

        private static Vector3 GuiToScreenPoint(Vector2 gui)
        {
            float scaleX = UI.screenWidth > 0 ? Screen.width / (float)UI.screenWidth : 1f;
            float scaleY = UI.screenHeight > 0 ? Screen.height / (float)UI.screenHeight : 1f;
            return new Vector3(gui.x * scaleX, (UI.screenHeight - gui.y) * scaleY, 0f);
        }

        private static Vector3 GuiToMapPosition(Vector2 gui)
        {
            return UI.UIToMapPosition(new Vector2(gui.x, UI.screenHeight - gui.y));
        }
    }
}
