using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BallisticsLab.Core;

namespace BallisticsLab.Validation;

internal static class PhysicalTransitionInvariantValidator
{
    private static readonly string[] ComponentNonNegativeFields =
    {
        "originalMassKilograms",
        "retainedMassKilograms",
        "nominalDiameterMetres",
        "deformedDiameterMetres",
        "projectedAreaSquareMetres",
        "equivalentDiameterMetres",
        "lengthMetres",
        "aspectRatio",
        "dragCoefficient",
        "ballisticCoefficientKilogramsPerSquareMetre",
        "speedMetresPerSecond",
        "translationalKineticEnergyJoules",
        "penetrationCapabilityJoulesPerSquareMetre",
        "damageCapabilityJoules"
    };

    internal static bool Validate(JsonElement root, out int validatedCount, out string failure)
    {
        validatedCount = 0;
        failure = string.Empty;
        if (!root.TryGetProperty("physicalTransitions", out JsonElement transitions)
            || transitions.ValueKind != JsonValueKind.Array)
        {
            failure = "physicalTransitions array is missing";
            return false;
        }

        HashSet<long> ordinals = new();
        HashSet<string> transitionIds = new(StringComparer.Ordinal);
        foreach (JsonElement transition in transitions.EnumerateArray())
        {
            validatedCount++;
            string prefix = "physical transition " + validatedCount.ToString(CultureInfo.InvariantCulture);
            if (!TryInt64(transition, "firstSeenOrdinal", out long ordinal)
                || ordinal <= 0L
                || !ordinals.Add(ordinal))
            {
                failure = prefix + " has a missing, nonpositive, or duplicate first-seen ordinal";
                return false;
            }
            if (!TryInt64(transition, "lastUpdatedRevision", out long lastUpdatedRevision)
                || lastUpdatedRevision <= 0L)
            {
                failure = prefix + " has a missing or nonpositive last-updated revision";
                return false;
            }
            string transitionId = RequiredString(transition, "transitionId");
            if (string.IsNullOrEmpty(transitionId) || !transitionIds.Add(transitionId))
            {
                failure = prefix + " has a missing or duplicate transition ID";
                return false;
            }
            if (!TryInt32(transition, "preparedDuplicateCount", out int preparedDuplicates)
                || preparedDuplicates < 0
                || !TryInt32(transition, "resolvedDuplicateCount", out int resolvedDuplicates)
                || resolvedDuplicates < 0)
            {
                failure = prefix + " has an invalid duplicate count";
                return false;
            }
            string state = RequiredString(transition, "state");
            if (!TryNullableObject(
                    transition,
                    "prepared",
                    out bool hasPrepared,
                    out JsonElement prepared)
                || !TryNullableObject(
                    transition,
                    "resolved",
                    out bool hasResolved,
                    out JsonElement resolved))
            {
                failure = prefix + " omits prepared or resolved evidence fields";
                return false;
            }
            if (!StateMatches(state, hasPrepared, hasResolved))
            {
                failure = prefix + " state does not match its prepared and resolved evidence";
                return false;
            }
            if (hasPrepared
                && !ValidateEvent(prepared, transitionId, "CollisionPrepared", out failure))
            {
                failure = prefix + " prepared event: " + failure;
                return false;
            }
            if (hasResolved
                && !ValidateEvent(resolved, transitionId, "CollisionResolved", out failure))
            {
                failure = prefix + " resolved event: " + failure;
                return false;
            }
            if (hasPrepared
                && hasResolved
                && !ContextsMatch(prepared, resolved))
            {
                failure = prefix + " prepared and resolved contexts disagree";
                return false;
            }
        }
        return true;
    }

