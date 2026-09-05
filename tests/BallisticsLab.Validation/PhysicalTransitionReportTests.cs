using System.Text;
using System.Text.Json;
using BallisticsLab.Core;

namespace BallisticsLab.Validation;

internal static class PhysicalTransitionReportTests
{
    internal static bool SchemaFiveWriterPreservesCompletePhysicalEvidence()
    {
        var tracker = new PhysicalTransitionTracker(8);
        tracker.Add(Copy(FakePhysicalEvent.Prepared("complete")));
        tracker.Add(Copy(FakePhysicalEvent.Prepared("complete")));
        tracker.Add(Copy(FakePhysicalEvent.Resolved("complete")));
        tracker.Add(Copy(FakePhysicalEvent.Prepared("pending")));
        tracker.Add(Copy(FakePhysicalEvent.Resolved("orphan")));

        var builder = new StringBuilder();
        PhysicalTransitionJsonWriter.AppendArray(builder, tracker.Snapshot());
        using JsonDocument document = JsonDocument.Parse(builder.ToString());
        JsonElement transitions = document.RootElement;
        if (transitions.GetArrayLength() != 3)
        {
            return false;
        }

        JsonElement complete = transitions[0];
        JsonElement prepared = complete.GetProperty("prepared");
        JsonElement resolved = complete.GetProperty("resolved");
        JsonElement parent = resolved.GetProperty("parent");
        JsonElement parentOutput = resolved.GetProperty("outputs")[0];
        JsonElement output = resolved.GetProperty("outputs")[1];
        JsonElement conservation = resolved.GetProperty("conservation");
        JsonElement lossBudget = conservation.GetProperty("lossBudget");
        return complete.GetProperty("firstSeenOrdinal").GetInt64() == 1L
            && complete.GetProperty("lastUpdatedRevision").GetInt64() == 3L
            && complete.GetProperty("transitionId").GetString() == "complete"
            && complete.GetProperty("state").GetString() == "Completed"
            && complete.GetProperty("preparedDuplicateCount").GetInt32() == 1
            && complete.GetProperty("resolvedDuplicateCount").GetInt32() == 0
            && prepared.GetProperty("stage").GetString() == "CollisionPrepared"
            && resolved.GetProperty("stage").GetString() == "CollisionResolved"
            && resolved.GetProperty("host").GetProperty("ammunitionTemplateId").GetString()
                == "ammo-template"
            && Nearly(
                resolved.GetProperty("impact").GetProperty("physicalThicknessMetres").GetDouble(),
                0.0127d)
            && resolved.GetProperty("impact").GetProperty("targetSurfaceIdentity").GetString()
                == "fixture/700/plate/0"
            && parent.GetProperty("projectileId").GetString() == "projectile-parent"
            && Nearly(parent.GetProperty("originalMassKilograms").GetDouble(), 0.004d)
            && Nearly(parent.GetProperty("retainedMassKilograms").GetDouble(), 0.004d)
            && Nearly(parent.GetProperty("equivalentDiameterMetres").GetDouble(), 0.0057d)
            && parent.GetProperty("orientation").GetArrayLength() == 4
            && parent.GetProperty("collisionHistory").GetArrayLength() == 1
            && parentOutput.GetProperty("kind").GetString() == "DeformedProjectile"
            && !parentOutput.GetProperty("isParentDerivedMass").GetBoolean()
            && !parentOutput.GetProperty("isTargetMaterialOrigin").GetBoolean()
            && output.GetProperty("kind").GetString() == "TargetSpall"
            && output.GetProperty("sourceMaterialClass").GetString() == "ArmoredSteel"
            && output.GetProperty("isTargetMaterialOrigin").GetBoolean()
            && !output.GetProperty("isParentDerivedMass").GetBoolean()
            && Nearly(conservation.GetProperty("parentMassKilograms").GetDouble(), 0.004d)
            && Nearly(conservation.GetProperty("allocatedParentMassKilograms").GetDouble(), 0.003d)
            && Nearly(conservation.GetProperty("unallocatedParentMassKilograms").GetDouble(), 0.001d)
            && Nearly(conservation.GetProperty("targetSpallMassKilograms").GetDouble(), 0.0002d)
            && Nearly(lossBudget.GetProperty("totalLossJoules").GetDouble(), 510d)
            && Nearly(conservation.GetProperty("residualEnergyJoules").GetDouble(), 770d)
            && Nearly(conservation.GetProperty("outputEnergyJoules").GetDouble(), 760d)
            && Nearly(conservation.GetProperty("energyClosureErrorJoules").GetDouble(), 10d)
            && transitions[1].GetProperty("state").GetString() == "Pending"
            && transitions[1].GetProperty("resolved").ValueKind == JsonValueKind.Null
            && transitions[2].GetProperty("state").GetString() == "Orphaned"
            && transitions[2].GetProperty("prepared").ValueKind == JsonValueKind.Null;
    }

    internal static bool PhysicalOnlyChangesTriggerCombinedReportPolicy()
    {
        return !LabPolicies.ShouldSaveCombinedReport(0, 0L, 0L, 0, 0L, 0L)
            && LabPolicies.ShouldSaveCombinedReport(0, 0L, 0L, 1, 1L, 0L)
            && !LabPolicies.ShouldSaveCombinedReport(0, 0L, 0L, 1, 1L, 1L)
            && LabPolicies.ShouldSaveCombinedReport(1, 2L, 1L, 0, 0L, 0L);
    }

    internal static bool PhysicalOnlyDocumentUsesSchemaFiveAndEmptyShotArray()
    {
        var tracker = new PhysicalTransitionTracker(2);
        tracker.Add(Copy(FakePhysicalEvent.Prepared("physical-only")));
        string json = PhysicalReportDocumentWriter.Build("[]", tracker.Snapshot());
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("schema").GetInt32() == 5
            && document.RootElement.GetProperty("pluginVersion").GetString() == LabBuild.PluginVersion
            && document.RootElement.GetProperty("records").GetArrayLength() == 0
            && document.RootElement.GetProperty("physicalTransitions").GetArrayLength() == 1;
    }

    internal static bool UpdatedRevisionIdentifiesOnlyChangedTransitions()
    {
        var tracker = new PhysicalTransitionTracker(4);
        tracker.Add(Copy(FakePhysicalEvent.Prepared("first")));
        tracker.Add(Copy(FakePhysicalEvent.Prepared("second")));
        long savedRevision = tracker.Revision;
        tracker.Add(Copy(FakePhysicalEvent.Resolved("first")));

        PhysicalTransitionRecord[] changed = tracker.Snapshot()
            .Where(transition => transition.LastUpdatedRevision > savedRevision)
            .ToArray();
        return changed.Length == 1
            && changed[0].TransitionId == "first"
            && changed[0].LastUpdatedRevision == 3L;
    }

    private static PhysicalTelemetryEventRecord Copy(FakePhysicalEvent source)
    {
        if (!PhysicalTelemetryReflectionReader.TryCopy(
                PhysicalTelemetryContract.SupportedPublisherSchema,
                source.Event,
                out PhysicalTelemetryEventRecord? record,
                out string failure)
            || record == null)
        {
            throw new InvalidOperationException(failure);
        }
        return record;
    }

    private static bool Nearly(double actual, double expected)
    {
        return Math.Abs(actual - expected) <= 0.000000001d;
    }
}
