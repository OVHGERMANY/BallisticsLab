using System.Globalization;
using System.Text.Json;
using BallisticsLab.Core;
using BallisticsLab.Validation;

const string plateParent = "644120aa86ffbe10ee032b6f";
const string granitBr4 = "65573fa5655447403702a816";
const string granitBr5 = "64afc71497cf3a403c01ff38";
const int installedRuntimeEvidenceSchema = 3;

string[] positionalArguments = args
    .Where(argument => !argument.StartsWith("--", StringComparison.Ordinal))
    .ToArray();
bool requireReportCoverage = args.Contains(
    "--require-report-coverage",
    StringComparer.OrdinalIgnoreCase);
string itemsPath = positionalArguments.Length > 0
    ? Path.GetFullPath(positionalArguments[0])
    : @"E:\Games\SPT\SPT_Runtime\SPT_Data\database\templates\items.json";
string reportsPath = positionalArguments.Length > 1
    ? Path.GetFullPath(positionalArguments[1])
    : string.Empty;

List<string> failures = new();
int passed = 0;
Dictionary<string, AmmoRow> ammunition = new(StringComparer.Ordinal);

Check(
    ConstantMatches(typeof(LabBuild), nameof(LabBuild.PluginGuid), "com.janky.ballisticslab")
    && ConstantMatches(typeof(LabBuild), nameof(LabBuild.PluginName), "Janky-BallisticsLab")
    && ConstantMatches(typeof(LabBuild), nameof(LabBuild.PluginVersion), "0.2.8")
    && ConstantMatches(typeof(LabBuild), nameof(LabBuild.ReportSchema), 4)
    && ConstantMatches(
        typeof(PhysicalTelemetryContract),
        nameof(PhysicalTelemetryContract.SupportedPublisherSchema),
        1)
    && ConstantMatches(
        typeof(PhysicalTelemetryContract),
        nameof(PhysicalTelemetryContract.SnapshotSchema),
        1)
    && ConstantMatches(
        typeof(PhysicalTelemetryContract),
        nameof(PhysicalTelemetryContract.PublisherTypeName),
        "BallisticPenetration.Core.Physics.PhysicalProjectileTelemetry"),
    "report provenance constants match the current plugin build");
Check(LabPolicies.OutcomeName(0, false, false) == "PENETRATED / CONTINUING", "continuing taxonomy");
Check(LabPolicies.OutcomeName(1, false, false) == "PENETRATED / DEVIATED", "deviation taxonomy");
Check(LabPolicies.OutcomeName(3, false, false) == "PENETRATED / FRAGMENTED", "fragment taxonomy");
Check(LabPolicies.OutcomeName(4, true, false) == "STOPPED / ARMOR BLOCK", "armor block taxonomy");
Check(LabPolicies.OutcomeName(2, false, true) == "RICOCHET", "ricochet taxonomy");
Check(Math.Abs(LabPolicies.ImpactAngleDegrees(1f)) < 0.0001f, "normal impact angle");
Check(Math.Abs(LabPolicies.ImpactAngleDegrees(0f) - 90f) < 0.0001f, "grazing impact angle");
Check(Math.Abs(LabPolicies.ImpactAngleDegrees(-0.5f) - 60f) < 0.0001f, "back-face angle folds into zero-to-ninety range");
Check(LabPolicies.Csv("a,b") == "\"a,b\"", "CSV escaping");
Check(LabPolicies.Json("a\n\"b") == "\"a\\n\\\"b\"", "JSON escaping");
Check(ReportPairValidator.ParserHandlesQuotedFields(), "CSV report parser handles commas, quotes, and embedded newlines");
Check(ReportPairValidator.ValidatorMatchesSyntheticPair(), "CSV and JSON report validator accepts a matching schema-4 pair");
Check(ReportPairValidator.ValidatorRejectsSyntheticMismatch(), "CSV and JSON report validator rejects a field mismatch");
Check(
    ReportPairValidator.ValidatorAcceptsPhysicalOnlySchemaFourPair(),
    "CSV and JSON validator accepts a schema-4 physical-only report with the flat CSV header");
