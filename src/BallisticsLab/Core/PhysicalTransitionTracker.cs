using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace BallisticsLab.Core
{
    internal enum PhysicalTransitionState
    {
        Pending = 0,
        Completed = 1,
        Orphaned = 2
    }

    internal sealed class PhysicalTransitionRecord
    {
        internal PhysicalTransitionRecord(
            long firstSeenOrdinal,
            string transitionId,
            PhysicalTelemetryEventRecord? prepared,
            PhysicalTelemetryEventRecord? resolved,
            int preparedDuplicateCount,
            int resolvedDuplicateCount)
        {
            FirstSeenOrdinal = firstSeenOrdinal;
            TransitionId = transitionId;
            Prepared = prepared;
            Resolved = resolved;
            PreparedDuplicateCount = preparedDuplicateCount;
            ResolvedDuplicateCount = resolvedDuplicateCount;
            State = prepared != null
                ? resolved != null
                    ? PhysicalTransitionState.Completed
                    : PhysicalTransitionState.Pending
                : PhysicalTransitionState.Orphaned;
        }

        internal long FirstSeenOrdinal { get; }
        internal string TransitionId { get; }
        internal PhysicalTransitionState State { get; }
        internal PhysicalTelemetryEventRecord? Prepared { get; }
        internal PhysicalTelemetryEventRecord? Resolved { get; }
        internal int PreparedDuplicateCount { get; }
        internal int ResolvedDuplicateCount { get; }
        internal int DuplicateEventCount => PreparedDuplicateCount + ResolvedDuplicateCount;
        internal bool HasDuplicateEvents => DuplicateEventCount != 0;
    }

    internal sealed class PhysicalTransitionTracker
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, Entry> _entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Queue<string> _order = new Queue<string>();
        private readonly int _maximumTransitions;
        private long _nextOrdinal;
        private long _revision;

        internal PhysicalTransitionTracker(int maximumTransitions)
        {
            if (maximumTransitions <= 0)
            {
                ThrowNonPositiveMaximum(nameof(maximumTransitions));
            }
            _maximumTransitions = maximumTransitions;
        }

        internal int Count
        {
            get
            {
                lock (_sync)
                {
                    return _entries.Count;
                }
            }
        }

        internal long Revision
        {
            get
            {
                lock (_sync)
                {
                    return _revision;
                }
            }
        }

        internal void Add(PhysicalTelemetryEventRecord record)
        {
            if (record == null)
            {
                ThrowNullRecord(nameof(record));
            }
            if (record.Stage != PhysicalTelemetryStageRecord.CollisionPrepared
                && record.Stage != PhysicalTelemetryStageRecord.CollisionResolved)
            {
                ThrowUnsupportedStage(record.Stage);
            }

            lock (_sync)
            {
                if (!_entries.TryGetValue(record.TransitionId, out Entry? entry))
                {
                    EvictOldestIfFull();
                    entry = new Entry(++_nextOrdinal, record.TransitionId);
                    _entries.Add(record.TransitionId, entry);
                    _order.Enqueue(record.TransitionId);
                }

                if (record.Stage == PhysicalTelemetryStageRecord.CollisionPrepared)
                {
                    if (entry.Prepared == null)
                    {
                        entry.Prepared = record;
                    }
                    else
                    {
                        entry.PreparedDuplicateCount++;
                    }
                }
                else if (record.Stage == PhysicalTelemetryStageRecord.CollisionResolved)
                {
                    if (entry.Resolved == null)
                    {
                        entry.Resolved = record;
                    }
                    else
                    {
                        entry.ResolvedDuplicateCount++;
                    }
                }
                _revision++;
            }
        }

        internal IReadOnlyList<PhysicalTransitionRecord> Snapshot()
        {
            lock (_sync)
            {
                var records = new List<PhysicalTransitionRecord>(_entries.Count);
                foreach (string transitionId in _order)
                {
                    Entry entry = _entries[transitionId];
                    records.Add(entry.ToRecord());
                }
                return new ReadOnlyCollection<PhysicalTransitionRecord>(records);
            }
        }

        internal void Clear()
        {
            lock (_sync)
            {
                _entries.Clear();
                _order.Clear();
                _nextOrdinal = 0L;
                _revision = 0L;
            }
        }

        private void EvictOldestIfFull()
        {
            if (_entries.Count < _maximumTransitions)
            {
                return;
            }
            string oldest = _order.Dequeue();
            _entries.Remove(oldest);
        }

        [DoesNotReturn]
        private static void ThrowNonPositiveMaximum(string parameterName)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Maximum transition count must be positive.");
        }

        [DoesNotReturn]
        private static void ThrowNullRecord(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }

        [DoesNotReturn]
        private static void ThrowUnsupportedStage(PhysicalTelemetryStageRecord stage)
        {
            throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unsupported physical telemetry stage.");
        }

        private sealed class Entry
        {
            internal Entry(long firstSeenOrdinal, string transitionId)
            {
                FirstSeenOrdinal = firstSeenOrdinal;
                TransitionId = transitionId;
            }

            internal long FirstSeenOrdinal { get; }
            internal string TransitionId { get; }
            internal PhysicalTelemetryEventRecord? Prepared { get; set; }
            internal PhysicalTelemetryEventRecord? Resolved { get; set; }
            internal int PreparedDuplicateCount { get; set; }
            internal int ResolvedDuplicateCount { get; set; }

            internal PhysicalTransitionRecord ToRecord()
            {
                return new PhysicalTransitionRecord(
                    FirstSeenOrdinal,
                    TransitionId,
                    Prepared,
                    Resolved,
                    PreparedDuplicateCount,
                    ResolvedDuplicateCount);
            }
        }
    }
}
