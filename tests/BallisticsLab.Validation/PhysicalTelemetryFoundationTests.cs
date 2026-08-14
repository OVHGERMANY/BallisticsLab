using System.Reflection;
using BallisticsLab.Core;

namespace BallisticsLab.Validation;

internal static class PhysicalTelemetryFoundationTests
{
    internal static bool AbsentPublisherIsSafe()
    {
        using var connection = new PhysicalTelemetryPublisherConnection(
            "BallisticsLab.Validation.MissingPublisher",
            1);
        bool attached = connection.TryAttach(
            Array.Empty<Assembly>(),
            _ => throw new InvalidOperationException("Absent publisher delivered an event."));
        return !attached
            && !connection.IsAttached
            && connection.Status == PhysicalTelemetryConnectionStatus.PublisherAbsent;
    }

    internal static bool UnsupportedSchemaIsRejected()
    {
        UnsupportedPublisher.Reset();
        using var connection = new PhysicalTelemetryPublisherConnection(
            typeof(UnsupportedPublisher).FullName!,
            1);
        bool attached = connection.TryAttach(
            new[] { typeof(UnsupportedPublisher).Assembly },
            _ => throw new InvalidOperationException("Unsupported publisher delivered an event."));
        return !attached
            && !connection.IsAttached
            && connection.PublisherSchema == 7
            && connection.Status == PhysicalTelemetryConnectionStatus.UnsupportedSchema
            && UnsupportedPublisher.SubscriberCount == 0;
    }

    internal static bool LateDiscoveryAttachesAndSessionDetachReleasesDelegate()
    {
        ValidPublisher.Reset();
        int delivered = 0;
        using var connection = new PhysicalTelemetryPublisherConnection(
            typeof(ValidPublisher).FullName!,
            1);
        bool absent = !connection.TryAttach(Array.Empty<Assembly>(), _ => delivered++);
        bool attached = connection.TryAttach(
            new[] { typeof(ValidPublisher).Assembly },
            _ => delivered++);
        ValidPublisher.Publish(FakePhysicalEvent.Prepared().Event);
        bool detached = connection.TryDetach(out string failure);
        ValidPublisher.Publish(FakePhysicalEvent.Prepared().Event);
        return absent
            && attached
            && detached
            && string.IsNullOrEmpty(failure)
            && delivered == 1
            && ValidPublisher.SubscriberCount == 0
            && !connection.IsAttached;
    }

    internal static bool PreparedEventIsCopiedCompletelyAndDetached()
    {
        ValidPublisher.Reset();
        FakePhysicalEvent source = FakePhysicalEvent.Prepared();
        PhysicalTelemetryEventRecord? captured = null;
        using var connection = new PhysicalTelemetryPublisherConnection(
            typeof(ValidPublisher).FullName!,
            1);
        if (!connection.TryAttach(
                new[] { typeof(ValidPublisher).Assembly },
                record => captured = record))
        {
            return false;
        }

        ValidPublisher.Publish(source.Event);
        source.ParentHistory.Clear();
        source.Outputs.Clear();
        if (captured == null)
        {
            return false;
        }

        return captured.SnapshotSchema == 1
            && captured.PublisherSchema == 1
            && captured.Stage == PhysicalTelemetryStageRecord.CollisionPrepared
            && captured.TransitionId == "transition-prepared"
            && captured.Outcome == "Unknown"
            && captured.Host.RootFireIndex == 17
            && captured.Host.RootRandomSeed == 991
            && captured.Host.CurrentFragmentIndex == 3
            && captured.Host.ParentDepth == 2
            && captured.Host.AmmunitionTemplateId == "ammo-template"
            && captured.Impact.TargetProfileId == "target-profile"
            && captured.Impact.TargetMaterialClass == "ArmoredSteel"
            && captured.Impact.TargetSurfaceIdentity == "fixture/700/plate/0"
            && Nearly(captured.Impact.PhysicalThicknessMetres, 0.0127d)
            && Nearly(captured.Impact.PositionMetres.X, 1.25d)
            && captured.Parent.Kind == "IntactProjectile"
            && captured.Parent.ProjectileId == "projectile-parent"
            && !ReferenceEquals(captured.Parent.ProjectileId, source.Parent.ProjectileId)
            && captured.Parent.RootShotId == "root-shot"
            && captured.Parent.Construction == "SteelCoreJacketed"
            && captured.Parent.ShapeClass == "Spitzer"
            && Nearly(captured.Parent.RetainedMassKilograms, 0.004d)
            && Nearly(captured.Parent.EquivalentDiameterMetres, 0.0057d)
            && Nearly(captured.Parent.SpeedMetresPerSecond, 800d)
            && Nearly(captured.Parent.TranslationalKineticEnergyJoules, 1280d)
            && Nearly(captured.Parent.Orientation.W, 1d)
            && captured.Parent.CollisionHistory.Count == 1
            && captured.Parent.CollisionHistory[0].CollisionId == "collision-prior"
            && captured.Parent.CollisionHistory[0].Outcome == "Penetrated"
            && captured.Outputs.Count == 0
            && captured.Conservation == null;
    }

