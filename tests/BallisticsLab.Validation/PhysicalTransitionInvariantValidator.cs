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
                    out ComponentOrigin origin,
                    out failure))
            {
                failure = "output component: " + failure;
                return false;
            }
            outputEnergy += kineticEnergy;
            if (origin == ComponentOrigin.ParentDerived)
            {
                parentDerivedMass += retainedMass;
                parentDerivedCount++;
            }
            else if (origin == ComponentOrigin.TargetMaterial)
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
        return ValidateConservation(
            conservation,
            parent,
            outputEnergy,
            parentDerivedMass,
            targetMaterialMass,
            parentDerivedCount,
            targetMaterialCount,
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

    private static bool ValidateImpact(JsonElement impact)
    {
        return TryVector(impact, "positionMetres")
            && TryVectorValues(impact, "surfaceNormal", out double nx, out double ny, out double nz)
            && NearlyUnit(Math.Sqrt(nx * nx + ny * ny + nz * nz))
            && NonNegative(impact, "physicalThicknessMetres")
            && NonNegative(impact, "effectivePathLengthMetres")
            && !string.IsNullOrEmpty(RequiredString(impact, "targetMaterialClass"))
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
        out ComponentOrigin origin,
        out string failure)
    {
        retainedMass = 0d;
        kineticEnergy = 0d;
        origin = ComponentOrigin.Ambiguous;
        failure = string.Empty;
        if (string.IsNullOrEmpty(RequiredString(component, "projectileId"))
            || string.IsNullOrEmpty(RequiredString(component, "rootShotId"))
            || string.IsNullOrEmpty(RequiredString(component, "kind"))
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
            || fragmentIndex < 0
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
        foreach (JsonElement collision in history.EnumerateArray())
        {
            if (string.IsNullOrEmpty(RequiredString(collision, "collisionId"))
                || string.IsNullOrEmpty(RequiredString(collision, "materialClass"))
                || string.IsNullOrEmpty(RequiredString(collision, "outcome"))
                || !TryInt32(collision, "sequence", out int sequence)
                || sequence < 0
                || !TryVector(collision, "positionMetres")
                || !TryVector(collision, "incomingVelocityMetresPerSecond")
                || !TryVector(collision, "outgoingVelocityMetresPerSecond")
                || !NonNegative(collision, "incomingTranslationalEnergyJoules")
                || !NonNegative(collision, "outgoingTranslationalEnergyJoules")
                || !NonNegative(collision, "impactAngleRadians")
                || !NonNegative(collision, "effectivePathLengthMetres"))
            {
                failure = "collision history entry is invalid";
                return false;
            }
        }
        if (!TryBoolean(component, "isTargetMaterialOrigin", out bool targetOrigin)
            || !TryBoolean(component, "isParentDerivedMass", out bool parentDerived))
        {
            failure = "component mass-origin flags are missing or invalid";
            return false;
        }
        origin = targetOrigin == parentDerived
            ? ComponentOrigin.Ambiguous
            : targetOrigin
                ? ComponentOrigin.TargetMaterial
                : ComponentOrigin.ParentDerived;
        if (origin == ComponentOrigin.TargetMaterial
            && string.IsNullOrEmpty(RequiredString(component, "sourceMaterialId")))
        {
            failure = "target-material output omits its source material";
            return false;
        }
        if (origin == ComponentOrigin.ParentDerived
            && string.IsNullOrEmpty(RequiredString(component, "sourceProjectileId")))
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
            "unallocatedParentMassKilograms",
            "targetSpallMassKilograms",
            "parentEnergyJoules",
            "modeledLossEnergyJoules",
            "residualEnergyJoules",
            "outputEnergyJoules",
            "energyClosureErrorJoules"
        };
        foreach (string field in conservationFields)
        {
            if (!NonNegative(conservation, field))
            {
                failure = "conservation " + field + " is invalid";
                return false;
            }
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
        if (!TryInt32(conservation, "parentDerivedOutputCount", out int parentDerivedCount)
            || parentDerivedCount < 0
            || !TryInt32(conservation, "targetSpallOutputCount", out int targetSpallCount)
            || targetSpallCount < 0
            || !Nearly(parentMass, parent.GetProperty("retainedMassKilograms").GetDouble())
            || !Nearly(parentEnergy, parent.GetProperty("translationalKineticEnergyJoules").GetDouble())
            || !Nearly(parentMass, allocatedMass + unallocatedMass)
            || !Nearly(allocatedMass, calculatedParentDerivedMass)
            || !Nearly(targetSpallMass, calculatedTargetMaterialMass)
            || parentDerivedCount != calculatedParentDerivedCount
            || targetSpallCount != calculatedTargetMaterialCount
            || !Nearly(totalLoss, lossSum)
            || !Nearly(modeledLoss, totalLoss)
            || !Nearly(residualEnergy, Math.Max(0d, parentEnergy - modeledLoss))
            || !Nearly(outputEnergy, calculatedOutputEnergy)
            || !Nearly(closureError, residualEnergy - outputEnergy))
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

    private enum ComponentOrigin
    {
        Ambiguous = 0,
        ParentDerived = 1,
        TargetMaterial = 2
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

    internal static bool RejectsAmbiguousOutputMassProvenance()
    {
        JsonNode root = JsonNode.Parse(CreateReport())!;
        root["physicalTransitions"]![0]!["resolved"]!["outputs"]![0]![
            "isTargetMaterialOrigin"] = true;
        using JsonDocument document = JsonDocument.Parse(root.ToJsonString());
        return !PhysicalTransitionInvariantValidator.Validate(
                document.RootElement,
                out _,
                out string failure)
            && failure.Contains("ambiguous", StringComparison.OrdinalIgnoreCase);
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

    private static string CreateReport()
    {
        var tracker = new PhysicalTransitionTracker(2);
        tracker.Add(Copy(FakePhysicalEvent.Prepared("balanced")));
        tracker.Add(Copy(FakePhysicalEvent.Resolved("balanced")));
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