Check(ReportInvariantValidator.AcceptsSyntheticReport(), "report invariants accept a valid collision record");
Check(ReportInvariantValidator.RejectsIncorrectFalloff(), "report invariants reject incorrect penetration falloff");
Check(ReportInvariantValidator.RejectsDetachedTrajectoryEndpoint(), "report invariants reject a detached trajectory endpoint");
Check(ReportInvariantValidator.RejectsMissingForwardHitState(), "report invariants reject a missing forward-hit state");
Check(ReportInvariantValidator.RejectsMismatchedContinuationSource(), "report invariants reject a continuation attached to the wrong source collision");
Check(ReportInvariantValidator.RejectsChangedRootIdentity(), "report invariants reject a changed root identity inside one chain");
Check(CurrentReportSetValidator.RejectsCorruptEarlierCurrentReport(), "current-report gate rejects corruption in an earlier contributing export");
Check(CurrentReportSetValidator.IgnoresCorruptHistoricalReport(), "current-report gate excludes historical versions from current acceptance");
Check(
    CurrentReportSetValidator.SelectsPhysicalOnlySchemaFourReport(),
    "current-report selection includes schema-4 physical-only evidence");
Check(AcceptanceCoverageEvaluator.CompleteSyntheticCoveragePasses(), "report coverage accepts a complete controlled fixture matrix");
Check(AcceptanceCoverageEvaluator.CasualBotTrafficCannotSatisfyFixtureCoverage(), "casual bot traffic cannot satisfy controlled fixture gates");
Check(AcceptanceCoverageEvaluator.DuplicateBatchesDoNotInflateCoverage(), "duplicate automatic batches do not inflate acceptance coverage");
Check(PhysicalTelemetryFoundationTests.AbsentPublisherIsSafe(), "absent physical telemetry publisher is a safe no-op");
Check(PhysicalTelemetryFoundationTests.UnsupportedSchemaIsRejected(), "unsupported physical telemetry schema is rejected before subscription");
Check(
    PhysicalTelemetryFoundationTests.LateDiscoveryAttachesAndSessionDetachReleasesDelegate(),
    "late physical telemetry discovery attaches and session teardown releases the delegate");
Check(
    PhysicalTelemetryFoundationTests.PreparedEventIsCopiedCompletelyAndDetached(),
    "prepared physical telemetry is copied completely without retaining foreign collections");
Check(
    PhysicalTelemetryFoundationTests.ResolvedEventCopiesOutputsProvenanceAndConservation(),
    "resolved physical telemetry preserves output provenance and conservation data");
Check(
    PhysicalTelemetryFoundationTests.CaptureBufferIsBoundedAndReturnsDetachedSnapshots(),
    "physical telemetry capture buffer is bounded and snapshots are detached");
Check(
    PhysicalTelemetryFoundationTests.RejectsInvalidOrNonFiniteForeignEvents(),
    "invalid physical telemetry is rejected without partial capture");
Check(
    PhysicalTransitionTrackerTests.PreparedAndResolvedPairByExactTransitionId(),
    "prepared and resolved events pair by exact transition ID");
Check(
    PhysicalTransitionTrackerTests.PreparedOnlyRemainsPendingEvidence(),
    "prepared-only physical transitions remain pending evidence");
Check(
    PhysicalTransitionTrackerTests.ResolvedOnlyIsMarkedOrphaned(),
    "resolved-only physical transitions are marked orphaned");
Check(
    PhysicalTransitionTrackerTests.OutOfOrderArrivalConvergesToCompleted(),
    "out-of-order transition events converge to one completed pair");
Check(
    PhysicalTransitionTrackerTests.DuplicateStagesAreCountedAndFirstEventWins(),
    "duplicate physical stages are counted without duplicating transitions");
Check(
    PhysicalTransitionTrackerTests.TransitionIdsUseOrdinalCaseSensitiveIdentity(),
    "physical transition IDs use exact ordinal identity");
Check(
    PhysicalTransitionTrackerTests.CapacityEvictsOldestTransitionDeterministically(),
    "physical transition capacity evicts the oldest evidence deterministically");
Check(
    PhysicalTransitionReportTests.SchemaFourWriterPreservesCompletePhysicalEvidence(),
    "schema-4 physical transition JSON preserves complete paired evidence");
Check(
    PhysicalTransitionReportTests.PhysicalOnlyChangesTriggerCombinedReportPolicy(),
    "physical-only revisions trigger report capture without a shot record");
Check(
    PhysicalTransitionReportTests.PhysicalOnlyDocumentUsesSchemaFourAndEmptyShotArray(),
    "physical-only document uses schema 4 with an unchanged empty shot-record array");
