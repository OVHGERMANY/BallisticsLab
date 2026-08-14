using System;

namespace BallisticsLab.Core
{
    internal readonly struct LabPanelBounds
    {
        internal LabPanelBounds(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        internal float X { get; }
        internal float Y { get; }
        internal float Width { get; }
        internal float Height { get; }
    }

    internal static class LabPresentationPolicies
    {
        internal const float PreferredPanelWidth = 840f;
        internal const float PreferredPanelHeight = 900f;
        internal const float PanelScreenMargin = 10f;
        internal const float CompactControlThreshold = 700f;
        internal const float AimMarkerMinimumViewDistanceMetres = 0.5f;

        internal static LabPanelBounds FitPanelToScreen(
            float currentX,
            float currentY,
            int screenWidth,
            int screenHeight)
        {
            float safeScreenWidth = Math.Max(1, screenWidth);
            float safeScreenHeight = Math.Max(1, screenHeight);
            float horizontalMargin = Math.Min(
                PanelScreenMargin,
                Math.Max(0f, (safeScreenWidth - 1f) * 0.5f));
            float verticalMargin = Math.Min(
                PanelScreenMargin,
                Math.Max(0f, (safeScreenHeight - 1f) * 0.5f));
            float width = Math.Min(
                PreferredPanelWidth,
                Math.Max(1f, safeScreenWidth - horizontalMargin * 2f));
            float height = Math.Min(
                PreferredPanelHeight,
                Math.Max(1f, safeScreenHeight - verticalMargin * 2f));
            float maximumX = Math.Max(horizontalMargin, safeScreenWidth - horizontalMargin - width);
            float maximumY = Math.Max(verticalMargin, safeScreenHeight - verticalMargin - height);
            float x = ClampFinite(currentX, horizontalMargin, maximumX);
            float y = ClampFinite(currentY, verticalMargin, maximumY);
            return new LabPanelBounds(x, y, width, height);
        }

        internal static bool UseCompactControls(float panelWidth)
        {
            return !IsFinite(panelWidth) || panelWidth < CompactControlThreshold;
        }

        internal static bool ShouldCaptureGameInput(bool panelVisible, bool pluginEnabled)
        {
            return panelVisible && pluginEnabled;
        }

        internal static bool ShouldShowAimMarker(
            float cameraDistanceMetres,
            float cameraSideDot,
            float viewportDepth)
        {
            return IsFinite(cameraDistanceMetres)
                && cameraDistanceMetres >= AimMarkerMinimumViewDistanceMetres
                && IsFinite(cameraSideDot)
                && cameraSideDot < -0.001f
                && IsFinite(viewportDepth)
                && viewportDepth > 0f;
        }

        private static float ClampFinite(float value, float minimum, float maximum)
        {
            if (!IsFinite(value))
            {
                return minimum;
            }
            if (value < minimum)
            {
                return minimum;
            }
            return value > maximum ? maximum : value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
