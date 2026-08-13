using BallisticsLab.Core;
using System.Reflection;

internal static class ProtocolTests
{
    private const string VerifiedAmmo = "verified-ammunition-template";
    private static readonly ProtocolAmmunitionMapping[] VerifiedAmmunitionMappings =
    {
        ProtocolAmmunitionMapping.Installed(
            ProtocolAmmunitionIdentityStatus.ExactDesignation,
            VerifiedAmmo,
            "verified-internal-name",
            "Verified ammunition",
            "VerifiedCaliber",
            0.0079d,
            0.0079d,
            720d,
            "Verified designation",
            "Проверенное обозначение",
            "7.9 gram",
            "Synthetic exact mapping for pure evaluator tests.")
    };
    private static readonly int[] LayerZero = { 0 };

    internal static bool CatalogMatchesPublishedNominalThreatTable()
    {
        IReadOnlyList<ProtocolThreatDefinition> definitions = GostProtocolCatalog.All;
        return definitions.Count == 8
            && ConstantMatches(
                nameof(GostProtocolCatalog.ClassificationStandard),
                "GOST 34286-2017")
            && ConstantMatches(
                nameof(GostProtocolCatalog.TestMethodStandard),
                "GOST R 55623-2013")
            && GostProtocolCatalog.ForClass("Br1").Count == 1
            && GostProtocolCatalog.ForClass("Br2").Count == 1
            && GostProtocolCatalog.ForClass("Br3").Count == 1
            && GostProtocolCatalog.ForClass("Br4").Count == 2
            && GostProtocolCatalog.ForClass("Br5").Count == 2
            && GostProtocolCatalog.ForClass("Br6").Count == 1
            && ThreatMatches(
                "gost-34286-br1-9x18-pst",
                "Br1",
                0.0059d,
                325d,
                345d,
                5d,
                0.1d)
            && ThreatMatches(
                "gost-34286-br4-5.45x39-pp-7n10",
                "Br4",
                0.0035d,
                880d,
                910d,
                10d,
                0.1d)
            && ThreatMatches(
                "gost-34286-br4-7.62x39-ps-57-n-231",
                "Br4",
                0.0079d,
                705d,
                735d,
                10d,
                0.1d)
            && ThreatMatches(
                "gost-34286-br5-7.62x54-pp-7n13",
                "Br5",
                0.0094d,
                815d,
                845d,
                10d,
                0.1d)
            && ThreatMatches(
                "gost-34286-br5-7.62x54-b32-7-bz-3",
                "Br5",
                0.0104d,
                795d,
                825d,
                10d,
                0.1d)
            && ThreatMatches(
                "gost-34286-br6-12.7x108-b32-57-bz-542",
                "Br6",
                0.0482d,
                810d,
                850d,
                50d,
                0.5d)
            && definitions.All(definition => definition.RequiredQualifyingShots == 5)
            && definitions.All(definition => Nearly(
                definition.VelocityMeasurementDistanceMetres,
                3d))
            && definitions.All(definition => Nearly(
                definition.MaximumImpactAngleDegrees,
                5d))
            && definitions.All(definition => Nearly(
                definition.MinimumSeparationDiameters,
                5d))
            && definitions.All(definition => definition.SimulationScreeningOnly)
            && definitions.Sum(definition => definition.AmmunitionMappings.Count) == 8
            && definitions.Sum(definition => definition.AmmunitionMappings.Count(mapping =>
                mapping.IdentityStatus == ProtocolAmmunitionIdentityStatus.ExactDesignation)) == 5
            && definitions.Sum(definition => definition.AmmunitionMappings.Count(mapping =>
                mapping.IdentityStatus == ProtocolAmmunitionIdentityStatus.VariantDesignation)) == 1
            && definitions.Sum(definition => definition.AmmunitionMappings.Count(mapping =>
                mapping.IdentityStatus
                    == ProtocolAmmunitionIdentityStatus.NotAvailableInSupportedDatabase)) == 2
            && definitions.Sum(definition => definition.VerifiedAmmunitionTemplateIds.Count) == 5;
    }