Check(
    PhysicalTransitionReportTests.UpdatedRevisionIdentifiesOnlyChangedTransitions(),
    "automatic physical evidence selection includes only transitions changed since save");
Check(
    PhysicalTransitionInvariantTests.AcceptsBalancedSchemaFourEvidence(),
    "physical transition invariants accept balanced mass and energy evidence");
Check(
    PhysicalTransitionInvariantTests.RejectsBrokenMassClosure(),
    "physical transition invariants reject broken mass closure");
Check(
    PhysicalTransitionInvariantTests.RejectsBrokenEnergyClosure(),
    "physical transition invariants reject broken energy closure");
Check(
    CampaignTests.SeedDerivationIsStableAndCaseSpecific(),
    "campaign case seeds are stable and distinct across cases and repetitions");
Check(
    CampaignTests.EvidenceRevisionRemainsMonotonicAcrossCampaignResets(),
    "campaign evidence revisions remain monotonic across campaign replacements");
Check(
    CampaignTests.DefinitionRejectsDuplicateCaseIdentity(),
    "campaign definitions reject duplicate case identities");
Check(
    CampaignTests.EvidenceCanonicalizesHitLayers(),
    "campaign evidence canonicalizes layer indices deterministically");
Check(
    CampaignTests.EvidenceRejectsLayersOutsideRecordedFixture(),
    "campaign evidence rejects layer indices outside the recorded fixture");
Check(
    CampaignTests.ConservationRequirementCannotExistWithoutPhysicalEvidence(),
    "campaign definitions reject conservation gates without physical-transition evidence");
Check(
    CampaignTests.TrackerEnforcesResetAndCompletesInOrder(),
    "campaign tracker enforces fixture and reset order without counting duplicate chains");
Check(
    CampaignTests.TrackerAppliesBackstopPhysicalAndConservationGates(),
    "campaign tracker applies backstop, physical-evidence, and conservation gates");
Check(
    CampaignTests.SnapshotDoesNotChangeAfterLaterAttempts(),
    "campaign snapshots remain detached from later attempts");
Check(
    CampaignTests.MatrixSummarizesAcceptedAndRejectedAttempts(),
    "campaign result matrix summarizes accepted and rejected attempts");
Check(
    CampaignTests.TrackerRejectsWrongFixtureSelectorBeforeOtherGates(),
    "campaign tracker rejects wrong physical fixtures before accepting shot evidence");
Check(
    CampaignTests.BuiltInCatalogsDeclareExpectedCasesAndEvidenceRules(),
    "built-in campaigns declare the controlled stacks and physical material evidence rules");
Check(
    CampaignTests.PhysicalEvidenceUsesExactHostIdentityAndChecksClosure(),
    "campaign physical evidence uses exact host identity and measures mass and energy closure");
Check(
    CampaignTests.CampaignJsonContainsDefinitionAttemptsAndMatrix(),
    "schema 4 campaign JSON contains definitions, attempts, seed semantics, and result matrix");
Check(
    CampaignTests.CampaignJsonRejectsPartialDefinitionAndSnapshot(),
    "campaign JSON refuses a partial definition/snapshot pair");
Check(
    CampaignTests.CampaignReportRejectsCorruptedSeedAndMatrix(),
    "campaign report validation rejects corrupted case seeds and summary matrices");
Check(
    CampaignTests.CampaignReportRejectsCorruptedFixtureIdentity(),
    "campaign report validation rejects fixture identity inconsistent with attempt status");
Check(
    CampaignTests.CampaignReportRejectsCorruptedAttemptAndHeaderCursors(),
    "campaign report validation rejects impossible attempt order and header cursors");
Check(
    CampaignTests.ReportInvariantAcceptsCampaignOnlyEvidence(),
    "campaign-only schema 4 evidence passes the report invariant gate");
Check(
    CampaignTests.ReportInvariantRejectsEmptyCampaignShell(),
    "an empty campaign shell is not accepted as report evidence");
Check(
    CampaignTests.StoppedCampaignPreservesAttemptsAndRejectsFurtherShots(),
    "stopping a campaign preserves evidence and rejects later shot chains");
Check(!LabPolicies.IsFiniteNonNegative(float.NaN) && LabPolicies.IsFiniteNonNegative(0f), "finite guard");
Check(
    !LabPolicies.ShouldSaveReport(0, 1, 0)
    && !LabPolicies.ShouldSaveReport(1, 1, 1)
    && LabPolicies.ShouldSaveReport(1, 2, 1),
    "automatic reports save only nonempty changed captures");
