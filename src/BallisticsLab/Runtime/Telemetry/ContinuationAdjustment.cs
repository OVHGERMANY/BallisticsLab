using System.Runtime.CompilerServices;
using EFT.Ballistics;

namespace BallisticsLab.Runtime.Telemetry
{
    internal sealed class ContinuationAdjustment
    {
        internal ContinuationAdjustment(
            Shot shot,
            long fixtureId,
            int sourceLayerIndex,
            string kind,
            float penetrationFactor,
            float velocityFactor,
            float outcomeFactor,
            float armorCf,
            float damageBefore,
            float penetrationBefore,
            float damageAfter,
            float penetrationAfter)
        {
            FireIndex = shot.FireIndex;
            RandomSeed = shot.RandomSeed;
            FragmentIndex = shot.FragmentIndex;
            Parent = shot.Parent;
            FixtureId = fixtureId;
            SourceLayerIndex = sourceLayerIndex;
            Kind = kind;
            PenetrationFactor = penetrationFactor;
            VelocityFactor = velocityFactor;
            OutcomeFactor = outcomeFactor;
            ArmorCf = armorCf;
            DamageBefore = damageBefore;
            PenetrationBefore = penetrationBefore;
            DamageAfter = damageAfter;
            PenetrationAfter = penetrationAfter;
        }

        internal int FireIndex { get; }
        internal int RandomSeed { get; }
        internal int FragmentIndex { get; }
        internal Shot Parent { get; }
        internal long FixtureId { get; }
        internal int SourceLayerIndex { get; }
        internal string Kind { get; }
        internal float PenetrationFactor { get; }
        internal float VelocityFactor { get; }
        internal float OutcomeFactor { get; }
        internal float ArmorCf { get; }
        internal float DamageBefore { get; }
        internal float PenetrationBefore { get; }
        internal float DamageAfter { get; }
        internal float PenetrationAfter { get; }
    }

    internal static class ContinuationAdjustmentStore
    {
        private static readonly ConditionalWeakTable<Shot, ContinuationAdjustment> Values =
            new ConditionalWeakTable<Shot, ContinuationAdjustment>();

        internal static void Set(Shot shot, ContinuationAdjustment adjustment)
        {
            lock (Values)
            {
                Values.Remove(shot);
                Values.Add(shot, adjustment);
            }
        }

        internal static ContinuationAdjustment Take(Shot shot)
        {
            lock (Values)
            {
                if (!Values.TryGetValue(shot, out ContinuationAdjustment adjustment))
                {
                    return null;
                }

                Values.Remove(shot);
                if (adjustment.FireIndex != shot.FireIndex
                    || adjustment.RandomSeed != shot.RandomSeed
                    || adjustment.FragmentIndex != shot.FragmentIndex
                    || !ReferenceEquals(adjustment.Parent, shot.Parent))
                {
                    return null;
                }

                return adjustment;
            }
        }
    }
}
