using BallisticsLab.Core;

internal static class ProtocolTrajectorySamplerTests
{
    private static readonly ProtocolTrajectorySample[] ExactSamples =
    {
        new ProtocolTrajectorySample(0d, 0d, 0d, 800d),
        new ProtocolTrajectorySample(3d, 0d, 0d, 780d),
        new ProtocolTrajectorySample(6d, 0d, 0d, 760d)
    };

    internal static bool ExactThreeMetreNodeReturnsNodeSpeed()
    {
        return ProtocolTrajectorySampler.TryInterpolateSpeedAtPathDistance(
                ExactSamples,
                3d,
                out double speed)
            && Nearly(speed, 780d);
    }

    internal static bool CurvedPathUsesCumulativeDistanceAndInterpolatesSpeed()
    {
        ProtocolTrajectorySample[] samples =
        {
            new ProtocolTrajectorySample(0d, 0d, 0d, 900d),
            new ProtocolTrajectorySample(1d, 0d, 0d, 850d),
            new ProtocolTrajectorySample(1d, 2d, 0d, 750d)
        };
        return ProtocolTrajectorySampler.TryInterpolateSpeedAtPathDistance(
                samples,
                2d,
                out double speed)
            && Nearly(speed, 800d);
    }

    internal static bool DuplicateNodesDoNotInflateDistance()
    {
        ProtocolTrajectorySample[] samples =
        {
            new ProtocolTrajectorySample(0d, 0d, 0d, 900d),
            new ProtocolTrajectorySample(0d, 0d, 0d, 875d),
            new ProtocolTrajectorySample(4d, 0d, 0d, 700d)
        };
        return ProtocolTrajectorySampler.TryInterpolateSpeedAtPathDistance(
                samples,
                3d,
                out double speed)
            && Nearly(speed, 743.75d);
    }

    internal static bool InsufficientPathDoesNotExtrapolate()
    {
        ProtocolTrajectorySample[] samples =
        {
            new ProtocolTrajectorySample(0d, 0d, 0d, 500d),
            new ProtocolTrajectorySample(2.99d, 0d, 0d, 490d)
        };
        return !ProtocolTrajectorySampler.TryInterpolateSpeedAtPathDistance(
            samples,
            ProtocolTrajectorySampler.StandardMeasurementDistanceMetres,
            out _);
    }

    internal static bool InvalidInputsAreRejected()
    {
        bool invalidSample = Throws<ArgumentOutOfRangeException>(() =>
            _ = new ProtocolTrajectorySample(double.NaN, 0d, 0d, 1d));
        bool invalidDistance = Throws<ArgumentOutOfRangeException>(() =>
            ProtocolTrajectorySampler.TryInterpolateSpeedAtPathDistance(
                ExactSamples,
                -1d,
                out _));
        bool nullSamples = Throws<ArgumentNullException>(() =>
            ProtocolTrajectorySampler.TryInterpolateSpeedAtPathDistance(
                null!,
                3d,
                out _));
        return invalidSample && invalidDistance && nullSamples;
    }

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    private static bool Nearly(double actual, double expected)
    {
        return Math.Abs(actual - expected) < 0.000000001d;
    }
}
