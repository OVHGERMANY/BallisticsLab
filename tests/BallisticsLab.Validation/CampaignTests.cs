using System.Text.Json;
using System.Text.Json.Nodes;
using BallisticsLab.Core;
using BallisticsLab.Validation;

internal static class CampaignTests
{
    private static readonly int[] LayerZero = { 0 };
    private static readonly int[] LayersZeroOne = { 0, 1 };
    private static readonly int[] LayersZeroOneTwo = { 0, 1, 2 };
    private static readonly int[] UnorderedDuplicateLayers = { 2, 0, 2, 1 };
    private static readonly string[] PhysicalMaterials =
    {
        "ArmoredSteel",
        "Ceramic",
        "UHMWPE",
        "Titan",
        "Aluminium",
        "Aramid",
        "Combined"
    };
    private static readonly string[] RunnableProtocolThreatIds =
    {
        "gost-34286-br3-9x19-pst-7n21",
        "gost-34286-br4-5.45x39-pp-7n10",
        "gost-34286-br4-7.62x39-ps-57-n-231",
        "gost-34286-br6-12.7x108-b32-57-bz-542"
    };

    internal static bool SeedDerivationIsStableAndCaseSpecific()
    {
        const ulong runSeed = 0x1122334455667788UL;
        ulong first = CampaignSeedDeriver.Derive(runSeed, "fixture-baseline", "steel-c6", 0);
        ulong repeated = CampaignSeedDeriver.Derive(runSeed, "fixture-baseline", "steel-c6", 0);
        ulong nextRepetition = CampaignSeedDeriver.Derive(
            runSeed,
            "fixture-baseline",
            "steel-c6",
            1);
        ulong nextCase = CampaignSeedDeriver.Derive(runSeed, "fixture-baseline", "steel-c3", 0);
        return first == repeated
            && first != 0UL
            && first != nextRepetition
            && first != nextCase
            && nextRepetition != nextCase;
    }

