using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace BallisticsLab.Core
{
    internal readonly struct ProtocolTrajectorySample
    {
        internal ProtocolTrajectorySample(
            double xMetres,
            double yMetres,
            double zMetres,
            double speedMetresPerSecond)
        {
            if (!IsFinite(xMetres)
                || !IsFinite(yMetres)
                || !IsFinite(zMetres)
                || !IsFinite(speedMetresPerSecond)
                || speedMetresPerSecond < 0d)
            {
                ThrowInvalidSample();
            }

            XMetres = xMetres;
            YMetres = yMetres;
            ZMetres = zMetres;
            SpeedMetresPerSecond = speedMetresPerSecond;
        }

        internal double XMetres { get; }
        internal double YMetres { get; }
        internal double ZMetres { get; }
        internal double SpeedMetresPerSecond { get; }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        [DoesNotReturn]
        private static void ThrowInvalidSample()
        {
            throw new ArgumentOutOfRangeException(
                "sample",
                "Trajectory coordinates and speed must be finite; speed must be non-negative.");
        }
    }

    internal static class ProtocolTrajectorySampler
    {
        internal const double StandardMeasurementDistanceMetres = 3d;

        internal static bool TryInterpolateSpeedAtPathDistance(
            IReadOnlyList<ProtocolTrajectorySample> samples,
            double pathDistanceMetres,
            out double speedMetresPerSecond)
        {
            speedMetresPerSecond = 0d;
            if (samples == null)
            {
                ThrowNullSamples(nameof(samples));
            }
            if (!IsFinite(pathDistanceMetres) || pathDistanceMetres < 0d)
            {
                ThrowInvalidDistance(nameof(pathDistanceMetres));
            }
            if (samples.Count == 0)
            {
                return false;
            }
            if (pathDistanceMetres == 0d)
            {
                speedMetresPerSecond = samples[0].SpeedMetresPerSecond;
                return true;
            }

            double cumulativeDistance = 0d;
            ProtocolTrajectorySample previous = samples[0];
            for (int index = 1; index < samples.Count; index++)
            {
                ProtocolTrajectorySample current = samples[index];
                double deltaX = current.XMetres - previous.XMetres;
                double deltaY = current.YMetres - previous.YMetres;
                double deltaZ = current.ZMetres - previous.ZMetres;
                double segmentLength = Math.Sqrt(
                    deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
                if (!IsFinite(segmentLength))
                {
                    return false;
                }
                if (segmentLength > 0d
                    && cumulativeDistance + segmentLength >= pathDistanceMetres)
                {
                    double fraction = (pathDistanceMetres - cumulativeDistance)
                        / segmentLength;
                    speedMetresPerSecond = previous.SpeedMetresPerSecond
                        + (current.SpeedMetresPerSecond - previous.SpeedMetresPerSecond)
                        * fraction;
                    return IsFinite(speedMetresPerSecond) && speedMetresPerSecond >= 0d;
                }
                cumulativeDistance += segmentLength;
                previous = current;
            }
            return false;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        [DoesNotReturn]
        private static void ThrowNullSamples(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }

        [DoesNotReturn]
        private static void ThrowInvalidDistance(string parameterName)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Path distance must be finite and non-negative.");
        }
    }
}