    internal static bool ResolvedEventCopiesOutputsProvenanceAndConservation()
    {
        ValidPublisher.Reset();
        FakePhysicalEvent source = FakePhysicalEvent.Resolved();
        PhysicalTelemetryEventRecord? captured = null;
        using var connection = new PhysicalTelemetryPublisherConnection(
            typeof(ValidPublisher).FullName!,
            1);
        if (!connection.TryAttach(
                new[] { typeof(ValidPublisher).Assembly },
                record => captured = record))
        {
            return false;
        }

        ValidPublisher.Publish(source.Event);
        source.OutputHistory.Clear();
        source.Outputs.Clear();
        if (captured?.Conservation == null || captured.Outputs.Count != 2)
        {
            return false;
        }

        PhysicalComponentRecord parentOutput = captured.Outputs[0];
        PhysicalComponentRecord output = captured.Outputs[1];
        PhysicalConservationRecord conservation = captured.Conservation;
        return captured.Stage == PhysicalTelemetryStageRecord.CollisionResolved
            && captured.TransitionId == "transition-resolved"
            && captured.Outcome == "Fragmented"
            && output.Kind == "TargetSpall"
            && parentOutput.Kind == "DeformedProjectile"
            && parentOutput.IsParentDerivedMass
            && !parentOutput.IsTargetMaterialOrigin
            && output.ParentProjectileId == "projectile-parent"
            && output.SourceMaterialId == "target-profile"
            && output.SourceMaterialClass == "ArmoredSteel"
            && output.SourceCollisionId == "collision-current"
            && output.IsTargetMaterialOrigin
            && !output.IsParentDerivedMass
            && output.CollisionHistory.Count == 1
            && Nearly(conservation.ParentMassKilograms, 0.004d)
            && Nearly(conservation.AllocatedParentMassKilograms, 0.003d)
            && Nearly(conservation.UnallocatedParentMassKilograms, 0.001d)
            && Nearly(conservation.TargetSpallMassKilograms, 0.0002d)
            && Nearly(conservation.ParentEnergyJoules, 1280d)
            && Nearly(conservation.LossBudget.PenetrationLossJoules, 300d)
            && Nearly(conservation.LossBudget.DeformationLossJoules, 100d)
            && Nearly(conservation.LossBudget.FractureLossJoules, 80d)
            && Nearly(conservation.LossBudget.HeatLossJoules, 20d)
            && Nearly(conservation.LossBudget.OtherLossJoules, 10d)
            && Nearly(conservation.ModeledLossEnergyJoules, 510d)
            && Nearly(conservation.ResidualEnergyJoules, 770d)
            && Nearly(conservation.OutputEnergyJoules, 760d)
            && Nearly(conservation.EnergyClosureErrorJoules, 10d)
            && conservation.ParentDerivedOutputCount == 1
            && conservation.TargetSpallOutputCount == 1;
    }

    internal static bool CaptureBufferIsBoundedAndReturnsDetachedSnapshots()
    {
        FakePhysicalEvent first = FakePhysicalEvent.Prepared("first");
        FakePhysicalEvent second = FakePhysicalEvent.Prepared("second");
        FakePhysicalEvent third = FakePhysicalEvent.Prepared("third");
        if (!PhysicalTelemetryReflectionReader.TryCopy(1, first.Event, out PhysicalTelemetryEventRecord? firstRecord, out _)
            || !PhysicalTelemetryReflectionReader.TryCopy(1, second.Event, out PhysicalTelemetryEventRecord? secondRecord, out _)
            || !PhysicalTelemetryReflectionReader.TryCopy(1, third.Event, out PhysicalTelemetryEventRecord? thirdRecord, out _)
            || firstRecord == null
            || secondRecord == null
            || thirdRecord == null)
        {
            return false;
        }

        var buffer = new PhysicalTelemetryCaptureBuffer(2);
        buffer.Add(firstRecord);
        buffer.Add(secondRecord);
        IReadOnlyList<PhysicalTelemetryEventRecord> snapshot = buffer.Snapshot();
        buffer.Add(thirdRecord);
        return buffer.Count == 2
            && snapshot.Count == 2
            && snapshot[0].TransitionId == "first"
            && snapshot[1].TransitionId == "second"
            && buffer.Snapshot()[0].TransitionId == "second";
    }

