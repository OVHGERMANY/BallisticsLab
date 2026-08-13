using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using BallisticsLab.Core;

namespace BallisticsLab.Runtime.Telemetry
{
    internal static class TelemetryStore
    {
        private static readonly object Sync = new object();
        private static readonly List<ShotRecord> Records = new List<ShotRecord>();
        private static long _sequence;
        private static long _revision;
        private static long _savedRevision;
        private static long _savedPhysicalRevision;
        private static long _savedSequence;
        private static DateTime _lastRecordUtc;
        private static DateTime _nextAutomaticAttemptUtc;
        private static int _captureOrdinal;
        private static int _manualExportOrdinal;

        internal static ShotRecord? Latest
        {
            get
            {
                lock (Sync)
                {
                    return Records.Count > 0 ? Records[Records.Count - 1] : null;
                }
            }
        }

        internal static int Count
        {
            get
            {
                lock (Sync)
                {
                    return Records.Count;
                }
            }
        }

        internal static void Complete(ShotApplicationState state)
        {
            if (state?.Shot == null)
            {
                return;
            }

            ShotRecord record = ShotRecord.Complete(Interlocked.Increment(ref _sequence), state);
            lock (Sync)
            {
                Records.Add(record);
                if (Records.Count > LabPolicies.MaximumRecords)
                {
                    Records.RemoveAt(0);
                }
                _revision++;
                _lastRecordUtc = DateTime.UtcNow;
            }

            LabRuntime.NotifyRecord(record);
        }

        internal static IReadOnlyList<ShotRecord> Snapshot()
        {
            lock (Sync)
            {
                return Records.ToArray();
            }
        }

        internal static IReadOnlyList<ShotRecord> SnapshotChain(string chainId)
        {
            if (string.IsNullOrEmpty(chainId))
            {
                return Array.Empty<ShotRecord>();
            }

            lock (Sync)
            {
                return Records.Where(record => record.ChainId == chainId).ToArray();
            }
        }

        internal static void Clear()
        {
            lock (Sync)
            {
                Records.Clear();
                Interlocked.Exchange(ref _sequence, 0L);
                _revision = 0L;
                _savedRevision = 0L;
                _savedPhysicalRevision = 0L;
                _savedSequence = 0L;
                _lastRecordUtc = DateTime.MinValue;
                _nextAutomaticAttemptUtc = DateTime.MinValue;
            }
        }

        internal static string Export()
        {
            ShotRecord[] records;
            IReadOnlyList<PhysicalTransitionRecord> transitions =
                PhysicalTelemetrySessionBridge.SnapshotTransitions(
                    out long physicalRevision,
                    out _);
            long revision;
            lock (Sync)
            {
                if (Records.Count == 0 && transitions.Count == 0)
                {
                    throw new InvalidOperationException(
                        "No shot records or physical transitions are available to export.");
                }
                records = Records.ToArray();
                revision = _revision;
            }
            int ordinal = Interlocked.Increment(ref _manualExportOrdinal);
            string stem = LabPolicies.ReportStem(DateTime.UtcNow, ordinal);
            string result = WriteReportPair(records, transitions, stem);
            lock (Sync)
            {
                if (revision > _savedRevision)
                {
                    _savedRevision = revision;
                    if (records.Length != 0)
                    {
                        _savedSequence = records.Max(record => record.Sequence);
                    }
                }
                if (physicalRevision > _savedPhysicalRevision)
                {
                    _savedPhysicalRevision = physicalRevision;
                }
            }
            return result;
        }