    internal static bool EvidenceRevisionRemainsMonotonicAcrossCampaignResets()
    {
        var revision = new CampaignEvidenceRevisionTracker();
        DateTime first = new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);
        DateTime second = first.AddSeconds(1d);
        long firstRevision = revision.Mark(first);
        revision.ResetTimestamp();
        bool preserved = revision.Revision == firstRevision
            && revision.LastUpdatedUtc == DateTime.MinValue;
        long secondRevision = revision.Mark(second);
        return firstRevision == 1L
            && preserved
            && secondRevision == 2L
            && revision.LastUpdatedUtc == second;
    }

    internal static bool DefinitionRejectsDuplicateCaseIdentity()
    {
        CampaignCaseDefinition campaignCase = Case(
            "duplicate",
            repetitions: 1,
            CampaignResetPolicy.BeforeEachCase);
        try
        {
            _ = new CampaignDefinition(
                "duplicate-campaign",
                "Duplicate campaign",
                new List<CampaignCaseDefinition> { campaignCase, campaignCase });
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    internal static bool EvidenceCanonicalizesHitLayers()
    {
        CampaignShotEvidence evidence = Evidence(
            fixtureId: 1L,
            chainId: "canonical",
            velocityFraction: 1d,
            layers: UnorderedDuplicateLayers,
            fixtureLayerCount: 3);
        return evidence.HitLayers.SequenceEqual(LayersZeroOneTwo);
    }

    internal static bool EvidenceRejectsLayersOutsideRecordedFixture()
    {
        try
        {
            _ = Evidence(
                fixtureId: 1L,
                chainId: "outside-fixture",
                velocityFraction: 1d,
                layers: LayersZeroOne,
                fixtureLayerCount: 1);
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return true;
        }
    }

    internal static bool ConservationRequirementCannotExistWithoutPhysicalEvidence()
    {
        try
        {
            _ = new CampaignCaseDefinition(
                "invalid-conservation",
                "Invalid conservation requirement",
                CampaignFixtureSelectorKind.MaterialAndArmorClass,
                string.Empty,
                "ArmoredSteel",
                4,
                1,
                0.15d,
                0.0127d,
                8d,
                0d,
                true,
                1,
                CampaignResetPolicy.BeforeEachShot,
                0.5d,
                1.5d,
                false,
                false,
                true,
                0.000001d,
                1d);
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    internal static bool TrackerEnforcesResetAndCompletesInOrder()
    {
        CampaignDefinition definition = Definition();
        var tracker = new CampaignRunTracker(definition, 0x12345678UL);
        if (!tracker.Start()
            || tracker.Snapshot().State != CampaignRunState.AwaitingFixture
            || !tracker.AttachFixture(100L))
        {
            return false;
        }

        CampaignAttemptRecord? wrongFixture = tracker.RecordShot(
            Evidence(999L, "wrong-fixture", 1d, LayerZero));
        CampaignAttemptRecord? velocityRejected = tracker.RecordShot(
            Evidence(100L, "velocity-rejected", 0.4d, LayerZero));
        if (wrongFixture != null
            || velocityRejected?.Status != CampaignAttemptStatus.VelocityOutOfRange
            || tracker.Snapshot().State != CampaignRunState.AwaitingReset
            || tracker.ConfirmReset(999L)
            || !tracker.ConfirmReset(100L))
        {
            return false;
        }

        CampaignAttemptRecord? firstAccepted = tracker.RecordShot(
            Evidence(100L, "first-accepted", 1d, LayerZero));
        if (firstAccepted?.Status != CampaignAttemptStatus.Accepted
            || tracker.Snapshot().State != CampaignRunState.AwaitingReset
            || tracker.RecordShot(Evidence(100L, "first-accepted", 1d, LayerZero)) != null
            || !tracker.ConfirmReset(100L))
        {
            return false;
        }

        CampaignAttemptRecord? secondAccepted = tracker.RecordShot(
            Evidence(100L, "second-accepted", 1.1d, LayerZero));
        CampaignRunSnapshot afterFirstCase = tracker.Snapshot();
        if (secondAccepted?.Status != CampaignAttemptStatus.Accepted
            || afterFirstCase.State != CampaignRunState.AwaitingFixture
            || afterFirstCase.CurrentCaseIndex != 1
            || !tracker.AttachFixture(200L))
        {
            return false;
        }

        CampaignAttemptRecord? incomplete = tracker.RecordShot(
            Evidence(
                200L,
                "incomplete",
                1d,
                LayerZero,
                fixtureArmorClass: 3,
                fixtureLayerCount: 2));
        CampaignAttemptRecord? finalAccepted = tracker.RecordShot(
            Evidence(
                200L,
                "final",
                1d,
                LayersZeroOne,
                fixtureArmorClass: 3,
                fixtureLayerCount: 2));
        CampaignRunSnapshot completed = tracker.Snapshot();
        return incomplete?.Status == CampaignAttemptStatus.IncompleteLayerChain
            && finalAccepted?.Status == CampaignAttemptStatus.Accepted
            && completed.State == CampaignRunState.Completed
            && completed.Attempts.Count == 5
            && velocityRejected.LabCaseSeed == firstAccepted.LabCaseSeed
            && firstAccepted.LabCaseSeed != secondAccepted.LabCaseSeed;
    }

    internal static bool TrackerAppliesBackstopPhysicalAndConservationGates()
    {
        CampaignCaseDefinition gated = new CampaignCaseDefinition(
            "gated",
            "Gated evidence",
            CampaignFixtureSelectorKind.MaterialAndArmorClass,
            string.Empty,
            "ArmoredSteel",
            4,
            1,
            0.15d,
            0.0127d,
            8d,
            0d,
            true,
            1,
            CampaignResetPolicy.BeforeEachCase,
            0.8d,
            1.2d,
            true,
            true,
            true,
            0.000001d,
            0.01d);
        var tracker = new CampaignRunTracker(
            new CampaignDefinition(
                "gates",
                "Gates",
                new List<CampaignCaseDefinition> { gated }),
            5UL);
        tracker.Start();
        tracker.AttachFixture(300L);

        CampaignAttemptRecord? noBackstop = tracker.RecordShot(
            Evidence(
                300L,
                "no-backstop",
                1d,
                LayerZero,
                fixtureArmorClass: 4));
        CampaignAttemptRecord? noPhysical = tracker.RecordShot(
            Evidence(
                300L,
                "no-physical",
                1d,
                LayerZero,
                reachedBackstop: true,
                fixtureArmorClass: 4));
        CampaignAttemptRecord? brokenClosure = tracker.RecordShot(
            Evidence(
                300L,
                "broken-closure",
                1d,
                LayerZero,
                reachedBackstop: true,
                physicalTransitionCount: 1,
                massClosureError: 0.001d,
                fixtureArmorClass: 4));
        CampaignAttemptRecord? missingConservation = tracker.RecordShot(
            Evidence(
                300L,
                "missing-conservation",
                1d,
                LayerZero,
                reachedBackstop: true,
                physicalTransitionCount: 1,
                conservationRecordCount: 0,
                fixtureArmorClass: 4));
        CampaignAttemptRecord? accepted = tracker.RecordShot(
            Evidence(
                300L,
                "accepted",
                1d,
                LayerZero,
                reachedBackstop: true,
                physicalTransitionCount: 1,
                massClosureError: 0.0000001d,
                energyClosureError: 0.001d,
                fixtureArmorClass: 4));
        return noBackstop?.Status == CampaignAttemptStatus.MissingBackstop
            && noPhysical?.Status == CampaignAttemptStatus.MissingPhysicalEvidence
            && brokenClosure?.Status == CampaignAttemptStatus.ConservationFailure
            && missingConservation?.Status == CampaignAttemptStatus.MissingConservationEvidence
            && accepted?.Status == CampaignAttemptStatus.Accepted
            && tracker.Snapshot().State == CampaignRunState.Completed;
    }

    internal static bool SnapshotDoesNotChangeAfterLaterAttempts()
    {
        CampaignDefinition definition = new CampaignDefinition(
            "snapshot",
            "Snapshot",
            new List<CampaignCaseDefinition>
            {
                Case("single", 2, CampaignResetPolicy.BeforeEachCase)
            });
        var tracker = new CampaignRunTracker(definition, 9UL);
        tracker.Start();
        tracker.AttachFixture(400L);
        tracker.RecordShot(Evidence(400L, "first", 1d, LayerZero));
        CampaignRunSnapshot before = tracker.Snapshot();
        tracker.RecordShot(Evidence(400L, "second", 1d, LayerZero));
        return before.Attempts.Count == 1
            && before.State == CampaignRunState.AwaitingShot
            && tracker.Snapshot().Attempts.Count == 2;
    }

    internal static bool MatrixSummarizesAcceptedAndRejectedAttempts()
    {
        CampaignDefinition definition = Definition();
        var tracker = new CampaignRunTracker(definition, 10UL);
        tracker.Start();
        tracker.AttachFixture(500L);
        tracker.RecordShot(Evidence(500L, "bad-speed", 0.2d, LayerZero));
        tracker.ConfirmReset(500L);
        tracker.RecordShot(Evidence(500L, "good-one", 1d, LayerZero));
        tracker.ConfirmReset(500L);
        tracker.RecordShot(Evidence(500L, "good-two", 1.2d, LayerZero));
        tracker.AttachFixture(600L);
        tracker.RecordShot(
            Evidence(
                600L,
                "bad-layer",
                1d,
                LayerZero,
                fixtureArmorClass: 3,
                fixtureLayerCount: 2));
        tracker.RecordShot(
            Evidence(
                600L,
                "good-final",
                1d,
                LayersZeroOne,
                fixtureArmorClass: 3,
                fixtureLayerCount: 2));

        CampaignResultMatrix matrix = CampaignResultMatrixBuilder.Build(
            definition,
            tracker.Snapshot());
        return matrix.Complete
            && matrix.State == CampaignRunState.Completed
            && matrix.AttemptCount == 5
            && matrix.AcceptedCount == 3
            && matrix.Rows.Count == 2
            && matrix.Rows[0].FixtureRejectCount == 0
            && matrix.Rows[0].VelocityRejectCount == 1
            && matrix.Rows[0].AcceptedCount == 2
            && Math.Abs(matrix.Rows[0].MeanVelocityFraction - 0.8d) < 0.000000001d
            && matrix.Rows[1].LayerRejectCount == 1
            && Math.Abs(matrix.Rows[1].MeanLayersHit - 1.5d) < 0.000000001d;
    }

    internal static bool TrackerRejectsWrongFixtureSelectorBeforeOtherGates()
    {
        var exactCase = new CampaignCaseDefinition(
            "exact-template",
            "Exact template",
            CampaignFixtureSelectorKind.ExactTemplate,
            "expected-template",
            string.Empty,
            0,
            1,
            0.15d,
            0.0127d,
            8d,
            0d,
            true,
            1,
            CampaignResetPolicy.BeforeEachCase,
            0.5d,
            1.5d,
            false,
            false,
            false,
            1d,
            100d);
        CampaignDefinition definition = new CampaignDefinition(
            "exact-fixture",
            "Exact fixture",
            new List<CampaignCaseDefinition> { exactCase });
        var tracker = new CampaignRunTracker(definition, 11UL);
        tracker.Start();
        tracker.AttachFixture(610L);

        CampaignAttemptRecord? wrong = tracker.RecordShot(
            Evidence(
                610L,
                "wrong-template",
                1d,
                LayerZero,
                fixtureTemplateId: "wrong-template"));
        CampaignAttemptRecord? accepted = tracker.RecordShot(
            Evidence(
                610L,
                "matching-template",
                1d,
                LayerZero,
                fixtureTemplateId: "expected-template"));
        CampaignResultMatrix matrix = CampaignResultMatrixBuilder.Build(
            definition,
            tracker.Snapshot());
        return wrong?.Status == CampaignAttemptStatus.FixtureMismatch
            && accepted?.Status == CampaignAttemptStatus.Accepted
            && tracker.Snapshot().State == CampaignRunState.Completed
            && matrix.Rows[0].FixtureRejectCount == 1;
    }

    internal static bool BuiltInCatalogsDeclareExpectedCasesAndEvidenceRules()
    {
        CampaignDefinition baseline = CampaignCatalog.ControlledFixtureBaseline();
        CampaignDefinition physical = CampaignCatalog.PhysicalMaterialMatrix();
        IReadOnlyList<ProtocolThreatDefinition> protocolThreats =
            CampaignCatalog.ProtocolScreeningThreats;
        return baseline.Cases.Count == 10
            && baseline.RequiredRepetitions == 10
            && baseline.Cases[0].CaseId == "steel-c6-one"
            && baseline.Cases[7].LayerSpacingMetres == 0.30d
            && baseline.Cases[8].TemplateId == "65573fa5655447403702a816"
            && baseline.Cases.All(campaignCase => !campaignCase.RequirePhysicalEvidence)
            && physical.Cases.Count == 7
            && physical.RequiredRepetitions == 21
            && physical.Cases.Select(campaignCase => campaignCase.Material).SequenceEqual(
                PhysicalMaterials,
                StringComparer.Ordinal)
            && physical.Cases.Single(campaignCase => campaignCase.Material == "Aramid").ArmorClass == 2
            && physical.Cases.Where(campaignCase => campaignCase.Material != "Aramid")
                .All(campaignCase => campaignCase.ArmorClass == 4)
            && physical.Cases.All(campaignCase => campaignCase.RequirePhysicalEvidence)
            && physical.Cases.All(campaignCase => campaignCase.RequireConservationEvidence)
            && protocolThreats.Count == 4
            && protocolThreats.Select(threat => threat.ThreatId).SequenceEqual(
                RunnableProtocolThreatIds,
                StringComparer.Ordinal)
            && protocolThreats.All(threat => threat.AmmunitionMappings.Count(mapping =>
                mapping.CanQualifySimulationScreening) == 1)
            && protocolThreats.All(threat =>
                CampaignCatalog.GostSimulationScreening(threat.ThreatId).Cases.Single()
                    .IsProtocolSequence)
            && RejectsUnavailableProtocolCampaign();
    }

    internal static bool ProtocolTrackerPreservesOneSampleAcrossFiveAcceptedShots()
    {
        CampaignDefinition definition = CampaignCatalog.GostSimulationScreening(
            "gost-34286-br4-7.62x39-ps-57-n-231");
        CampaignCaseDefinition campaignCase = definition.Cases[0];
        var tracker = new CampaignRunTracker(definition, 71UL);
        if (!tracker.Start() || !tracker.AttachFixture(1000L))
        {
            return false;
        }
        for (int index = 0; index < 5; index++)
        {
            CampaignAttemptRecord? attempt = tracker.RecordShot(
                ProtocolEvidence(campaignCase, 1000L, "protocol-" + index, index));
            CampaignRunSnapshot snapshot = tracker.Snapshot();
            if (attempt?.Status != CampaignAttemptStatus.Accepted
                || attempt.SampleOrdinal != 0
                || attempt.RepetitionIndex != index
                || attempt.ProtocolQualificationReason
                    != ProtocolShotQualificationReason.Qualifying
                || (index < 4
                    && (snapshot.State != CampaignRunState.AwaitingShot
                        || snapshot.CurrentFixtureId != 1000L
                        || snapshot.CurrentRepetitionIndex != index + 1)))
            {
                return false;
            }
        }
        CampaignRunSnapshot completed = tracker.Snapshot();
        CampaignResultMatrix matrix = CampaignResultMatrixBuilder.Build(definition, completed);
        return completed.State == CampaignRunState.Completed
            && completed.Attempts.Count == 5
            && completed.Attempts.All(attempt => attempt.Evidence.FixtureId == 1000L)
            && matrix.AcceptedCount == 5
            && matrix.Complete;
    }

    internal static bool ProtocolRejectionInvalidatesPartialSampleAndRoundTripsEvidence()
    {
        CampaignDefinition definition = CampaignCatalog.GostSimulationScreening(
            "gost-34286-br4-7.62x39-ps-57-n-231");
        CampaignCaseDefinition campaignCase = definition.Cases[0];
        var tracker = new CampaignRunTracker(definition, 72UL);
        tracker.Start();
        tracker.AttachFixture(1100L);
        tracker.RecordShot(ProtocolEvidence(campaignCase, 1100L, "first-a", 0));
        tracker.RecordShot(ProtocolEvidence(campaignCase, 1100L, "first-b", 1));
        CampaignAttemptRecord? rejected = tracker.RecordShot(
            ProtocolEvidence(campaignCase, 1100L, "first-rejected", 0));
        CampaignRunSnapshot restarted = tracker.Snapshot();
        if (rejected?.Status != CampaignAttemptStatus.ProtocolRejected
            || rejected.ProtocolQualificationReason
                != ProtocolShotQualificationReason.ExpectedPointMismatch
            || restarted.State != CampaignRunState.AwaitingFixture
            || restarted.CurrentFixtureId != 0L
            || restarted.CurrentRepetitionIndex != 0
            || restarted.Attempts.Take(2).Any(attempt =>
                attempt.Status != CampaignAttemptStatus.SequenceInvalidated)
            || tracker.ConfirmReset(1100L)
            || !tracker.AttachFixture(1200L))
        {
            return false;
        }
        for (int index = 0; index < 5; index++)
        {
            CampaignAttemptRecord? attempt = tracker.RecordShot(
                ProtocolEvidence(campaignCase, 1200L, "second-" + index, index));
            if (attempt?.Status != CampaignAttemptStatus.Accepted
                || attempt.SampleOrdinal != 1)
            {
                return false;
            }
        }

        CampaignRunSnapshot completed = tracker.Snapshot();
        CampaignResultMatrix matrix = CampaignResultMatrixBuilder.Build(definition, completed);
        string report = PhysicalReportDocumentWriter.Build(
            "[]",
            Array.Empty<PhysicalTransitionRecord>(),
            definition,
            completed);
        using JsonDocument document = JsonDocument.Parse(report);
        JsonElement campaign = document.RootElement.GetProperty("campaign");
        JsonElement definitionElement = campaign.GetProperty("cases")[0];
        JsonElement firstAttempt = campaign.GetProperty("attempts")[0];
        JsonElement rejectedAttempt = campaign.GetProperty("attempts")[2];
        return completed.State == CampaignRunState.Completed
            && completed.Attempts.Count == 8
            && matrix.AcceptedCount == 5
            && matrix.Rows[0].RejectedCount == 3
            && matrix.Rows[0].ProtocolRejectCount == 1
            && matrix.Rows[0].SequenceInvalidatedCount == 2
            && definitionElement.GetProperty("shotSequencePolicy").GetString()
                == "SameFixtureProtocolPattern"
            && definitionElement.GetProperty("protocolThreatId").GetString()
                == campaignCase.ProtocolThreatId
            && firstAttempt.GetProperty("sampleOrdinal").GetInt32() == 0
            && firstAttempt.GetProperty("status").GetString() == "SequenceInvalidated"
            && rejectedAttempt.GetProperty("protocolQualificationReason").GetString()
                == "ExpectedPointMismatch"
            && CampaignReportInvariantValidator.Validate(
                document.RootElement,
                out int attemptCount,
                out _)
            && attemptCount == 8;
    }

    internal static bool QueuedSixthProtocolShotCannotCompleteAContaminatedSample()
    {
        CampaignDefinition definition = CampaignCatalog.GostSimulationScreening(
            "gost-34286-br4-7.62x39-ps-57-n-231");
        CampaignCaseDefinition campaignCase = definition.Cases[0];
        var tracker = new CampaignRunTracker(definition, 73UL);
        tracker.Start();
        tracker.AttachFixture(1300L);
        for (int index = 0; index < 4; index++)
        {
            tracker.RecordShot(ProtocolEvidence(campaignCase, 1300L, "queued-" + index, index));
        }
        CampaignAttemptRecord? fifth = tracker.RecordShot(
            ProtocolEvidence(campaignCase, 1300L, "queued-4", 4),
            deferProtocolCompletion: true);
        CampaignRunSnapshot deferred = tracker.Snapshot();
        CampaignAttemptRecord? sixth = tracker.RecordShot(
            ProtocolEvidence(campaignCase, 1300L, "queued-5", 4));
        CampaignRunSnapshot invalidated = tracker.Snapshot();
        return fifth?.Status == CampaignAttemptStatus.Accepted
            && deferred.State == CampaignRunState.AwaitingShot
            && deferred.CurrentRepetitionIndex == 4
            && sixth?.Status == CampaignAttemptStatus.ProtocolRejected
            && sixth.ProtocolQualificationReason
                == ProtocolShotQualificationReason.NeighbourDistanceInsufficient
            && invalidated.State == CampaignRunState.AwaitingFixture
            && invalidated.Attempts.Take(5).All(attempt =>
                attempt.Status == CampaignAttemptStatus.SequenceInvalidated)
            && invalidated.Attempts[5].Status == CampaignAttemptStatus.ProtocolRejected;
    }

    internal static bool InvalidProtocolEvidenceCanDiscardDamagedSampleWithoutResettingIt()
    {
        CampaignDefinition definition = CampaignCatalog.GostSimulationScreening(
            "gost-34286-br4-7.62x39-ps-57-n-231");
        CampaignCaseDefinition campaignCase = definition.Cases[0];
        var tracker = new CampaignRunTracker(definition, 74UL);
        tracker.Start();
        tracker.AttachFixture(1400L);
        tracker.RecordShot(ProtocolEvidence(campaignCase, 1400L, "valid-before-invalid", 0));
        bool invalidated = tracker.InvalidateCurrentProtocolSample();
        CampaignRunSnapshot snapshot = tracker.Snapshot();

        var ordinary = new CampaignRunTracker(Definition(), 75UL);
        ordinary.Start();
        ordinary.AttachFixture(1500L);
        return invalidated
            && snapshot.State == CampaignRunState.AwaitingFixture
            && snapshot.CurrentFixtureId == 0L
            && snapshot.CurrentRepetitionIndex == 0
            && snapshot.Attempts.Count == 1
            && snapshot.Attempts[0].Status == CampaignAttemptStatus.SequenceInvalidated
            && !ordinary.InvalidateCurrentProtocolSample();
    }

    internal static bool ProtocolResultSummarizesCompletedSampleAndStandards()
    {
        string report = CreateCompletedProtocolReport(deferCompletion: false);
        using JsonDocument document = JsonDocument.Parse(report);
        JsonElement result = document.RootElement.GetProperty("protocolScreeningResult");
        JsonElement screening = result.GetProperty("screenings")[0];
        JsonElement sample = screening.GetProperty("samples")[0];
        return result.GetProperty("documentSchema").GetInt32() == 1
            && result.GetProperty("documentType").GetString()
                == "BallisticsLabProtocolScreening"
            && result.GetProperty("classificationStandard").GetString()
                == GostProtocolCatalog.ClassificationStandard
            && result.GetProperty("testMethodStandard").GetString()
                == GostProtocolCatalog.TestMethodStandard
            && result.GetProperty("simulationScreeningOnly").GetBoolean()
            && !result.GetProperty("certificationClaim").GetBoolean()
            && screening.GetProperty("threatId").GetString()
                == "gost-34286-br4-7.62x39-ps-57-n-231"
            && screening.GetProperty("resultStatus").GetString()
                == "SimulationScreeningComplete"
            && screening.GetProperty("evaluationStatus").GetString()
                == "SimulationScreeningComplete"
            && screening.GetProperty("observedOutcome").GetString()
                == "NoThroughPenetrationObserved"
            && screening.GetProperty("qualifyingShotCount").GetInt32() == 5
            && screening.GetProperty("fixtureMaterial").GetString() == "ArmoredSteel"
            && screening.GetProperty("fixtureArmorClass").GetInt32() == 4
            && screening.GetProperty("fixtureLayerCount").GetInt32() == 1
            && Math.Abs(screening.GetProperty(
                    "meanObservedProtocolVelocityMetresPerSecond").GetDouble() - 720d)
                < 0.000000001d
            && screening.GetProperty("currentThroughPenetrationCount").GetInt32() == 0
            && screening.GetProperty("currentNoThroughPenetrationCount").GetInt32() == 5
            && screening.GetProperty("sampleCount").GetInt32() == 1
            && screening.GetProperty("invalidatedSampleCount").GetInt32() == 0
            && screening.GetProperty("complete").GetBoolean()
            && sample.GetProperty("state").GetString() == "Complete"
            && sample.GetProperty("acceptedAttemptCount").GetInt32() == 5
            && sample.GetProperty("throughPenetrationCount").GetInt32() == 0
            && CampaignReportInvariantValidator.Validate(
                document.RootElement,
                out int attemptCount,
                out _)
            && attemptCount == 5;
    }

    internal static bool ProtocolResultPreservesInvalidatedSampleHistory()
    {
        string report = CreateRecoveredProtocolReport();
        using JsonDocument document = JsonDocument.Parse(report);
        JsonElement screening = document.RootElement
            .GetProperty("protocolScreeningResult")
            .GetProperty("screenings")[0];
        JsonElement samples = screening.GetProperty("samples");
        JsonElement invalidated = samples[0];
        JsonElement completed = samples[1];
        return screening.GetProperty("resultStatus").GetString()
                == "SimulationScreeningComplete"
            && screening.GetProperty("currentSampleOrdinal").GetInt32() == 1
            && screening.GetProperty("sampleCount").GetInt32() == 2
            && screening.GetProperty("invalidatedSampleCount").GetInt32() == 1
            && screening.GetProperty("totalAttemptCount").GetInt32() == 8
            && screening.GetProperty("protocolRejectCount").GetInt32() == 1
            && screening.GetProperty("sequenceInvalidatedAttemptCount").GetInt32() == 2
            && invalidated.GetProperty("state").GetString() == "Invalidated"
            && invalidated.GetProperty("qualifyingAttemptCount").GetInt32() == 2
            && invalidated.GetProperty("invalidatedAttemptCount").GetInt32() == 2
            && invalidated.GetProperty("rejectedAttemptCount").GetInt32() == 1
            && invalidated.GetProperty("terminalQualificationReason").GetString()
                == "ExpectedPointMismatch"
            && completed.GetProperty("state").GetString() == "Complete"
            && completed.GetProperty("acceptedAttemptCount").GetInt32() == 5
            && CampaignReportInvariantValidator.Validate(
                document.RootElement,
                out int attemptCount,
                out _)
            && attemptCount == 8;
    }

    internal static bool ProtocolResultCannotCompleteWhileExtraEvidenceIsPending()
    {
        string report = CreateCompletedProtocolReport(deferCompletion: true);
        using JsonDocument document = JsonDocument.Parse(report);
        JsonElement screening = document.RootElement
            .GetProperty("protocolScreeningResult")
            .GetProperty("screenings")[0];
        JsonElement sample = screening.GetProperty("samples")[0];
        return screening.GetProperty("evaluationStatus").GetString()
                == "SimulationScreeningComplete"
            && screening.GetProperty("resultStatus").GetString() == "PendingEvidence"
            && screening.GetProperty("observedOutcome").GetString() == "NotDetermined"
            && !screening.GetProperty("complete").GetBoolean()
            && sample.GetProperty("state").GetString() == "PendingEvidence"
            && CampaignReportInvariantValidator.Validate(
                document.RootElement,
                out int attemptCount,
                out _)
            && attemptCount == 5;
    }

    internal static bool CampaignReportRejectsCorruptedProtocolResult()
    {
        string report = CreateRecoveredProtocolReport();
        JsonNode falseCertification = JsonNode.Parse(report)!;
        falseCertification["protocolScreeningResult"]!["certificationClaim"] = true;
        using JsonDocument certificationDocument = JsonDocument.Parse(
            falseCertification.ToJsonString());
        bool certificationRejected = !CampaignReportInvariantValidator.Validate(
            certificationDocument.RootElement,
            out _,
            out _);

        JsonNode wrongResult = JsonNode.Parse(report)!;
        wrongResult["protocolScreeningResult"]!["screenings"]![0]!["resultStatus"] =
            "InsufficientEvidence";
        using JsonDocument resultDocument = JsonDocument.Parse(wrongResult.ToJsonString());
        bool resultRejected = !CampaignReportInvariantValidator.Validate(
            resultDocument.RootElement,
            out _,
            out _);

        JsonNode wrongSample = JsonNode.Parse(report)!;
        wrongSample["protocolScreeningResult"]!["screenings"]![0]!["samples"]![0]![
            "invalidatedAttemptCount"] = 1;
        using JsonDocument sampleDocument = JsonDocument.Parse(wrongSample.ToJsonString());
        bool sampleRejected = !CampaignReportInvariantValidator.Validate(
            sampleDocument.RootElement,
            out _,
            out _);
        return certificationRejected && resultRejected && sampleRejected;
    }

    internal static bool CampaignReportRejectsProtocolResultWithoutCampaign()
    {
        const string json = "{\"schema\":4,\"pluginVersion\":\"0.2.8\",\"records\":[],"
            + "\"physicalTransitions\":[],\"campaign\":null,"
            + "\"protocolScreeningResult\":{\"certificationClaim\":false}}";
        using JsonDocument document = JsonDocument.Parse(json);
        return !CampaignReportInvariantValidator.Validate(
            document.RootElement,
            out _,
            out string failure)
            && failure.Contains("without campaign", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool PhysicalEvidenceUsesExactHostIdentityAndChecksClosure()
    {
        FakePhysicalEvent fake = FakePhysicalEvent.Resolved("campaign-physical");
        if (!PhysicalTelemetryReflectionReader.TryCopy(
                1,
                fake.Event,
                out PhysicalTelemetryEventRecord? resolved,
                out _)
            || resolved == null)
        {
            return false;
        }
        var tracker = new PhysicalTransitionTracker(4);
        tracker.Add(resolved);
        CampaignPhysicalEvidenceSummary matching = CampaignPhysicalEvidenceCalculator.Calculate(
            tracker.Snapshot(),
            17,
            991,
            "ammo-template",
            "profile");
        CampaignPhysicalEvidenceSummary mismatched = CampaignPhysicalEvidenceCalculator.Calculate(
            tracker.Snapshot(),
            18,
            991,
            "ammo-template",
            "profile");
        CampaignPhysicalEvidenceSummary wrongProfile = CampaignPhysicalEvidenceCalculator.Calculate(
            tracker.Snapshot(),
            17,
            991,
            "ammo-template",
            "other-profile");
        CampaignPhysicalEvidenceSummary missingProfile = CampaignPhysicalEvidenceCalculator.Calculate(
            tracker.Snapshot(),
            17,
            991,
            "ammo-template",
            string.Empty);
        return matching.TransitionCount == 1
            && matching.ConservationRecordCount == 1
            && Math.Abs(matching.MaximumMassClosureErrorKilograms) < 0.000000000001d
            && Math.Abs(matching.MaximumEnergyClosureErrorJoules - 10d) < 0.000000001d
            && mismatched.TransitionCount == 0
            && mismatched.ConservationRecordCount == 0
            && wrongProfile.TransitionCount == 0
            && missingProfile.TransitionCount == 0;
    }

    internal static bool CampaignJsonContainsDefinitionAttemptsAndMatrix()
    {
        CampaignDefinition definition = new CampaignDefinition(
            "json-campaign",
            "JSON campaign",
            new List<CampaignCaseDefinition>
            {
                Case("json-case", 1, CampaignResetPolicy.BeforeEachShot)
            });
        var tracker = new CampaignRunTracker(definition, 42UL);
        tracker.Start();
        tracker.AttachFixture(700L);
        tracker.RecordShot(Evidence(700L, "json-chain", 1d, LayerZero));
        string report = PhysicalReportDocumentWriter.Build(
            "[]",
            Array.Empty<PhysicalTransitionRecord>(),
            definition,
            tracker.Snapshot());
        using JsonDocument document = JsonDocument.Parse(report);
        JsonElement campaign = document.RootElement.GetProperty("campaign");
        JsonElement caseDefinition = campaign.GetProperty("cases")[0];
        JsonElement attempt = campaign.GetProperty("attempts")[0];
        JsonElement protocol = attempt.GetProperty("protocolEvidence");
        JsonElement row = campaign.GetProperty("matrix").GetProperty("rows")[0];
        return CampaignReportInvariantValidator.Validate(
                document.RootElement,
                out int attemptCount,
                out _)
            && attemptCount == 1
            && campaign.GetProperty("campaignId").GetString() == "json-campaign"
            && campaign.GetProperty("runSeed").GetUInt64() == 42UL
            && CampaignRunIdentity.IsValid(campaign.GetProperty("runInstanceId").GetString())
            && !campaign.GetProperty("gameShotSeedOverridden").GetBoolean()
            && campaign.GetProperty("state").GetString() == "Completed"
            && document.RootElement.GetProperty("protocolScreeningResult").ValueKind
                == JsonValueKind.Null
            && caseDefinition.GetProperty("shotSequencePolicy").GetString()
                == "IndependentShots"
            && string.IsNullOrEmpty(
                caseDefinition.GetProperty("protocolThreatId").GetString())
            && attempt.GetProperty("chainId").GetString() == "json-chain"
            && attempt.GetProperty("sampleOrdinal").GetInt32() == 0
            && string.IsNullOrEmpty(
                attempt.GetProperty("protocolQualificationReason").GetString())
            && attempt.GetProperty("fixtureTemplateId").GetString() == "fixture-template"
            && attempt.GetProperty("fixtureArmorMaterial").GetString() == "ArmoredSteel"
            && attempt.GetProperty("fixtureArmorClass").GetInt32() == 6
            && attempt.GetProperty("fixtureLayerCount").GetInt32() == 1
            && attempt.GetProperty("rootFireIndex").GetInt32() == 17
            && attempt.GetProperty("rootShooterProfileId").GetString() == "profile"
            && attempt.GetProperty("status").GetString() == "Accepted"
            && protocol.GetProperty("velocityMeasurementBasis").GetString()
                == "TargetImpactProxy"
            && Math.Abs(protocol.GetProperty("protocolVelocityMetresPerSecond").GetDouble() - 800d)
                < 0.000001d
            && Math.Abs(protocol.GetProperty("targetImpactSpeedMetresPerSecond").GetDouble() - 800d)
                < 0.000001d
            && Math.Abs(protocol.GetProperty("projectileMassKilograms").GetDouble() - 0.008d)
                < 0.000000001d
            && Math.Abs(protocol.GetProperty("projectileDiameterMetres").GetDouble() - 0.00762d)
                < 0.000000001d
            && protocol.GetProperty("witnessBackstopConfigured").GetBoolean()
            && row.GetProperty("acceptedCount").GetInt32() == 1
            && row.GetProperty("fixtureRejectCount").GetInt32() == 0
            && row.GetProperty("complete").GetBoolean();
    }

    internal static bool CampaignJsonRejectsPartialDefinitionAndSnapshot()
    {
        CampaignDefinition definition = new CampaignDefinition(
            "partial-campaign",
            "Partial campaign",
            new List<CampaignCaseDefinition>
            {
                Case("partial-case", 1, CampaignResetPolicy.BeforeEachShot)
            });
        try
        {
            _ = PhysicalReportDocumentWriter.Build(
                "[]",
                Array.Empty<PhysicalTransitionRecord>(),
                definition,
                null);
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    internal static bool CampaignReportRejectsCorruptedSeedAndMatrix()
    {
        string report = CreateCompletedCampaignReport();
        JsonNode wrongSeed = JsonNode.Parse(report)!;
        wrongSeed["campaign"]!["attempts"]![0]!["labCaseSeed"] = 1UL;
        using JsonDocument seedDocument = JsonDocument.Parse(wrongSeed.ToJsonString());
        bool seedRejected = !CampaignReportInvariantValidator.Validate(
            seedDocument.RootElement,
            out _,
            out string seedFailure);

        JsonNode wrongMatrix = JsonNode.Parse(report)!;
        wrongMatrix["campaign"]!["matrix"]!["acceptedCount"] = 2;
        using JsonDocument matrixDocument = JsonDocument.Parse(wrongMatrix.ToJsonString());
        bool matrixRejected = !CampaignReportInvariantValidator.Validate(
            matrixDocument.RootElement,
            out _,
            out string matrixFailure);
        return seedRejected
            && seedFailure.Contains("attempt", StringComparison.OrdinalIgnoreCase)
            && matrixRejected
            && matrixFailure.Contains("matrix", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool CampaignReportRejectsCorruptedFixtureIdentity()
    {
        JsonNode corrupted = JsonNode.Parse(CreateCompletedCampaignReport())!;
        corrupted["campaign"]!["attempts"]![0]!["fixtureArmorClass"] = 5;
        using JsonDocument document = JsonDocument.Parse(corrupted.ToJsonString());
        return !CampaignReportInvariantValidator.Validate(
                document.RootElement,
                out _,
                out string failure)
            && failure.Contains("status", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool CampaignReportRejectsCorruptedProtocolEvidence()
    {
        JsonNode outsideFace = JsonNode.Parse(CreateCompletedCampaignReport())!;
        outsideFace["campaign"]!["attempts"]![0]!["protocolEvidence"]![
            "fixtureLocalHitXMetres"] = 2d;
        using JsonDocument outsideDocument = JsonDocument.Parse(outsideFace.ToJsonString());
        bool outsideRejected = !CampaignReportInvariantValidator.Validate(
            outsideDocument.RootElement,
            out _,
            out string outsideFailure);

        JsonNode outcomeMismatch = JsonNode.Parse(CreateCompletedCampaignReport())!;
        outcomeMismatch["campaign"]!["attempts"]![0]!["protocolEvidence"]![
            "throughPenetrationObserved"] = true;
        using JsonDocument outcomeDocument = JsonDocument.Parse(outcomeMismatch.ToJsonString());
        bool outcomeRejected = !CampaignReportInvariantValidator.Validate(
            outcomeDocument.RootElement,
            out _,
            out string outcomeFailure);
        return outsideRejected
            && outsideFailure.Contains("attempt", StringComparison.OrdinalIgnoreCase)
            && outcomeRejected
            && outcomeFailure.Contains("attempt", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool CampaignReportRejectsCorruptedProtocolSequenceState()
    {
        string report = CreateInvalidatedProtocolReport();
        using JsonDocument cleanDocument = JsonDocument.Parse(report);
        bool cleanAccepted = CampaignReportInvariantValidator.Validate(
            cleanDocument.RootElement,
            out _,
            out _);

        JsonNode wrongSample = JsonNode.Parse(report)!;
        wrongSample["campaign"]!["attempts"]![0]!["sampleOrdinal"] = 1;
        using JsonDocument sampleDocument = JsonDocument.Parse(wrongSample.ToJsonString());
        bool sampleRejected = !CampaignReportInvariantValidator.Validate(
            sampleDocument.RootElement,
            out _,
            out _);

        JsonNode wrongReason = JsonNode.Parse(report)!;
        wrongReason["campaign"]!["attempts"]![2]!["protocolQualificationReason"] =
            "Qualifying";
        using JsonDocument reasonDocument = JsonDocument.Parse(wrongReason.ToJsonString());
        bool reasonRejected = !CampaignReportInvariantValidator.Validate(
            reasonDocument.RootElement,
            out _,
            out _);

        JsonNode wrongFinalStatus = JsonNode.Parse(report)!;
        wrongFinalStatus["campaign"]!["attempts"]![0]!["status"] = "Accepted";
        using JsonDocument statusDocument = JsonDocument.Parse(wrongFinalStatus.ToJsonString());
        bool statusRejected = !CampaignReportInvariantValidator.Validate(
            statusDocument.RootElement,
            out _,
            out _);
        return cleanAccepted && sampleRejected && reasonRejected && statusRejected;
    }

    internal static bool CampaignReportRejectsCorruptedAttemptAndHeaderCursors()
    {
        string report = CreateCompletedCampaignReport();
        JsonNode wrongAttempt = JsonNode.Parse(report)!;
        wrongAttempt["campaign"]!["attempts"]![0]!["attemptIndex"] = 1;
        using JsonDocument attemptDocument = JsonDocument.Parse(wrongAttempt.ToJsonString());
        bool attemptRejected = !CampaignReportInvariantValidator.Validate(
            attemptDocument.RootElement,
            out _,
            out string attemptFailure);

        JsonNode wrongHeader = JsonNode.Parse(report)!;
        wrongHeader["campaign"]!["currentCaseIndex"] = 0;
        using JsonDocument headerDocument = JsonDocument.Parse(wrongHeader.ToJsonString());
        bool headerRejected = !CampaignReportInvariantValidator.Validate(
            headerDocument.RootElement,
            out _,
            out string headerFailure);
        return attemptRejected
            && attemptFailure.Contains("cursor", StringComparison.OrdinalIgnoreCase)
            && headerRejected
            && headerFailure.Contains("cursor", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ReportInvariantAcceptsCampaignOnlyEvidence()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "BallisticsLab.Campaign." + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, CreateCompletedCampaignReport());
            return ReportInvariantValidator.Validate(path, out _);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    internal static bool ReportInvariantRejectsEmptyCampaignShell()
    {
        CampaignDefinition definition = new CampaignDefinition(
            "empty-shell",
            "Empty shell",
            new List<CampaignCaseDefinition>
            {
                Case("empty-case", 1, CampaignResetPolicy.BeforeEachShot)
            });
        var tracker = new CampaignRunTracker(definition, 43UL);
        tracker.Start();
        tracker.AttachFixture(800L);
        string path = Path.Combine(
            Path.GetTempPath(),
            "BallisticsLab.EmptyCampaign." + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(
                path,
                PhysicalReportDocumentWriter.Build(
                    "[]",
                    Array.Empty<PhysicalTransitionRecord>(),
                    definition,
                    tracker.Snapshot()));
            return !ReportInvariantValidator.Validate(path, out string failure)
                && failure.Contains("no shot", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    internal static bool StoppedCampaignPreservesAttemptsAndRejectsFurtherShots()
    {
        CampaignDefinition definition = new CampaignDefinition(
            "stopped",
            "Stopped campaign",
            new List<CampaignCaseDefinition>
            {
                Case("two-repetitions", 2, CampaignResetPolicy.BeforeEachShot)
            });
        var tracker = new CampaignRunTracker(definition, 7UL);
        tracker.Start();
        tracker.AttachFixture(900L);
        tracker.RecordShot(Evidence(900L, "first", 1d, LayerZero));
        tracker.ConfirmReset(900L);
        bool stopped = tracker.Stop();
        CampaignAttemptRecord? afterStop = tracker.RecordShot(
            Evidence(900L, "after-stop", 1d, LayerZero));
        CampaignRunSnapshot snapshot = tracker.Snapshot();
        return stopped
            && snapshot.State == CampaignRunState.Stopped
            && snapshot.Attempts.Count == 1
            && afterStop == null
            && !tracker.Stop();
    }

    private static string CreateCompletedCampaignReport()
    {
        CampaignDefinition definition = new CampaignDefinition(
            "report-campaign",
            "Report campaign",
            new List<CampaignCaseDefinition>
            {
                Case("report-case", 1, CampaignResetPolicy.BeforeEachShot)
            });
        var tracker = new CampaignRunTracker(definition, 42UL);
        tracker.Start();
        tracker.AttachFixture(701L);
        tracker.RecordShot(Evidence(701L, "report-chain", 1d, LayerZero));
        return PhysicalReportDocumentWriter.Build(
            "[]",
            Array.Empty<PhysicalTransitionRecord>(),
            definition,
            tracker.Snapshot());
    }

    private static CampaignDefinition Definition()
    {
        return new CampaignDefinition(
            "fixture-baseline",
            "Fixture baseline",
            new List<CampaignCaseDefinition>
            {
                Case("steel-c6", 2, CampaignResetPolicy.BeforeEachShot),
                new CampaignCaseDefinition(
                    "steel-c3-two",
                    "Two-layer steel",
                    CampaignFixtureSelectorKind.MaterialAndArmorClass,
                    string.Empty,
                    "ArmoredSteel",
                    3,
                    2,
                    0.15d,
                    0.0127d,
                    8d,
                    0d,
                    true,
                    1,
                    CampaignResetPolicy.BeforeEachCase,
                    0.5d,
                    1.5d,
                    false,
                    false,
                    false,
                    1d,
                    100d)
            });
    }

    private static CampaignCaseDefinition Case(
        string caseId,
        int repetitions,
        CampaignResetPolicy resetPolicy)
    {
        return new CampaignCaseDefinition(
            caseId,
            caseId + " label",
            CampaignFixtureSelectorKind.MaterialAndArmorClass,
            string.Empty,
            "ArmoredSteel",
            6,
            1,
            0.15d,
            0.0127d,
            8d,
            0d,
            true,
            repetitions,
            resetPolicy,
            0.5d,
            1.5d,
            false,
            false,
            false,
            1d,
            100d);
    }

    private static CampaignShotEvidence Evidence(
        long fixtureId,
        string chainId,
        double velocityFraction,
        IReadOnlyList<int> layers,
        bool reachedBackstop = false,
        int physicalTransitionCount = 0,
        int conservationRecordCount = -1,
        double massClosureError = 0d,
        double energyClosureError = 0d,
        string fixtureTemplateId = "fixture-template",
        string fixtureArmorMaterial = "ArmoredSteel",
        int fixtureArmorClass = 6,
        int fixtureLayerCount = 1)
    {
        return new CampaignShotEvidence(
            fixtureId,
            chainId,
            fixtureTemplateId,
            fixtureArmorMaterial,
            fixtureArmorClass,
            fixtureLayerCount,
            17,
            12345,
            "profile",
            "ammo-template",
            velocityFraction,
            layers,
            "PENETRATED",
            reachedBackstop,
            physicalTransitionCount,
            conservationRecordCount < 0 ? physicalTransitionCount : conservationRecordCount,
            massClosureError,
            energyClosureError,
            new ProtocolShotEvidence(
                fixtureId,
                "ammo-template",
                ProtocolVelocityMeasurementBasis.TargetImpactProxy,
                800d,
                0.008d,
                0.00762d,
                0d,
                0d,
                0d,
                1d,
                1.5d,
                8d,
                true,
                reachedBackstop));
    }

    private static CampaignShotEvidence ProtocolEvidence(
        CampaignCaseDefinition campaignCase,
        long fixtureId,
        string chainId,
        int pointIndex)
    {
        if (!campaignCase.TryGetProtocolDefinition(
                out ProtocolThreatDefinition? threat,
                out ProtocolAmmunitionMapping? mapping)
            || threat == null
            || mapping == null
            || !ProtocolImpactPattern.TryCreate(
                threat,
                mapping,
                1d,
                1.5d,
                out ProtocolImpactPattern? pattern,
                out _)
            || pattern == null)
        {
            throw new InvalidOperationException("Protocol test fixture could not be constructed.");
        }
        ProtocolImpactPoint point = pattern.Points[pointIndex];
        double velocity = (threat.MinimumVelocityMetresPerSecond
            + threat.MaximumVelocityMetresPerSecond) * 0.5d;
        var protocol = new ProtocolShotEvidence(
            fixtureId,
            mapping.TemplateId,
            ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres,
            velocity,
            mapping.InstalledProjectileMassKilograms,
            mapping.InstalledProjectileDiameterMetres,
            0d,
            point.LocalXMetres,
            point.LocalYMetres,
            1d,
            1.5d,
            threat.TestDistanceMetres,
            true,
            false,
            velocity);
        return new CampaignShotEvidence(
            fixtureId,
            chainId,
            "protocol-fixture",
            "ArmoredSteel",
            campaignCase.ArmorClass,
            1,
            17 + pointIndex,
            12345 + pointIndex,
            "profile",
            mapping.TemplateId,
            1d,
            LayerZero,
            "STOPPED",
            false,
            1,
            1,
            0d,
            0d,
            protocol);
    }

    private static bool RejectsUnavailableProtocolCampaign()
    {
        try
        {
            _ = CampaignCatalog.GostSimulationScreening("gost-34286-br2-9x21-p-7n28");
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static string CreateInvalidatedProtocolReport()
    {
        CampaignDefinition definition = CampaignCatalog.GostSimulationScreening(
            "gost-34286-br4-7.62x39-ps-57-n-231");
        CampaignCaseDefinition campaignCase = definition.Cases[0];
        var tracker = new CampaignRunTracker(definition, 76UL);
        tracker.Start();
        tracker.AttachFixture(1600L);
        tracker.RecordShot(ProtocolEvidence(campaignCase, 1600L, "corrupt-a", 0));
        tracker.RecordShot(ProtocolEvidence(campaignCase, 1600L, "corrupt-b", 1));
        tracker.RecordShot(ProtocolEvidence(campaignCase, 1600L, "corrupt-c", 0));
        return PhysicalReportDocumentWriter.Build(
            "[]",
            Array.Empty<PhysicalTransitionRecord>(),
            definition,
            tracker.Snapshot());
    }

    private static string CreateCompletedProtocolReport(bool deferCompletion)
    {
        CampaignDefinition definition = CampaignCatalog.GostSimulationScreening(
            "gost-34286-br4-7.62x39-ps-57-n-231");
        CampaignCaseDefinition campaignCase = definition.Cases[0];
        var tracker = new CampaignRunTracker(definition, 77UL);
        tracker.Start();
        tracker.AttachFixture(1700L);
        for (int index = 0; index < 5; index++)
        {
            tracker.RecordShot(
                ProtocolEvidence(campaignCase, 1700L, "result-" + index, index),
                deferProtocolCompletion: deferCompletion && index == 4);
        }
        return PhysicalReportDocumentWriter.Build(
            "[]",
            Array.Empty<PhysicalTransitionRecord>(),
            definition,
            tracker.Snapshot());
    }

    private static string CreateRecoveredProtocolReport()
    {
        CampaignDefinition definition = CampaignCatalog.GostSimulationScreening(
            "gost-34286-br4-7.62x39-ps-57-n-231");
        CampaignCaseDefinition campaignCase = definition.Cases[0];
        var tracker = new CampaignRunTracker(definition, 78UL);
        tracker.Start();
        tracker.AttachFixture(1800L);
        tracker.RecordShot(ProtocolEvidence(campaignCase, 1800L, "recovered-a", 0));
        tracker.RecordShot(ProtocolEvidence(campaignCase, 1800L, "recovered-b", 1));
        tracker.RecordShot(ProtocolEvidence(campaignCase, 1800L, "recovered-c", 0));
        tracker.AttachFixture(1900L);
        for (int index = 0; index < 5; index++)
        {
            tracker.RecordShot(
                ProtocolEvidence(campaignCase, 1900L, "recovered-" + index, index));
        }
        return PhysicalReportDocumentWriter.Build(
            "[]",
            Array.Empty<PhysicalTransitionRecord>(),
            definition,
            tracker.Snapshot());
    }
}