    internal static bool RejectsInvalidOrNonFiniteForeignEvents()
    {
        FakePhysicalEvent source = FakePhysicalEvent.Prepared();
        source.Parent.SpeedMetresPerSecond = double.NaN;
        bool copied = PhysicalTelemetryReflectionReader.TryCopy(
            1,
            source.Event,
            out PhysicalTelemetryEventRecord? record,
            out string failure);
        return !copied
            && record == null
            && failure.Contains("SpeedMetresPerSecond", StringComparison.Ordinal);
    }

    private static bool Nearly(double actual, double expected)
    {
        return Math.Abs(actual - expected) <= 0.000000001d;
    }
}

internal static class ValidPublisher
{
    public const int SchemaVersion = 1;
    private static Action<object>? _observers;

    internal static int SubscriberCount => _observers?.GetInvocationList().Length ?? 0;

    public static void Subscribe(Action<object> observer)
    {
        _observers += observer;
    }

    public static void Unsubscribe(Action<object> observer)
    {
        _observers -= observer;
    }

    internal static void Publish(object value)
    {
        _observers?.Invoke(value);
    }

    internal static void Reset()
    {
        _observers = null;
    }
}

internal static class UnsupportedPublisher
{
    public const int SchemaVersion = 7;
    private static Action<object>? _observers;

    internal static int SubscriberCount => _observers?.GetInvocationList().Length ?? 0;

    public static void Subscribe(Action<object> observer)
    {
        _observers += observer;
    }

    public static void Unsubscribe(Action<object> observer)
    {
        _observers -= observer;
    }

    internal static void Reset()
    {
        _observers = null;
    }
}

internal sealed class FakePhysicalEvent
{
    private FakePhysicalEvent(
        FakeTelemetryEvent value,
        FakeComponent parent,
        List<object> parentHistory,
        List<object> outputs,
        List<object> outputHistory)
    {
        Event = value;
        Parent = parent;
        ParentHistory = parentHistory;
        Outputs = outputs;
        OutputHistory = outputHistory;
    }

    internal FakeTelemetryEvent Event { get; }
    internal FakeComponent Parent { get; }
    internal List<object> ParentHistory { get; }
    internal List<object> Outputs { get; }
    internal List<object> OutputHistory { get; }

    internal static FakePhysicalEvent Prepared(string transitionId = "transition-prepared")
    {
        List<object> parentHistory = new() { Collision("collision-prior", FakeOutcome.Penetrated) };
        FakeComponent parent = ParentComponent(parentHistory);
        List<object> outputs = new();
        var value = new FakeTelemetryEvent
        {
            Stage = FakeStage.CollisionPrepared,
            TransitionId = transitionId,
            Outcome = FakeOutcome.Unknown,
            Host = Host(),
            Impact = Impact(),
            Parent = parent,
            Outputs = outputs,
            Conservation = null
        };
        return new FakePhysicalEvent(value, parent, parentHistory, outputs, new List<object>());
    }

    internal static FakePhysicalEvent Resolved(
        string transitionId = "transition-resolved",
        string targetSurfaceIdentity = "fixture/700/plate/0")
    {
        List<object> parentHistory = new() { Collision("collision-prior", FakeOutcome.Penetrated) };
        List<object> outputHistory = new() { Collision("collision-current", FakeOutcome.Fragmented) };
        FakeComponent parent = ParentComponent(parentHistory);
        List<object> outputs = new()
        {
            ParentOutputComponent(outputHistory),
            SpallComponent(outputHistory)
        };
        var value = new FakeTelemetryEvent
        {
            Stage = FakeStage.CollisionResolved,
            TransitionId = transitionId,
            Outcome = FakeOutcome.Fragmented,
            Host = Host(),
            Impact = Impact(targetSurfaceIdentity),
            Parent = parent,
            Outputs = outputs,
            Conservation = new FakeConservation
            {
                ParentMassKilograms = 0.004d,
                AllocatedParentMassKilograms = 0.003d,
                UnallocatedParentMassKilograms = 0.001d,
                TargetSpallMassKilograms = 0.0002d,
                ParentEnergyJoules = 1280d,
                LossBudget = new FakeLossBudget
                {
                    PenetrationLossJoules = 300d,
                    DeformationLossJoules = 100d,
                    FractureLossJoules = 80d,
                    HeatLossJoules = 20d,
                    OtherLossJoules = 10d,
                    TotalLossJoules = 510d
                },
                ModeledLossEnergyJoules = 510d,
                ResidualEnergyJoules = 770d,
                OutputEnergyJoules = 760d,
                EnergyClosureErrorJoules = 10d,
                ParentDerivedOutputCount = 1,
                TargetSpallOutputCount = 1
            }
        };
        return new FakePhysicalEvent(value, parent, parentHistory, outputs, outputHistory);
    }