        internal static string? ExportAutomatic(bool force)
        {
            IReadOnlyList<ShotRecord> records;
            PhysicalTransitionRecord[] transitionsToWrite;
            IReadOnlyList<PhysicalTransitionRecord> transitions =
                PhysicalTelemetrySessionBridge.SnapshotTransitions(
                    out long physicalRevision,
                    out DateTime lastPhysicalRecordUtc);
            string stem;
            long revision;
            DateTime now = DateTime.UtcNow;
            lock (Sync)
            {
                bool shotChanged = LabPolicies.ShouldSaveReport(
                    Records.Count,
                    _revision,
                    _savedRevision);
                bool physicalChanged = LabPolicies.ShouldSaveReport(
                    transitions.Count,
                    physicalRevision,
                    _savedPhysicalRevision);
                DateTime lastEvidenceUtc = _lastRecordUtc > lastPhysicalRecordUtc
                    ? _lastRecordUtc
                    : lastPhysicalRecordUtc;
                if (!LabPolicies.ShouldSaveCombinedReport(
                        Records.Count,
                        _revision,
                        _savedRevision,
                        transitions.Count,
                        physicalRevision,
                        _savedPhysicalRevision)
                    || (!force && now < lastEvidenceUtc.AddSeconds(1.25))
                    || (!force && now < _nextAutomaticAttemptUtc))
                {
                    return null;
                }

                records = shotChanged
                    ? LabPolicies.SelectChangedChains(
                        Records,
                        _savedSequence,
                        record => record.Sequence,
                        record => record.ChainId)
                    : Array.Empty<ShotRecord>();
                transitionsToWrite = physicalChanged
                    ? transitions
                        .Where(transition => transition.LastUpdatedRevision > _savedPhysicalRevision)
                        .ToArray()
                    : Array.Empty<PhysicalTransitionRecord>();
                if (records.Count == 0 && transitionsToWrite.Length == 0)
                {
                    return null;
                }
                revision = _revision;
                _captureOrdinal++;
                stem = LabPolicies.ReportStem(now, _captureOrdinal) + "-auto";
                _nextAutomaticAttemptUtc = now.AddSeconds(5.0);
            }

            string result = WriteReportPair(records, transitionsToWrite, stem);
            lock (Sync)
            {
                if (revision > _savedRevision)
                {
                    _savedRevision = revision;
                    long savedSequence = records.Max(record => record.Sequence);
                    if (savedSequence > _savedSequence)
                    {
                        _savedSequence = savedSequence;
                    }
                }
                if (physicalRevision > _savedPhysicalRevision)
                {
                    _savedPhysicalRevision = physicalRevision;
                }
            }
            return result;
        }

        private static string WriteReportPair(
            IReadOnlyList<ShotRecord> records,
            IReadOnlyList<PhysicalTransitionRecord> transitions,
            string stem)
        {
            string pluginDirectory = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
            string reports = Path.Combine(pluginDirectory ?? string.Empty, "Reports");
            Directory.CreateDirectory(reports);
            return ReportPairWriter.Write(
                reports,
                stem,
                BuildCsv(records),
                BuildJson(records, transitions)).ToString();
        }

        private static string BuildCsv(IReadOnlyList<ShotRecord> records)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(
                "schema,pluginVersion,sequence,utc,chainId,fireIndex,fragmentIndex,parentDepth,rootRandomSeed,isForwardHit,ammoTemplateId,ammoName,shooter,targetKind,target,material,fixtureId,layer,layerCount,fixtureTemplateId,fixtureName,fixtureArmorClass,fixtureArmorMaterial,layerSpacing,colliderThickness,outcome,angleDegrees,impactSpeed,templateSpeed,fraction,incomingDamage,incomingPenetration,decisionDamage,decisionPenetration,armorRealResistance,armorClassResistance,armorCf,penetrationChancePercent,blockedBy,deflectedBy,fragments,durabilityBefore,durabilityAfter,fixtureMaximumDurability,bodyHealthBefore,bodyHealthAfter,targetAliveBefore,targetAliveAfter,armorChanges,continuationKind,continuationSourceFixtureId,continuationSourceLayer,continuationPenetrationFactor,continuationVelocityFactor,continuationOutcomeFactor,continuationArmorCf,continuationDamageBefore,continuationPenetrationBefore,continuationDamageAfter,continuationPenetrationAfter,hitX,hitY,hitZ");
            foreach (ShotRecord record in records)
            {
                string[] values =
                {
                    LabBuild.ReportSchema.ToString(CultureInfo.InvariantCulture),
                    LabBuild.PluginVersion,
                    record.Sequence.ToString(CultureInfo.InvariantCulture),
                    record.Utc.ToString("O", CultureInfo.InvariantCulture),
                    record.ChainId,
                    record.FireIndex.ToString(CultureInfo.InvariantCulture),
                    record.FragmentIndex.ToString(CultureInfo.InvariantCulture),
                    record.ParentDepth.ToString(CultureInfo.InvariantCulture),
                    record.RootRandomSeed.ToString(CultureInfo.InvariantCulture),
                    record.IsForwardHit ? "true" : "false",
                    record.AmmoTemplateId,
                    record.AmmoName,
                    record.ShooterProfileId,
                    record.TargetKind,
                    record.Target,
                    record.Material,
                    record.FixtureId.ToString(CultureInfo.InvariantCulture),
                    record.LayerIndex.ToString(CultureInfo.InvariantCulture),
                    record.FixtureLayerCount.ToString(CultureInfo.InvariantCulture),
                    record.FixtureTemplateId,
                    record.FixtureName,
                    record.FixtureArmorClass.ToString(CultureInfo.InvariantCulture),
                    record.FixtureArmorMaterial,
                    F(record.FixtureLayerSpacing),
                    F(record.FixtureColliderThickness),
                    record.Outcome,
                    F(record.ImpactAngle),
                    F(record.ImpactSpeed),
                    F(record.TemplateSpeed),
                    F(record.Fraction),
                    F(record.IncomingDamage),
                    F(record.IncomingPenetration),
                    F(record.DecisionDamage),
                    F(record.DecisionPenetration),
                    F(record.ArmorRealResistance),
                    F(record.ArmorClassResistance),
                    F(record.ArmorCf),
                    F(record.PenetrationChancePercent),
                    record.BlockedBy,
                    record.DeflectedBy,
                    record.FragmentCount.ToString(CultureInfo.InvariantCulture),
                    F(record.DurabilityBefore),
                    F(record.DurabilityAfter),
                    F(record.FixtureMaximumDurability),
                    F(record.BodyHealthBefore),
                    F(record.BodyHealthAfter),
                    record.TargetAliveBefore ? "true" : "false",
                    record.TargetAliveAfter ? "true" : "false",
                    record.ArmorChanges,
                    record.ContinuationKind,
                    record.ContinuationSourceFixtureId.ToString(CultureInfo.InvariantCulture),
                    record.ContinuationSourceLayerIndex.ToString(CultureInfo.InvariantCulture),
                    F(record.ContinuationPenetrationFactor),
                    F(record.ContinuationVelocityFactor),
                    F(record.ContinuationOutcomeFactor),
                    F(record.ContinuationArmorCf),
                    F(record.ContinuationDamageBefore),
                    F(record.ContinuationPenetrationBefore),
                    F(record.ContinuationDamageAfter),
                    F(record.ContinuationPenetrationAfter),
                    F(record.HitPoint.x),
                    F(record.HitPoint.y),
                    F(record.HitPoint.z)
                };
                builder.AppendLine(string.Join(",", values.Select(LabPolicies.Csv)));
            }