    private static bool ValidateEvent(
        JsonElement telemetryEvent,
        string transitionId,
        string expectedStage,
        out string failure)
    {
        failure = string.Empty;
        if (!TryInt32(telemetryEvent, "snapshotSchema", out int snapshotSchema)
            || snapshotSchema != PhysicalTelemetryContract.SnapshotSchema
            || !TryInt32(telemetryEvent, "publisherSchema", out int publisherSchema)
            || publisherSchema != PhysicalTelemetryContract.SupportedPublisherSchema)
        {
            failure = "snapshot or publisher schema is unsupported";
            return false;
        }
        if (RequiredString(telemetryEvent, "transitionId") != transitionId
            || RequiredString(telemetryEvent, "stage") != expectedStage)
        {
            failure = "stage or transition identity is inconsistent";
            return false;
        }
        if (string.IsNullOrEmpty(RequiredString(telemetryEvent, "outcome")))
        {
            failure = "outcome is missing";
            return false;
        }
        if (!TryObject(telemetryEvent, "host", out JsonElement host)
            || !TryObject(telemetryEvent, "impact", out JsonElement impact)
            || !TryObject(telemetryEvent, "parent", out JsonElement parent)
            || !telemetryEvent.TryGetProperty("outputs", out JsonElement outputs)
            || outputs.ValueKind != JsonValueKind.Array)
        {
            failure = "host, impact, parent, or outputs are missing";
            return false;
        }
        if (!ValidateHost(host) || !ValidateImpact(impact))
        {
            failure = "host or impact data is invalid";
            return false;
        }
        if (!ValidateComponent(parent, out _, out _, out _, out failure))
        {
            failure = "parent component: " + failure;
            return false;
        }

        double outputEnergy = 0d;
        double parentDerivedMass = 0d;
        double targetMaterialMass = 0d;
        int parentDerivedCount = 0;
        int targetMaterialCount = 0;
        foreach (JsonElement output in outputs.EnumerateArray())
        {
            if (!ValidateComponent(
                    output,
                    out double retainedMass,
                    out double kineticEnergy,
                    out TransitionMassSource massSource,
                    out failure))
            {
                failure = "output component: " + failure;
                return false;
            }
            outputEnergy += kineticEnergy;
            if (massSource == TransitionMassSource.ParentDerived)
            {
                parentDerivedMass += retainedMass;
                parentDerivedCount++;
            }
            else if (massSource == TransitionMassSource.FreshTargetMaterial)
            {
                targetMaterialMass += retainedMass;
                targetMaterialCount++;
            }
            else
            {
                failure = "output component has ambiguous mass provenance";
                return false;
            }
        }

        if (!TryNullableObject(
                telemetryEvent,
                "conservation",
                out bool hasConservation,
                out JsonElement conservation))
        {
            failure = "conservation field is missing or invalid";
            return false;
        }
        if (expectedStage == "CollisionPrepared")
        {
            if (outputs.GetArrayLength() != 0 || hasConservation)
            {
                failure = "prepared event contains resolved output or conservation data";
                return false;
            }
            return true;
        }
        if (outputs.GetArrayLength() == 0 || !hasConservation)
        {
            failure = "resolved event omits output or conservation data";
            return false;
        }
        if (!ValidateConservation(
                conservation,
                parent,
                outputEnergy,
                parentDerivedMass,
                targetMaterialMass,
                parentDerivedCount,
                targetMaterialCount,
                out failure))
        {
            return false;
        }
        return ValidateResolvedRelationships(
            telemetryEvent,
            transitionId,
            parent,
            outputs,
            impact,
            conservation,
            out failure);
    }

    private static bool ValidateHost(JsonElement host)
    {
        return TryInt32(host, "rootFireIndex", out int rootFireIndex)
            && rootFireIndex >= 0
            && TryInt32(host, "rootRandomSeed", out _)
            && TryInt32(host, "currentFireIndex", out int currentFireIndex)
            && currentFireIndex >= 0
            && TryInt32(host, "currentRandomSeed", out _)
            && TryInt32(host, "currentFragmentIndex", out int fragmentIndex)
            && fragmentIndex >= 0
            && TryInt32(host, "parentDepth", out int parentDepth)
            && parentDepth >= 0
            && !string.IsNullOrEmpty(RequiredString(host, "ammunitionTemplateId"));
    }

    private static bool ContextsMatch(JsonElement prepared, JsonElement resolved)
    {
        return prepared.GetProperty("host").GetRawText()
                == resolved.GetProperty("host").GetRawText()
            && prepared.GetProperty("impact").GetRawText()
                == resolved.GetProperty("impact").GetRawText()
            && prepared.GetProperty("parent").GetRawText()
                == resolved.GetProperty("parent").GetRawText();
    }

    private static bool ValidateResolvedRelationships(
        JsonElement telemetryEvent,
        string transitionId,
        JsonElement parent,
        JsonElement outputs,
        JsonElement impact,
        JsonElement conservation,
        out string failure)
    {
        failure = string.Empty;
        JsonElement parentHistory = parent.GetProperty("collisionHistory");
        int parentHistoryCount = parentHistory.GetArrayLength();
        string parentRootShotId = RequiredString(parent, "rootShotId");
        var outputIds = new HashSet<string>(StringComparer.Ordinal);
        JsonElement currentCollision = default;
        bool hasCurrentCollision = false;
        foreach (JsonElement output in outputs.EnumerateArray())
        {
            string outputId = RequiredString(output, "projectileId");
            JsonElement outputHistory = output.GetProperty("collisionHistory");
            if (!outputIds.Add(outputId)
                || RequiredString(output, "rootShotId") != parentRootShotId
                || outputHistory.GetArrayLength() != parentHistoryCount + 1)
            {
                failure = "output identity or collision-history length is inconsistent";
                return false;
            }
            for (int index = 0; index < parentHistoryCount; index++)
            {
                if (parentHistory[index].GetRawText() != outputHistory[index].GetRawText())
                {
                    failure = "output does not preserve prior collision history";
                    return false;
                }
            }
            JsonElement candidate = outputHistory[parentHistoryCount];
            if (hasCurrentCollision
                && currentCollision.GetRawText() != candidate.GetRawText())
            {
                failure = "outputs disagree about the current collision record";
                return false;
            }
            currentCollision = candidate;
            hasCurrentCollision = true;
        }
        if (!hasCurrentCollision
            || RequiredString(currentCollision, "collisionId") != transitionId
            || RequiredString(currentCollision, "materialId")
                != RequiredString(impact, "targetProfileId")
            || RequiredString(currentCollision, "materialClass")
                != RequiredString(impact, "targetMaterialClass")
            || RequiredString(currentCollision, "outcome")
                != RequiredString(telemetryEvent, "outcome")
            || currentCollision.GetProperty("sequence").GetInt32() != parentHistoryCount
            || !VectorsNearly(currentCollision, "positionMetres", impact, "positionMetres")
            || !VectorsNearly(parent, "positionMetres", impact, "positionMetres")
            || !VectorsNearly(
                currentCollision,
                "incomingVelocityMetresPerSecond",
                parent,
                "velocityMetresPerSecond")
            || !Nearly(
                currentCollision.GetProperty(
                    "incomingTranslationalEnergyJoules").GetDouble(),
                parent.GetProperty("translationalKineticEnergyJoules").GetDouble())
            || !Nearly(
                currentCollision.GetProperty(
                    "outgoingTranslationalEnergyJoules").GetDouble(),
                conservation.GetProperty("residualEnergyJoules").GetDouble())
            || !Nearly(
                currentCollision.GetProperty("effectivePathLengthMetres").GetDouble(),
                impact.GetProperty("effectivePathLengthMetres").GetDouble()))
        {
            failure = "current collision does not match its transition context";
            return false;
        }
        return true;
    }