    internal static bool ShotEvidenceRejectsInvalidFaceGeometry()
    {
        ProtocolShotEvidence valid = Shot(
            1L,
            ProtocolVelocityMeasurementBasis.TargetImpactProxy,
            720d,
            0.49d,
            0.74d,
            false);
        bool outsideRejected = Throws<ArgumentOutOfRangeException>(() => _ = new ProtocolShotEvidence(
            1L,
            VerifiedAmmo,
            ProtocolVelocityMeasurementBasis.TargetImpactProxy,
            720d,
            0.0079d,
            0.00762d,
            0d,
            0.51d,
            0d,
            1d,
            1.5d,
            10d,
            true,
            false));
        bool diameterRejected = Throws<ArgumentOutOfRangeException>(() => _ = new ProtocolShotEvidence(
            1L,
            VerifiedAmmo,
            ProtocolVelocityMeasurementBasis.TargetImpactProxy,
            720d,
            0.0079d,
            0d,
            0d,
            0d,
            0d,
            1d,
            1.5d,
            10d,
            true,
            false));
        return Nearly(valid.DistanceToNearestEdgeMetres, 0.01d)
            && outsideRejected
            && diameterRejected;
    }

    internal static bool MappingCollectionsAreDetachedAndRejectDuplicates()
    {
        ProtocolAmmunitionMapping mapping = VerifiedAmmunitionMappings[0];
        var source = new[] { mapping };
        ProtocolThreatDefinition threat = SyntheticThreat(source);
        source[0] = ProtocolAmmunitionMapping.Unavailable("Mutation sentinel.");
        bool duplicateRejected = Throws<ArgumentException>(() => _ = SyntheticThreat(
            new[] { mapping, mapping }));
        return ReferenceEquals(threat.AmmunitionMappings[0], mapping)
            && threat.VerifiedAmmunitionTemplateIds.SequenceEqual(
                new[] { VerifiedAmmo },
                StringComparer.Ordinal)
            && duplicateRejected;
    }

    internal static bool TargetImpactProxyCannotCompleteScreening()
    {
        ProtocolThreatDefinition threat = SyntheticThreat();
        IReadOnlyList<ProtocolShotEvidence> shots = Pattern(
            ProtocolVelocityMeasurementBasis.TargetImpactProxy,
            720d,
            false);
        ProtocolScreeningEvaluation result = ProtocolScreeningEvaluator.Evaluate(threat, shots);
        return result.Status == ProtocolScreeningStatus.InsufficientEvidence
            && result.ObservedOutcome == ProtocolObservedOutcome.NotDetermined
            && result.QualifyingShotCount == 0
            && result.Shots.All(shot => shot.Reason
                == ProtocolShotQualificationReason.MeasurementBasisMismatch)
            && !result.IsCertificationClaim;
    }

    internal static bool UnverifiedCatalogAmmunitionCannotCompleteScreening()
    {
        ProtocolThreatDefinition threat = GostProtocolCatalog.ForClass("Br5")[0];
        IReadOnlyList<ProtocolShotEvidence> shots = Pattern(
            ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres,
            830d,
            false,
            ammunitionTemplateId: "unavailable-7n13");
        ProtocolScreeningEvaluation result = ProtocolScreeningEvaluator.Evaluate(threat, shots);
        return result.Status == ProtocolScreeningStatus.InsufficientEvidence
            && result.QualifyingShotCount == 0
            && result.Shots.All(shot => shot.Reason
                == ProtocolShotQualificationReason.AmmunitionIdentityUnverified);
    }

