using System.Runtime.CompilerServices;
using EFT.Ballistics;
using UnityEngine;

namespace BallisticsLab.Runtime.Telemetry
{
    internal sealed class CollisionSnapshot
    {
        internal CollisionSnapshot(float damage, float penetrationPower, float speed, Vector3 segmentStart)
        {
            Damage = damage;
            PenetrationPower = penetrationPower;
            Speed = speed;
            SegmentStart = segmentStart;
        }

        internal float Damage { get; }
        internal float PenetrationPower { get; }
        internal float Speed { get; }
        internal Vector3 SegmentStart { get; }
    }

    internal static class CollisionSnapshotStore
    {
        private static readonly ConditionalWeakTable<Shot, CollisionSnapshot> Values =
            new ConditionalWeakTable<Shot, CollisionSnapshot>();

        internal static void Set(Shot shot, CollisionSnapshot snapshot)
        {
            lock (Values)
            {
                Values.Remove(shot);
                Values.Add(shot, snapshot);
            }
        }

        internal static CollisionSnapshot Take(Shot shot)
        {
            lock (Values)
            {
                if (!Values.TryGetValue(shot, out CollisionSnapshot snapshot))
                {
                    return null;
                }

                Values.Remove(shot);
                return snapshot;
            }
        }
    }
}

