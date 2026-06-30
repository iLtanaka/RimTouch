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
        private static void SamplePanVelocityFromMapDelta(Vector3 mapDelta)
        {
            float now = Time.realtimeSinceStartup;
            float dt = now - lastPanSampleTime;
            lastPanSampleTime = now;

            if (dt <= 0.0001f || dt > 0.18f)
            {
                return;
            }

            Vector3 velocity = mapDelta / dt;
            velocity.y = 0f;
            if (!IsFinite(velocity))
            {
                return;
            }

            if (velocity.magnitude > MaxPanInertiaSpeed)
            {
                velocity = velocity.normalized * MaxPanInertiaSpeed;
            }

            RecordPanVelocitySample(velocity, now);
            panVelocity = GetRecentPanVelocity(now);
        }

        private static void SampleZoomVelocity(float previousValue, float currentValue)
        {
            float now = Time.realtimeSinceStartup;
            float dt = now - lastZoomSampleTime;
            lastZoomSampleTime = now;

            if (dt <= 0.0001f || dt > 0.18f || previousValue <= 0f || currentValue <= 0f)
            {
                return;
            }

            float velocity = Mathf.Log(currentValue / previousValue) / dt;
            if (float.IsNaN(velocity) || float.IsInfinity(velocity))
            {
                return;
            }

            velocity = Mathf.Clamp(velocity, -MaxZoomInertiaSpeed, MaxZoomInertiaSpeed);
            RecordZoomVelocitySample(velocity, now);
            zoomVelocity = GetRecentZoomVelocity(now);
        }

        private static void UpdateInertia()
        {
            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Clamp(now - lastUpdateTime, 0.001f, 0.05f);
            lastUpdateTime = now;

            if (IsWorldMapActive())
            {
                UpdateWorldInertia(dt);
                return;
            }

            if (Current.ProgramState != ProgramState.Playing)
            {
                StopInertia();
                return;
            }

            if (Find.CurrentMap != null)
            {
                UpdateMapInertia(dt);
                return;
            }

            StopInertia();
        }

        private static void UpdateMapInertia(float dt)
        {
            CameraDriver cameraDriver = Find.CameraDriver;
            if (cameraDriver == null)
            {
                StopInertia();
                return;
            }

            worldPanVelocity = Vector2.zero;
            Vector3 rootPos;
            float size;
            if (!TryGetCameraRootPos(cameraDriver, out rootPos) || !TryGetCameraRootSize(cameraDriver, out size))
            {
                StopInertia();
                return;
            }

            bool changed = false;

            if (panVelocity.sqrMagnitude > MinPanInertiaSpeed * MinPanInertiaSpeed && PanInertiaStrength > 0f)
            {
                rootPos += panVelocity * PanInertiaStrength * dt;
                panVelocity *= Mathf.Exp(-PanInertiaDamping * dt);
                changed = true;
            }
            else
            {
                panVelocity = Vector3.zero;
            }

            if (Mathf.Abs(zoomVelocity) > MinZoomInertiaSpeed && ZoomInertiaStrength > 0f)
            {
                float targetSize = Mathf.Clamp(size * Mathf.Exp(zoomVelocity * ZoomInertiaStrength * dt), GetMinZoomSize(cameraDriver), GetMaxZoomSize(cameraDriver));
                if (Mathf.Abs(targetSize - size) > 0.001f)
                {
                    if (zoomInertiaAnchorValid)
                    {
                        cameraDriver.SetRootPosAndSize(rootPos, size);
                        Vector3 before = GuiToMapPosition(zoomInertiaGui);
                        cameraDriver.SetRootPosAndSize(rootPos, targetSize);
                        Vector3 after = GuiToMapPosition(zoomInertiaGui);
                        Vector3 zoomedRootPos;
                        if (!TryGetCameraRootPos(cameraDriver, out zoomedRootPos))
                        {
                            StopInertia();
                            return;
                        }
                        rootPos = zoomedRootPos + (before - after);
                    }
                    size = targetSize;
                    zoomVelocity *= Mathf.Exp(ZoomInertiaDamping * -dt);
                    changed = true;
                }
                else
                {
                    zoomVelocity = 0f;
                    zoomInertiaAnchorValid = false;
                }
            }
            else
            {
                zoomVelocity = 0f;
                zoomInertiaAnchorValid = false;
            }

            if (changed)
            {
                cameraDriver.SetRootPosAndSize(rootPos, size);
            }
        }

        private static void UpdateWorldInertia(float dt)
        {
            WorldCameraDriver driver = Find.WorldCameraDriver;
            if (driver == null)
            {
                StopInertia();
                return;
            }

            panVelocity = Vector3.zero;

            float worldPanStopSpeed = GetWorldPanInertiaStopSpeed(driver);
            if (worldPanVelocity.sqrMagnitude > worldPanStopSpeed * worldPanStopSpeed && PanInertiaStrength > 0f)
            {
                if (ShouldDisableWorldPanInertia(driver))
                {
                    worldPanVelocity = Vector2.zero;
                    worldInertiaAnchorValid = false;
                }
                else
                {
                    worldInertiaGui += worldPanVelocity * PanInertiaStrength * WorldPanInertiaStrengthScale * GetWorldPanInertiaMoveScale(driver) * dt;
                    if (!KeepWorldPointUnderGui(driver, worldInertiaAnchorPoint, worldInertiaGui))
                    {
                        worldPanVelocity = Vector2.zero;
                        worldInertiaAnchorValid = false;
                    }
                    else
                    {
                        worldPanVelocity *= Mathf.Exp(-GetWorldPanInertiaDamping(driver) * dt);
                        if (worldPanVelocity.sqrMagnitude <= worldPanStopSpeed * worldPanStopSpeed)
                        {
                            worldPanVelocity = Vector2.zero;
                            worldInertiaAnchorValid = false;
                        }
                    }
                }
            }
            else
            {
                worldPanVelocity = Vector2.zero;
                worldInertiaAnchorValid = false;
            }

            if (Mathf.Abs(zoomVelocity) > MinZoomInertiaSpeed && ZoomInertiaStrength > 0f)
            {
                float currentAltitude;
                if (!TryGetWorldAltitude(driver, out currentAltitude))
                {
                    zoomVelocity = 0f;
                    return;
                }

                float maxAltitude = GetWorldMaxAltitude(driver);
                float targetAltitude = Mathf.Clamp(currentAltitude * Mathf.Exp(zoomVelocity * ZoomInertiaStrength * dt), WorldCameraDriver.MinAltitude, maxAltitude);
                if (Mathf.Abs(targetAltitude - currentAltitude) > 0.001f)
                {
                    if (!TrySetWorldAltitude(driver, targetAltitude))
                    {
                        zoomVelocity = 0f;
                        return;
                    }
                    zoomVelocity *= Mathf.Exp(ZoomInertiaDamping * -dt);
                }
                else
                {
                    zoomVelocity = 0f;
                }
            }
            else
            {
                zoomVelocity = 0f;
            }
        }

        private static void FinalizeInertiaFromGesture()
        {
            if (mode == TouchMode.MapPan || mode == TouchMode.TwoFinger)
            {
                float now = Time.realtimeSinceStartup;
                zoomVelocity = GetRecentZoomVelocity(now);

                if (IsWorldMapActive())
                {
                    panVelocity = Vector3.zero;
                    worldPanVelocity = GetRecentWorldPanVelocity(now);

                    WorldCameraDriver worldDriver = Find.WorldCameraDriver;
                    if (panGestureDistance < DragSlopPixels * 1.25f
                        || worldPanVelocity.magnitude < GetWorldPanInertiaStartSpeed(worldDriver)
                        || ShouldDisableWorldPanInertia(worldDriver))
                    {
                        worldPanVelocity = Vector2.zero;
                        worldInertiaAnchorValid = false;
                    }
                }
                else
                {
                    worldPanVelocity = Vector2.zero;
                    panVelocity = GetRecentPanVelocity(now);

                    if (panGestureDistance < DragSlopPixels * 1.25f || panVelocity.magnitude < MinPanInertiaStartSpeed)
                    {
                        panVelocity = Vector3.zero;
                    }
                }

                if (Mathf.Abs(zoomVelocity) < MinZoomInertiaStartSpeed)
                {
                    zoomVelocity = 0f;
                    zoomInertiaAnchorValid = false;
                }
            }
            else
            {
                StopInertia();
            }
        }

        private static void StopInertia()
        {
            panVelocity = Vector3.zero;
            worldPanVelocity = Vector2.zero;
            pendingWorldCameraDolly = Vector2.zero;
            worldInertiaAnchorValid = false;
            worldInertiaAnchorPoint = Vector3.zero;
            worldInertiaGui = Vector2.zero;
            zoomInertiaGui = Vector2.zero;
            zoomInertiaAnchorValid = false;
            zoomVelocity = 0f;
        }

        private static void ClearInertiaSamples()
        {
            ClearPanVelocitySamples();
            ClearWorldPanVelocitySamples();
            ClearZoomVelocitySamples();
        }

        private static void ClearPanVelocitySamples()
        {
            panVelocitySampleIndex = 0;
            panVelocitySampleCount = 0;
            for (int i = 0; i < PanVelocitySamples.Length; i++)
            {
                PanVelocitySamples[i] = Vector3.zero;
                PanVelocitySampleTimes[i] = 0f;
            }
        }

        private static void ClearZoomVelocitySamples()
        {
            zoomVelocitySampleIndex = 0;
            zoomVelocitySampleCount = 0;
            for (int i = 0; i < ZoomVelocitySamples.Length; i++)
            {
                ZoomVelocitySamples[i] = 0f;
                ZoomVelocitySampleTimes[i] = 0f;
            }
        }

        private static void RecordPanVelocitySample(Vector3 velocity, float time)
        {
            PanVelocitySamples[panVelocitySampleIndex] = velocity;
            PanVelocitySampleTimes[panVelocitySampleIndex] = time;
            panVelocitySampleIndex = (panVelocitySampleIndex + 1) % PanVelocitySamples.Length;
            panVelocitySampleCount = Mathf.Min(panVelocitySampleCount + 1, PanVelocitySamples.Length);
        }

        private static Vector3 GetRecentPanVelocity(float now)
        {
            if (panVelocitySampleCount <= 0)
            {
                return panVelocity;
            }

            Vector3 sum = Vector3.zero;
            float totalWeight = 0f;
            for (int i = 0; i < panVelocitySampleCount; i++)
            {
                int index = (panVelocitySampleIndex - 1 - i + PanVelocitySamples.Length) % PanVelocitySamples.Length;
                float age = now - PanVelocitySampleTimes[index];
                if (age < 0f || age > PanVelocityHistorySeconds)
                {
                    continue;
                }

                float recencyWeight = Mathf.Clamp01(1f - age / PanVelocityHistorySeconds);
                float orderWeight = 1f / (1f + i * 0.35f);
                float weight = Mathf.Max(0.05f, recencyWeight) * orderWeight;
                sum += PanVelocitySamples[index] * weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
            {
                return Vector3.zero;
            }
            return sum / totalWeight;
        }

        private static void ClearWorldPanVelocitySamples()
        {
            worldPanVelocitySampleIndex = 0;
            worldPanVelocitySampleCount = 0;
            for (int i = 0; i < WorldPanVelocitySamples.Length; i++)
            {
                WorldPanVelocitySamples[i] = Vector2.zero;
                WorldPanVelocitySampleTimes[i] = 0f;
            }
        }

        private static void SampleWorldPanVelocity(Vector2 guiDelta)
        {
            float now = Time.realtimeSinceStartup;
            float dt = now - lastWorldPanSampleTime;
            lastWorldPanSampleTime = now;

            if (dt <= 0.0001f || dt > 0.18f || guiDelta == Vector2.zero)
            {
                return;
            }

            Vector2 velocity = guiDelta / dt;
            if (!IsFinite(velocity))
            {
                return;
            }

            if (velocity.magnitude > MaxWorldPanInertiaSpeed)
            {
                velocity = velocity.normalized * MaxWorldPanInertiaSpeed;
            }

            RecordWorldPanVelocitySample(velocity, now);
            worldPanVelocity = GetRecentWorldPanVelocity(now);
        }

        private static void RecordWorldPanVelocitySample(Vector2 velocity, float time)
        {
            WorldPanVelocitySamples[worldPanVelocitySampleIndex] = velocity;
            WorldPanVelocitySampleTimes[worldPanVelocitySampleIndex] = time;
            worldPanVelocitySampleIndex = (worldPanVelocitySampleIndex + 1) % WorldPanVelocitySamples.Length;
            worldPanVelocitySampleCount = Mathf.Min(worldPanVelocitySampleCount + 1, WorldPanVelocitySamples.Length);
        }

        private static Vector2 GetRecentWorldPanVelocity(float now)
        {
            if (worldPanVelocitySampleCount <= 0)
            {
                return worldPanVelocity;
            }

            Vector2 sum = Vector2.zero;
            float totalWeight = 0f;
            for (int i = 0; i < worldPanVelocitySampleCount; i++)
            {
                int index = (worldPanVelocitySampleIndex - 1 - i + WorldPanVelocitySamples.Length) % WorldPanVelocitySamples.Length;
                float age = now - WorldPanVelocitySampleTimes[index];
                if (age < 0f || age > WorldPanVelocityHistorySeconds)
                {
                    continue;
                }

                float recencyWeight = Mathf.Clamp01(1f - age / WorldPanVelocityHistorySeconds);
                float orderWeight = 1f / (1f + i * 0.35f);
                float weight = Mathf.Max(0.05f, recencyWeight) * orderWeight;
                sum += WorldPanVelocitySamples[index] * weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
            {
                return Vector2.zero;
            }
            return sum / totalWeight;
        }

        private static void RecordZoomVelocitySample(float velocity, float time)
        {
            ZoomVelocitySamples[zoomVelocitySampleIndex] = velocity;
            ZoomVelocitySampleTimes[zoomVelocitySampleIndex] = time;
            zoomVelocitySampleIndex = (zoomVelocitySampleIndex + 1) % ZoomVelocitySamples.Length;
            zoomVelocitySampleCount = Mathf.Min(zoomVelocitySampleCount + 1, ZoomVelocitySamples.Length);
        }

        private static float GetRecentZoomVelocity(float now)
        {
            if (zoomVelocitySampleCount <= 0)
            {
                return zoomVelocity;
            }

            float sum = 0f;
            float totalWeight = 0f;
            for (int i = 0; i < zoomVelocitySampleCount; i++)
            {
                int index = (zoomVelocitySampleIndex - 1 - i + ZoomVelocitySamples.Length) % ZoomVelocitySamples.Length;
                float age = now - ZoomVelocitySampleTimes[index];
                if (age < 0f || age > ZoomVelocityHistorySeconds)
                {
                    continue;
                }

                float recencyWeight = Mathf.Clamp01(1f - age / ZoomVelocityHistorySeconds);
                float orderWeight = 1f / (1f + i * 0.35f);
                float weight = Mathf.Max(0.05f, recencyWeight) * orderWeight;
                sum += ZoomVelocitySamples[index] * weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
            {
                return 0f;
            }
            return sum / totalWeight;
        }

        private static bool IsFinite(Vector3 vector)
        {
            return !float.IsNaN(vector.x) && !float.IsInfinity(vector.x)
                && !float.IsNaN(vector.y) && !float.IsInfinity(vector.y)
                && !float.IsNaN(vector.z) && !float.IsInfinity(vector.z);
        }

        private static bool IsFinite(Vector2 vector)
        {
            return !float.IsNaN(vector.x) && !float.IsInfinity(vector.x)
                && !float.IsNaN(vector.y) && !float.IsInfinity(vector.y);
        }

        private static float PanInertiaStrength
        {
            get
            {
                return RimTouchMod.Settings != null ? RimTouchMod.Settings.panInertiaStrength : DefaultPanInertiaStrength;
            }
        }

        private static float PanInertiaDamping
        {
            get
            {
                return RimTouchMod.Settings != null ? Mathf.Max(0.1f, RimTouchMod.Settings.panInertiaDamping) : DefaultPanInertiaDamping;
            }
        }

        private static float ZoomInertiaStrength
        {
            get
            {
                return RimTouchMod.Settings != null ? RimTouchMod.Settings.zoomInertiaStrength : DefaultZoomInertiaStrength;
            }
        }

        private static float ZoomInertiaDamping
        {
            get
            {
                return RimTouchMod.Settings != null ? Mathf.Max(0.1f, RimTouchMod.Settings.zoomInertiaDamping) : DefaultZoomInertiaDamping;
            }
        }
    }
}