    internal static bool VariantDesignationCannotCompleteScreening()
    {
        ProtocolThreatDefinition threat = GostProtocolCatalog.ForClass("Br1")[0];
        IReadOnlyList<ProtocolShotEvidence> shots = Pattern(
            ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres,
            335d,
            false,
            ammunitionTemplateId: "5737201124597760fc4431f1",
            projectileMassKilograms: 0.0059d,
            projectileDiameterMetres: 0.00927d,
            fixtureDistanceMetres: 5d);
        ProtocolScreeningEvaluation result = ProtocolScreeningEvaluator.Evaluate(threat, shots);
        return result.Status == ProtocolScreeningStatus.InsufficientEvidence
            && result.QualifyingShotCount == 0
            && result.Shots.All(shot => shot.Reason
                == ProtocolShotQualificationReason.AmmunitionDesignationVariant);
    }

    internal static bool ExactCatalogMappingCanCompleteSimulationScreening()
    {
        ProtocolThreatDefinition threat = GostProtocolCatalog.ForClass("Br4")[1];
        IReadOnlyList<ProtocolShotEvidence> shots = Pattern(
            ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres,
            720d,
            false,
            ammunitionTemplateId: "5656d7c34bdc2d9d198b4587");
        ProtocolScreeningEvaluation result = ProtocolScreeningEvaluator.Evaluate(threat, shots);
        return result.Status == ProtocolScreeningStatus.SimulationScreeningComplete
            && result.QualifyingShotCount == 5
            && !result.HasKnownNominalMassMismatch
            && !result.HasInstalledLocaleMassMismatch
            && !result.IsCertificationClaim;
    }

    internal static bool ExactIdentityReportsKnownGameRepresentationMismatches()
    {
        ProtocolThreatDefinition threat = GostProtocolCatalog.ForClass("Br4")[0];
        IReadOnlyList<ProtocolShotEvidence> shots = Pattern(
            ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres,
            895d,
            false,
            ammunitionTemplateId: "56dff2ced2720bb4668b4567",
            projectileMassKilograms: 0.00368d,
            projectileDiameterMetres: 0.00545d);
        ProtocolScreeningEvaluation result = ProtocolScreeningEvaluator.Evaluate(threat, shots);
        return result.Status == ProtocolScreeningStatus.SimulationScreeningComplete
            && result.QualifyingShotCount == 5
            && result.HasKnownNominalMassMismatch
            && result.HasInstalledLocaleMassMismatch
            && !result.IsCertificationClaim;
    }

    internal static bool RecordedMassMustMatchMappedGameRepresentation()
    {
        ProtocolThreatDefinition threat = GostProtocolCatalog.ForClass("Br4")[0];
        IReadOnlyList<ProtocolShotEvidence> shots = Pattern(
            ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres,
            895d,
            false,
            ammunitionTemplateId: "56dff2ced2720bb4668b4567",
            projectileMassKilograms: 0.0035d,
            projectileDiameterMetres: 0.00545d);
        ProtocolScreeningEvaluation result = ProtocolScreeningEvaluator.Evaluate(threat, shots);
        return result.Status == ProtocolScreeningStatus.InsufficientEvidence
            && result.QualifyingShotCount == 0
            && result.Shots.All(shot => shot.Reason
                == ProtocolShotQualificationReason.AmmunitionPhysicalStateMismatch);
    }

    internal static bool ValidFiveShotPatternCompletesSimulationScreening()
    {
        ProtocolScreeningEvaluation result = ProtocolScreeningEvaluator.Evaluate(
            SyntheticThreat(),
            Pattern(ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres, 720d, false));
        return result.Status == ProtocolScreeningStatus.SimulationScreeningComplete
            && result.QualifyingShotCount == 5
            && result.ObservedOutcome
                == ProtocolObservedOutcome.NoThroughPenetrationObserved
            && result.Shots.All(shot => shot.IsQualifying)
            && !result.IsCertificationClaim;
    }

