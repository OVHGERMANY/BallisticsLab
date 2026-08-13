using System;
using System.Collections.Generic;

namespace BallisticsLab.Core
{
    internal readonly struct ProtocolImpactPoint
    {
        internal ProtocolImpactPoint(
            int shotIndex,
            double localXMetres,
            double localYMetres,
            double targetToleranceMetres)
        {
            ShotIndex = shotIndex;
            LocalXMetres = localXMetres;
            LocalYMetres = localYMetres;
            TargetToleranceMetres = targetToleranceMetres;
        }

        internal int ShotIndex { get; }
        internal double LocalXMetres { get; }
        internal double LocalYMetres { get; }
        internal double TargetToleranceMetres { get; }

        internal bool Contains(double localXMetres, double localYMetres)
        {
            if (!IsFinite(localXMetres) || !IsFinite(localYMetres))
            {
                return false;
            }
            double deltaX = localXMetres - LocalXMetres;
            double deltaY = localYMetres - LocalYMetres;
            return deltaX * deltaX + deltaY * deltaY
                <= TargetToleranceMetres * TargetToleranceMetres;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    internal sealed class ProtocolImpactPattern
    {
        private const double TargetToleranceDiameters = 1.5d;
        private const double MinimumTargetToleranceMetres = 0.02d;
        private const double SeparationSafetyDiameters = 0.25d;
        private readonly IReadOnlyList<ProtocolImpactPoint> _points;

        private ProtocolImpactPattern(
            double projectileDiameterMetres,
            double requiredEdgeDistanceMetres,
            double nominalPointSpacingMetres,
            IReadOnlyList<ProtocolImpactPoint> points)
        {
            ProjectileDiameterMetres = projectileDiameterMetres;
            RequiredEdgeDistanceMetres = requiredEdgeDistanceMetres;
            NominalPointSpacingMetres = nominalPointSpacingMetres;
            _points = points;
        }

        internal double ProjectileDiameterMetres { get; }
        internal double RequiredEdgeDistanceMetres { get; }
        internal double NominalPointSpacingMetres { get; }
        internal IReadOnlyList<ProtocolImpactPoint> Points => _points;

        internal static bool TryCreate(
            ProtocolThreatDefinition threat,
            ProtocolAmmunitionMapping mapping,
            double faceWidthMetres,
            double faceHeightMetres,
            out ProtocolImpactPattern? pattern,
            out string failure)
        {
            pattern = null;
            failure = string.Empty;
            if (threat == null || mapping == null)
            {
                failure = "Protocol threat and ammunition mapping are required.";
                return false;
            }
            if (!mapping.CanQualifySimulationScreening)
            {
                failure = "The selected ammunition mapping cannot qualify a simulation screening.";
                return false;
            }
            if (threat.RequiredQualifyingShots != 5)
            {
                failure = "The deterministic impact pattern requires exactly five shots.";
                return false;
            }
            if (!IsFinitePositive(faceWidthMetres) || !IsFinitePositive(faceHeightMetres))
            {
                failure = "The fixture face dimensions must be finite and positive.";
                return false;
            }

            double diameter = mapping.InstalledProjectileDiameterMetres;
            if (!IsFinitePositive(diameter))
            {
                failure = "The installed projectile diameter is unavailable.";
                return false;
            }
            double tolerance = Math.Max(
                diameter * TargetToleranceDiameters,
                MinimumTargetToleranceMetres);
            double requiredEdgeDistance = diameter * threat.MinimumSeparationDiameters;
            double spacing = requiredEdgeDistance
                + 2d * tolerance
                + diameter * SeparationSafetyDiameters;
            double requiredHalfExtent = spacing + tolerance + requiredEdgeDistance;
            if (faceWidthMetres * 0.5d < requiredHalfExtent
                || faceHeightMetres * 0.5d < requiredHalfExtent)
            {
                failure = "The fixture face is too small for five separated impact regions.";
                return false;
            }

            ProtocolImpactPoint[] points =
            {
                new ProtocolImpactPoint(0, 0d, 0d, tolerance),
                new ProtocolImpactPoint(1, -spacing, 0d, tolerance),
                new ProtocolImpactPoint(2, spacing, 0d, tolerance),
                new ProtocolImpactPoint(3, 0d, -spacing, tolerance),
                new ProtocolImpactPoint(4, 0d, spacing, tolerance)
            };
            pattern = new ProtocolImpactPattern(
                diameter,
                requiredEdgeDistance,
                spacing,
                Array.AsReadOnly(points));
            return true;
        }

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        }
    }
}
