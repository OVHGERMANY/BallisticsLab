using EFT;
using EFT.Ballistics;

namespace BallisticsLab.Runtime.Fixtures
{
    internal sealed class LabBackstopCollider : BallisticCollider
    {
        internal long FixtureId { get; private set; }
        internal int LayerCount { get; private set; }

        internal void Configure(long fixtureId, int layerCount)
        {
            FixtureId = fixtureId;
            LayerCount = layerCount;
            TypeOfMaterial = MaterialType.MetalThick;
            PenetrationLevel = 9999f;
            PenetrationChance = 0f;
            RicochetChance = 0f;
            FragmentationChance = 0f;
            TrajectoryDeviationChance = 0f;
            TrajectoryDeviation = 0f;
            Associate(TypeOfMaterial);
        }
    }
}