    private static FakeHost Host()
    {
        return new FakeHost
        {
            RootFireIndex = 17,
            RootRandomSeed = 991,
            CurrentFireIndex = 18,
            CurrentRandomSeed = 992,
            CurrentFragmentIndex = 3,
            ParentDepth = 2,
            RootShooterProfileId = "profile",
            AmmunitionTemplateId = "ammo-template",
            AmmunitionTemplateName = "Test ammunition"
        };
    }

    private static FakeImpact Impact(string targetSurfaceIdentity = "fixture/700/plate/0")
    {
        return new FakeImpact
        {
            PositionMetres = new FakeVector(1.25d, 2.5d, 3.75d),
            SurfaceNormal = new FakeVector(0d, 0d, -1d),
            PhysicalThicknessMetres = 0.0127d,
            EffectivePathLengthMetres = 0.018d,
            TargetProfileId = "target-profile",
            TargetMaterialClass = FakeMaterial.ArmoredSteel,
            TargetSurfaceIdentity = targetSurfaceIdentity,
            TargetDensityKilogramsPerCubicMetre = 7850d,
            TargetResistancePressurePascals = 900000000d,
            ProjectileDeformationCoupling = 0.4d,
            ProjectileFractureCoupling = 0.5d,
            HeatLossFraction = 0.02d
        };
    }

    private static FakeComponent ParentComponent(List<object> history)
    {
        return new FakeComponent
        {
            Kind = FakeKind.IntactProjectile,
            ProjectileId = "projectile-parent",
            RootShotId = "root-shot",
            ParentProjectileId = null,
            SourceProjectileId = null,
            SourceMaterialId = "projectile-steel-core",
            SourceMaterialClass = FakeMaterial.MildSteel,
            SourceCollisionId = null,
            FragmentIndex = 0,
            FragmentGeneration = 0,
            DeterministicSeed = 123456789UL,
            Construction = FakeConstruction.SteelCoreJacketed,
            ShapeClass = FakeShape.Spitzer,
            OriginalMassKilograms = 0.004d,
            RetainedMassKilograms = 0.004d,
            NominalDiameterMetres = 0.00545d,
            DeformedDiameterMetres = 0.0055d,
            ProjectedAreaSquareMetres = 0.000025d,
            EquivalentDiameterMetres = 0.0057d,
            LengthMetres = 0.025d,
            AspectRatio = 4.4d,
            DragCoefficient = 0.3d,
            BallisticCoefficientKilogramsPerSquareMetre = 533.3d,
            PositionMetres = new FakeVector(1d, 2d, 3d),
            VelocityMetresPerSecond = new FakeVector(0d, 0d, 800d),
            SpeedMetresPerSecond = 800d,
            MomentumKilogramMetresPerSecond = new FakeVector(0d, 0d, 3.2d),
            TranslationalKineticEnergyJoules = 1280d,
            Orientation = new FakeOrientation(0d, 0d, 0d, 1d),
            YawAngleRadians = 0.01d,
            TumbleState = FakeTumble.Stable,
            PenetrationCapabilityJoulesPerSquareMetre = 51200000d,
            DamageCapabilityJoules = 1280d,
            TerminalState = FakeTerminal.Continuing,
            RenderState = FakeRender.Visible,
            IsTargetMaterialOrigin = false,
            IsParentDerivedMass = false,
            CollisionHistory = history
        };
    }

