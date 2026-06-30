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
        public static bool ProcessScrollViewTouch(Rect outRect, Rect viewRect, ref Vector2 scrollPosition)
        {
            if (!TouchModeEnabled)
            {
                return false;
            }

            float maxX = Mathf.Max(0f, viewRect.width - outRect.width);
            float maxY = Mathf.Max(0f, viewRect.height - outRect.height);
            if (maxX <= 1f && maxY <= 1f)
            {
                return false;
            }

            if (Input.touchCount == 0)
            {
                if (uiScrollInertiaActive && Time.realtimeSinceStartup - uiScrollInertiaStartTime > 1.25f)
                {
                    StopUiScrollInertia();
                    return false;
                }
                return ApplyUiScrollInertia(outRect, maxX, maxY, ref scrollPosition);
            }

            if (Input.touchCount != 1 || ignoreTouchesUntilAllReleased)
            {
                return false;
            }

            Touch touch;
            try
            {
                touch = Input.GetTouch(0);
            }
            catch
            {
                return false;
            }

            if (primaryFingerId >= 0 && touch.fingerId != primaryFingerId)
            {
                return false;
            }

            Vector2 gui = TouchToGui(touch.position);
            Vector2 reportedGuiDelta = TouchDeltaToGui(touch.deltaPosition);
            Vector2 previousGui = uiScrollGestureActive ? uiScrollLastGui : gui - reportedGuiDelta;
            if (!uiScrollGestureActive)
            {
                if (!IsTouchOverScrollRect(outRect, gui, previousGui) || Vector2.Distance(gui, startGui) <= DragSlopPixels)
                {
                    return false;
                }

                StopUiScrollInertia();
                ClearUiScrollVelocitySamples();
                ClaimTouchForUiScroll();
                uiScrollGestureActive = true;
                uiScrollActiveRect = outRect;
                uiScrollLastGui = previousGui;
                lastUiScrollSampleTime = Time.realtimeSinceStartup;
            }
            else if (!SameScrollRect(uiScrollActiveRect, outRect))
            {
                return false;
            }

            SuppressUiScrollFallout();
            suppressReleaseTap = true;
            TouchTapRepair.Clear();

            if (lastUiScrollApplyFrame == Time.frameCount)
            {
                return true;
            }

            Vector2 guiDelta = gui - uiScrollLastGui;
            if (guiDelta.sqrMagnitude <= 0.01f)
            {
                uiScrollLastGui = gui;
                return true;
            }

            Vector2 scrollDelta = Vector2.zero;
            if (maxX > 1f)
            {
                scrollDelta.x = -guiDelta.x;
                scrollPosition.x = Mathf.Clamp(scrollPosition.x + scrollDelta.x, 0f, maxX);
            }
            if (maxY > 1f)
            {
                scrollDelta.y = -guiDelta.y;
                scrollPosition.y = Mathf.Clamp(scrollPosition.y + scrollDelta.y, 0f, maxY);
            }

            SampleUiScrollVelocity(scrollDelta);
            uiScrollLastGui = gui;
            lastUiScrollApplyFrame = Time.frameCount;
            return true;
        }

        private static bool IsTouchOverScrollRect(Rect outRect, Vector2 gui, Vector2 previousGui)
        {
            Rect hitRect = ExpandScrollStartHitRect(outRect);
            if (hitRect.Contains(GuiToLocalGuiPoint(gui)) || hitRect.Contains(GuiToLocalGuiPoint(previousGui)))
            {
                return true;
            }

            if (hitRect.Contains(gui) || hitRect.Contains(previousGui))
            {
                return true;
            }

            Event current = Event.current;
            if (current != null && current.isMouse && hitRect.Contains(current.mousePosition))
            {
                return true;
            }

            return false;
        }

        private static Rect ExpandScrollStartHitRect(Rect rect)
        {
            const float sidePadding = 24f;
            const float topPadding = 8f;
            const float bottomPadding = 36f;
            return new Rect(
                rect.xMin - sidePadding,
                rect.yMin - topPadding,
                rect.width + sidePadding * 2f,
                rect.height + topPadding + bottomPadding);
        }

        private static void ClaimTouchForUiScroll()
        {
            startedOverUi = true;
            suppressReleaseTap = true;

            if (mode == TouchMode.SelectionHold)
            {
                CancelRimWorldDragBox();
            }

            if (mode == TouchMode.MapPan || mode == TouchMode.SelectionHold)
            {
                mode = TouchMode.OneFinger;
            }

            CancelRimWorldDragBox();
            CancelWorldDragBox();
            SuppressUiScrollFallout();
        }

        private static bool ApplyUiScrollInertia(Rect outRect, float maxX, float maxY, ref Vector2 scrollPosition)
        {
            if (!uiScrollInertiaActive || !SameScrollRect(uiScrollInertiaRect, outRect))
            {
                return false;
            }

            if (uiScrollVelocity.sqrMagnitude <= MinUiScrollInertiaSpeed * MinUiScrollInertiaSpeed)
            {
                StopUiScrollInertia();
                return false;
            }

            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Clamp(now - lastUiScrollInertiaTime, 0.001f, 0.05f);
            lastUiScrollInertiaTime = now;

            if (lastUiScrollInertiaApplyFrame == Time.frameCount)
            {
                SuppressUiScrollFallout();
                return true;
            }

            Vector2 before = scrollPosition;
            if (maxX > 1f)
            {
                scrollPosition.x = Mathf.Clamp(scrollPosition.x + uiScrollVelocity.x * dt, 0f, maxX);
            }
            else
            {
                uiScrollVelocity.x = 0f;
            }
            if (maxY > 1f)
            {
                scrollPosition.y = Mathf.Clamp(scrollPosition.y + uiScrollVelocity.y * dt, 0f, maxY);
            }
            else
            {
                uiScrollVelocity.y = 0f;
            }

            if ((scrollPosition - before).sqrMagnitude <= 0.0001f)
            {
                StopUiScrollInertia();
                return false;
            }

            uiScrollVelocity *= Mathf.Exp(-PanInertiaDamping * 0.75f * dt);
            lastUiScrollInertiaApplyFrame = Time.frameCount;
            SuppressUiScrollFallout();
            return true;
        }

        private static void FinalizeUiScrollInertia()
        {
            if (!uiScrollGestureActive)
            {
                return;
            }

            Vector2 recentVelocity = GetRecentUiScrollVelocity(Time.realtimeSinceStartup);
            if (recentVelocity.magnitude < MinUiScrollInertiaStartSpeed)
            {
                StopUiScrollInertia();
                return;
            }

            uiScrollVelocity = recentVelocity;
            uiScrollInertiaActive = true;
            uiScrollInertiaRect = uiScrollActiveRect;
            uiScrollInertiaStartTime = Time.realtimeSinceStartup;
            lastUiScrollInertiaTime = uiScrollInertiaStartTime;
            lastUiScrollInertiaApplyFrame = -1000;
        }

        private static void StopUiScrollInertia()
        {
            uiScrollVelocity = Vector2.zero;
            uiScrollInertiaActive = false;
            uiScrollInertiaRect = default(Rect);
            uiScrollInertiaStartTime = 0f;
            lastUiScrollInertiaApplyFrame = -1000;
        }

        private static void SampleUiScrollVelocity(Vector2 scrollDelta)
        {
            float now = Time.realtimeSinceStartup;
            float dt = now - lastUiScrollSampleTime;
            lastUiScrollSampleTime = now;

            if (dt <= 0.0001f || dt > 0.18f || scrollDelta.sqrMagnitude <= 0.01f)
            {
                return;
            }

            Vector2 velocity = scrollDelta / dt;
            if (!IsFinite(velocity))
            {
                return;
            }

            if (velocity.magnitude > MaxUiScrollInertiaSpeed)
            {
                velocity = velocity.normalized * MaxUiScrollInertiaSpeed;
            }

            RecordUiScrollVelocitySample(velocity, now);
            uiScrollVelocity = GetRecentUiScrollVelocity(now);
        }

        private static void RecordUiScrollVelocitySample(Vector2 velocity, float time)
        {
            UiScrollVelocitySamples[uiScrollVelocitySampleIndex] = velocity;
            UiScrollVelocitySampleTimes[uiScrollVelocitySampleIndex] = time;
            uiScrollVelocitySampleIndex = (uiScrollVelocitySampleIndex + 1) % UiScrollVelocitySamples.Length;
            uiScrollVelocitySampleCount = Mathf.Min(uiScrollVelocitySampleCount + 1, UiScrollVelocitySamples.Length);
        }

        private static Vector2 GetRecentUiScrollVelocity(float now)
        {
            if (uiScrollVelocitySampleCount <= 0)
            {
                return uiScrollVelocity;
            }

            Vector2 sum = Vector2.zero;
            float totalWeight = 0f;
            for (int i = 0; i < uiScrollVelocitySampleCount; i++)
            {
                int index = (uiScrollVelocitySampleIndex - 1 - i + UiScrollVelocitySamples.Length) % UiScrollVelocitySamples.Length;
                float age = now - UiScrollVelocitySampleTimes[index];
                if (age < 0f || age > UiScrollVelocityHistorySeconds)
                {
                    continue;
                }

                float recencyWeight = Mathf.Clamp01(1f - age / UiScrollVelocityHistorySeconds);
                float orderWeight = 1f / (1f + i * 0.35f);
                float weight = Mathf.Max(0.05f, recencyWeight) * orderWeight;
                sum += UiScrollVelocitySamples[index] * weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
            {
                return Vector2.zero;
            }
            return sum / totalWeight;
        }

        private static void ClearUiScrollVelocitySamples()
        {
            uiScrollVelocitySampleIndex = 0;
            uiScrollVelocitySampleCount = 0;
            for (int i = 0; i < UiScrollVelocitySamples.Length; i++)
            {
                UiScrollVelocitySamples[i] = Vector2.zero;
                UiScrollVelocitySampleTimes[i] = 0f;
            }
        }
    }
}