Check(
    LabPolicies.ReportStem(new DateTime(2026, 8, 12, 1, 2, 3, 456, DateTimeKind.Utc), 7)
        == "BallisticsLab-20260812-010203-456-007",
    "automatic report stem is stable and capture-specific");
Check(ChangedChainBatchKeepsParents(), "automatic batch keeps full ancestry for every changed chain");
Check(ChangedChainBatchExcludesSavedChains(), "automatic batch omits unchanged chains");
Check(ChangedChainBatchKeepsUnchainedRecordsSeparate(), "automatic batch does not merge unrelated unchained records");
Check(ReportPairWriterCreatesOnlyACompletePair(), "report writer commits one complete UTF-8 pair");
Check(ReportPairWriterRejectsExistingStem(), "report writer never overwrites an existing checkpoint");
float[] installedBodyArmorPreset = { 0f, 0.097f, 0.378f, 0.249f, 0.28f, 0.463f };
LabColliderBallisticSettings liveBodyArmorSettings =
    LabPolicies.ResolveBodyArmorBallisticSettings(installedBodyArmorPreset);
Check(
    Nearly(liveBodyArmorSettings.PenetrationLevel, 0f)
    && Nearly(liveBodyArmorSettings.PenetrationChance, 0.097f)
    && Nearly(liveBodyArmorSettings.RicochetChance, 0.378f)
    && Nearly(liveBodyArmorSettings.FragmentationChance, 0.249f)
    && Nearly(liveBodyArmorSettings.TrajectoryDeviationChance, 0.28f)
    && Nearly(liveBodyArmorSettings.TrajectoryDeviation, 0.463f),
    "Lab plates preserve all six installed BodyArmor ballistic fields");
float[] invalidBodyArmorPreset = { 0f, 0.097f, 0.378f, 0f, 0.28f, 0.463f };
LabColliderBallisticSettings fallbackBodyArmorSettings =
    LabPolicies.ResolveBodyArmorBallisticSettings(invalidBodyArmorPreset);
Check(
    Nearly(
        fallbackBodyArmorSettings.FragmentationChance,
        LabPolicies.InstalledBodyArmorFragmentationChance)
    && Nearly(
        fallbackBodyArmorSettings.TrajectoryDeviation,
        LabPolicies.InstalledBodyArmorTrajectoryDeviation),
    "invalid BodyArmor preset data selects the exact supported-build fallback");
Check(
    LabPolicies.RequiresFixtureContinuationCorrection(1)
    && LabPolicies.RequiresFixtureContinuationCorrection(3)
    && !LabPolicies.RequiresFixtureContinuationCorrection(0)
    && !LabPolicies.RequiresFixtureContinuationCorrection(2)
    && !LabPolicies.RequiresFixtureContinuationCorrection(4),
    "only deviated and fragmented children require fixture body-armor correction");
Check(
    Nearly(LabPolicies.PenetratedChildFactor(40f, 0f, 0f, 0f), 0.4f),
    "penetrated child factor reproduces EFT body-plate branch");
Check(
    Nearly(LabPolicies.PenetratedChildFactor(25f, 10f, 0.05f, -0.03f), 0.17f),
    "penetrated child factor includes collider and ammunition modifiers");
Check(
    Nearly(LabPolicies.PenetratedChildFactor(-10f, 0f, 0f, 0f), 0f)
    && Nearly(LabPolicies.PenetratedChildFactor(250f, 0f, 0f, 0f), 1f),
    "penetrated child factor clamps to EFT zero-to-one range");
Check(
    Nearly(LabPolicies.DeviatedChildVelocityFactor(0f), 0.8f)
    && Nearly(LabPolicies.DeviatedChildVelocityFactor(1f), 1f),
    "deviated child velocity factor reproduces EFT interpolation");
Check(
    Nearly(LabPolicies.DeviatedChildOutcomeFactor(0f), 0.2f)
    && Nearly(LabPolicies.DeviatedChildOutcomeFactor(1f), 1f),
    "deviated child future-outcome factor reproduces EFT interpolation");
float firstLayerPenetration = 50f
    * LabPolicies.PenetratedChildFactor(50f, 0f, 0f, 0f)
    * 0.7f;
float secondLayerPenetration = firstLayerPenetration
    * LabPolicies.PenetratedChildFactor(firstLayerPenetration, 0f, 0f, 0f)
    * 0.7f;