    private static FakeComponent SpallComponent(List<object> history)
    {
        FakeComponent component = ParentComponent(history);
        component.Kind = FakeKind.TargetSpall;
        component.ProjectileId = "spall-1";
        component.ParentProjectileId = "projectile-parent";
        component.SourceProjectileId = "projectile-parent";
        component.SourceMaterialId = "target-profile";
        component.SourceMaterialClass = FakeMaterial.ArmoredSteel;
        component.SourceCollisionId = "collision-current";
        component.FragmentIndex = 1;
        component.FragmentGeneration = 1;
        component.Construction = FakeConstruction.TargetMaterial;
        component.ShapeClass = FakeShape.TargetSpallFlake;
        component.OriginalMassKilograms = 0.0002d;
        component.RetainedMassKilograms = 0.0002d;
        component.VelocityMetresPerSecond = new FakeVector(0d, 0d, 1000d);
        component.SpeedMetresPerSecond = 1000d;
        component.MomentumKilogramMetresPerSecond = new FakeVector(0d, 0d, 0.2d);
        component.TranslationalKineticEnergyJoules = 100d;
        component.IsTargetMaterialOrigin = true;
        component.IsParentDerivedMass = false;
        return component;
    }

    private static FakeComponent ParentOutputComponent(List<object> history)
    {
        FakeComponent component = ParentComponent(history);
        const double speed = 663.3249580710799d;
        component.Kind = FakeKind.DeformedProjectile;
        component.ProjectileId = "projectile-output";
        component.ParentProjectileId = "projectile-parent";
        component.SourceProjectileId = "projectile-parent";
        component.SourceCollisionId = "collision-current";
        component.RetainedMassKilograms = 0.003d;
        component.VelocityMetresPerSecond = new FakeVector(0d, 0d, speed);
        component.SpeedMetresPerSecond = speed;
        component.MomentumKilogramMetresPerSecond = new FakeVector(0d, 0d, 0.003d * speed);
        component.TranslationalKineticEnergyJoules = 660d;
        component.IsTargetMaterialOrigin = false;
        component.IsParentDerivedMass = true;
        return component;
    }

    private static FakeCollision Collision(string collisionId, FakeOutcome outcome)
    {
        return new FakeCollision
        {
            CollisionId = collisionId,
            MaterialId = "material",
            MaterialClass = FakeMaterial.ArmoredSteel,
            Sequence = 1,
            PositionMetres = new FakeVector(1d, 2d, 3d),
            IncomingVelocityMetresPerSecond = new FakeVector(0d, 0d, 800d),
            OutgoingVelocityMetresPerSecond = new FakeVector(0d, 0d, 600d),
            IncomingTranslationalEnergyJoules = 1280d,
            OutgoingTranslationalEnergyJoules = 720d,
            ImpactAngleRadians = 0.25d,
            EffectivePathLengthMetres = 0.018d,
            Outcome = outcome
        };
    }
}

internal enum FakeStage { CollisionPrepared, CollisionResolved }
internal enum FakeOutcome { Unknown, Penetrated, Stopped, Deviated, Ricocheted, Fragmented }
internal enum FakeKind { IntactProjectile, DeformedProjectile, ProjectileFragment, TargetSpall, TargetSpallFragment }
internal enum FakeMaterial { Air, MildSteel, ArmoredSteel }
internal enum FakeConstruction { SteelCoreJacketed, TargetMaterial }
internal enum FakeShape { Spitzer, TargetSpallFlake }
internal enum FakeTumble { Stable, Yawing, Tumbling }
internal enum FakeTerminal { Continuing, Exited, Embedded, Stopped }
internal enum FakeRender { NotRendered, Visible, Embedded, Culled, Expired }

internal readonly record struct FakeVector(double X, double Y, double Z);
internal readonly record struct FakeOrientation(double X, double Y, double Z, double W);

internal sealed class FakeTelemetryEvent
{
    public FakeStage Stage { get; init; }
    public string TransitionId { get; init; } = string.Empty;
    public FakeOutcome Outcome { get; init; }
    public FakeHost Host { get; init; } = new();
    public FakeImpact Impact { get; init; } = new();
    public FakeComponent Parent { get; init; } = new();
    public List<object> Outputs { get; init; } = new();
    public FakeConservation? Conservation { get; init; }
}

internal sealed class FakeHost
{
    public int RootFireIndex { get; init; }
    public int RootRandomSeed { get; init; }
    public int CurrentFireIndex { get; init; }
    public int CurrentRandomSeed { get; init; }
    public int CurrentFragmentIndex { get; init; }
    public int ParentDepth { get; init; }
    public string RootShooterProfileId { get; init; } = string.Empty;
    public string AmmunitionTemplateId { get; init; } = string.Empty;
    public string AmmunitionTemplateName { get; init; } = string.Empty;
}

