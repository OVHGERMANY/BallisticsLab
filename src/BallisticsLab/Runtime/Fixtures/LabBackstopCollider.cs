using EFT;
using EFT.Ballistics;
using BallisticsLab.Core;

namespace BallisticsLab.Runtime.Fixtures
{
    public sealed class LabBackstopCollider : BallisticCollider
    {
        private readonly int _physicalBallisticsSurfaceSchema =
            FixturePhysicalMaterialContract.SurfaceSchema;

        internal long FixtureId { get; private set; }
        internal int LayerCount { get; private set; }

        public int PhysicalBallisticsSurfaceSchema => _physicalBallisticsSurfaceSchema;

        public string PhysicalBallisticsMaterialClass => FixtureId > 0L
            ? "ArmoredSteel"
            : string.Empty;

        public string PhysicalBallisticsSurfaceIdentity => FixtureId > 0L
            ? FixturePhysicalMaterialContract.CreateBackstopSurfaceIdentity(FixtureId)
            : string.Empty;

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