Check(
    Nearly(firstLayerPenetration, 17.5f)
    && Nearly(secondLayerPenetration, 2.14375f),
    "layered continuation compounds from the corrected child rather than the original shot");
Check(
    LabPolicies.ShotChainId("profile", 17, 42) == "profile:17:42",
    "shot chain identity is stable across child records");
Check(
    LabPolicies.ShotChainId("profile", 17, 42) != LabPolicies.ShotChainId("profile", 18, 42)
    && LabPolicies.ShotChainId("profile", 17, 42) != LabPolicies.ShotChainId("profile", 17, 43),
    "shot chain identity separates different fires and root seeds");
Check(
    LabPolicies.AuthoritativeAmmoValue("5c0d5e4486f77478390952fe", "5c0d688c86f77413ae3407b2")
        == "5c0d5e4486f77478390952fe"
    && LabPolicies.AuthoritativeAmmoValue(string.Empty, "5c0d688c86f77413ae3407b2")
        == "5c0d688c86f77413ae3407b2"
    && string.IsNullOrEmpty(LabPolicies.AuthoritativeAmmoValue(null, null)),
    "runtime ammo template identity overrides stale item identity");
Check(
    LabPolicies.ShouldDisplayBodyTelemetry(50f, 10f, string.Empty)
    && LabPolicies.ShouldDisplayBodyTelemetry(0f, 0f, "armor 40.00->35.00")
    && !LabPolicies.ShouldDisplayBodyTelemetry(0f, 0f, string.Empty),
    "postmortem armor changes remain visible at zero body health");
float fixtureGap = 0.15f;
float fixtureThickness = 0.0127f;
bool exactFaceGaps = true;
for (int layer = 1; layer < LabPolicies.MaximumLayers; layer++)
{
    float previousBackFace = LabPolicies.LayerCenterOffset(
        layer - 1,
        fixtureGap,
        fixtureThickness) + fixtureThickness * 0.5f;
    float currentFrontFace = LabPolicies.LayerCenterOffset(
        layer,
        fixtureGap,
        fixtureThickness) - fixtureThickness * 0.5f;
    exactFaceGaps &= Nearly(currentFrontFace - previousBackFace, fixtureGap);
}
Check(exactFaceGaps, "one-to-six-layer geometry preserves the configured face gap");
float lastPlateBackFace = LabPolicies.LayerCenterOffset(
    LabPolicies.MaximumLayers - 1,
    fixtureGap,
    fixtureThickness) + fixtureThickness * 0.5f;
float backstopFrontFace = LabPolicies.BackstopCenterOffset(
    LabPolicies.MaximumLayers,
    fixtureGap,
    fixtureThickness,
    1f,
    0.08f) - 0.04f;
Check(
    Nearly(backstopFrontFace - lastPlateBackFace, 1f),
    "six-layer backstop retains one meter of face clearance");
Check(
    ConstantMatches(typeof(LabPolicies), nameof(LabPolicies.MaximumLayers), 6),
    "fixture layer limit remains six");

