using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace BallisticsLab.Core
{
    internal sealed class CampaignPhysicalEvidenceSummary
    {
        internal CampaignPhysicalEvidenceSummary(
            int transitionCount,
            int conservationRecordCount,
            double maximumMassClosureErrorKilograms,
            double maximumEnergyClosureErrorJoules,
            IReadOnlyList<string> targetMaterialClasses)
        {
            TransitionCount = transitionCount;
            ConservationRecordCount = conservationRecordCount;
            MaximumMassClosureErrorKilograms = maximumMassClosureErrorKilograms;
            MaximumEnergyClosureErrorJoules = maximumEnergyClosureErrorJoules;
            TargetMaterialClasses = targetMaterialClasses;
        }

        internal int TransitionCount { get; }
        internal int ConservationRecordCount { get; }
        internal double MaximumMassClosureErrorKilograms { get; }
        internal double MaximumEnergyClosureErrorJoules { get; }
        internal IReadOnlyList<string> TargetMaterialClasses { get; }
    }

    internal static class CampaignPhysicalEvidenceCalculator
    {
        internal static CampaignPhysicalEvidenceSummary Calculate(
            IReadOnlyList<PhysicalTransitionRecord> transitions,
            int rootFireIndex,
            int rootRandomSeed,
            string ammunitionTemplateId,
            string rootShooterProfileId,
            long fixtureId,
            int fixtureLayerCount)
        {
            if (transitions == null)
            {
                ThrowNullTransitions(nameof(transitions));
            }
            if (string.IsNullOrWhiteSpace(ammunitionTemplateId))
            {
                ThrowMissingIdentity(nameof(ammunitionTemplateId));
            }
            if (fixtureId <= 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fixtureId),
                    "Fixture identity must be positive.");
            }
            if (fixtureLayerCount <= 0 || fixtureLayerCount > LabPolicies.MaximumLayers)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fixtureLayerCount),
                    "Fixture layer count is outside the supported range.");
            }

            var plateSurfaceIdentities = new HashSet<string>(StringComparer.Ordinal);
            for (int layerIndex = 0; layerIndex < fixtureLayerCount; layerIndex++)
            {
                plateSurfaceIdentities.Add(
                    FixturePhysicalMaterialContract.CreatePlateSurfaceIdentity(
                        fixtureId,
                        layerIndex));
            }

            int transitionCount = 0;
            int conservationRecordCount = 0;
            double maximumMassError = 0d;
            double maximumEnergyError = 0d;
            var targetMaterialClasses = new SortedSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < transitions.Count; index++)
            {
                PhysicalTelemetryEventRecord? resolved = transitions[index].Resolved;
                if (resolved == null || !Matches(
                        resolved.Host,
                        rootFireIndex,
                        rootRandomSeed,
                        ammunitionTemplateId,
                        rootShooterProfileId)
                    || !plateSurfaceIdentities.Contains(
                        resolved.Impact.TargetSurfaceIdentity))
                {
                    continue;
                }

                transitionCount++;
                targetMaterialClasses.Add(resolved.Impact.TargetMaterialClass);
                PhysicalConservationRecord? conservation = resolved.Conservation;
                if (conservation == null)
                {
                    continue;
                }

                conservationRecordCount++;
                maximumMassError = Math.Max(
                    maximumMassError,
                    MaximumMassError(conservation, resolved.Outputs));
                maximumEnergyError = Math.Max(
                    maximumEnergyError,
                    Math.Abs(conservation.EnergyClosureErrorJoules));
            }

            return new CampaignPhysicalEvidenceSummary(
                transitionCount,
                conservationRecordCount,
                maximumMassError,
                maximumEnergyError,
                new List<string>(targetMaterialClasses).AsReadOnly());
        }

        private static bool Matches(
            PhysicalTelemetryHostRecord host,
            int rootFireIndex,
            int rootRandomSeed,
            string ammunitionTemplateId,
            string rootShooterProfileId)
        {
            return host.RootFireIndex == rootFireIndex
                && host.RootRandomSeed == rootRandomSeed
                && string.Equals(
                    host.AmmunitionTemplateId,
                    ammunitionTemplateId,
                    StringComparison.Ordinal)
                && string.Equals(
                    host.RootShooterProfileId,
                    rootShooterProfileId,
                    StringComparison.Ordinal);
        }

        private static double MaximumMassError(
            PhysicalConservationRecord conservation,
            IReadOnlyList<PhysicalComponentRecord> outputs)
        {
            double parentDerivedMass = 0d;
            double targetMaterialMass = 0d;
            for (int index = 0; index < outputs.Count; index++)
            {
                PhysicalComponentRecord output = outputs[index];
                if (output.IsParentDerivedMass)
                {
                    parentDerivedMass += output.RetainedMassKilograms;
                }
                if (output.IsTargetMaterialOrigin)
                {
                    targetMaterialMass += output.RetainedMassKilograms;
                }
            }

            double allocationError = Math.Abs(
                conservation.ParentMassKilograms
                - conservation.AllocatedParentMassKilograms
                - conservation.UnallocatedParentMassKilograms);
            double outputParentMassError = Math.Abs(
                conservation.AllocatedParentMassKilograms - parentDerivedMass);
            double targetSpallMassError = Math.Abs(
                conservation.TargetSpallMassKilograms - targetMaterialMass);
            return Math.Max(allocationError, Math.Max(outputParentMassError, targetSpallMassError));
        }

        [DoesNotReturn]
        private static void ThrowNullTransitions(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }

        [DoesNotReturn]
        private static void ThrowMissingIdentity(string parameterName)
        {
            throw new ArgumentException("Ammunition template identity is required.", parameterName);
        }
    }
}
