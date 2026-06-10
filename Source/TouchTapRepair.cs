using UnityEngine;
using Verse;

namespace RimTouch
{
    public static class TouchTapRepair
    {
        private const float DoubleTapMaxSeconds = 0.38f;
        private const float DoubleTapMaxDistancePixels = 36f;
        private const float TouchButtonPaddingPixels = 8f;
        private const float MaxExpandedButtonSidePixels = 120f;

        private static bool pendingTap;
        private static Vector2 tapGuiPosition;
        private static int tapFrame;
        private static int pendingClickCount = 1;
        private static bool hasLastTap;
        private static Vector2 lastTapGuiPosition;
        private static float lastTapTime;
        private static int suppressWindowCloseUntilFrame;

        public static bool ShouldSuppressWindowClose
        {
            get
            {
                return Time.frameCount <= suppressWindowCloseUntilFrame;
            }
        }

        public static void QueueTap(Vector2 guiPosition)
        {
            pendingTap = true;
            tapGuiPosition = guiPosition;
            tapFrame = Time.frameCount;
            pendingClickCount = RegisterTap(guiPosition);
        }

        public static int RegisterTap(Vector2 guiPosition)
        {
            float now = Time.realtimeSinceStartup;
            int clickCount = 1;
            if (hasLastTap
                && now - lastTapTime <= DoubleTapMaxSeconds
                && Vector2.Distance(guiPosition, lastTapGuiPosition) <= DoubleTapMaxDistancePixels)
            {
                clickCount = 2;
                hasLastTap = false;
            }
            else
            {
                hasLastTap = true;
                lastTapGuiPosition = guiPosition;
                lastTapTime = now;
            }

            return clickCount;
        }

        public static bool TryConsumeTap(Rect rect)
        {
            if (!pendingTap)
            {
                return false;
            }

            Event current = Event.current;
            if (current == null || current.type != EventType.MouseUp || current.button != 0)
            {
                return false;
            }

            if (Time.frameCount - tapFrame > 8)
            {
                pendingTap = false;
                return false;
            }

            if (!GetTouchHitRect(rect).Contains(tapGuiPosition))
            {
                return false;
            }

            pendingTap = false;
            suppressWindowCloseUntilFrame = Time.frameCount + 2;
            current.clickCount = pendingClickCount;
            current.mousePosition = tapGuiPosition;
            current.Use();
            return true;
        }

        public static void Clear()
        {
            pendingTap = false;
            pendingClickCount = 1;
        }

        private static Rect GetTouchHitRect(Rect rect)
        {
            if (rect.width > MaxExpandedButtonSidePixels || rect.height > MaxExpandedButtonSidePixels)
            {
                return rect;
            }

            return new Rect(
                rect.xMin - TouchButtonPaddingPixels,
                rect.yMin - TouchButtonPaddingPixels,
                rect.width + TouchButtonPaddingPixels * 2f,
                rect.height + TouchButtonPaddingPixels * 2f);
        }
    }
}