if (!File.Exists(itemsPath))
{
    failures.Add("items.json was not found: " + itemsPath);
}
else
{
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(itemsPath));
    List<PlateRow> plates = new();
    foreach (JsonProperty item in document.RootElement.EnumerateObject())
    {
        JsonElement value = item.Value;
        if (value.TryGetProperty("_name", out JsonElement ammunitionNameElement)
            && value.TryGetProperty("_props", out JsonElement ammunitionProps)
            && ammunitionProps.TryGetProperty("InitialSpeed", out JsonElement initialSpeedElement)
            && TryGetFloat(initialSpeedElement, out float initialSpeed))
        {
            ammunition[item.Name] = new AmmoRow(
                ammunitionNameElement.GetString() ?? item.Name,
                initialSpeed);
        }

        if (!value.TryGetProperty("_parent", out JsonElement parent)
            || parent.GetString() != plateParent
            || !value.TryGetProperty("_props", out JsonElement props)
            || !props.TryGetProperty("armorClass", out JsonElement armorClassElement)
            || !TryGetInt(armorClassElement, out int armorClass)
            || armorClass <= 0
            || !props.TryGetProperty("Durability", out JsonElement durabilityElement)
            || !TryGetInt(durabilityElement, out int durability)
            || durability <= 0
            || !props.TryGetProperty("ArmorMaterial", out JsonElement materialElement))
        {
            continue;
        }

        string name = value.TryGetProperty("_name", out JsonElement nameElement)
            ? nameElement.GetString() ?? item.Name
            : item.Name;
        plates.Add(new PlateRow(item.Name, name, armorClass, materialElement.GetString() ?? string.Empty, durability));
    }

    Check(plates.Count == 39, "39 usable installed plate templates");
    string[] installedMaterials = plates
        .Select(plate => plate.Material)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(material => material, StringComparer.Ordinal)
        .ToArray();
    string[] expectedMaterials =
    {
        "Aluminium",
        "Aramid",
        "ArmoredSteel",
        "Ceramic",
        "Combined",
        "Titan",
        "UHMWPE"
    };
    Check(
        installedMaterials.SequenceEqual(expectedMaterials, StringComparer.Ordinal),
        "exact installed plate-material set matches the lab controls");
    Check(plates.Any(plate => plate.Id == granitBr4 && plate.ArmorClass == 5 && plate.Material == "Ceramic"), "Granit Br4 regression");
    Check(plates.Any(plate => plate.Id == granitBr5 && plate.ArmorClass == 6 && plate.Material == "Ceramic"), "Granit Br5 regression");
    Check(Enumerable.Range(3, 4).All(armorClass => plates.Any(plate => plate.Material == "ArmoredSteel" && plate.ArmorClass == armorClass)), "steel classes 3 through 6");
    Check(plates.All(plate => plate.Durability > 0 && plate.ArmorClass is >= 2 and <= 6), "plate class and durability bounds");
    Check(
        CampaignCatalog.PhysicalMaterialMatrix().Cases.All(
            campaignCase => plates.Any(
                plate => plate.Material == campaignCase.Material
                    && plate.ArmorClass == campaignCase.ArmorClass)),
        "every physical material campaign case resolves an exact installed material and armor class");

    Console.WriteLine("Catalog: " + plates.Count.ToString(CultureInfo.InvariantCulture) + " usable plates");
    foreach (IGrouping<string, PlateRow> group in plates.GroupBy(plate => plate.Material).OrderBy(group => group.Key, StringComparer.Ordinal))
    {
        Console.WriteLine("  " + group.Key + ": " + group.Count().ToString(CultureInfo.InvariantCulture));
    }
}