    internal static bool ExtraIncompleteObservationDoesNotInvalidateCompleteScreening()
    {
        var shots = new List<ProtocolShotEvidence>(
            Pattern(ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres, 720d, false))
        {
            Shot(
                1L,
                ProtocolVelocityMeasurementBasis.TargetImpactProxy,
                720d,
                0.20d,
                0d,
                false)
        };
        ProtocolScreeningEvaluation result = ProtocolScreeningEvaluator.Evaluate(
            SyntheticThreat(),
            shots);
        return result.Status == ProtocolScreeningStatus.SimulationScreeningComplete
            && result.QualifyingShotCount == 5
            && result.Shots[5].Reason
                == ProtocolShotQualificationReason.MeasurementBasisMismatch;
    }

    internal static bool VelocitySeverityExceptionsAreApplied()
    {
        var shots = new List<ProtocolShotEvidence>
        {
            Shot(1L, ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres, 690d, -0.10d, 0d, true),
            Shot(1L, ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres, 750d, 0.10d, 0d, false),
            Shot(1L, ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres, 720d, 0d, 0d, false),
            Shot(1L, ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres, 720d, 0d, 0.10d, false),
            Shot(1L, ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres, 720d, 0d, -0.10d, false)
        };
        ProtocolScreeningEvaluation result = ProtocolScreeningEvaluator.Evaluate(
            SyntheticThreat(),
            shots);
        return result.Status == ProtocolScreeningStatus.SimulationScreeningComplete
            && result.QualifyingShotCount == 5
            && result.ObservedOutcome == ProtocolObservedOutcome.ThroughPenetrationObserved;
    }

    internal static bool OppositeVelocityExceptionsRemainNonQualifying()
    {
        var shots = new List<ProtocolShotEvidence>
        {
            Shot(1L, ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres, 690d, -0.10d, 0d, false),
            Shot(1L, ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres, 750d, 0.10d, 0d, true)
        };
        ProtocolScreeningEvaluation result = ProtocolScreeningEvaluator.Evaluate(
            SyntheticThreat(),
            shots);
        return result.Status == ProtocolScreeningStatus.NonQualifyingEvidence
            && result.QualifyingShotCount == 0
            && result.Shots.All(shot => shot.Reason
                == ProtocolShotQualificationReason.VelocityOutOfRange);
    }

    internal static bool EdgeSpacingAndFixtureIdentityAreEnforced()
    {
        var shots = new List<ProtocolShotEvidence>
        {
            Shot(1L, ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres, 720d, 0d, 0d, false),
            Shot(1L, ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres, 720d, 0.02d, 0d, false),
            Shot(1L, ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres, 720d, 0.49d, 0d, false),
            Shot(2L, ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres, 720d, 0.10d, 0d, false)
        };
        ProtocolScreeningEvaluation result = ProtocolScreeningEvaluator.Evaluate(
            SyntheticThreat(),
            shots);
        return result.Status == ProtocolScreeningStatus.NonQualifyingEvidence
            && result.QualifyingShotCount == 1
            && result.Shots[0].Reason == ProtocolShotQualificationReason.Qualifying
            && result.Shots[1].Reason
                == ProtocolShotQualificationReason.NeighbourDistanceInsufficient
            && result.Shots[2].Reason
                == ProtocolShotQualificationReason.EdgeDistanceInsufficient
            && result.Shots[3].Reason == ProtocolShotQualificationReason.FixtureMismatch;
    }

    internal static bool CampaignEvidenceRejectsMismatchedProtocolIdentity()
    {
        ProtocolShotEvidence protocol = Shot(
            2L,
            ProtocolVelocityMeasurementBasis.TargetImpactProxy,
            720d,
            0d,
            0d,
            false);
        return Throws<ArgumentException>(() => _ = new CampaignShotEvidence(
            1L,
            "chain",
            "fixture-template",
            "ArmoredSteel",
            4,
            1,
            1,
            2,
            "profile",
            VerifiedAmmo,
            1d,
            LayerZero,
            "STOPPED",
            false,
            0,
            0,
            0d,
            0d,
            protocol));
    }