    private static bool VectorsNearly(
        JsonElement left,
        string leftName,
        JsonElement right,
        string rightName)
    {
        return TryVectorValues(left, leftName, out double lx, out double ly, out double lz)
            && TryVectorValues(right, rightName, out double rx, out double ry, out double rz)
            && Nearly(lx, rx)
            && Nearly(ly, ry)
            && Nearly(lz, rz);
    }

    private static double PublisherEnergyTolerance(double energyJoules)
    {
        return Math.Max(1d, energyJoules) * 0.000000001d;
    }

    private static bool ValidateImpact(JsonElement impact)
    {
        return TryVector(impact, "positionMetres")
            && TryVectorValues(impact, "surfaceNormal", out double nx, out double ny, out double nz)
            && NearlyUnit(Math.Sqrt(nx * nx + ny * ny + nz * nz))
            && NonNegative(impact, "physicalThicknessMetres")
            && NonNegative(impact, "effectivePathLengthMetres")
            && !string.IsNullOrEmpty(RequiredString(impact, "targetMaterialClass"))
            && HasString(impact, "targetSurfaceIdentity")
            && NonNegative(impact, "targetDensityKilogramsPerCubicMetre")
            && NonNegative(impact, "targetResistancePressurePascals")
            && Probability(impact, "projectileDeformationCoupling")
            && Probability(impact, "projectileFractureCoupling")
            && Probability(impact, "heatLossFraction");
    }