            return builder.ToString();
        }

        private static string BuildJson(
            IReadOnlyList<ShotRecord> records,
            IReadOnlyList<PhysicalTransitionRecord> transitions)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append('[');
            for (int index = 0; index < records.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                ShotRecord record = records[index];
                builder.Append("{\"sequence\":").Append(record.Sequence.ToString(CultureInfo.InvariantCulture));
                AppendJson(builder, "utc", LabPolicies.Json(record.Utc.ToString("O", CultureInfo.InvariantCulture)));
                AppendJson(builder, "chainId", LabPolicies.Json(record.ChainId));
                AppendJson(builder, "fireIndex", record.FireIndex.ToString(CultureInfo.InvariantCulture));
                AppendJson(builder, "fragmentIndex", record.FragmentIndex.ToString(CultureInfo.InvariantCulture));
                AppendJson(builder, "parentDepth", record.ParentDepth.ToString(CultureInfo.InvariantCulture));
                AppendJson(builder, "rootRandomSeed", record.RootRandomSeed.ToString(CultureInfo.InvariantCulture));
                AppendJson(builder, "isForwardHit", record.IsForwardHit ? "true" : "false");
                AppendJson(builder, "ammoTemplateId", LabPolicies.Json(record.AmmoTemplateId));
                AppendJson(builder, "ammoName", LabPolicies.Json(record.AmmoName));
                AppendJson(builder, "shooter", LabPolicies.Json(record.ShooterProfileId));
                AppendJson(builder, "targetKind", LabPolicies.Json(record.TargetKind));
                AppendJson(builder, "target", LabPolicies.Json(record.Target));
                AppendJson(builder, "material", LabPolicies.Json(record.Material));
                AppendJson(builder, "fixtureId", record.FixtureId.ToString(CultureInfo.InvariantCulture));
                AppendJson(builder, "layer", record.LayerIndex.ToString(CultureInfo.InvariantCulture));
                AppendJson(builder, "layerCount", record.FixtureLayerCount.ToString(CultureInfo.InvariantCulture));
                AppendJson(builder, "fixtureTemplateId", LabPolicies.Json(record.FixtureTemplateId));
                AppendJson(builder, "fixtureName", LabPolicies.Json(record.FixtureName));
                AppendJson(builder, "fixtureArmorClass", record.FixtureArmorClass.ToString(CultureInfo.InvariantCulture));
                AppendJson(builder, "fixtureArmorMaterial", LabPolicies.Json(record.FixtureArmorMaterial));
                AppendJson(builder, "layerSpacing", F(record.FixtureLayerSpacing));
                AppendJson(builder, "colliderThickness", F(record.FixtureColliderThickness));
                AppendJson(builder, "outcome", LabPolicies.Json(record.Outcome));
                AppendJson(builder, "angleDegrees", F(record.ImpactAngle));
                AppendJson(builder, "impactSpeed", F(record.ImpactSpeed));
                AppendJson(builder, "templateSpeed", F(record.TemplateSpeed));
                AppendJson(builder, "fraction", F(record.Fraction));
                AppendJson(builder, "incomingDamage", F(record.IncomingDamage));
                AppendJson(builder, "incomingPenetration", F(record.IncomingPenetration));
                AppendJson(builder, "decisionDamage", F(record.DecisionDamage));
                AppendJson(builder, "decisionPenetration", F(record.DecisionPenetration));
                AppendJson(builder, "armorRealResistance", F(record.ArmorRealResistance));
                AppendJson(builder, "armorClassResistance", F(record.ArmorClassResistance));
                AppendJson(builder, "armorCf", F(record.ArmorCf));
                AppendJson(builder, "penetrationChancePercent", F(record.PenetrationChancePercent));
                AppendJson(builder, "blockedBy", LabPolicies.Json(record.BlockedBy));
                AppendJson(builder, "deflectedBy", LabPolicies.Json(record.DeflectedBy));
                AppendJson(builder, "fragmentCount", record.FragmentCount.ToString(CultureInfo.InvariantCulture));
                AppendJson(builder, "durabilityBefore", F(record.DurabilityBefore));
                AppendJson(builder, "durabilityAfter", F(record.DurabilityAfter));
                AppendJson(builder, "fixtureMaximumDurability", F(record.FixtureMaximumDurability));
                AppendJson(builder, "bodyHealthBefore", F(record.BodyHealthBefore));
                AppendJson(builder, "bodyHealthAfter", F(record.BodyHealthAfter));
                AppendJson(builder, "targetAliveBefore", record.TargetAliveBefore ? "true" : "false");
                AppendJson(builder, "targetAliveAfter", record.TargetAliveAfter ? "true" : "false");
                AppendJson(builder, "armorChanges", LabPolicies.Json(record.ArmorChanges));
                AppendJson(builder, "continuationKind", LabPolicies.Json(record.ContinuationKind));
                AppendJson(builder, "continuationSourceFixtureId", record.ContinuationSourceFixtureId.ToString(CultureInfo.InvariantCulture));
                AppendJson(builder, "continuationSourceLayer", record.ContinuationSourceLayerIndex.ToString(CultureInfo.InvariantCulture));
                AppendJson(builder, "continuationPenetrationFactor", F(record.ContinuationPenetrationFactor));
                AppendJson(builder, "continuationVelocityFactor", F(record.ContinuationVelocityFactor));
                AppendJson(builder, "continuationOutcomeFactor", F(record.ContinuationOutcomeFactor));
                AppendJson(builder, "continuationArmorCf", F(record.ContinuationArmorCf));
                AppendJson(builder, "continuationDamageBefore", F(record.ContinuationDamageBefore));
                AppendJson(builder, "continuationPenetrationBefore", F(record.ContinuationPenetrationBefore));
                AppendJson(builder, "continuationDamageAfter", F(record.ContinuationDamageAfter));
                AppendJson(builder, "continuationPenetrationAfter", F(record.ContinuationPenetrationAfter));
                builder.Append(",\"hitPoint\":[").Append(F(record.HitPoint.x)).Append(',').Append(F(record.HitPoint.y)).Append(',').Append(F(record.HitPoint.z)).Append(']');
                builder.Append(",\"path\":[");
                for (int pointIndex = 0; pointIndex < record.Path.Count; pointIndex++)
                {
                    if (pointIndex > 0)
                    {
                        builder.Append(',');
                    }
                    builder.Append('[')
                        .Append(F(record.Path[pointIndex].x)).Append(',')
                        .Append(F(record.Path[pointIndex].y)).Append(',')
                        .Append(F(record.Path[pointIndex].z)).Append(']');
                }
                builder.Append("]}");
            }
            builder.Append(']');
            return PhysicalReportDocumentWriter.Build(builder.ToString(), transitions);
        }

        private static void AppendJson(StringBuilder builder, string name, string value)
        {
            builder.Append(",\"").Append(name).Append("\":");
            builder.Append(value);
        }

        private static string F(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? "null"
                : value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
