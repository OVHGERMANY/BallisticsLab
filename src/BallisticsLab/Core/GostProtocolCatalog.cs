using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace BallisticsLab.Core
{
    internal static class GostProtocolCatalog
    {
        internal const string ClassificationStandard = "GOST 34286-2017";
        internal const string TestMethodStandard = "GOST R 55623-2013";
        internal const string ClassificationSource =
            "https://protect.gost.ru/gost/details/1f0d4f81-957c-42b1-a509-1963dd51e528";
        internal const string TestMethodSource =
            "https://protect.gost.ru/gost/details/8db08cb9-43cc-4656-8f9a-bb11a14deb6b";

        private const double ShortRangeMetres = 5d;
        private const double RifleRangeMetres = 10d;
        private const double HeavyRangeMetres = 50d;
        private const double ShortRangeToleranceMetres = 0.1d;
        private const double HeavyRangeToleranceMetres = 0.5d;
        private const double VelocityMeasurementDistanceMetres = 3d;
        private const int RequiredRifledShots = 5;
        private const double MaximumImpactAngleDegrees = 5d;
        private const double MinimumSeparationDiameters = 5d;

        private static readonly IReadOnlyList<ProtocolThreatDefinition> Definitions =
            new ReadOnlyCollection<ProtocolThreatDefinition>(
                new[]
                {
                    Threat(
                        "gost-34286-br1-9x18-pst",
                        "Br1",
                        "9x18 mm Pst 57-N-181S",
                        "Steel core",
                        0.0059d,
                        335d,
                        10d,
                        ShortRangeMetres,
                        ShortRangeToleranceMetres),
                    Threat(
                        "gost-34286-br2-9x21-p-7n28",
                        "Br2",
                        "9x21 mm P 7N28",
                        "Lead core",
                        0.00793d,
                        390d,
                        10d,
                        ShortRangeMetres,
                        ShortRangeToleranceMetres),
                    Threat(
                        "gost-34286-br3-9x19-pst-7n21",
                        "Br3",
                        "9x19 mm Pst 7N21",
                        "Heat-treated steel core",
                        0.007d,
                        410d,
                        10d,
                        ShortRangeMetres,
                        ShortRangeToleranceMetres),
                    Threat(
                        "gost-34286-br4-5.45x39-pp-7n10",
                        "Br4",
                        "5.45x39 mm PP 7N10",
                        "Heat-treated steel core",
                        0.0035d,
                        895d,
                        15d,
                        RifleRangeMetres,
                        ShortRangeToleranceMetres),
                    Threat(
                        "gost-34286-br4-7.62x39-ps-57-n-231",
                        "Br4",
                        "7.62x39 mm PS 57-N-231",
                        "Heat-treated steel core",
                        0.0079d,
                        720d,
                        15d,
                        RifleRangeMetres,
                        ShortRangeToleranceMetres),
                    Threat(
                        "gost-34286-br5-7.62x54-pp-7n13",
                        "Br5",
                        "7.62x54 mm PP 7N13",
                        "Heat-treated steel core",
                        0.0094d,
                        830d,
                        15d,
                        RifleRangeMetres,
                        ShortRangeToleranceMetres),
                    Threat(
                        "gost-34286-br5-7.62x54-b32-7-bz-3",
                        "Br5",
                        "7.62x54 mm B-32 7-BZ-3",
                        "Armor-piercing incendiary steel core",
                        0.0104d,
                        810d,
                        15d,
                        RifleRangeMetres,
                        ShortRangeToleranceMetres),
                    Threat(
                        "gost-34286-br6-12.7x108-b32-57-bz-542",
                        "Br6",
                        "12.7x108 mm B-32 57-BZ-542",
                        "Armor-piercing incendiary steel core",
                        0.0482d,
                        830d,
                        20d,
                        HeavyRangeMetres,
                        HeavyRangeToleranceMetres)
                });

        internal static IReadOnlyList<ProtocolThreatDefinition> All => Definitions;

        internal static IReadOnlyList<ProtocolThreatDefinition> ForClass(string protectionClass)
        {
            if (string.IsNullOrWhiteSpace(protectionClass))
            {
                ThrowMissingProtectionClass(nameof(protectionClass));
            }
            return Array.AsReadOnly(
                Definitions
                    .Where(definition => string.Equals(
                        definition.ProtectionClass,
                        protectionClass,
                        StringComparison.Ordinal))
                    .ToArray());
        }

        internal static bool TryGet(string threatId, out ProtocolThreatDefinition? definition)
        {
            definition = Definitions.FirstOrDefault(candidate => string.Equals(
                candidate.ThreatId,
                threatId,
                StringComparison.Ordinal));
            return definition != null;
        }

        private static ProtocolThreatDefinition Threat(
            string threatId,
            string protectionClass,
            string cartridgeDesignation,
            string projectileConstruction,
            double projectileMassKilograms,
            double nominalVelocityMetresPerSecond,
            double velocityToleranceMetresPerSecond,
            double testDistanceMetres,
            double testDistanceToleranceMetres)
        {
            return new ProtocolThreatDefinition(
                threatId,
                protectionClass,
                cartridgeDesignation,
                projectileConstruction,
                projectileMassKilograms,
                nominalVelocityMetresPerSecond - velocityToleranceMetresPerSecond,
                nominalVelocityMetresPerSecond + velocityToleranceMetresPerSecond,
                testDistanceMetres,
                testDistanceToleranceMetres,
                VelocityMeasurementDistanceMetres,
                RequiredRifledShots,
                MaximumImpactAngleDegrees,
                MinimumSeparationDiameters,
                Array.Empty<string>());
        }

        [DoesNotReturn]
        private static void ThrowMissingProtectionClass(string parameterName)
        {
            throw new ArgumentException("A protection-class identifier is required.", parameterName);
        }
    }
}