    private static bool ValidateComponent(
        JsonElement component,
        out double retainedMass,
        out double kineticEnergy,
        out TransitionMassSource massSource,
        out string failure)
    {
        retainedMass = 0d;
        kineticEnergy = 0d;
        massSource = TransitionMassSource.Ambiguous;
        failure = string.Empty;
        string kind = RequiredString(component, "kind");
        if (string.IsNullOrEmpty(RequiredString(component, "projectileId"))
            || string.IsNullOrEmpty(RequiredString(component, "rootShotId"))
            || string.IsNullOrEmpty(kind)
            || string.IsNullOrEmpty(RequiredString(component, "construction"))
            || string.IsNullOrEmpty(RequiredString(component, "shapeClass"))
            || string.IsNullOrEmpty(RequiredString(component, "sourceMaterialClass"))
            || string.IsNullOrEmpty(RequiredString(component, "tumbleState"))
            || string.IsNullOrEmpty(RequiredString(component, "terminalState"))
            || string.IsNullOrEmpty(RequiredString(component, "renderState")))
        {
            failure = "component identity or state classification is missing";
            return false;
        }
        if (!TryInt32(component, "fragmentIndex", out int fragmentIndex)
            || fragmentIndex < -1
            || !TryInt32(component, "fragmentGeneration", out int fragmentGeneration)
            || fragmentGeneration < 0
            || !TryUInt64(component, "deterministicSeed", out _)
            || !Finite(component, "yawAngleRadians"))
        {
            failure = "fragment identity, deterministic seed, or yaw is invalid";
            return false;
        }
        foreach (string field in ComponentNonNegativeFields)
        {
            if (!NonNegative(component, field))
            {
                failure = field + " is missing, negative, or non-finite";
                return false;
            }
        }
        double originalMass = component.GetProperty("originalMassKilograms").GetDouble();
        retainedMass = component.GetProperty("retainedMassKilograms").GetDouble();
        kineticEnergy = component.GetProperty("translationalKineticEnergyJoules").GetDouble();
        if (retainedMass > originalMass + Tolerance(originalMass))
        {
            failure = "retained mass exceeds original mass";
            return false;
        }
        if (!TryVector(component, "positionMetres")
            || !TryVectorValues(
                component,
                "velocityMetresPerSecond",
                out double velocityX,
                out double velocityY,
                out double velocityZ)
            || !TryVectorValues(
                component,
                "momentumKilogramMetresPerSecond",
                out double momentumX,
                out double momentumY,
                out double momentumZ)
            || !TryArrayValues(component, "orientation", 4, out double[] orientation)
            || !component.TryGetProperty("collisionHistory", out JsonElement history)
            || history.ValueKind != JsonValueKind.Array)
        {
            failure = "motion, orientation, or collision history is invalid";
            return false;
        }
        double speed = component.GetProperty("speedMetresPerSecond").GetDouble();
        double calculatedSpeed = Math.Sqrt(
            velocityX * velocityX + velocityY * velocityY + velocityZ * velocityZ);
        double calculatedEnergy = 0.5d * retainedMass * calculatedSpeed * calculatedSpeed;
        double orientationMagnitude = Math.Sqrt(
            orientation[0] * orientation[0]
            + orientation[1] * orientation[1]
            + orientation[2] * orientation[2]
            + orientation[3] * orientation[3]);
        if (!NearlyKinematic(speed, calculatedSpeed)
            || !NearlyKinematic(momentumX, retainedMass * velocityX)
            || !NearlyKinematic(momentumY, retainedMass * velocityY)
            || !NearlyKinematic(momentumZ, retainedMass * velocityZ)
            || !NearlyKinematic(kineticEnergy, calculatedEnergy)
            || !NearlyUnit(orientationMagnitude))
        {
            failure = "component speed, momentum, energy, or orientation is internally inconsistent";
            return false;
        }
        var collisionIds = new HashSet<string>(StringComparer.Ordinal);
        int expectedSequence = 0;
        foreach (JsonElement collision in history.EnumerateArray())
        {
            string collisionId = RequiredString(collision, "collisionId");
            if (string.IsNullOrEmpty(collisionId)
                || !collisionIds.Add(collisionId)
                || string.IsNullOrEmpty(RequiredString(collision, "materialId"))
                || string.IsNullOrEmpty(RequiredString(collision, "materialClass"))
                || string.IsNullOrEmpty(RequiredString(collision, "outcome"))
                || !TryInt32(collision, "sequence", out int sequence)
                || sequence != expectedSequence
                || !TryVector(collision, "positionMetres")
                || !TryVector(collision, "incomingVelocityMetresPerSecond")
                || !TryVector(collision, "outgoingVelocityMetresPerSecond")
                || !NonNegative(collision, "incomingTranslationalEnergyJoules")
                || !NonNegative(collision, "outgoingTranslationalEnergyJoules")
                || !NonNegative(collision, "impactAngleRadians")
                || collision.GetProperty("impactAngleRadians").GetDouble() > Math.PI * 0.5d
                || !NonNegative(collision, "effectivePathLengthMetres"))
            {
                failure = "collision history entry is invalid";
                return false;
            }
            double incomingEnergy = collision.GetProperty(
                "incomingTranslationalEnergyJoules").GetDouble();
            double outgoingEnergy = collision.GetProperty(
                "outgoingTranslationalEnergyJoules").GetDouble();
            if (outgoingEnergy > incomingEnergy + PublisherEnergyTolerance(incomingEnergy))
            {
                failure = "collision history gains energy";
                return false;
            }
            expectedSequence++;
        }
        if (!TryBoolean(component, "isTargetMaterialOrigin", out bool targetOrigin)
            || !TryBoolean(component, "isParentDerivedMass", out bool parentDerived))
        {
            failure = "component mass-origin flags are missing or invalid";
            return false;
        }
        string construction = RequiredString(component, "construction");
        string shapeClass = RequiredString(component, "shapeClass");
        string parentProjectileId = RequiredString(component, "parentProjectileId");
        string sourceProjectileId = RequiredString(component, "sourceProjectileId");
        string sourceMaterialId = RequiredString(component, "sourceMaterialId");
        string sourceCollisionId = RequiredString(component, "sourceCollisionId");
        bool hasParent = !string.IsNullOrEmpty(parentProjectileId);
        bool isTargetSpall = string.Equals(kind, "TargetSpall", StringComparison.Ordinal);
        bool isTargetSpallFragment = string.Equals(
            kind,
            "TargetSpallFragment",
            StringComparison.Ordinal);
        bool isProjectileFragment = string.Equals(
            kind,
            "ProjectileFragment",
            StringComparison.Ordinal);
        bool isPrimary = string.Equals(kind, "IntactProjectile", StringComparison.Ordinal)
            || string.Equals(kind, "DeformedProjectile", StringComparison.Ordinal);
        bool expectedTargetOrigin = isTargetSpall || isTargetSpallFragment;
        bool expectedParentLineage = hasParent && !isTargetSpall;
        bool hasTargetConstruction = string.Equals(
            construction,
            "TargetMaterial",
            StringComparison.Ordinal);
        bool hasTargetShape = string.Equals(
                shapeClass,
                "TargetSpallFlake",
                StringComparison.Ordinal)
            || string.Equals(shapeClass, "TargetSpallChunk", StringComparison.Ordinal);
        if ((!isPrimary && !isProjectileFragment && !isTargetSpall && !isTargetSpallFragment)
            || targetOrigin != expectedTargetOrigin
            || parentDerived != expectedParentLineage
            || hasTargetConstruction != expectedTargetOrigin
            || hasTargetShape != expectedTargetOrigin)
        {
            failure = "component kind and provenance flags disagree";
            return false;
        }
        if ((!hasParent
                && (fragmentGeneration != 0
                    || fragmentIndex != -1
                    || !isPrimary))
            || (hasParent
                && (fragmentGeneration <= 0
                    || fragmentIndex < 0
                    || string.IsNullOrEmpty(sourceProjectileId)
                    || string.IsNullOrEmpty(sourceMaterialId)
                    || string.IsNullOrEmpty(sourceCollisionId))))
        {
            failure = "component lineage is inconsistent";
            return false;
        }
        massSource = isTargetSpall
            ? TransitionMassSource.FreshTargetMaterial
            : TransitionMassSource.ParentDerived;
        if (targetOrigin && string.IsNullOrEmpty(sourceMaterialId))
        {
            failure = "target-material output omits its source material";
            return false;
        }
        if (parentDerived && string.IsNullOrEmpty(sourceProjectileId))
        {
            failure = "parent-derived output omits its source projectile";
            return false;
        }
        return true;
    }

