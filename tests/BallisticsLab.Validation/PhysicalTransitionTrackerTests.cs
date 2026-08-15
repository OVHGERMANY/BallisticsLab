using BallisticsLab.Core;

namespace BallisticsLab.Validation;

internal static class PhysicalTransitionTrackerTests
{
    internal static bool PreparedAndResolvedPairByExactTransitionId()
    {
        var tracker = new PhysicalTransitionTracker(4);
        tracker.Add(Copy(FakePhysicalEvent.Prepared("pair")));
        tracker.Add(Copy(FakePhysicalEvent.Resolved("pair")));
        IReadOnlyList<PhysicalTransitionRecord> records = tracker.Snapshot();
        return records.Count == 1
            && records[0].TransitionId == "pair"
            && records[0].State == PhysicalTransitionState.Completed
            && records[0].Prepared?.Stage == PhysicalTelemetryStageRecord.CollisionPrepared
            && records[0].Resolved?.Stage == PhysicalTelemetryStageRecord.CollisionResolved
            && records[0].DuplicateEventCount == 0
            && tracker.Revision == 2L;
    }

    internal static bool PreparedOnlyRemainsPendingEvidence()
    {
        var tracker = new PhysicalTransitionTracker(4);
        tracker.Add(Copy(FakePhysicalEvent.Prepared("pending")));
        PhysicalTransitionRecord record = tracker.Snapshot()[0];
        return record.State == PhysicalTransitionState.Pending
            && record.Prepared != null
            && record.Resolved == null;
    }

    internal static bool ResolvedOnlyIsMarkedOrphaned()
    {
        var tracker = new PhysicalTransitionTracker(4);
        tracker.Add(Copy(FakePhysicalEvent.Resolved("orphan")));
        PhysicalTransitionRecord record = tracker.Snapshot()[0];
        return record.State == PhysicalTransitionState.Orphaned
            && record.Prepared == null
            && record.Resolved != null;
    }

    internal static bool OutOfOrderArrivalConvergesToCompleted()
    {
        var tracker = new PhysicalTransitionTracker(4);
        tracker.Add(Copy(FakePhysicalEvent.Resolved("late-prepared")));
        bool beganOrphaned = tracker.Snapshot()[0].State == PhysicalTransitionState.Orphaned;
        tracker.Add(Copy(FakePhysicalEvent.Prepared("late-prepared")));
        PhysicalTransitionRecord completed = tracker.Snapshot()[0];
        return beganOrphaned
            && completed.State == PhysicalTransitionState.Completed
            && completed.FirstSeenOrdinal == 1L;
    }

    internal static bool DuplicateStagesAreCountedAndFirstEventWins()
    {
        var tracker = new PhysicalTransitionTracker(4);
        PhysicalTelemetryEventRecord firstPrepared = Copy(FakePhysicalEvent.Prepared("duplicate"));
        PhysicalTelemetryEventRecord duplicatePrepared = Copy(FakePhysicalEvent.Prepared("duplicate"));
        PhysicalTelemetryEventRecord firstResolved = Copy(FakePhysicalEvent.Resolved("duplicate"));
        PhysicalTelemetryEventRecord duplicateResolved = Copy(FakePhysicalEvent.Resolved("duplicate"));
        tracker.Add(firstPrepared);
        tracker.Add(duplicatePrepared);
        tracker.Add(firstResolved);
        tracker.Add(duplicateResolved);
        PhysicalTransitionRecord record = tracker.Snapshot()[0];
        return tracker.Count == 1
            && tracker.Revision == 4L
            && ReferenceEquals(record.Prepared, firstPrepared)
            && ReferenceEquals(record.Resolved, firstResolved)
            && record.PreparedDuplicateCount == 1
            && record.ResolvedDuplicateCount == 1
            && record.DuplicateEventCount == 2
            && record.HasDuplicateEvents;
    }

    internal static bool TransitionIdsUseOrdinalCaseSensitiveIdentity()
    {
        var tracker = new PhysicalTransitionTracker(4);
        tracker.Add(Copy(FakePhysicalEvent.Prepared("Case")));
        tracker.Add(Copy(FakePhysicalEvent.Resolved("case")));
        IReadOnlyList<PhysicalTransitionRecord> records = tracker.Snapshot();
        return records.Count == 2
            && records[0].TransitionId == "Case"
            && records[0].State == PhysicalTransitionState.Pending
            && records[1].TransitionId == "case"
            && records[1].State == PhysicalTransitionState.Orphaned;
    }

    internal static bool CapacityEvictsOldestTransitionDeterministically()
    {
        var tracker = new PhysicalTransitionTracker(2);
        tracker.Add(Copy(FakePhysicalEvent.Prepared("first")));
        tracker.Add(Copy(FakePhysicalEvent.Prepared("second")));
        IReadOnlyList<PhysicalTransitionRecord> beforeEviction = tracker.Snapshot();
        tracker.Add(Copy(FakePhysicalEvent.Prepared("third")));
        IReadOnlyList<PhysicalTransitionRecord> afterEviction = tracker.Snapshot();
        return beforeEviction.Count == 2
            && beforeEviction[0].TransitionId == "first"
            && afterEviction.Count == 2
            && afterEviction[0].TransitionId == "second"
            && afterEviction[0].FirstSeenOrdinal == 2L
            && afterEviction[1].TransitionId == "third"
            && afterEviction[1].FirstSeenOrdinal == 3L;
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
}