    private static ProtocolThreatDefinition SyntheticThreat()
    {
        return SyntheticThreat(VerifiedAmmunitionMappings);
    }

    private static ProtocolThreatDefinition SyntheticThreat(
        IReadOnlyList<ProtocolAmmunitionMapping> mappings)
    {
        return new ProtocolThreatDefinition(
            "synthetic-threat",
            "Screening",
            "Synthetic cartridge",
            "Synthetic projectile",
            0.0079d,
            705d,
            735d,
            10d,
            0.1d,
            3d,
            5,
            5d,
            5d,
            mappings);
    }

    private static ProtocolShotEvidence[] Pattern(
        ProtocolVelocityMeasurementBasis measurementBasis,
        double velocity,
        bool throughPenetration,
        string ammunitionTemplateId = VerifiedAmmo,
        double projectileMassKilograms = 0.0079d,
        double projectileDiameterMetres = 0.00762d,
        double fixtureDistanceMetres = 10d)
    {
        return new[]
        {
            Shot(1L, measurementBasis, velocity, 0d, 0d, throughPenetration, ammunitionTemplateId, projectileMassKilograms, projectileDiameterMetres, fixtureDistanceMetres),
            Shot(1L, measurementBasis, velocity, -0.10d, 0d, throughPenetration, ammunitionTemplateId, projectileMassKilograms, projectileDiameterMetres, fixtureDistanceMetres),
            Shot(1L, measurementBasis, velocity, 0.10d, 0d, throughPenetration, ammunitionTemplateId, projectileMassKilograms, projectileDiameterMetres, fixtureDistanceMetres),
            Shot(1L, measurementBasis, velocity, 0d, -0.10d, throughPenetration, ammunitionTemplateId, projectileMassKilograms, projectileDiameterMetres, fixtureDistanceMetres),
            Shot(1L, measurementBasis, velocity, 0d, 0.10d, throughPenetration, ammunitionTemplateId, projectileMassKilograms, projectileDiameterMetres, fixtureDistanceMetres)
        };
    }

    private static ProtocolShotEvidence Shot(
        long fixtureId,
        ProtocolVelocityMeasurementBasis measurementBasis,
        double velocity,
        double localX,
        double localY,
        bool throughPenetration,
        string ammunitionTemplateId = VerifiedAmmo,
        double projectileMassKilograms = 0.0079d,
        double projectileDiameterMetres = 0.00762d,
        double fixtureDistanceMetres = 10d)
    {
        return new ProtocolShotEvidence(
            fixtureId,
            ammunitionTemplateId,
            measurementBasis,
            velocity,
            projectileMassKilograms,
            projectileDiameterMetres,
            0d,
            localX,
            localY,
            1d,
            1.5d,
            fixtureDistanceMetres,
            true,
            throughPenetration);
    }

    private static bool ThreatMatches(
        string threatId,
        string protectionClass,
        double mass,
        double minimumVelocity,
        double maximumVelocity,
        double distance,
        double distanceTolerance)
    {
        return GostProtocolCatalog.TryGet(threatId, out ProtocolThreatDefinition? definition)
            && definition != null
            && definition.ProtectionClass == protectionClass
            && Nearly(definition.NominalProjectileMassKilograms, mass)
            && Nearly(definition.MinimumVelocityMetresPerSecond, minimumVelocity)
            && Nearly(definition.MaximumVelocityMetresPerSecond, maximumVelocity)
            && Nearly(definition.TestDistanceMetres, distance)
            && Nearly(definition.TestDistanceToleranceMetres, distanceTolerance);
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

    private static bool ConstantMatches(string fieldName, string expected)
    {
        FieldInfo? field = typeof(GostProtocolCatalog).GetField(
            fieldName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return field?.IsLiteral == true
            && string.Equals(field.GetRawConstantValue() as string, expected, StringComparison.Ordinal);
    }
}
