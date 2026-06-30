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
        private static Vector3 KeepMapPointUnderGui(Vector3 mapPoint, Vector2 gui)
        {
            CameraDriver cameraDriver = Find.CameraDriver;
            if (cameraDriver == null || Find.CurrentMap == null)
            {
                return Vector3.zero;
            }

            Vector3 rootPos;
            float size;
            if (!TryGetCameraRootPos(cameraDriver, out rootPos) || !TryGetCameraRootSize(cameraDriver, out size))
            {
                return Vector3.zero;
            }

            Vector3 currentMap = GuiToMapPosition(gui);
            Vector3 delta = mapPoint - currentMap;
            cameraDriver.SetRootPosAndSize(rootPos + delta, size);
            return delta;
        }

        private static void ApplyPinchZoom(float previousDistance, float currentDistance, Vector2 midpointGui)
        {
            if (previousDistance <= 1f || currentDistance <= 1f)
            {
                return;
            }

            CameraDriver cameraDriver = Find.CameraDriver;
            if (cameraDriver == null)
            {
                return;
            }

            Vector3 rootPos;
            float currentSize;
            if (!TryGetCameraRootPos(cameraDriver, out rootPos) || !TryGetCameraRootSize(cameraDriver, out currentSize))
            {
                return;
            }

            float zoomRatio = previousDistance / currentDistance;
            float targetSize = Mathf.Clamp(currentSize * zoomRatio, GetMinZoomSize(cameraDriver), GetMaxZoomSize(cameraDriver));
            Vector3 before = GuiToMapPosition(midpointGui);
            cameraDriver.SetRootPosAndSize(rootPos, targetSize);
            Vector3 after = GuiToMapPosition(midpointGui);
            Vector3 zoomedRootPos;
            if (!TryGetCameraRootPos(cameraDriver, out zoomedRootPos))
            {
                return;
            }
            cameraDriver.SetRootPosAndSize(zoomedRootPos + (before - after), targetSize);
            zoomInertiaGui = midpointGui;
            zoomInertiaAnchorValid = true;
            SampleZoomVelocity(currentSize, targetSize);
        }

        public static void SuppressCameraEdgeScrollState(CameraDriver cameraDriver)
        {
            if (cameraDriver == null)
            {
                return;
            }

            ReflectionGuard.TrySetField(CameraDesiredDollyField, cameraDriver, Vector2.zero);
            ReflectionGuard.TrySetField(CameraDesiredDollyRawField, cameraDriver, Vector2.zero);
            ReflectionGuard.TrySetField(CameraVelocityField, cameraDriver, Vector3.zero);
            ReflectionGuard.TrySetField(CameraMouseBottomEdgeStartField, cameraDriver, 0f);
        }

        private static Vector3 GetCameraRootPos(CameraDriver cameraDriver)
        {
            Vector3 rootPos;
            return TryGetCameraRootPos(cameraDriver, out rootPos) ? rootPos : Vector3.zero;
        }

        private static float GetCameraRootSize(CameraDriver cameraDriver)
        {
            float rootSize;
            return TryGetCameraRootSize(cameraDriver, out rootSize) ? rootSize : GetMinZoomSize(cameraDriver);
        }

        private static bool TryGetCameraRootPos(CameraDriver cameraDriver, out Vector3 rootPos)
        {
            rootPos = Vector3.zero;
            return cameraDriver != null && ReflectionGuard.TryGetField(CameraRootPosField, cameraDriver, out rootPos);
        }

        private static bool TryGetCameraRootSize(CameraDriver cameraDriver, out float rootSize)
        {
            rootSize = 0f;
            return cameraDriver != null && ReflectionGuard.TryGetField(CameraRootSizeField, cameraDriver, out rootSize);
        }

        private static float GetMinZoomSize(CameraDriver cameraDriver)
        {
            if (cameraDriver != null && cameraDriver.config != null)
            {
                float min = cameraDriver.config.sizeRange.min;
                if (min > 0f)
                {
                    return min;
                }
            }
            return FallbackMinZoomSize;
        }

        private static float GetMaxZoomSize(CameraDriver cameraDriver)
        {
            if (cameraDriver != null && cameraDriver.config != null)
            {
                float max = cameraDriver.config.sizeRange.max;
                if (max > 0f)
                {
                    return max;
                }
            }
            return FallbackMaxZoomSize;
        }
    }
}