    private static bool ValidateConservation(
        JsonElement conservation,
        JsonElement parent,
        double calculatedOutputEnergy,
        double calculatedParentDerivedMass,
        double calculatedTargetMaterialMass,
        int calculatedParentDerivedCount,
        int calculatedTargetMaterialCount,
        out string failure)
    {
        failure = string.Empty;
        if (!TryObject(conservation, "lossBudget", out JsonElement lossBudget))
        {
            failure = "loss budget is missing";
            return false;
        }
        string[] lossFields =
        {
            "penetrationLossJoules",
            "deformationLossJoules",
            "fractureLossJoules",
            "heatLossJoules",
            "otherLossJoules",
            "totalLossJoules"
        };
        foreach (string field in lossFields)
        {
            if (!NonNegative(lossBudget, field))
            {
                failure = "loss budget " + field + " is invalid";
                return false;
            }
        }
        string[] conservationFields =
        {
            "parentMassKilograms",
            "allocatedParentMassKilograms",
            "targetSpallMassKilograms",
            "parentEnergyJoules",
            "modeledLossEnergyJoules",
            "residualEnergyJoules",
            "outputEnergyJoules"
        };
        foreach (string field in conservationFields)
        {
            if (!NonNegative(conservation, field))
            {
                failure = "conservation " + field + " is invalid";
                return false;
            }
        }
        if (!Finite(conservation, "unallocatedParentMassKilograms")
            || !Finite(conservation, "energyClosureErrorJoules"))
        {
            failure = "signed conservation remainder is missing or non-finite";
            return false;
        }

        double parentMass = conservation.GetProperty("parentMassKilograms").GetDouble();
        double allocatedMass = conservation.GetProperty("allocatedParentMassKilograms").GetDouble();
        double unallocatedMass = conservation.GetProperty("unallocatedParentMassKilograms").GetDouble();
        double targetSpallMass = conservation.GetProperty("targetSpallMassKilograms").GetDouble();
        double parentEnergy = conservation.GetProperty("parentEnergyJoules").GetDouble();
        double modeledLoss = conservation.GetProperty("modeledLossEnergyJoules").GetDouble();
        double residualEnergy = conservation.GetProperty("residualEnergyJoules").GetDouble();
        double outputEnergy = conservation.GetProperty("outputEnergyJoules").GetDouble();
        double closureError = conservation.GetProperty("energyClosureErrorJoules").GetDouble();
        double lossSum = lossBudget.GetProperty("penetrationLossJoules").GetDouble()
            + lossBudget.GetProperty("deformationLossJoules").GetDouble()
            + lossBudget.GetProperty("fractureLossJoules").GetDouble()
            + lossBudget.GetProperty("heatLossJoules").GetDouble()
            + lossBudget.GetProperty("otherLossJoules").GetDouble();
        double totalLoss = lossBudget.GetProperty("totalLossJoules").GetDouble();
        double massTolerance = Math.Max(0.000000000001d, parentMass * 0.000000001d);
        double targetMassTolerance = Math.Max(
            0.000000000001d,
            targetSpallMass * 0.000000001d);
        double energyTolerance = Math.Max(1d, parentEnergy) * 0.000000001d;
        if (!TryInt32(conservation, "parentDerivedOutputCount", out int parentDerivedCount)
            || parentDerivedCount < 0
            || !TryInt32(conservation, "targetSpallOutputCount", out int targetSpallCount)
            || targetSpallCount < 0
            || allocatedMass > parentMass + massTolerance
            || unallocatedMass < -massTolerance
            || outputEnergy > residualEnergy + energyTolerance
            || !Within(
                parentMass,
                parent.GetProperty("retainedMassKilograms").GetDouble(),
                massTolerance)
            || !Within(
                parentEnergy,
                parent.GetProperty("translationalKineticEnergyJoules").GetDouble(),
                energyTolerance)
            || !Within(parentMass, allocatedMass + unallocatedMass, massTolerance)
            || !Within(allocatedMass, calculatedParentDerivedMass, massTolerance)
            || !Within(targetSpallMass, calculatedTargetMaterialMass, targetMassTolerance)
            || parentDerivedCount != calculatedParentDerivedCount
            || targetSpallCount != calculatedTargetMaterialCount
            || !Within(totalLoss, lossSum, energyTolerance)
            || !Within(modeledLoss, totalLoss, energyTolerance)
            || !Within(
                residualEnergy,
                Math.Max(0d, parentEnergy - modeledLoss),
                energyTolerance)
            || !Within(outputEnergy, calculatedOutputEnergy, energyTolerance)
            || !Within(closureError, residualEnergy - outputEnergy, energyTolerance))
        {
            failure = "mass, output count, loss, or energy closure does not balance";
            return false;
        }
        return true;
    }

