using BallisticsLab.Core;

namespace BallisticsLab.Validation;

internal static class PresentationPolicyTests
{
    internal static bool PanelRemainsFullyVisibleAtSmallResolutions()
    {
        LabPanelBounds bounds = LabPresentationPolicies.FitPanelToScreen(
            4000f,
            -500f,
            800,
            600);
        return Nearly(bounds.X, 10f)
            && Nearly(bounds.Y, 10f)
            && Nearly(bounds.Width, 780f)
            && Nearly(bounds.Height, 580f)
            && bounds.X + bounds.Width <= 790.0001f
            && bounds.Y + bounds.Height <= 590.0001f;
    }

    internal static bool PanelRestoresPreferredSizeWhenResolutionGrows()
    {
        LabPanelBounds compact = LabPresentationPolicies.FitPanelToScreen(
            40f,
            60f,
            800,
            600);
        LabPanelBounds restored = LabPresentationPolicies.FitPanelToScreen(
            compact.X,
            compact.Y,
            1920,
            1080);
        return Nearly(restored.X, 10f)
            && Nearly(restored.Y, 10f)
            && Nearly(restored.Width, LabPresentationPolicies.PreferredPanelWidth)
            && Nearly(restored.Height, LabPresentationPolicies.PreferredPanelHeight);
    }

    internal static bool PanelRejectsInvalidCoordinates()
    {
        LabPanelBounds bounds = LabPresentationPolicies.FitPanelToScreen(
            float.NaN,
            float.PositiveInfinity,
            1920,
            1080);
        return Nearly(bounds.X, LabPresentationPolicies.PanelScreenMargin)
            && Nearly(bounds.Y, LabPresentationPolicies.PanelScreenMargin);
    }

    internal static bool CompactControlsFollowVisiblePanelWidth()
    {
        return LabPresentationPolicies.UseCompactControls(699.9f)
            && LabPresentationPolicies.UseCompactControls(float.NaN)
            && !LabPresentationPolicies.UseCompactControls(700f)
            && !LabPresentationPolicies.UseCompactControls(840f);
    }

    internal static bool VisibleEnabledPanelOwnsGameInput()
    {
        return LabPresentationPolicies.ShouldCaptureGameInput(
                panelVisible: true,
                pluginEnabled: true)
            && !LabPresentationPolicies.ShouldCaptureGameInput(
                panelVisible: false,
                pluginEnabled: true)
            && !LabPresentationPolicies.ShouldCaptureGameInput(
                panelVisible: true,
                pluginEnabled: false)
            && !LabPresentationPolicies.ShouldCaptureGameInput(
                panelVisible: false,
                pluginEnabled: false);
    }

    internal static bool AimMarkerRequiresSafeFrontFaceView()
    {
        return LabPresentationPolicies.ShouldShowAimMarker(
                cameraDistanceMetres: 8f,
                cameraSideDot: -8f,
                viewportDepth: 8f)
            && !LabPresentationPolicies.ShouldShowAimMarker(
                cameraDistanceMetres: 0.49f,
                cameraSideDot: -0.49f,
                viewportDepth: 0.49f)
            && !LabPresentationPolicies.ShouldShowAimMarker(
                cameraDistanceMetres: 8f,
                cameraSideDot: 8f,
                viewportDepth: 8f)
            && !LabPresentationPolicies.ShouldShowAimMarker(
                cameraDistanceMetres: 8f,
                cameraSideDot: -8f,
                viewportDepth: -1f)
            && !LabPresentationPolicies.ShouldShowAimMarker(
                cameraDistanceMetres: float.NaN,
                cameraSideDot: -8f,
                viewportDepth: 8f);
    }

    private static bool Nearly(float left, float right)
    {
        return Math.Abs(left - right) <= 0.0001f;
    }
}
