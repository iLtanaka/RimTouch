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
    public static class TouchInputDriver
    {
        private const float DragSlopPixels = 12f;
        private const float SelectHoldSeconds = 0.22f;
        private const float RightClickHoldSeconds = 0.50f;
        private const float RightClickHoldSlopPixels = 14f;
        private const float TouchMapTargetRadiusPixels = 28f;
        private const float FallbackMinZoomSize = 11f;
        private const float FallbackMaxZoomSize = 60f;
        private const float DefaultPanInertiaStrength = 1.05f;
        private const float DefaultPanInertiaDamping = 4.8f;
        private const float MinPanInertiaStartSpeed = 0.12f;
        private const float MinPanInertiaSpeed = 0.035f;
        private const float MaxPanInertiaSpeed = 120f;
        private const float PanVelocityHistorySeconds = 0.14f;
        private const int PanVelocitySampleCapacity = 6;
        private const float WorldPanPixelsToRotation = 0.12f;
        private const float WorldPanInertiaStrengthScale = 0.56f;
        private const float MinWorldPanInertiaStartSpeed = 80f;
        private const float MinWorldPanInertiaSpeed = 8f;
        private const float MaxWorldPanInertiaSpeed = 3500f;
        private const float WorldPanVelocityHistorySeconds = 0.14f;
        private const int WorldPanVelocitySampleCapacity = 6;
        private const float WorldAnchorPoleDampingStart = 0.74f;
        private const float WorldAnchorPoleMinimumScale = 0.18f;
        private const float MaxWorldAnchorRotationDegrees = 6f;
        private const float WorldPoleInertiaDisable = 0.92f;
        private const float WorldCloseZoomInertiaStartSpeed = 100f;
        private const float WorldCloseZoomInertiaStopSpeed = 55f;
        private const float WorldCloseZoomInertiaDampingScale = 2.15f;
        private const float WorldCloseZoomInertiaMoveScale = 0.62f;
        private const float TwoFingerCancelTapSeconds = 0.42f;
        private const float TwoFingerCancelMoveSlopPixels = 18f;
        private const float TwoFingerCancelDistanceSlopPixels = 18f;
        private const float DefaultZoomInertiaStrength = 0.22f;
        private const float DefaultZoomInertiaDamping = 7.5f;
        private const float MinZoomInertiaStartSpeed = 0.035f;
        private const float MinZoomInertiaSpeed = 0.01f;
        private const int ZoomVelocitySampleCapacity = 5;
        private const float ZoomVelocityHistorySeconds = 0.12f;

        private static readonly FieldInfo CameraRootPosField = AccessTools.Field(typeof(CameraDriver), "rootPos");
        private static readonly FieldInfo CameraRootSizeField = AccessTools.Field(typeof(CameraDriver), "rootSize");
        private static readonly FieldInfo CameraVelocityField = AccessTools.Field(typeof(CameraDriver), "velocity");
        private static readonly FieldInfo CameraDesiredDollyField = AccessTools.Field(typeof(CameraDriver), "desiredDolly");
        private static readonly FieldInfo CameraDesiredDollyRawField = AccessTools.Field(typeof(CameraDriver), "desiredDollyRaw");
        private static readonly FieldInfo CameraMouseBottomEdgeStartField = AccessTools.Field(typeof(CameraDriver), "mouseTouchingScreenBottomEdgeStartTime");
        private static readonly FieldInfo SelectorDragBoxField = AccessTools.Field(typeof(Selector), "dragBox");
        private static readonly FieldInfo DragBoxActiveField = AccessTools.Field(typeof(DragBox), "active");
        private static readonly FieldInfo DragBoxStartField = AccessTools.Field(typeof(DragBox), "start");
        private static readonly FieldInfo FloatMenuOptionActionField = AccessTools.Field(typeof(FloatMenuOption), "action");
        private static readonly MethodInfo SelectorSelectInsideDragBoxMethod = AccessTools.Method(typeof(Selector), "SelectInsideDragBox");
        private static readonly MethodInfo FloatMenuMakerMapGetAutoTakeOptionMethod = AccessTools.Method(typeof(FloatMenuMakerMap), "GetAutoTakeOption");
        private static readonly FieldInfo WorldCameraDesiredRotationRawField = AccessTools.Field(typeof(WorldCameraDriver), "desiredRotationRaw");
        private static readonly FieldInfo WorldCameraRotationVelocityField = AccessTools.Field(typeof(WorldCameraDriver), "rotationVelocity");
        private static readonly FieldInfo WorldCameraMouseBottomEdgeStartField = AccessTools.Field(typeof(WorldCameraDriver), "mouseTouchingScreenBottomEdgeStartTime");
        private static readonly FieldInfo WorldCameraAltitudeField = AccessTools.Field(typeof(WorldCameraDriver), "altitude");
        private static readonly FieldInfo WorldCameraDesiredAltitudeField = AccessTools.Field(typeof(WorldCameraDriver), "desiredAltitude");
        private static readonly FieldInfo WorldCameraMaxAltitudeField = AccessTools.Field(typeof(WorldCameraDriver), "MaxAltitude");
        private static readonly FieldInfo WorldCameraSphereRotationField = AccessTools.Field(typeof(WorldCameraDriver), "sphereRotation");
        private static readonly FieldInfo WorldCameraSphereRadiusField = AccessTools.Field(typeof(WorldCameraDriver), "SphereRadius");
        private static readonly FieldInfo WorldCameraLayerOriginOffsetField = AccessTools.Field(typeof(WorldCameraDriver), "layerOriginOffset");
        private static readonly FieldInfo WorldCameraCachedCameraField = AccessTools.Field(typeof(WorldCameraDriver), "cachedCamera");
        private static readonly MethodInfo WorldCameraApplyPositionMethod = AccessTools.Method(typeof(WorldCameraDriver), "ApplyPositionToGameObject");
        private static readonly FieldInfo WorldSelectorDragBoxField = AccessTools.Field(typeof(WorldSelector), "dragBox");
        private static readonly FieldInfo WorldDragBoxActiveField = AccessTools.Field(typeof(WorldDragBox), "active");
        private static readonly PropertyInfo WorldRenderedNowProperty = AccessTools.Property(typeof(WorldRendererUtility), "WorldRenderedNow");
        private static readonly MethodInfo WorldRenderedNowGetter = AccessTools.Method(typeof(WorldRendererUtility), "get_WorldRenderedNow");
        private static readonly FieldInfo WorldRenderedNowField = AccessTools.Field(typeof(WorldRendererUtility), "WorldRenderedNow");

        private static TouchMode mode = TouchMode.None;
        private static int primaryFingerId = -1;
        private static Vector2 startGui;
        private static Vector2 lastGui;
        private static Vector3 startMap;
        private static Vector3 panAnchorMap;
        private static Vector3 twoFingerPanAnchorMap;
        private static float startTime;
        private static bool startedOverUi;
        private static bool rightClickDone;
        private static bool suppressReleaseTap;
        private static bool worldPanAnchorValid;
        private static Vector3 worldPanAnchorPoint;
        private static bool worldInertiaAnchorValid;
        private static Vector3 worldInertiaAnchorPoint;
        private static Vector2 worldInertiaGui;

        private static float lastTwoFingerDistance;
        private static Vector2 lastTwoFingerMidpoint;
        private static Vector2 twoFingerStartGuiA;
        private static Vector2 twoFingerStartGuiB;
        private static float twoFingerStartDistance;
        private static Vector2 twoFingerStartMidpoint;
        private static float twoFingerStartTime;
        private static float twoFingerMaxMove;
        private static float twoFingerMaxDistanceChange;
        private static bool twoFingerStartedWithVanillaMapTool;
        private static int lastTouchFrame = -1000;
        private static int lastMultiTouchFrame = -1000;
        private static int suppressUiClickUntilFrame = -1000;
        private static int suppressWindowCloseUntilFrame = -1000;
        private static int suppressVanillaMapInputUntilFrame = -1000;
        private static bool ignoreTouchesUntilAllReleased;
        private static bool hadTouchLastFrame;
        private static bool suppressEdgeScrollUntilMouseMoves;
        private static bool suppressVanillaMapUntilMouseMoves;
        private static Vector3 mousePositionWhenTouchEnded;
        private static Vector3 panVelocity;
        private static Vector2 worldPanVelocity;
        private static Vector2 pendingWorldCameraDolly;
        private static float zoomVelocity;
        private static float lastUpdateTime;
        private static float lastPanSampleTime;
        private static float lastWorldPanSampleTime;
        private static float lastZoomSampleTime;
        private static float panGestureDistance;
        private static readonly Vector3[] PanVelocitySamples = new Vector3[PanVelocitySampleCapacity];
        private static readonly float[] PanVelocitySampleTimes = new float[PanVelocitySampleCapacity];
        private static int panVelocitySampleIndex;
        private static int panVelocitySampleCount;
        private static readonly float[] ZoomVelocitySamples = new float[ZoomVelocitySampleCapacity];
        private static readonly float[] ZoomVelocitySampleTimes = new float[ZoomVelocitySampleCapacity];
        private static int zoomVelocitySampleIndex;
        private static int zoomVelocitySampleCount;
        private static readonly Vector2[] WorldPanVelocitySamples = new Vector2[WorldPanVelocitySampleCapacity];
        private static readonly float[] WorldPanVelocitySampleTimes = new float[WorldPanVelocitySampleCapacity];
        private static int worldPanVelocitySampleIndex;
        private static int worldPanVelocitySampleCount;
        private static readonly Vector2[] MapTargetSearchOffsets = new Vector2[]
        {
            Vector2.zero,
            new Vector2(0f, -TouchMapTargetRadiusPixels),
            new Vector2(TouchMapTargetRadiusPixels, 0f),
            new Vector2(0f, TouchMapTargetRadiusPixels),
            new Vector2(-TouchMapTargetRadiusPixels, 0f),
            new Vector2(TouchMapTargetRadiusPixels, -TouchMapTargetRadiusPixels),
            new Vector2(TouchMapTargetRadiusPixels, TouchMapTargetRadiusPixels),
            new Vector2(-TouchMapTargetRadiusPixels, TouchMapTargetRadiusPixels),
            new Vector2(-TouchMapTargetRadiusPixels, -TouchMapTargetRadiusPixels)
        };

        private static bool TouchModeEnabled
        {
            get
            {
                return RimTouchMod.Settings != null && RimTouchMod.Settings.enableTouchMode;
            }
        }

        public static bool RecentTouchInput
        {
            get
            {
                return TouchModeEnabled
                    && Time.frameCount - lastTouchFrame <= 30;
            }
        }

        public static bool ShouldSuppressUiClickFromGesture
        {
            get
            {
                return TouchModeEnabled
                    && (Input.touchCount >= 2
                        || Time.frameCount <= suppressUiClickUntilFrame
                        || Time.frameCount - lastMultiTouchFrame <= 30);
            }
        }

        public static bool ShouldSuppressWindowCloseFromGesture
        {
            get
            {
                return TouchModeEnabled
                    && (Input.touchCount >= 2
                        || HasCurrentOneFingerLocalMapTouch()
                        || Time.frameCount <= suppressWindowCloseUntilFrame
                        || Time.frameCount - lastMultiTouchFrame <= 30);
            }
        }

        public static bool ShouldSuppressMainTabsFromGesture
        {
            get
            {
                return ShouldSuppressUiClickFromGesture || ShouldSuppressWindowCloseFromGesture;
            }
        }

        public static bool ShouldSuppressUiClickInRect(Rect rect)
        {
            if (!ShouldSuppressUiClickFromGesture)
            {
                return false;
            }

            Event current = Event.current;
            if (current != null && current.isMouse)
            {
                return rect.Contains(current.mousePosition);
            }

            try
            {
                int count = Math.Min(Input.touchCount, 2);
                for (int i = 0; i < count; i++)
                {
                    if (rect.Contains(TouchToGui(Input.GetTouch(i).position)))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public static bool ShouldSuppressAfterMultiTouch
        {
            get
            {
                return TouchModeEnabled
                    && Time.frameCount - lastMultiTouchFrame <= 30;
            }
        }

        public static bool ShouldSuppressEdgeScroll
        {
            get
            {
                return TouchModeEnabled
                    && Input.touchCount == 0
                    && (Time.frameCount - lastTouchFrame <= 90 || suppressEdgeScrollUntilMouseMoves)
                    && !IsVanillaMapToolActive();
            }
        }

        public static bool ShouldSuppressVanillaMapInput
        {
            get
            {
                if (!TouchModeEnabled)
                {
                    return false;
                }

                if (Input.touchCount >= 2)
                {
                    return true;
                }

                if (Time.frameCount <= suppressVanillaMapInputUntilFrame)
                {
                    return true;
                }

                if (ShouldSuppressAfterMultiTouch)
                {
                    return true;
                }

                if (Input.touchCount > 0 && Find.CurrentMap != null && !IsVanillaMapToolActive())
                {
                    Vector2 gui = TouchToGui(Input.GetTouch(0).position);
                    if (!IsLikelyUiPosition(gui))
                    {
                        return true;
                    }
                }

                if (!startedOverUi && Time.frameCount - lastTouchFrame <= 45 && !IsVanillaMapToolActive())
                {
                    return true;
                }

                if (suppressVanillaMapUntilMouseMoves && !IsVanillaMapToolActive())
                {
                    return true;
                }

                return mode != TouchMode.None
                    && mode != TouchMode.VanillaMapTool
                    && (!startedOverUi || mode == TouchMode.TwoFinger);
            }
        }

        public static bool ShouldSuppressVanillaMapToolInput
        {
            get
            {
                if (!TouchModeEnabled || Find.CurrentMap == null)
                {
                    return false;
                }

                if (Input.touchCount >= 2)
                {
                    return true;
                }

                return mode == TouchMode.TwoFinger
                    || ignoreTouchesUntilAllReleased
                    || Time.frameCount - lastMultiTouchFrame <= 30;
            }
        }

        public static bool ShouldSuppressVanillaWorldInput
        {
            get
            {
                if (!TouchModeEnabled || !IsWorldMapActive())
                {
                    return false;
                }

                if (Input.touchCount >= 2)
                {
                    return true;
                }

                if (Input.touchCount == 1)
                {
                    Vector2 gui = TouchToGui(Input.GetTouch(0).position);
                    return !IsLikelyUiPosition(gui);
                }

                return Time.frameCount - lastTouchFrame <= 45 || suppressVanillaMapUntilMouseMoves;
            }
        }

        public static bool ShouldSuppressVanillaDragBoxDrawing
        {
            get
            {
                return ShouldSuppressVanillaMapInput && mode != TouchMode.SelectionHold;
            }
        }

        public static bool ShouldSuppressVanillaWorldDragBoxDrawing
        {
            get
            {
                return ShouldSuppressVanillaWorldInput;
            }
        }

        public static bool ShouldSuppressWorldEdgeScroll
        {
            get
            {
                return TouchModeEnabled
                    && IsWorldMapActive()
                    && (Input.touchCount > 0 || Time.frameCount - lastTouchFrame <= 90 || suppressEdgeScrollUntilMouseMoves);
            }
        }

        private enum TouchMode
        {
            None,
            OneFinger,
            MapPan,
            SelectionHold,
            TwoFinger,
            VanillaMapTool
        }

        public static void Update()
        {
            try
            {
                if (!TouchModeEnabled)
                {
                    ResetState();
                    return;
                }

                int touchCount = Input.touchCount;
                if (touchCount <= 0)
                {
                    bool finalizedTouch = false;
                    if (hadTouchLastFrame)
                    {
                        FinalizeTouchDisappearance();
                        finalizedTouch = true;
                        suppressEdgeScrollUntilMouseMoves = true;
                        if (!startedOverUi && mode != TouchMode.VanillaMapTool)
                        {
                            suppressVanillaMapUntilMouseMoves = true;
                        }
                        mousePositionWhenTouchEnded = Input.mousePosition;
                    }
                    hadTouchLastFrame = false;
                    UpdateEdgeScrollSuppression();
                    ignoreTouchesUntilAllReleased = false;
                    if (mode != TouchMode.None)
                    {
                        if (!finalizedTouch)
                        {
                            FinalizeInertiaFromGesture();
                        }
                        ResetState();
                    }
                    if (ShouldSuppressVanillaMapInput)
                    {
                        CancelRimWorldDragBox();
                    }
                    if (ShouldSuppressVanillaWorldInput)
                    {
                        CancelWorldDragBox();
                    }
                    UpdateInertia();
                    return;
                }

                if (!hadTouchLastFrame)
                {
                    StopInertia();
                    ClearInertiaSamples();
                }

                hadTouchLastFrame = true;
                suppressEdgeScrollUntilMouseMoves = true;
                mousePositionWhenTouchEnded = Input.mousePosition;
                lastTouchFrame = Time.frameCount;
                lastUpdateTime = Time.realtimeSinceStartup;

                if (touchCount >= 2)
                {
                    lastMultiTouchFrame = Time.frameCount;
                    ignoreTouchesUntilAllReleased = true;
                    UpdateTwoFinger(Input.GetTouch(0), Input.GetTouch(1));
                    return;
                }

                if (ignoreTouchesUntilAllReleased)
                {
                    CancelRimWorldDragBox();
                    return;
                }

                Touch touch = Input.GetTouch(0);
                UpdateOneFinger(touch);
            }
            catch (Exception ex)
            {
                Log.Warning("[RimTouch] Touch update failed: " + ex);
                ResetState();
            }
        }

        public static void OnGUI()
        {
            EnsureRimWorldDragBoxState();
        }

        private static void UpdateOneFinger(Touch touch)
        {
            Vector2 gui = TouchToGui(touch.position);

            if (touch.phase == TouchPhase.Began || mode == TouchMode.None || primaryFingerId != touch.fingerId)
            {
                primaryFingerId = touch.fingerId;
                startGui = gui;
                lastGui = gui;
                startMap = IsWorldMapActive() ? Vector3.zero : (Find.CurrentMap != null ? GuiToMapPosition(gui) : Vector3.zero);
                startTime = Time.realtimeSinceStartup;
                lastWorldPanSampleTime = startTime;
                startedOverUi = IsLikelyUiPosition(gui);
                rightClickDone = false;
                suppressReleaseTap = false;
                panGestureDistance = 0f;
                ClearInertiaSamples();
                if (!startedOverUi)
                {
                    SuppressUiClickFallout();
                }
                if (!startedOverUi && IsVanillaMapToolActive())
                {
                    mode = TouchMode.VanillaMapTool;
                    return;
                }
                if (!startedOverUi && Find.CurrentMap != null && !IsWorldMapActive())
                {
                    SuppressVanillaMapInputFallout();
                    CancelRimWorldDragBox();
                }
                mode = TouchMode.OneFinger;
                return;
            }

            float elapsed = Time.realtimeSinceStartup - startTime;
            float moveDistance = Vector2.Distance(gui, startGui);

            if (mode == TouchMode.VanillaMapTool)
            {
                lastGui = gui;
                if (IsEnded(touch))
                {
                    FinishTouch();
                }
                return;
            }

            if (startedOverUi)
            {
                if (IsEnded(touch) && moveDistance <= DragSlopPixels && !suppressReleaseTap)
                {
                    TouchTapRepair.QueueTap(gui);
                }
                lastGui = gui;
                if (IsEnded(touch))
                {
                    FinishTouch();
                }
                return;
            }

            if (IsWorldMapActive())
            {
                UpdateOneFingerWorld(touch, gui);
                return;
            }

            if (mode == TouchMode.OneFinger)
            {
                SuppressVanillaMapInputFallout();
                CancelRimWorldDragBox();
                if (moveDistance > DragSlopPixels && elapsed < SelectHoldSeconds)
                {
                    mode = TouchMode.MapPan;
                    panAnchorMap = startMap;
                    panGestureDistance = moveDistance;
                    lastPanSampleTime = startTime;
                    suppressReleaseTap = true;
                    SuppressUiClickFallout();
                    CancelRimWorldDragBox();
                }
                else if (elapsed >= RightClickHoldSeconds && moveDistance <= RightClickHoldSlopPixels && !rightClickDone)
                {
                    DoMapRightClick(gui);
                    rightClickDone = true;
                    suppressReleaseTap = true;
                    CancelRimWorldDragBox();
                }
                else if (elapsed >= SelectHoldSeconds && moveDistance > RightClickHoldSlopPixels)
                {
                    mode = TouchMode.SelectionHold;
                    suppressReleaseTap = true;
                    BeginRimWorldDragBox();
                }
            }

            if (mode == TouchMode.MapPan)
            {
                SuppressUiClickFallout();
                SuppressVanillaMapInputFallout();
                panGestureDistance = Mathf.Max(panGestureDistance, moveDistance);
                Vector3 appliedDelta = KeepMapPointUnderGui(panAnchorMap, gui);
                if (moveDistance > DragSlopPixels)
                {
                    SamplePanVelocityFromMapDelta(appliedDelta);
                }
                CancelRimWorldDragBox();
            }

            if (IsEnded(touch))
            {
                if (mode == TouchMode.OneFinger && moveDistance <= DragSlopPixels && !rightClickDone)
                {
                    if (!TryDoDraftedPawnTapCommand(gui))
                    {
                        DoMapLeftClick(gui);
                    }
                }
                else if (mode == TouchMode.SelectionHold)
                {
                    FinishRimWorldDragBox();
                }
                FinalizeInertiaFromGesture();
                FinishTouch();
                return;
            }

            lastGui = gui;
        }

        private static void UpdateOneFingerWorld(Touch touch, Vector2 gui)
        {
            float elapsed = Time.realtimeSinceStartup - startTime;
            float moveDistance = Vector2.Distance(gui, startGui);

            if (mode == TouchMode.OneFinger)
            {
                if (moveDistance > DragSlopPixels && elapsed < SelectHoldSeconds)
                {
                    mode = TouchMode.MapPan;
                    panGestureDistance = moveDistance;
                    lastWorldPanSampleTime = startTime;
                    worldPanAnchorValid = TryGetWorldSpherePointAtGui(startGui, out worldPanAnchorPoint);
                    suppressReleaseTap = true;
                    SuppressUiClickFallout();
                    CancelWorldDragBox();
                }
            }

            if (mode == TouchMode.MapPan)
            {
                SuppressUiClickFallout();
                panGestureDistance = Mathf.Max(panGestureDistance, moveDistance);
                Vector2 panSample = PanWorldByGuiDelta(lastGui, gui);
                SampleWorldPanVelocity(panSample);
                CancelWorldDragBox();
            }

            if (IsEnded(touch))
            {
                if (mode == TouchMode.OneFinger && moveDistance <= DragSlopPixels && !rightClickDone)
                {
                    DoWorldLeftClick(gui);
                }
                FinalizeInertiaFromGesture();
                FinishTouch();
                return;
            }

            lastGui = gui;
        }

        private static void UpdateTwoFinger(Touch a, Touch b)
        {
            Vector2 guiA = TouchToGui(a.position);
            Vector2 guiB = TouchToGui(b.position);
            Vector2 mid = (guiA + guiB) * 0.5f;
            float distance = Vector2.Distance(guiA, guiB);

            if (mode != TouchMode.TwoFinger)
            {
                mode = TouchMode.TwoFinger;
                suppressReleaseTap = true;
                SuppressUiClickFallout();
                lastTwoFingerMidpoint = mid;
                lastTwoFingerDistance = distance;
                twoFingerStartGuiA = guiA;
                twoFingerStartGuiB = guiB;
                twoFingerStartMidpoint = mid;
                twoFingerStartDistance = distance;
                twoFingerStartTime = Time.realtimeSinceStartup;
                twoFingerMaxMove = 0f;
                twoFingerMaxDistanceChange = 0f;
                twoFingerStartedWithVanillaMapTool = Find.CurrentMap != null && !IsWorldMapActive() && IsVanillaMapToolActive();
                twoFingerPanAnchorMap = IsWorldMapActive() ? Vector3.zero : (Find.CurrentMap != null ? GuiToMapPosition(mid) : Vector3.zero);
                lastPanSampleTime = Time.realtimeSinceStartup;
                lastWorldPanSampleTime = lastPanSampleTime;
                worldPanAnchorValid = IsWorldMapActive() && TryGetWorldSpherePointAtGui(mid, out worldPanAnchorPoint);
                lastZoomSampleTime = lastPanSampleTime;
                panGestureDistance = 0f;
                ClearInertiaSamples();
                TouchTapRepair.Clear();
                CancelRimWorldDragBox();
                return;
            }

            bool ended = IsEnded(a) || IsEnded(b);
            twoFingerMaxMove = Mathf.Max(twoFingerMaxMove, Vector2.Distance(mid, twoFingerStartMidpoint));
            twoFingerMaxDistanceChange = Mathf.Max(twoFingerMaxDistanceChange, Mathf.Abs(distance - twoFingerStartDistance));

            if (!ended && IsWorldMapActive())
            {
                SuppressUiClickFallout();
                panGestureDistance += Vector2.Distance(mid, lastTwoFingerMidpoint);
                Vector2 panSample = PanWorldByGuiDelta(lastTwoFingerMidpoint, mid);
                SampleWorldPanVelocity(panSample);
                ApplyWorldPinchZoom(lastTwoFingerDistance, distance);
                CancelWorldDragBox();
            }
            else if (!ended && Find.CurrentMap != null)
            {
                SuppressUiClickFallout();
                Vector3 appliedDelta = KeepMapPointUnderGui(twoFingerPanAnchorMap, mid);
                panGestureDistance += Vector2.Distance(mid, lastTwoFingerMidpoint);
                SamplePanVelocityFromMapDelta(appliedDelta);
                ApplyPinchZoom(lastTwoFingerDistance, distance, mid);
                CancelRimWorldDragBox();
            }

            lastTwoFingerMidpoint = mid;
            lastTwoFingerDistance = distance;

            if (ended)
            {
                suppressReleaseTap = true;
                if (TryCancelVanillaMapToolFromTwoFingerTap() || TrySelectFromTwoFingerDraftedTap())
                {
                    StopInertia();
                }
                else
                {
                    FinalizeInertiaFromGesture();
                }
                FinishTouch();
            }
        }

        private static void FinalizeTouchDisappearance()
        {
            if (mode == TouchMode.OneFinger)
            {
                if (startedOverUi)
                {
                    float moveDistance = Vector2.Distance(lastGui, startGui);
                    if (moveDistance <= DragSlopPixels && !suppressReleaseTap)
                    {
                        TouchTapRepair.QueueTap(lastGui);
                    }
                }
                else
                {
                    float moveDistance = Vector2.Distance(lastGui, startGui);
                    if (moveDistance <= DragSlopPixels && !rightClickDone)
                    {
                        if (IsWorldMapActive())
                        {
                            DoWorldLeftClick(lastGui);
                        }
                        else if (!TryDoDraftedPawnTapCommand(lastGui))
                        {
                            DoMapLeftClick(lastGui);
                        }
                    }
                }
            }
            else if (mode == TouchMode.SelectionHold)
            {
                FinishRimWorldDragBox();
            }
            else if (mode == TouchMode.TwoFinger && TryCancelVanillaMapToolFromTwoFingerTap())
            {
                StopInertia();
                return;
            }
            else if (mode == TouchMode.TwoFinger && TrySelectFromTwoFingerDraftedTap())
            {
                StopInertia();
                return;
            }

            FinalizeInertiaFromGesture();
        }

        private static Vector3 KeepMapPointUnderGui(Vector3 mapPoint, Vector2 gui)
        {
            CameraDriver cameraDriver = Find.CameraDriver;
            if (cameraDriver == null || Find.CurrentMap == null)
            {
                return Vector3.zero;
            }

            float size = GetCameraRootSize(cameraDriver);
            Vector3 currentMap = GuiToMapPosition(gui);
            Vector3 delta = mapPoint - currentMap;
            cameraDriver.SetRootPosAndSize(GetCameraRootPos(cameraDriver) + delta, size);
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

            float currentSize = GetCameraRootSize(cameraDriver);
            float zoomRatio = previousDistance / currentDistance;
            float targetSize = Mathf.Clamp(currentSize * zoomRatio, GetMinZoomSize(cameraDriver), GetMaxZoomSize(cameraDriver));
            Vector3 before = GuiToMapPosition(midpointGui);
            cameraDriver.SetRootPosAndSize(GetCameraRootPos(cameraDriver), targetSize);
            Vector3 after = GuiToMapPosition(midpointGui);
            cameraDriver.SetRootPosAndSize(GetCameraRootPos(cameraDriver) + (before - after), targetSize);
            SampleZoomVelocity(targetSize - currentSize);
        }

        private static void ApplyWorldPinchZoom(float previousDistance, float currentDistance)
        {
            if (previousDistance <= 1f || currentDistance <= 1f || WorldCameraAltitudeField == null || WorldCameraDesiredAltitudeField == null)
            {
                return;
            }

            WorldCameraDriver driver = Find.WorldCameraDriver;
            if (driver == null)
            {
                return;
            }

            float currentAltitude = (float)WorldCameraAltitudeField.GetValue(driver);
            float zoomRatio = previousDistance / currentDistance;
            float maxAltitude = WorldCameraMaxAltitudeField != null ? (float)WorldCameraMaxAltitudeField.GetValue(driver) : 75f;
            float targetAltitude = Mathf.Clamp(currentAltitude * zoomRatio, WorldCameraDriver.MinAltitude, maxAltitude);
            WorldCameraAltitudeField.SetValue(driver, targetAltitude);
            WorldCameraDesiredAltitudeField.SetValue(driver, targetAltitude);
            SampleZoomVelocity(targetAltitude - currentAltitude);
            if (WorldCameraMouseBottomEdgeStartField != null)
            {
                WorldCameraMouseBottomEdgeStartField.SetValue(driver, 0f);
            }
        }

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

        private static void SampleZoomVelocity(float zoomDelta)
        {
            float now = Time.realtimeSinceStartup;
            float dt = now - lastZoomSampleTime;
            lastZoomSampleTime = now;

            if (dt <= 0.0001f || dt > 0.18f)
            {
                return;
            }

            float velocity = zoomDelta / dt;
            if (float.IsNaN(velocity) || float.IsInfinity(velocity))
            {
                return;
            }

            RecordZoomVelocitySample(velocity, now);
            zoomVelocity = GetRecentZoomVelocity(now);
        }

        private static void UpdateInertia()
        {
            if (Current.ProgramState != ProgramState.Playing)
            {
                StopInertia();
                return;
            }

            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Clamp(now - lastUpdateTime, 0.001f, 0.05f);
            lastUpdateTime = now;

            if (IsWorldMapActive())
            {
                UpdateWorldInertia(dt);
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
            Vector3 rootPos = GetCameraRootPos(cameraDriver);
            float size = GetCameraRootSize(cameraDriver);
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
                size = Mathf.Clamp(size + zoomVelocity * ZoomInertiaStrength * dt, GetMinZoomSize(cameraDriver), GetMaxZoomSize(cameraDriver));
                zoomVelocity *= Mathf.Exp(-ZoomInertiaDamping * dt);
                changed = true;
            }
            else
            {
                zoomVelocity = 0f;
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

            if (Mathf.Abs(zoomVelocity) > MinZoomInertiaSpeed && ZoomInertiaStrength > 0f && WorldCameraAltitudeField != null && WorldCameraDesiredAltitudeField != null)
            {
                float currentAltitude = (float)WorldCameraAltitudeField.GetValue(driver);
                float maxAltitude = WorldCameraMaxAltitudeField != null ? (float)WorldCameraMaxAltitudeField.GetValue(driver) : 75f;
                float targetAltitude = Mathf.Clamp(currentAltitude + zoomVelocity * ZoomInertiaStrength * dt, WorldCameraDriver.MinAltitude, maxAltitude);
                WorldCameraAltitudeField.SetValue(driver, targetAltitude);
                WorldCameraDesiredAltitudeField.SetValue(driver, targetAltitude);
                zoomVelocity *= Mathf.Exp(-ZoomInertiaDamping * dt);
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

        public static void SuppressCameraEdgeScrollState(CameraDriver cameraDriver)
        {
            if (cameraDriver == null)
            {
                return;
            }

            CameraDesiredDollyField.SetValue(cameraDriver, Vector2.zero);
            CameraDesiredDollyRawField.SetValue(cameraDriver, Vector2.zero);
            CameraVelocityField.SetValue(cameraDriver, Vector3.zero);
            CameraMouseBottomEdgeStartField.SetValue(cameraDriver, 0f);
        }

        private static void UpdateEdgeScrollSuppression()
        {
            if (!suppressEdgeScrollUntilMouseMoves)
            {
                return;
            }

            if ((Input.mousePosition - mousePositionWhenTouchEnded).sqrMagnitude > 16f || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                suppressEdgeScrollUntilMouseMoves = false;
                suppressVanillaMapUntilMouseMoves = false;
            }
        }

        private static void DoMapLeftClick(Vector2 gui)
        {
            Map map = Find.CurrentMap;
            if (map == null || Find.Selector == null)
            {
                return;
            }

            IntVec3 cell = GuiToMapPosition(gui).ToIntVec3();
            if (!cell.InBounds(map))
            {
                return;
            }

            int clickCount = TouchTapRepair.RegisterTap(gui);
            if (clickCount >= 2 && TryDoMapDoubleClick(gui, map))
            {
                return;
            }

            object selectable = FindSelectableObjectNearGui(gui, map);
            if (selectable != null)
            {
                Find.Selector.ClearSelection();
                Find.Selector.Select(selectable, true, true);
            }
            else
            {
                Find.Selector.ClearSelection();
            }
        }

        private static bool TryDoMapDoubleClick(Vector2 gui, Map map)
        {
            if (Find.Selector == null || Find.CameraDriver == null || map == null)
            {
                return false;
            }

            Thing target = FindSelectableThingNearGui(gui, map);
            if (target == null)
            {
                return false;
            }

            CellRect viewRect = Find.CameraDriver.CurrentViewRect;
            List<Thing> allThings = map.listerThings != null ? map.listerThings.AllThings : null;
            if (allThings == null)
            {
                return false;
            }

            List<Thing> matches = new List<Thing>();
            for (int i = 0; i < allThings.Count; i++)
            {
                Thing thing = allThings[i];
                if (thing == null || !thing.Spawned || thing.Map != map || !viewRect.Contains(thing.Position))
                {
                    continue;
                }
                if (IsSameDoubleClickSelectionKind(target, thing))
                {
                    matches.Add(thing);
                }
            }

            if (matches.Count <= 0)
            {
                return false;
            }

            Find.Selector.ClearSelection();
            for (int i = 0; i < matches.Count; i++)
            {
                Find.Selector.Select(matches[i], i == 0, true);
            }
            return true;
        }

        private static object FindSelectableObjectNearGui(Vector2 gui, Map map)
        {
            if (map == null)
            {
                return null;
            }

            object best = null;
            float bestScore = float.MaxValue;
            HashSet<object> seen = new HashSet<object>();
            for (int i = 0; i < MapTargetSearchOffsets.Length; i++)
            {
                Vector2 sampleGui = gui + MapTargetSearchOffsets[i];
                IntVec3 cell = GuiToMapPosition(sampleGui).ToIntVec3();
                if (!cell.InBounds(map))
                {
                    continue;
                }

                IEnumerator<object> enumerator = Selector.SelectableObjectsAt(cell, map).GetEnumerator();
                try
                {
                    while (enumerator.MoveNext())
                    {
                        object candidate = enumerator.Current;
                        if (candidate == null || !seen.Add(candidate))
                        {
                            continue;
                        }

                        float score = GetSelectableGuiDistanceSquared(candidate, gui)
                            + MapTargetSearchOffsets[i].sqrMagnitude * 0.05f;
                        if (score < bestScore)
                        {
                            best = candidate;
                            bestScore = score;
                        }
                    }
                }
                finally
                {
                    enumerator.Dispose();
                }
            }

            return best;
        }

        private static Thing FindSelectableThingNearGui(Vector2 gui, Map map)
        {
            return FindSelectableObjectNearGui(gui, map) as Thing;
        }

        private static float GetSelectableGuiDistanceSquared(object selectable, Vector2 gui)
        {
            Thing thing = selectable as Thing;
            if (thing == null)
            {
                return 0f;
            }

            try
            {
                Vector2 ui = UI.MapToUIPosition(thing.DrawPos);
                Vector2 thingGui = new Vector2(ui.x, UI.screenHeight - ui.y);
                return (thingGui - gui).sqrMagnitude;
            }
            catch
            {
                return 0f;
            }
        }

        private static bool IsSameDoubleClickSelectionKind(Thing target, Thing candidate)
        {
            if (target == null || candidate == null || target.def != candidate.def)
            {
                return false;
            }

            Pawn targetPawn = target as Pawn;
            Pawn candidatePawn = candidate as Pawn;
            if (targetPawn != null || candidatePawn != null)
            {
                return targetPawn != null
                    && candidatePawn != null
                    && targetPawn.Faction == candidatePawn.Faction;
            }

            return true;
        }

        private static bool IsWorldMapActive()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.WorldCameraDriver == null)
            {
                return false;
            }

            try
            {
                if (Find.WorldSelector == null)
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            bool renderedNow;
            if (TryGetWorldRenderedNow(out renderedNow))
            {
                return renderedNow;
            }

            if (Find.World != null && Find.World.renderer != null)
            {
                object renderer = Find.World.renderer;
                object wantedMode = GetMemberValue(renderer, "wantedMode");
                if (wantedMode != null && wantedMode.ToString() != "None")
                {
                    return true;
                }
            }

            return Find.CurrentMap == null;
        }

        private static bool TryGetWorldRenderedNow(out bool renderedNow)
        {
            renderedNow = false;

            try
            {
                object value = null;

                if (WorldRenderedNowProperty != null)
                {
                    value = WorldRenderedNowProperty.GetValue(null, null);
                }
                else if (WorldRenderedNowGetter != null)
                {
                    value = WorldRenderedNowGetter.Invoke(null, null);
                }
                else if (WorldRenderedNowField != null)
                {
                    value = WorldRenderedNowField.GetValue(null);
                }

                if (value is bool)
                {
                    renderedNow = (bool)value;
                    return true;
                }
            }
            catch
            {
                renderedNow = false;
            }

            return false;
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null)
            {
                return null;
            }

            Type type = instance.GetType();
            FieldInfo field = AccessTools.Field(type, memberName);
            if (field != null)
            {
                return field.GetValue(instance);
            }

            PropertyInfo property = AccessTools.Property(type, memberName);
            if (property != null)
            {
                return property.GetValue(instance, null);
            }

            return null;
        }

        private static Vector2 PanWorldByGuiDelta(Vector2 previousGui, Vector2 currentGui)
        {
            WorldCameraDriver driver = Find.WorldCameraDriver;
            if (driver == null || WorldCameraDesiredRotationRawField == null)
            {
                return Vector2.zero;
            }

            Vector2 guiDelta = currentGui - previousGui;
            if (worldPanAnchorValid)
            {
                if (KeepWorldPointUnderGui(driver, worldPanAnchorPoint, currentGui))
                {
                    worldInertiaAnchorValid = true;
                    worldInertiaAnchorPoint = worldPanAnchorPoint;
                    worldInertiaGui = currentGui;
                }
                else
                {
                    worldInertiaAnchorValid = false;
                }
                return guiDelta;
            }

            Vector2 rotationDelta = GetWorldRotationDeltaFromGuiDelta(driver, previousGui, currentGui);
            ApplyWorldCameraDolly(driver, rotationDelta);
            worldInertiaAnchorValid = false;
            return guiDelta;
        }

        private static Vector2 GetWorldRotationDeltaFromGuiDelta(WorldCameraDriver driver, Vector2 previousGui, Vector2 currentGui)
        {
            Vector2 delta = currentGui - previousGui;
            float altitudeFactor = Mathf.Lerp(0.4f, 1.4f, Mathf.Clamp01(driver.AltitudePercent));
            return new Vector2(delta.x, -delta.y) * WorldPanPixelsToRotation * altitudeFactor;
        }

        private static bool KeepWorldPointUnderGui(WorldCameraDriver driver, Vector3 anchorPoint, Vector2 gui)
        {
            if (driver == null || WorldCameraSphereRotationField == null)
            {
                return false;
            }

            Vector3 currentPoint;
            if (!TryGetWorldSpherePointAtGui(gui, out currentPoint))
            {
                return false;
            }

            Vector3 center = GetWorldSphereCenter(driver);
            Vector3 anchorDir = anchorPoint - center;
            Vector3 currentDir = currentPoint - center;
            if (anchorDir.sqrMagnitude <= 0.0001f || currentDir.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Vector3 anchorNormal = anchorDir.normalized;
            Vector3 currentNormal = currentDir.normalized;
            float poleFactor = Mathf.Max(Mathf.Abs(anchorNormal.y), Mathf.Abs(currentNormal.y), GetWorldPoleFactor(driver));

            Quaternion sphereRotation = (Quaternion)WorldCameraSphereRotationField.GetValue(driver);
            Quaternion deltaRotation = Quaternion.FromToRotation(anchorNormal, currentNormal);
            float rotationAngle = Quaternion.Angle(Quaternion.identity, deltaRotation);
            if (float.IsNaN(rotationAngle) || float.IsInfinity(rotationAngle))
            {
                return false;
            }
            if (rotationAngle > MaxWorldAnchorRotationDegrees)
            {
                deltaRotation = Quaternion.Slerp(Quaternion.identity, deltaRotation, MaxWorldAnchorRotationDegrees / rotationAngle);
            }

            float poleDamping = Mathf.InverseLerp(WorldAnchorPoleDampingStart, 1f, poleFactor);
            if (poleDamping > 0f)
            {
                deltaRotation = Quaternion.Slerp(Quaternion.identity, deltaRotation, Mathf.Lerp(1f, WorldAnchorPoleMinimumScale, poleDamping));
            }

            WorldCameraSphereRotationField.SetValue(driver, sphereRotation * deltaRotation);
            SuppressWorldCameraEdgeScrollState(driver);
            if (WorldCameraApplyPositionMethod != null)
            {
                WorldCameraApplyPositionMethod.Invoke(driver, null);
            }
            return true;
        }

        private static bool TryGetWorldSpherePointAtGui(Vector2 gui, out Vector3 point)
        {
            point = Vector3.zero;
            WorldCameraDriver driver = Find.WorldCameraDriver;
            Camera camera = GetWorldCamera(driver);
            if (driver == null || camera == null)
            {
                return false;
            }

            Vector3 screenPoint = GuiToScreenPoint(gui);
            Ray ray = camera.ScreenPointToRay(screenPoint);
            Vector3 center = GetWorldSphereCenter(driver);
            float radius = GetWorldSphereRadius(driver);
            Vector3 originToCenter = ray.origin - center;
            float b = Vector3.Dot(originToCenter, ray.direction);
            float c = originToCenter.sqrMagnitude - radius * radius;
            float discriminant = b * b - c;
            if (discriminant < 0f)
            {
                return false;
            }

            float root = Mathf.Sqrt(discriminant);
            float distance = -b - root;
            if (distance < 0f)
            {
                distance = -b + root;
            }
            if (distance < 0f)
            {
                return false;
            }

            point = ray.origin + ray.direction * distance;
            return IsFinite(point);
        }

        private static Vector3 GetWorldSphereCenter(WorldCameraDriver driver)
        {
            if (driver != null && WorldCameraLayerOriginOffsetField != null)
            {
                object value = WorldCameraLayerOriginOffsetField.GetValue(driver);
                if (value is Vector3)
                {
                    return (Vector3)value;
                }
            }
            return Vector3.zero;
        }

        private static float GetWorldSphereRadius(WorldCameraDriver driver)
        {
            if (WorldCameraSphereRadiusField != null)
            {
                object value = WorldCameraSphereRadiusField.GetValue(WorldCameraSphereRadiusField.IsStatic ? null : driver);
                if (value is float && (float)value > 0f)
                {
                    return (float)value;
                }
            }
            return 100f;
        }

        private static float GetWorldPoleFactor(WorldCameraDriver driver)
        {
            if (driver == null || WorldCameraSphereRotationField == null)
            {
                return 0f;
            }

            Quaternion sphereRotation = (Quaternion)WorldCameraSphereRotationField.GetValue(driver);
            Vector3 lookPoint = -(Quaternion.Inverse(sphereRotation) * Vector3.forward);
            if (!IsFinite(lookPoint) || lookPoint.sqrMagnitude <= 0.0001f)
            {
                return 0f;
            }
            return Mathf.Abs(lookPoint.normalized.y);
        }

        private static bool ShouldDisableWorldPanInertia(WorldCameraDriver driver)
        {
            return driver == null || !worldInertiaAnchorValid || GetWorldPoleFactor(driver) >= WorldPoleInertiaDisable;
        }

        private static float GetWorldCloseZoomFactor(WorldCameraDriver driver)
        {
            if (driver == null)
            {
                return 0f;
            }
            return 1f - Mathf.Clamp01(driver.AltitudePercent);
        }

        private static float GetWorldPanInertiaStartSpeed(WorldCameraDriver driver)
        {
            return Mathf.Lerp(MinWorldPanInertiaStartSpeed, WorldCloseZoomInertiaStartSpeed, GetWorldCloseZoomFactor(driver));
        }

        private static float GetWorldPanInertiaStopSpeed(WorldCameraDriver driver)
        {
            return Mathf.Lerp(MinWorldPanInertiaSpeed, WorldCloseZoomInertiaStopSpeed, GetWorldCloseZoomFactor(driver));
        }

        private static float GetWorldPanInertiaDamping(WorldCameraDriver driver)
        {
            return PanInertiaDamping * Mathf.Lerp(1f, WorldCloseZoomInertiaDampingScale, GetWorldCloseZoomFactor(driver));
        }

        private static float GetWorldPanInertiaMoveScale(WorldCameraDriver driver)
        {
            return Mathf.Lerp(1f, WorldCloseZoomInertiaMoveScale, GetWorldCloseZoomFactor(driver));
        }

        private static Camera GetWorldCamera(WorldCameraDriver driver)
        {
            if (driver == null || WorldCameraCachedCameraField == null)
            {
                return null;
            }
            return WorldCameraCachedCameraField.GetValue(driver) as Camera;
        }

        private static void ApplyWorldCameraDolly(WorldCameraDriver driver, Vector2 dolly)
        {
            if (float.IsNaN(dolly.x) || float.IsInfinity(dolly.x) || float.IsNaN(dolly.y) || float.IsInfinity(dolly.y))
            {
                return;
            }

            if (driver == null || WorldCameraDesiredRotationRawField == null)
            {
                return;
            }

            Vector2 current = (Vector2)WorldCameraDesiredRotationRawField.GetValue(driver);
            if (!IsFinite(current))
            {
                current = Vector2.zero;
            }
            WorldCameraDesiredRotationRawField.SetValue(driver, current + dolly);
        }

        public static Vector2 ModifyWorldCameraInput(WorldCameraDriver driver, Vector2 vanillaInput)
        {
            if (driver == null || !TouchModeEnabled || !IsWorldMapActive())
            {
                pendingWorldCameraDolly = Vector2.zero;
                return vanillaInput;
            }

            Vector2 touchInput = pendingWorldCameraDolly;
            pendingWorldCameraDolly = Vector2.zero;

            if (touchInput.sqrMagnitude > 0.000001f || ShouldSuppressWorldEdgeScroll)
            {
                SuppressWorldCameraEdgeScrollState(driver);
                return touchInput;
            }

            return vanillaInput;
        }

        public static void SuppressWorldCameraEdgeScrollState(WorldCameraDriver driver)
        {
            if (driver == null)
            {
                return;
            }

            if (WorldCameraRotationVelocityField != null)
            {
                WorldCameraRotationVelocityField.SetValue(driver, Vector2.zero);
            }
            if (WorldCameraMouseBottomEdgeStartField != null)
            {
                WorldCameraMouseBottomEdgeStartField.SetValue(driver, 0f);
            }
        }

        private static void DoWorldLeftClick(Vector2 gui)
        {
            WorldSelector selector = Find.WorldSelector;
            if (selector == null)
            {
                return;
            }

            bool clickedDirectlyOnCaravan;
            bool usedColonistBar;
            IEnumerator<WorldObject> enumerator = selector.SelectableObjectsUnderMouse(out clickedDirectlyOnCaravan, out usedColonistBar).GetEnumerator();
            try
            {
                if (enumerator.MoveNext())
                {
                    selector.ClearSelection();
                    selector.Select(enumerator.Current, true);
                    return;
                }
            }
            finally
            {
                enumerator.Dispose();
            }

            selector.ClearSelection();
        }

        private static void DoMapRightClick(Vector2 gui)
        {
            Map map = Find.CurrentMap;
            if (map == null || Find.Selector == null || Find.Selector.SelectedPawns == null || Find.Selector.SelectedPawns.Count == 0)
            {
                return;
            }

            Vector3 clickPosition = GuiToMapPosition(gui);
            FloatMenuContext context;
            List<FloatMenuOption> options = FloatMenuMakerMap.GetOptions(Find.Selector.SelectedPawns, clickPosition, out context);
            if (options != null && options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private static bool TryDoDraftedPawnTapCommand(Vector2 gui)
        {
            if (!HasSelectedDraftedPawn())
            {
                return false;
            }

            Vector3 clickPosition = GuiToMapPosition(gui);
            FloatMenuContext context;
            List<FloatMenuOption> options = FloatMenuMakerMap.GetOptions(Find.Selector.SelectedPawns, clickPosition, out context);
            if (options == null || options.Count == 0)
            {
                return false;
            }

            FloatMenuOption autoTake = null;
            if (FloatMenuMakerMapGetAutoTakeOptionMethod != null)
            {
                autoTake = (FloatMenuOption)FloatMenuMakerMapGetAutoTakeOptionMethod.Invoke(null, new object[] { options });
            }

            if (autoTake == null || autoTake.Disabled || FloatMenuOptionActionField == null)
            {
                Find.WindowStack.Add(new FloatMenu(options));
                return true;
            }

            Action action = (Action)FloatMenuOptionActionField.GetValue(autoTake);
            if (action == null)
            {
                Find.WindowStack.Add(new FloatMenu(options));
                return true;
            }

            action();
            return true;
        }

        private static bool HasSelectedDraftedPawn()
        {
            if (Find.Selector == null || Find.Selector.SelectedPawns == null)
            {
                return false;
            }

            foreach (Pawn pawn in Find.Selector.SelectedPawns)
            {
                if (pawn != null && pawn.Drafted)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TrySelectFromTwoFingerDraftedTap()
        {
            if (!IsTwoFingerTapGesture() || Find.CurrentMap == null || IsWorldMapActive() || !HasSelectedDraftedPawn())
            {
                return false;
            }

            if (IsLikelyUiPosition(twoFingerStartGuiA) || IsLikelyUiPosition(twoFingerStartGuiB))
            {
                return false;
            }

            object selectable = FindSelectableObjectNearGui(twoFingerStartGuiA, Find.CurrentMap);
            if (selectable == null)
            {
                selectable = FindSelectableObjectNearGui(twoFingerStartGuiB, Find.CurrentMap);
            }
            if (selectable == null)
            {
                selectable = FindSelectableObjectNearGui(twoFingerStartMidpoint, Find.CurrentMap);
            }

            Find.Selector.ClearSelection();
            if (selectable != null)
            {
                Find.Selector.Select(selectable, true, true);
            }

            suppressReleaseTap = true;
            SuppressUiClickFallout();
            SuppressVanillaMapInputFallout();
            TouchTapRepair.Clear();
            CancelRimWorldDragBox();
            return true;
        }

        private static bool IsTwoFingerTapGesture()
        {
            float elapsed = Time.realtimeSinceStartup - twoFingerStartTime;
            return elapsed <= TwoFingerCancelTapSeconds
                && twoFingerMaxMove <= TwoFingerCancelMoveSlopPixels
                && twoFingerMaxDistanceChange <= TwoFingerCancelDistanceSlopPixels;
        }

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

        private static Vector2 TouchToGui(Vector2 touchPosition)
        {
            float scaleX = Screen.width > 0 ? UI.screenWidth / (float)Screen.width : 1f;
            float scaleY = Screen.height > 0 ? UI.screenHeight / (float)Screen.height : 1f;
            return new Vector2(touchPosition.x * scaleX, (Screen.height - touchPosition.y) * scaleY);
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

        private static void BeginRimWorldDragBox()
        {
            DragBox dragBox = GetDragBox();
            if (dragBox == null)
            {
                return;
            }

            DragBoxStartField.SetValue(dragBox, startMap);
            DragBoxActiveField.SetValue(dragBox, true);
        }

        private static void EnsureRimWorldDragBoxState()
        {
            if (mode != TouchMode.SelectionHold)
            {
                return;
            }

            DragBox dragBox = GetDragBox();
            if (dragBox != null)
            {
                DragBoxActiveField.SetValue(dragBox, true);
            }
        }

        private static void FinishRimWorldDragBox()
        {
            Selector selector = Find.Selector;
            DragBox dragBox = GetDragBox();
            if (selector == null || dragBox == null)
            {
                return;
            }

            try
            {
                SelectorSelectInsideDragBoxMethod.Invoke(selector, null);
            }
            finally
            {
                DragBoxActiveField.SetValue(dragBox, false);
            }
        }

        private static void CancelRimWorldDragBox()
        {
            DragBox dragBox = GetDragBox();
            if (dragBox != null)
            {
                DragBoxActiveField.SetValue(dragBox, false);
            }
        }

        private static void CancelWorldDragBox()
        {
            if (WorldSelectorDragBoxField == null || WorldDragBoxActiveField == null)
            {
                return;
            }

            WorldSelector selector;
            try
            {
                selector = Find.WorldSelector;
            }
            catch
            {
                return;
            }

            if (selector == null)
            {
                return;
            }

            WorldDragBox dragBox = (WorldDragBox)WorldSelectorDragBoxField.GetValue(selector);
            if (dragBox != null)
            {
                WorldDragBoxActiveField.SetValue(dragBox, false);
            }
        }

        private static DragBox GetDragBox()
        {
            Selector selector = Find.Selector;
            if (selector == null || SelectorDragBoxField == null)
            {
                return null;
            }
            return (DragBox)SelectorDragBoxField.GetValue(selector);
        }

        private static bool IsEnded(Touch touch)
        {
            return touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
        }

        private static Vector3 GetCameraRootPos(CameraDriver cameraDriver)
        {
            return (Vector3)CameraRootPosField.GetValue(cameraDriver);
        }

        private static float GetCameraRootSize(CameraDriver cameraDriver)
        {
            return (float)CameraRootSizeField.GetValue(cameraDriver);
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

        private static void FinishTouch()
        {
            ResetState();
            hadTouchLastFrame = false;
        }

        private static void ResetState()
        {
            CancelRimWorldDragBox();
            CancelWorldDragBox();
            mode = TouchMode.None;
            primaryFingerId = -1;
            panAnchorMap = Vector3.zero;
            twoFingerPanAnchorMap = Vector3.zero;
            rightClickDone = false;
            suppressReleaseTap = false;
            worldPanAnchorValid = false;
            worldPanAnchorPoint = Vector3.zero;
            panGestureDistance = 0f;
            lastTwoFingerDistance = 0f;
            lastTwoFingerMidpoint = Vector2.zero;
            twoFingerStartGuiA = Vector2.zero;
            twoFingerStartGuiB = Vector2.zero;
            twoFingerStartDistance = 0f;
            twoFingerStartMidpoint = Vector2.zero;
            twoFingerStartTime = 0f;
            twoFingerMaxMove = 0f;
            twoFingerMaxDistanceChange = 0f;
            twoFingerStartedWithVanillaMapTool = false;
        }
    }
}
