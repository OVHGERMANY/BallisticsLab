using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BallisticsLab.Core
{
    internal static class PhysicalTransitionJsonWriter
    {
        internal static void AppendArray(
            StringBuilder builder,
            IReadOnlyList<PhysicalTransitionRecord> transitions)
        {
            builder.Append('[');
            for (int index = 0; index < transitions.Count; index++)
            {
                if (index != 0)
                {
                    builder.Append(',');
                }
                AppendTransition(builder, transitions[index]);
            }
            builder.Append(']');
        }

        private static void AppendTransition(StringBuilder builder, PhysicalTransitionRecord transition)
        {
            builder.Append('{');
            bool first = true;
            Number(builder, ref first, "firstSeenOrdinal", transition.FirstSeenOrdinal);
            Number(builder, ref first, "lastUpdatedRevision", transition.LastUpdatedRevision);
            String(builder, ref first, "transitionId", transition.TransitionId);
            String(builder, ref first, "state", transition.State.ToString());
            Number(builder, ref first, "preparedDuplicateCount", transition.PreparedDuplicateCount);
            Number(builder, ref first, "resolvedDuplicateCount", transition.ResolvedDuplicateCount);
            Property(builder, ref first, "prepared");
            AppendEventOrNull(builder, transition.Prepared);
            Property(builder, ref first, "resolved");
            AppendEventOrNull(builder, transition.Resolved);
            builder.Append('}');
        }

        private static void AppendEventOrNull(
            StringBuilder builder,
            PhysicalTelemetryEventRecord? telemetryEvent)
        {
            if (telemetryEvent == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('{');
            bool first = true;
            Number(builder, ref first, "snapshotSchema", telemetryEvent.SnapshotSchema);
            Number(builder, ref first, "publisherSchema", telemetryEvent.PublisherSchema);
            String(builder, ref first, "stage", telemetryEvent.Stage.ToString());
            String(builder, ref first, "transitionId", telemetryEvent.TransitionId);
            String(builder, ref first, "outcome", telemetryEvent.Outcome);
            Property(builder, ref first, "host");
            AppendHost(builder, telemetryEvent.Host);
            Property(builder, ref first, "impact");
            AppendImpact(builder, telemetryEvent.Impact);
            Property(builder, ref first, "parent");
            AppendComponent(builder, telemetryEvent.Parent);
            Property(builder, ref first, "outputs");
            AppendComponents(builder, telemetryEvent.Outputs);
            Property(builder, ref first, "conservation");
            AppendConservationOrNull(builder, telemetryEvent.Conservation);
            builder.Append('}');
        }

        private static void AppendHost(StringBuilder builder, PhysicalTelemetryHostRecord host)
        {
            builder.Append('{');
            bool first = true;
            Number(builder, ref first, "rootFireIndex", host.RootFireIndex);
            Number(builder, ref first, "rootRandomSeed", host.RootRandomSeed);
            Number(builder, ref first, "currentFireIndex", host.CurrentFireIndex);
            Number(builder, ref first, "currentRandomSeed", host.CurrentRandomSeed);
            Number(builder, ref first, "currentFragmentIndex", host.CurrentFragmentIndex);
            Number(builder, ref first, "parentDepth", host.ParentDepth);
            String(builder, ref first, "rootShooterProfileId", host.RootShooterProfileId);
            String(builder, ref first, "ammunitionTemplateId", host.AmmunitionTemplateId);
            String(builder, ref first, "ammunitionTemplateName", host.AmmunitionTemplateName);
            builder.Append('}');
        }

        private static void AppendImpact(StringBuilder builder, PhysicalTelemetryImpactRecord impact)
        {
            builder.Append('{');
            bool first = true;
            Vector(builder, ref first, "positionMetres", impact.PositionMetres);
            Vector(builder, ref first, "surfaceNormal", impact.SurfaceNormal);
            Number(builder, ref first, "physicalThicknessMetres", impact.PhysicalThicknessMetres);
            Number(builder, ref first, "effectivePathLengthMetres", impact.EffectivePathLengthMetres);
            String(builder, ref first, "targetProfileId", impact.TargetProfileId);
            String(builder, ref first, "targetMaterialClass", impact.TargetMaterialClass);
            String(builder, ref first, "targetSurfaceIdentity", impact.TargetSurfaceIdentity);
            Number(
                builder,
                ref first,
                "targetDensityKilogramsPerCubicMetre",
                impact.TargetDensityKilogramsPerCubicMetre);
            Number(
                builder,
                ref first,
                "targetResistancePressurePascals",
                impact.TargetResistancePressurePascals);
            Number(
                builder,
                ref first,
                "projectileDeformationCoupling",
                impact.ProjectileDeformationCoupling);
            Number(
                builder,
                ref first,
                "projectileFractureCoupling",
                impact.ProjectileFractureCoupling);
            Number(builder, ref first, "heatLossFraction", impact.HeatLossFraction);
            builder.Append('}');
        }

        private static void AppendComponents(
            StringBuilder builder,
            IReadOnlyList<PhysicalComponentRecord> components)
        {
            builder.Append('[');
            for (int index = 0; index < components.Count; index++)
            {
                if (index != 0)
                {
                    builder.Append(',');
                }
                AppendComponent(builder, components[index]);
            }
            builder.Append(']');
        }

        private static void AppendComponent(StringBuilder builder, PhysicalComponentRecord component)
        {
            builder.Append('{');
            bool first = true;
            String(builder, ref first, "kind", component.Kind);
            String(builder, ref first, "projectileId", component.ProjectileId);
            String(builder, ref first, "rootShotId", component.RootShotId);
            String(builder, ref first, "parentProjectileId", component.ParentProjectileId);
            String(builder, ref first, "sourceProjectileId", component.SourceProjectileId);
            String(builder, ref first, "sourceMaterialId", component.SourceMaterialId);
            String(builder, ref first, "sourceMaterialClass", component.SourceMaterialClass);
            String(builder, ref first, "sourceCollisionId", component.SourceCollisionId);
            Number(builder, ref first, "fragmentIndex", component.FragmentIndex);
            Number(builder, ref first, "fragmentGeneration", component.FragmentGeneration);
            Number(builder, ref first, "deterministicSeed", component.DeterministicSeed);
            String(builder, ref first, "construction", component.Construction);
            String(builder, ref first, "designClass", component.DesignClass);
            String(builder, ref first, "shapeClass", component.ShapeClass);
            Number(builder, ref first, "originalMassKilograms", component.OriginalMassKilograms);
            Number(builder, ref first, "retainedMassKilograms", component.RetainedMassKilograms);
            Number(builder, ref first, "nominalDiameterMetres", component.NominalDiameterMetres);
            Number(builder, ref first, "deformedDiameterMetres", component.DeformedDiameterMetres);
            Number(builder, ref first, "projectedAreaSquareMetres", component.ProjectedAreaSquareMetres);
            Number(builder, ref first, "equivalentDiameterMetres", component.EquivalentDiameterMetres);
            Number(builder, ref first, "lengthMetres", component.LengthMetres);
            Number(builder, ref first, "aspectRatio", component.AspectRatio);
            Number(builder, ref first, "dragCoefficient", component.DragCoefficient);
            Number(
                builder,
                ref first,
                "ballisticCoefficientKilogramsPerSquareMetre",
                component.BallisticCoefficientKilogramsPerSquareMetre);
            Vector(builder, ref first, "positionMetres", component.PositionMetres);
            Vector(builder, ref first, "velocityMetresPerSecond", component.VelocityMetresPerSecond);
            Number(builder, ref first, "speedMetresPerSecond", component.SpeedMetresPerSecond);
            Vector(
                builder,
                ref first,
                "momentumKilogramMetresPerSecond",
                component.MomentumKilogramMetresPerSecond);
            Number(
                builder,
                ref first,
                "translationalKineticEnergyJoules",
                component.TranslationalKineticEnergyJoules);
            Orientation(builder, ref first, "orientation", component.Orientation);
            Number(builder, ref first, "yawAngleRadians", component.YawAngleRadians);
            String(builder, ref first, "tumbleState", component.TumbleState);
            Number(
                builder,
                ref first,
                "penetrationCapabilityJoulesPerSquareMetre",
                component.PenetrationCapabilityJoulesPerSquareMetre);
            Number(builder, ref first, "damageCapabilityJoules", component.DamageCapabilityJoules);
            String(builder, ref first, "terminalState", component.TerminalState);
            String(builder, ref first, "renderState", component.RenderState);
            Boolean(builder, ref first, "isTargetMaterialOrigin", component.IsTargetMaterialOrigin);
            Boolean(builder, ref first, "isParentDerivedMass", component.IsParentDerivedMass);
            Property(builder, ref first, "collisionHistory");
            AppendCollisionHistory(builder, component.CollisionHistory);
            builder.Append('}');
        }

        private static void AppendCollisionHistory(
            StringBuilder builder,
            IReadOnlyList<PhysicalCollisionRecordSnapshot> history)
        {
            builder.Append('[');
            for (int index = 0; index < history.Count; index++)
            {
                if (index != 0)
                {
                    builder.Append(',');
                }
                PhysicalCollisionRecordSnapshot collision = history[index];
                builder.Append('{');
                bool first = true;
                String(builder, ref first, "collisionId", collision.CollisionId);
                String(builder, ref first, "materialId", collision.MaterialId);
                String(builder, ref first, "materialClass", collision.MaterialClass);
                Number(builder, ref first, "sequence", collision.Sequence);
                Vector(builder, ref first, "positionMetres", collision.PositionMetres);
                Vector(
                    builder,
                    ref first,
                    "incomingVelocityMetresPerSecond",
                    collision.IncomingVelocityMetresPerSecond);
                Vector(
                    builder,
                    ref first,
                    "outgoingVelocityMetresPerSecond",
                    collision.OutgoingVelocityMetresPerSecond);
                Number(
                    builder,
                    ref first,
                    "incomingTranslationalEnergyJoules",
                    collision.IncomingTranslationalEnergyJoules);
                Number(
                    builder,
                    ref first,
                    "outgoingTranslationalEnergyJoules",
                    collision.OutgoingTranslationalEnergyJoules);
                Number(builder, ref first, "impactAngleRadians", collision.ImpactAngleRadians);
                Number(
                    builder,
                    ref first,
                    "effectivePathLengthMetres",
                    collision.EffectivePathLengthMetres);
                String(builder, ref first, "outcome", collision.Outcome);
                builder.Append('}');
            }
            builder.Append(']');
        }

        private static void AppendConservationOrNull(
            StringBuilder builder,
            PhysicalConservationRecord? conservation)
        {
            if (conservation == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('{');
            bool first = true;
            Number(builder, ref first, "parentMassKilograms", conservation.ParentMassKilograms);
            Number(
                builder,
                ref first,
                "allocatedParentMassKilograms",
                conservation.AllocatedParentMassKilograms);
            Number(
                builder,
                ref first,
                "unallocatedParentMassKilograms",
                conservation.UnallocatedParentMassKilograms);
            Number(builder, ref first, "targetSpallMassKilograms", conservation.TargetSpallMassKilograms);
            Number(builder, ref first, "parentEnergyJoules", conservation.ParentEnergyJoules);
            Property(builder, ref first, "lossBudget");
            AppendLossBudget(builder, conservation.LossBudget);
            Number(builder, ref first, "modeledLossEnergyJoules", conservation.ModeledLossEnergyJoules);
            Number(builder, ref first, "residualEnergyJoules", conservation.ResidualEnergyJoules);
            Number(builder, ref first, "outputEnergyJoules", conservation.OutputEnergyJoules);
            Number(builder, ref first, "energyClosureErrorJoules", conservation.EnergyClosureErrorJoules);
            Number(builder, ref first, "parentDerivedOutputCount", conservation.ParentDerivedOutputCount);
            Number(builder, ref first, "targetSpallOutputCount", conservation.TargetSpallOutputCount);
            builder.Append('}');
        }

        private static void AppendLossBudget(StringBuilder builder, PhysicalLossBudgetRecord lossBudget)
        {
            builder.Append('{');
            bool first = true;
            Number(builder, ref first, "penetrationLossJoules", lossBudget.PenetrationLossJoules);
            Number(builder, ref first, "deformationLossJoules", lossBudget.DeformationLossJoules);
            Number(builder, ref first, "fractureLossJoules", lossBudget.FractureLossJoules);
            Number(builder, ref first, "heatLossJoules", lossBudget.HeatLossJoules);
            Number(builder, ref first, "otherLossJoules", lossBudget.OtherLossJoules);
            Number(builder, ref first, "totalLossJoules", lossBudget.TotalLossJoules);
            builder.Append('}');
        }

        private static void Vector(
            StringBuilder builder,
            ref bool first,
            string name,
            PhysicalVectorRecord vector)
        {
            Property(builder, ref first, name);
            builder.Append('[')
                .Append(Double(vector.X)).Append(',')
                .Append(Double(vector.Y)).Append(',')
                .Append(Double(vector.Z)).Append(']');
        }

        private static void Orientation(
            StringBuilder builder,
            ref bool first,
            string name,
            PhysicalOrientationRecord orientation)
        {
            Property(builder, ref first, name);
            builder.Append('[')
                .Append(Double(orientation.X)).Append(',')
                .Append(Double(orientation.Y)).Append(',')
                .Append(Double(orientation.Z)).Append(',')
                .Append(Double(orientation.W)).Append(']');
        }

        private static void String(StringBuilder builder, ref bool first, string name, string value)
        {
            Property(builder, ref first, name);
            builder.Append(LabPolicies.Json(value));
        }

        private static void Boolean(StringBuilder builder, ref bool first, string name, bool value)
        {
            Property(builder, ref first, name);
            builder.Append(value ? "true" : "false");
        }

        private static void Number(StringBuilder builder, ref bool first, string name, int value)
        {
            Property(builder, ref first, name);
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Number(StringBuilder builder, ref bool first, string name, long value)
        {
            Property(builder, ref first, name);
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Number(StringBuilder builder, ref bool first, string name, ulong value)
        {
            Property(builder, ref first, name);
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Number(StringBuilder builder, ref bool first, string name, double value)
        {
            Property(builder, ref first, name);
            builder.Append(Double(value));
        }

        private static string Double(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value)
                ? "null"
                : value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void Property(StringBuilder builder, ref bool first, string name)
        {
            if (!first)
            {
                builder.Append(',');
            }
            first = false;
            builder.Append('"').Append(name).Append("\":");
        }
    }
}
