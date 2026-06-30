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
        private static Vector2 tapScreenGuiPosition;
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

        public static int QueueTap(Vector2 guiPosition)
        {
            pendingTap = true;
            tapGuiPosition = guiPosition;
            tapScreenGuiPosition = GuiToScreenGuiPosition(guiPosition);
            tapFrame = Time.frameCount;
            pendingClickCount = RegisterTap(guiPosition);
            return pendingClickCount;
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
            int clickCount;
            return TryConsumeTap(rect, 1, out clickCount);
        }

        public static bool TryConsumeDoubleTap(Rect rect)
        {
            int clickCount;
            return TryConsumeTap(rect, 2, out clickCount);
        }

        private static bool TryConsumeTap(Rect rect, int minClickCount, out int clickCount)
        {
            clickCount = 0;
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

            if (pendingClickCount < minClickCount)
            {
                return false;
            }

            Rect hitRect = GetTouchHitRect(rect);
            Vector2 consumePosition = GUIUtility.ScreenToGUIPoint(tapScreenGuiPosition);
            if (!hitRect.Contains(consumePosition))
            {
                if (hitRect.Contains(current.mousePosition))
                {
                    consumePosition = current.mousePosition;
                }
                else if (hitRect.Contains(tapGuiPosition))
                {
                    consumePosition = tapGuiPosition;
                }
                else
                {
                    return false;
                }
            }

            if (!hitRect.Contains(consumePosition))
            {
                return false;
            }

            pendingTap = false;
            suppressWindowCloseUntilFrame = Time.frameCount + 2;
            clickCount = pendingClickCount;
            current.clickCount = pendingClickCount;
            current.mousePosition = consumePosition;
            current.Use();
            return true;
        }

        public static void Clear(bool clearTapHistory = true)
        {
            pendingTap = false;
            pendingClickCount = 1;

            if (clearTapHistory)
            {
                hasLastTap = false;
                lastTapGuiPosition = Vector2.zero;
                lastTapTime = 0f;
            }
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

        private static Vector2 GuiToScreenGuiPosition(Vector2 guiPosition)
        {
            float scaleX = UI.screenWidth > 0 ? Screen.width / (float)UI.screenWidth : 1f;
            float scaleY = UI.screenHeight > 0 ? Screen.height / (float)UI.screenHeight : 1f;
            return new Vector2(guiPosition.x * scaleX, guiPosition.y * scaleY);
        }
    }
}