    private static bool StateMatches(string state, bool prepared, bool resolved)
    {
        return state == "Completed" && prepared && resolved
            || state == "Pending" && prepared && !resolved
            || state == "Orphaned" && !prepared && resolved;
    }

    private static bool TryObject(JsonElement source, string name, out JsonElement value)
    {
        return source.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object;
    }

    private static bool TryNullableObject(
        JsonElement source,
        string name,
        out bool hasObject,
        out JsonElement value)
    {
        hasObject = false;
        if (!source.TryGetProperty(name, out value))
        {
            return false;
        }
        if (value.ValueKind == JsonValueKind.Null)
        {
            return true;
        }
        hasObject = value.ValueKind == JsonValueKind.Object;
        return hasObject;
    }

    private static bool TryVector(JsonElement source, string name)
    {
        return TryVectorValues(source, name, out _, out _, out _);
    }

    private static bool TryVectorValues(
        JsonElement source,
        string name,
        out double x,
        out double y,
        out double z)
    {
        x = 0d;
        y = 0d;
        z = 0d;
        if (!TryArrayValues(source, name, 3, out double[] values))
        {
            return false;
        }
        x = values[0];
        y = values[1];
        z = values[2];
        return true;
    }

    private static bool TryArrayValues(
        JsonElement source,
        string name,
        int length,
        out double[] values)
    {
        values = Array.Empty<double>();
        if (!source.TryGetProperty(name, out JsonElement value)
            || value.ValueKind != JsonValueKind.Array
            || value.GetArrayLength() != length)
        {
            return false;
        }
        values = new double[length];
        int index = 0;
        foreach (JsonElement number in value.EnumerateArray())
        {
            if (!number.TryGetDouble(out double parsed) || !double.IsFinite(parsed))
            {
                return false;
            }
            values[index++] = parsed;
        }
        return true;
    }

    private static bool Finite(JsonElement source, string name)
    {
        return source.TryGetProperty(name, out JsonElement value)
            && value.TryGetDouble(out double parsed)
            && double.IsFinite(parsed);
    }

    private static bool NonNegative(JsonElement source, string name)
    {
        return source.TryGetProperty(name, out JsonElement value)
            && value.TryGetDouble(out double parsed)
            && double.IsFinite(parsed)
            && parsed >= 0d;
    }

    private static bool Probability(JsonElement source, string name)
    {
        return NonNegative(source, name) && source.GetProperty(name).GetDouble() <= 1d;
    }

    private static bool TryBoolean(JsonElement source, string name, out bool value)
    {
        value = false;
        if (!source.TryGetProperty(name, out JsonElement element)
            || element.ValueKind != JsonValueKind.True && element.ValueKind != JsonValueKind.False)
        {
            return false;
        }
        value = element.GetBoolean();
        return true;
    }

    private static string RequiredString(JsonElement source, string name)
    {
        return source.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }

    private static bool HasString(JsonElement source, string name)
    {
        return source.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String;
    }

    private static bool TryInt32(JsonElement source, string name, out int value)
    {
        value = 0;
        return source.TryGetProperty(name, out JsonElement element) && element.TryGetInt32(out value);
    }

    private static bool TryInt64(JsonElement source, string name, out long value)
    {
        value = 0L;
        return source.TryGetProperty(name, out JsonElement element) && element.TryGetInt64(out value);
    }

    private static bool TryUInt64(JsonElement source, string name, out ulong value)
    {
        value = 0UL;
        return source.TryGetProperty(name, out JsonElement element)
            && element.TryGetUInt64(out value);
    }

    private static bool Nearly(double actual, double expected)
    {
        return Math.Abs(actual - expected) <= Tolerance(Math.Max(Math.Abs(actual), Math.Abs(expected)));
    }

    private static bool NearlyKinematic(double actual, double expected)
    {
        double scale = Math.Max(1d, Math.Max(Math.Abs(actual), Math.Abs(expected)));
        return Math.Abs(actual - expected) <= scale * 0.000001d;
    }

    private static bool NearlyUnit(double value)
    {
        return Math.Abs(value - 1d) <= 0.0001d;
    }

    private static double Tolerance(double scale)
    {
        return Math.Max(0.000000000001d, Math.Max(1d, scale) * 0.00000001d);
    }

    private static bool Within(double left, double right, double tolerance)
    {
        return Math.Abs(left - right) <= tolerance;
    }