internal sealed class FakeImpact
{
    public FakeVector PositionMetres { get; init; }
    public FakeVector SurfaceNormal { get; init; }
    public double PhysicalThicknessMetres { get; init; }
    public double EffectivePathLengthMetres { get; init; }
    public string TargetProfileId { get; init; } = string.Empty;
    public FakeMaterial TargetMaterialClass { get; init; }
    public string TargetSurfaceIdentity { get; init; } = string.Empty;
    public double TargetDensityKilogramsPerCubicMetre { get; init; }
    public double TargetResistancePressurePascals { get; init; }
    public double ProjectileDeformationCoupling { get; init; }
    public double ProjectileFractureCoupling { get; init; }
    public double HeatLossFraction { get; init; }
}

internal sealed class FakeLossBudget
{
    public double PenetrationLossJoules { get; init; }
    public double DeformationLossJoules { get; init; }
    public double FractureLossJoules { get; init; }
    public double HeatLossJoules { get; init; }
    public double OtherLossJoules { get; init; }
    public double TotalLossJoules { get; init; }
}

internal sealed class FakeConservation
{
    public double ParentMassKilograms { get; init; }
    public double AllocatedParentMassKilograms { get; init; }
    public double UnallocatedParentMassKilograms { get; init; }
    public double TargetSpallMassKilograms { get; init; }
    public double ParentEnergyJoules { get; init; }
    public FakeLossBudget LossBudget { get; init; } = new();
    public double ModeledLossEnergyJoules { get; init; }
    public double ResidualEnergyJoules { get; init; }
    public double OutputEnergyJoules { get; init; }
    public double EnergyClosureErrorJoules { get; init; }
    public int ParentDerivedOutputCount { get; init; }
    public int TargetSpallOutputCount { get; init; }
}

internal sealed class FakeComponent
{
    public FakeKind Kind { get; set; }
    public string ProjectileId { get; set; } = string.Empty;
    public string RootShotId { get; set; } = string.Empty;
    public string? ParentProjectileId { get; set; }
    public string? SourceProjectileId { get; set; }
    public string? SourceMaterialId { get; set; }
    public FakeMaterial SourceMaterialClass { get; set; }
    public string? SourceCollisionId { get; set; }
    public int FragmentIndex { get; set; }
    public int FragmentGeneration { get; set; }
    public ulong DeterministicSeed { get; set; }
    public FakeConstruction Construction { get; set; }
    public FakeShape ShapeClass { get; set; }
    public double OriginalMassKilograms { get; set; }
    public double RetainedMassKilograms { get; set; }
    public double NominalDiameterMetres { get; set; }
    public double DeformedDiameterMetres { get; set; }
    public double ProjectedAreaSquareMetres { get; set; }
    public double EquivalentDiameterMetres { get; set; }
    public double LengthMetres { get; set; }
    public double AspectRatio { get; set; }
    public double DragCoefficient { get; set; }
    public double BallisticCoefficientKilogramsPerSquareMetre { get; set; }
    public FakeVector PositionMetres { get; set; }
    public FakeVector VelocityMetresPerSecond { get; set; }
    public double SpeedMetresPerSecond { get; set; }
    public FakeVector MomentumKilogramMetresPerSecond { get; set; }
    public double TranslationalKineticEnergyJoules { get; set; }
    public FakeOrientation Orientation { get; set; }
    public double YawAngleRadians { get; set; }
    public FakeTumble TumbleState { get; set; }
    public double PenetrationCapabilityJoulesPerSquareMetre { get; set; }
    public double DamageCapabilityJoules { get; set; }
    public FakeTerminal TerminalState { get; set; }
    public FakeRender RenderState { get; set; }
    public bool IsTargetMaterialOrigin { get; set; }
    public bool IsParentDerivedMass { get; set; }
    public List<object> CollisionHistory { get; set; } = new();
}

internal sealed class FakeCollision
{
    public string CollisionId { get; init; } = string.Empty;
    public string MaterialId { get; init; } = string.Empty;
    public FakeMaterial MaterialClass { get; init; }
    public int Sequence { get; init; }
    public FakeVector PositionMetres { get; init; }
    public FakeVector IncomingVelocityMetresPerSecond { get; init; }
    public FakeVector OutgoingVelocityMetresPerSecond { get; init; }
    public double IncomingTranslationalEnergyJoules { get; init; }
    public double OutgoingTranslationalEnergyJoules { get; init; }
    public double ImpactAngleRadians { get; init; }
    public double EffectivePathLengthMetres { get; init; }
    public FakeOutcome Outcome { get; init; }
}