if (!string.IsNullOrEmpty(reportsPath))
{
    string[] reportFiles = Directory.Exists(reportsPath)
        ? Directory.GetFiles(reportsPath, "BallisticsLab-*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(File.GetLastWriteTimeUtc)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToArray()
        : Array.Empty<string>();
    string latestReport = reportFiles.LastOrDefault() ?? string.Empty;
    Check(!string.IsNullOrEmpty(latestReport), "latest BallisticsLab report exists");

    if (!string.IsNullOrEmpty(latestReport))
    {
        using JsonDocument report = JsonDocument.Parse(File.ReadAllText(latestReport));
        bool currentSchema = report.RootElement.TryGetProperty("schema", out JsonElement schemaElement)
            && schemaElement.TryGetInt32(out int schema)
            && schema == installedRuntimeEvidenceSchema;
        Check(currentSchema, "latest installed-runtime report uses the accepted baseline schema");
        string reportVersion = report.RootElement.TryGetProperty(
                "pluginVersion",
                out JsonElement versionElement)
            ? versionElement.GetString() ?? string.Empty
            : string.Empty;
        Check(
            string.Equals(reportVersion, LabBuild.PluginVersion, StringComparison.Ordinal),
            "latest installed-runtime report identifies the accepted plugin version");

        JsonElement recordsElement = report.RootElement.TryGetProperty("records", out JsonElement records)
            ? records
            : default;
        bool hasRecords = recordsElement.ValueKind == JsonValueKind.Array
            && recordsElement.GetArrayLength() > 0;
        Check(hasRecords, "latest installed-runtime report contains shot records");

        IReadOnlyList<string> currentReports = CurrentReportSetValidator.Select(
            reportFiles,
            installedRuntimeEvidenceSchema,
            LabBuild.PluginVersion);

        string identityFailure = string.Empty;
        string pairFailure = string.Empty;
        bool invariantsValid = CurrentReportSetValidator.ValidateInvariants(
            reportFiles,
            installedRuntimeEvidenceSchema,
            LabBuild.PluginVersion,
            out int invariantReportCount,
            out string invariantFailure);
        if (currentReports.Count == 0)
        {
            const string noCurrentReport = "no nonempty current-build reports were found";
            identityFailure = noCurrentReport;
            pairFailure = noCurrentReport;
        }
        foreach (string currentReport in currentReports)
        {
            string reportName = Path.GetFileName(currentReport);
            using JsonDocument current = JsonDocument.Parse(File.ReadAllText(currentReport));
            JsonElement currentRecords = current.RootElement.GetProperty("records");
            foreach (JsonElement record in currentRecords.EnumerateArray())
            {
                string templateId = record.TryGetProperty("ammoTemplateId", out JsonElement idElement)
                    ? idElement.GetString() ?? string.Empty
                    : string.Empty;
                string reportedName = record.TryGetProperty("ammoName", out JsonElement nameElement)
                    ? nameElement.GetString() ?? string.Empty
                    : string.Empty;
                float reportedSpeed = 0f;
                bool hasReportedSpeed = record.TryGetProperty("templateSpeed", out JsonElement speedElement)
                    && TryGetFloat(speedElement, out reportedSpeed);

                if (!ammunition.TryGetValue(templateId, out AmmoRow? expected))
                {
                    identityFailure = reportName + ": unknown template " + templateId;
                    break;
                }
                if (!string.Equals(reportedName, expected.Name, StringComparison.Ordinal))
                {
                    identityFailure = reportName + ": " + templateId + " reports name " + reportedName
                        + " but the installed template is " + expected.Name;
                    break;
                }
                if (!hasReportedSpeed || !NearlyWithin(reportedSpeed, expected.InitialSpeed, 0.001f))
                {
                    identityFailure = reportName + ": " + templateId
                        + " reports a template speed that differs from the installed template";
                    break;
                }
            }

            if (string.IsNullOrEmpty(pairFailure)
                && !ReportPairValidator.Validate(currentReport, out string currentPairFailure))
            {
                pairFailure = reportName + ": " + currentPairFailure;
            }
            if (!string.IsNullOrEmpty(identityFailure))
            {
                break;
            }
        }

        Check(
            currentReports.Count > 0 && string.IsNullOrEmpty(identityFailure),
            string.IsNullOrEmpty(identityFailure)
                ? "all installed-runtime report ammunition identities and speeds match the installed templates"
                : "installed-runtime report ammunition identity mismatch: " + identityFailure);
        Check(
            currentReports.Count > 0 && string.IsNullOrEmpty(pairFailure),
            string.IsNullOrEmpty(pairFailure)
                ? "all installed-runtime CSV and JSON exports match field for field"
                : "installed-runtime CSV and JSON export mismatch: " + pairFailure);
        Check(
            invariantsValid && invariantReportCount == currentReports.Count,
            invariantsValid && invariantReportCount == currentReports.Count
                ? "all installed-runtime reports satisfy ballistic, durability, trajectory, and lineage invariants"
                : "installed-runtime report invariant failure: " + invariantFailure);
        AcceptanceReportCoverage coverage = AcceptanceCoverageEvaluator.Evaluate(
            reportFiles,
            installedRuntimeEvidenceSchema,
            LabBuild.PluginVersion);
        Console.WriteLine(
            "Installed-runtime reports validated: "
            + currentReports.Count.ToString(CultureInfo.InvariantCulture)
            + "; latest: "
            + Path.GetFileName(latestReport));
        Console.WriteLine(
            "Controlled report coverage: "
            + coverage.UniqueRecords.ToString(CultureInfo.InvariantCulture)
            + " unique records in "
            + coverage.UniqueChains.ToString(CultureInfo.InvariantCulture)
            + " chains.");
        foreach (AcceptanceCoverageCheck coverageCheck in coverage.Checks)
        {
            Console.WriteLine(
                "  "
                + coverageCheck.Name.PadRight(30)
                + (coverageCheck.Passed ? "PASS" : "MISSING"));
        }
        if (coverage.Missing.Count > 0)
        {
            Console.WriteLine(
                "Missing controlled report gates: "
                + string.Join(", ", coverage.Missing));
        }
        if (requireReportCoverage)
        {
            Check(
                coverage.Complete,
                coverage.Complete
                    ? "all controlled current-build report gates are complete"
                    : "controlled current-build report gates are incomplete: "
                        + string.Join(", ", coverage.Missing));
        }
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine("FAILED " + failures.Count.ToString(CultureInfo.InvariantCulture));
    foreach (string failure in failures)
    {
        Console.Error.WriteLine("  " + failure);
    }
    return 1;
}

Console.WriteLine("PASS " + passed.ToString(CultureInfo.InvariantCulture) + " checks");
return 0;

void Check(bool condition, string name)
{
    if (condition)
    {
        passed++;
    }
    else
    {
        failures.Add(name);
    }
}

bool ConstantMatches(Type declaringType, string fieldName, object expected)
{
    System.Reflection.FieldInfo? field = declaringType.GetField(
        fieldName,
        System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic);
    return field != null
        && field.IsLiteral
        && Equals(field.GetRawConstantValue(), expected);
}

bool TryGetInt(JsonElement element, out int value)
{
    if (element.ValueKind == JsonValueKind.Number)
    {
        return element.TryGetInt32(out value);
    }

    if (element.ValueKind == JsonValueKind.String)
    {
        return int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    value = 0;
    return false;
}

bool TryGetFloat(JsonElement element, out float value)
{
    if (element.ValueKind == JsonValueKind.Number)
    {
        return element.TryGetSingle(out value);
    }

    if (element.ValueKind == JsonValueKind.String)
    {
        return float.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    value = 0f;
    return false;
}

bool Nearly(float actual, float expected)
{
    return Math.Abs(actual - expected) <= 0.00001f;
}

bool NearlyWithin(float actual, float expected, float tolerance)
{
    return Math.Abs(actual - expected) <= tolerance;
}

bool ReportPairWriterCreatesOnlyACompletePair()
{
    string directory = Path.Combine(Path.GetTempPath(), "BallisticsLab.Pair." + Guid.NewGuid().ToString("N"));
    try
    {
        ReportPairPaths paths = ReportPairWriter.Write(directory, "capture", "a,b\n1,2\n", "{\"records\":[]}");
        string[] files = Directory.GetFiles(directory)
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        return files.SequenceEqual(new[] { "capture.csv", "capture.json" }, StringComparer.Ordinal)
            && File.ReadAllText(paths.CsvPath) == "a,b\n1,2\n"
            && File.ReadAllText(paths.JsonPath) == "{\"records\":[]}"
            && !File.ReadAllBytes(paths.CsvPath).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF });
    }
    finally
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}

bool ReportPairWriterRejectsExistingStem()
{
    string directory = Path.Combine(Path.GetTempPath(), "BallisticsLab.Collision." + Guid.NewGuid().ToString("N"));
    try
    {
        ReportPairWriter.Write(directory, "capture", "first", "first");
        try
        {
            ReportPairWriter.Write(directory, "capture", "second", "second");
            return false;
        }
        catch (IOException)
        {
            return File.ReadAllText(Path.Combine(directory, "capture.csv")) == "first"
                && File.ReadAllText(Path.Combine(directory, "capture.json")) == "first";
        }
    }
    finally
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}

bool ChangedChainBatchKeepsParents()
{
    BatchRow[] records =
    {
        new(1, "chain-a"),
        new(2, "chain-a"),
        new(3, "chain-b"),
        new(4, "chain-a")
    };
    IReadOnlyList<BatchRow> batch = LabPolicies.SelectChangedChains(
        records,
        3,
        record => record.Sequence,
        record => record.ChainId);
    return batch.Select(record => record.Sequence).SequenceEqual(new long[] { 1, 2, 4 });
}

bool ChangedChainBatchExcludesSavedChains()
{
    BatchRow[] records =
    {
        new(1, "chain-a"),
        new(2, "chain-b")
    };
    return LabPolicies.SelectChangedChains(
            records,
            2,
            record => record.Sequence,
            record => record.ChainId).Count == 0;
}

bool ChangedChainBatchKeepsUnchainedRecordsSeparate()
{
    BatchRow[] records =
    {
        new(1, string.Empty),
        new(2, null),
        new(3, "chain-a")
    };
    IReadOnlyList<BatchRow> batch = LabPolicies.SelectChangedChains(
        records,
        1,
        record => record.Sequence,
        record => record.ChainId);
    return batch.Select(record => record.Sequence).SequenceEqual(new long[] { 2, 3 });
}

internal sealed record PlateRow(string Id, string Name, int ArmorClass, string Material, int Durability);
internal sealed record AmmoRow(string Name, float InitialSpeed);
internal sealed record BatchRow(long Sequence, string? ChainId);