    private enum TransitionMassSource
    {
        Ambiguous = 0,
        ParentDerived = 1,
        FreshTargetMaterial = 2
    }
}

internal static class PhysicalTransitionInvariantTests
{
    internal static bool AcceptsBalancedSchemaFourEvidence()
    {
        using JsonDocument document = JsonDocument.Parse(CreateReport());
        return PhysicalTransitionInvariantValidator.Validate(
            document.RootElement,
            out int count,
            out _)
            && count == 1;
    }

    internal static bool RejectsBrokenMassClosure()
    {
        JsonNode root = JsonNode.Parse(CreateReport())!;
        root["physicalTransitions"]![0]!["resolved"]!["conservation"]![
            "allocatedParentMassKilograms"] = 0.002d;
        using JsonDocument document = JsonDocument.Parse(root.ToJsonString());
        return !PhysicalTransitionInvariantValidator.Validate(
                document.RootElement,
                out _,
                out string failure)
            && failure.Contains("closure", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool RejectsBrokenEnergyClosure()
    {
        JsonNode root = JsonNode.Parse(CreateReport())!;
        root["physicalTransitions"]![0]!["resolved"]!["conservation"]![
            "outputEnergyJoules"] = 700d;
        using JsonDocument document = JsonDocument.Parse(root.ToJsonString());
        return !PhysicalTransitionInvariantValidator.Validate(
                document.RootElement,
                out _,
                out string failure)
            && failure.Contains("closure", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool RejectsUnsupportedTelemetrySchema()
    {
        JsonNode root = JsonNode.Parse(CreateReport())!;
        root["physicalTransitions"]![0]!["prepared"]!["snapshotSchema"] = 2;
        using JsonDocument document = JsonDocument.Parse(root.ToJsonString());
        return !PhysicalTransitionInvariantValidator.Validate(
                document.RootElement,
                out _,
                out string failure)
            && failure.Contains("schema", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool RejectsInconsistentComponentKinematics()
    {
        JsonNode root = JsonNode.Parse(CreateReport())!;
        root["physicalTransitions"]![0]!["resolved"]!["outputs"]![0]![
            "speedMetresPerSecond"] = 1d;
        using JsonDocument document = JsonDocument.Parse(root.ToJsonString());
        return !PhysicalTransitionInvariantValidator.Validate(
                document.RootElement,
                out _,
                out string failure)
            && failure.Contains("internally inconsistent", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool RejectsLostPriorCollisionHistory()
    {
        JsonNode root = JsonNode.Parse(CreateReport())!;
        root["physicalTransitions"]![0]!["resolved"]!["outputs"]![0]![
            "collisionHistory"]![0]!["collisionId"] = "altered-prior-collision";
        using JsonDocument document = JsonDocument.Parse(root.ToJsonString());
        return !PhysicalTransitionInvariantValidator.Validate(
                document.RootElement,
                out _,
                out string failure)
            && failure.Contains("prior collision history", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool RejectsPreparedResolvedContextMismatch()
    {
        JsonNode root = JsonNode.Parse(CreateReport())!;
        root["physicalTransitions"]![0]!["resolved"]!["host"]![
            "currentRandomSeed"] = 12345;
        using JsonDocument document = JsonDocument.Parse(root.ToJsonString());
        return !PhysicalTransitionInvariantValidator.Validate(
                document.RootElement,
                out _,
                out string failure)
            && failure.Contains("contexts disagree", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool RejectsOutputKindProvenanceMismatch()
    {
        JsonNode root = JsonNode.Parse(CreateReport())!;
        root["physicalTransitions"]![0]!["resolved"]!["outputs"]![0]![
            "isTargetMaterialOrigin"] = true;
        using JsonDocument document = JsonDocument.Parse(root.ToJsonString());
        return !PhysicalTransitionInvariantValidator.Validate(
                document.RootElement,
                out _,
                out string failure)
            && failure.Contains("provenance", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool AcceptsTargetMaterialFragmentDerivedFromImmediateParent()
    {
        FakePhysicalEvent resolved = FakePhysicalEvent.Resolved("target-spall-fragment");
        var fragment = (FakeComponent)resolved.Outputs[0];
        fragment.Kind = FakeKind.TargetSpallFragment;
        fragment.ProjectileId = "target-spall-fragment";
        fragment.Construction = FakeConstruction.TargetMaterial;
        fragment.ShapeClass = FakeShape.TargetSpallFlake;
        fragment.ParentProjectileId = "target-spall-parent";
        fragment.SourceProjectileId = "target-spall-parent";
        fragment.SourceCollisionId = resolved.Event.TransitionId;
        fragment.FragmentIndex = 0;
        fragment.FragmentGeneration = 2;
        fragment.SourceMaterialId = "target-profile";
        fragment.SourceMaterialClass = FakeMaterial.ArmoredSteel;
        fragment.IsTargetMaterialOrigin = true;
        fragment.IsParentDerivedMass = true;

        using JsonDocument document = JsonDocument.Parse(CreateReport(resolved));
        JsonElement transition = document.RootElement.GetProperty("physicalTransitions")[0];
        JsonElement resolvedSnapshot = transition.GetProperty("resolved");
        JsonElement output = resolvedSnapshot.GetProperty("outputs")[0];
        JsonElement conservation = resolvedSnapshot.GetProperty("conservation");
        return document.RootElement.GetProperty("schema").GetInt32() == 4
            && output.GetProperty("kind").GetString() == "TargetSpallFragment"
            && output.GetProperty("isTargetMaterialOrigin").GetBoolean()
            && output.GetProperty("isParentDerivedMass").GetBoolean()
            && Math.Abs(
                conservation.GetProperty("allocatedParentMassKilograms").GetDouble()
                - 0.003d) < 0.000000000001d
            && Math.Abs(
                conservation.GetProperty("unallocatedParentMassKilograms").GetDouble()
                - 0.001d) < 0.000000000001d
            && Math.Abs(
                conservation.GetProperty("targetSpallMassKilograms").GetDouble()
                - 0.0002d) < 0.000000000001d
            && conservation.GetProperty("parentDerivedOutputCount").GetInt32() == 1
            && conservation.GetProperty("targetSpallOutputCount").GetInt32() == 1
            && Math.Abs(
                conservation.GetProperty("residualEnergyJoules").GetDouble()
                - 770d) < 0.000000001d
            && Math.Abs(
                conservation.GetProperty("outputEnergyJoules").GetDouble()
                - 760d) < 0.000000001d
            && Math.Abs(
                conservation.GetProperty("energyClosureErrorJoules").GetDouble()
                - 10d) < 0.000000001d
            && PhysicalTransitionInvariantValidator.Validate(
                document.RootElement,
                out int count,
                out _)
            && count == 1;
    }

    internal static bool AcceptsSignedClosureRemaindersInsidePublisherTolerance()
    {
        JsonNode root = JsonNode.Parse(CreateReport())!;
        JsonNode output = root["physicalTransitions"]![0]!["resolved"]!["outputs"]![0]!;
        const double retainedMass = 0.003d;
        const double outputEnergy = 670.0000001d;
        double speed = Math.Sqrt((2d * outputEnergy) / retainedMass);
        output["velocityMetresPerSecond"] = new JsonArray(0d, 0d, speed);
        output["speedMetresPerSecond"] = speed;
        output["momentumKilogramMetresPerSecond"] = new JsonArray(
            0d,
            0d,
            retainedMass * speed);
        output["translationalKineticEnergyJoules"] = outputEnergy;
        JsonNode conservation = root["physicalTransitions"]![0]!["resolved"]![
            "conservation"]!;
        conservation["outputEnergyJoules"] = 770.0000001d;
        conservation["energyClosureErrorJoules"] = -0.0000001d;

        using JsonDocument document = JsonDocument.Parse(root.ToJsonString());
        return PhysicalTransitionInvariantValidator.Validate(
            document.RootElement,
            out _,
            out _);
    }

    internal static bool RejectsNegativeClosureBeyondPublisherTolerance()
    {
        JsonNode root = JsonNode.Parse(CreateReport())!;
        JsonNode output = root["physicalTransitions"]![0]!["resolved"]!["outputs"]![0]!;
        const double retainedMass = 0.003d;
        const double outputEnergy = 670.01d;
        double speed = Math.Sqrt((2d * outputEnergy) / retainedMass);
        output["velocityMetresPerSecond"] = new JsonArray(0d, 0d, speed);
        output["speedMetresPerSecond"] = speed;
        output["momentumKilogramMetresPerSecond"] = new JsonArray(
            0d,
            0d,
            retainedMass * speed);
        output["translationalKineticEnergyJoules"] = outputEnergy;
        JsonNode conservation = root["physicalTransitions"]![0]!["resolved"]![
            "conservation"]!;
        conservation["outputEnergyJoules"] = 770.01d;
        conservation["energyClosureErrorJoules"] = -0.01d;

        using JsonDocument document = JsonDocument.Parse(root.ToJsonString());
        return !PhysicalTransitionInvariantValidator.Validate(
                document.RootElement,
                out _,
                out string failure)
            && failure.Contains("closure", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool RejectsInvalidImpactCoupling()
    {
        JsonNode root = JsonNode.Parse(CreateReport())!;
        root["physicalTransitions"]![0]!["prepared"]!["impact"]![
            "projectileFractureCoupling"] = 1.01d;
        using JsonDocument document = JsonDocument.Parse(root.ToJsonString());
        return !PhysicalTransitionInvariantValidator.Validate(
                document.RootElement,
                out _,
                out string failure)
            && failure.Contains("impact", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateReport(FakePhysicalEvent? resolved = null)
    {
        resolved ??= FakePhysicalEvent.Resolved("balanced");
        var tracker = new PhysicalTransitionTracker(2);
        tracker.Add(Copy(FakePhysicalEvent.Prepared(resolved.Event.TransitionId)));
        tracker.Add(Copy(resolved));
        var builder = new StringBuilder("{\"schema\":4,\"pluginVersion\":\"0.2.8\",\"records\":[],\"physicalTransitions\":");
        PhysicalTransitionJsonWriter.AppendArray(builder, tracker.Snapshot());
        builder.Append('}');
        return builder.ToString();
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
