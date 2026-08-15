using System;
using System.Diagnostics.CodeAnalysis;

namespace BallisticsLab.Core
{
    internal enum ProtocolAmmunitionIdentityStatus
    {
        ExactDesignation = 0,
        VariantDesignation = 1,
        NotAvailableInSupportedDatabase = 2
    }

    internal sealed class ProtocolAmmunitionMapping
    {
        private const double MassComparisonToleranceKilograms = 0.0000001d;
        private const double DiameterComparisonToleranceMetres = 0.0000001d;

        private ProtocolAmmunitionMapping(
            ProtocolAmmunitionIdentityStatus identityStatus,
            string templateId,
            string installedInternalName,
            string installedDisplayName,
            string installedCaliber,
            double installedProjectileMassKilograms,
            double installedProjectileDiameterMetres,
            double localeProjectileMassKilograms,
            double installedInitialSpeedMetresPerSecond,
            string englishDesignationEvidence,
            string russianDesignationEvidence,
            string englishMassEvidence,
            string verificationNote)
        {
            if (identityStatus < ProtocolAmmunitionIdentityStatus.ExactDesignation
                || identityStatus
                    > ProtocolAmmunitionIdentityStatus.NotAvailableInSupportedDatabase)
            {
                ThrowInvalidIdentityStatus(identityStatus);
            }
            VerificationNote = Required(verificationNote, nameof(verificationNote));
            IdentityStatus = identityStatus;
            if (identityStatus == ProtocolAmmunitionIdentityStatus.NotAvailableInSupportedDatabase)
            {
                TemplateId = string.Empty;
                InstalledInternalName = string.Empty;
                InstalledDisplayName = string.Empty;
                InstalledCaliber = string.Empty;
                EnglishDesignationEvidence = string.Empty;
                RussianDesignationEvidence = string.Empty;
                EnglishMassEvidence = string.Empty;
                return;
            }

            TemplateId = Required(templateId, nameof(templateId));
            InstalledInternalName = Required(
                installedInternalName,
                nameof(installedInternalName));
            InstalledDisplayName = Required(
                installedDisplayName,
                nameof(installedDisplayName));
            InstalledCaliber = Required(installedCaliber, nameof(installedCaliber));
            ValidatePositive(
                installedProjectileMassKilograms,
                nameof(installedProjectileMassKilograms));
            ValidatePositive(
                installedProjectileDiameterMetres,
                nameof(installedProjectileDiameterMetres));
            ValidatePositive(
                localeProjectileMassKilograms,
                nameof(localeProjectileMassKilograms));
            ValidatePositive(
                installedInitialSpeedMetresPerSecond,
                nameof(installedInitialSpeedMetresPerSecond));
            InstalledProjectileMassKilograms = installedProjectileMassKilograms;
            InstalledProjectileDiameterMetres = installedProjectileDiameterMetres;
            LocaleProjectileMassKilograms = localeProjectileMassKilograms;
            InstalledInitialSpeedMetresPerSecond = installedInitialSpeedMetresPerSecond;
            EnglishDesignationEvidence = Required(
                englishDesignationEvidence,
                nameof(englishDesignationEvidence));
            RussianDesignationEvidence = Required(
                russianDesignationEvidence,
                nameof(russianDesignationEvidence));
            EnglishMassEvidence = Required(englishMassEvidence, nameof(englishMassEvidence));
        }

        internal ProtocolAmmunitionIdentityStatus IdentityStatus { get; }
        internal string TemplateId { get; }
        internal string InstalledInternalName { get; }
        internal string InstalledDisplayName { get; }
        internal string InstalledCaliber { get; }
        internal double InstalledProjectileMassKilograms { get; }
        internal double InstalledProjectileDiameterMetres { get; }
        internal double LocaleProjectileMassKilograms { get; }
        internal double InstalledInitialSpeedMetresPerSecond { get; }
        internal string EnglishDesignationEvidence { get; }
        internal string RussianDesignationEvidence { get; }
        internal string EnglishMassEvidence { get; }
        internal string VerificationNote { get; }
        internal bool HasInstalledTemplate => IdentityStatus
            != ProtocolAmmunitionIdentityStatus.NotAvailableInSupportedDatabase;
        internal bool CanQualifySimulationScreening => IdentityStatus
            == ProtocolAmmunitionIdentityStatus.ExactDesignation;

        internal static ProtocolAmmunitionMapping Installed(
            ProtocolAmmunitionIdentityStatus identityStatus,
            string templateId,
            string installedInternalName,
            string installedDisplayName,
            string installedCaliber,
            double installedProjectileMassKilograms,
            double installedProjectileDiameterMetres,
            double localeProjectileMassKilograms,
            double installedInitialSpeedMetresPerSecond,
            string englishDesignationEvidence,
            string russianDesignationEvidence,
            string englishMassEvidence,
            string verificationNote)
        {
            if (identityStatus == ProtocolAmmunitionIdentityStatus.NotAvailableInSupportedDatabase)
            {
                ThrowInstalledMappingCannotBeUnavailable();
            }
            return new ProtocolAmmunitionMapping(
                identityStatus,
                templateId,
                installedInternalName,
                installedDisplayName,
                installedCaliber,
                installedProjectileMassKilograms,
                installedProjectileDiameterMetres,
                localeProjectileMassKilograms,
                installedInitialSpeedMetresPerSecond,
                englishDesignationEvidence,
                russianDesignationEvidence,
                englishMassEvidence,
                verificationNote);
        }

        internal static ProtocolAmmunitionMapping Unavailable(string verificationNote)
        {
            return new ProtocolAmmunitionMapping(
                ProtocolAmmunitionIdentityStatus.NotAvailableInSupportedDatabase,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                0d,
                0d,
                0d,
                0d,
                string.Empty,
                string.Empty,
                string.Empty,
                verificationNote);
        }

        internal bool MatchesRecordedProjectileMass(double projectileMassKilograms)
        {
            return IsFinite(projectileMassKilograms)
                && projectileMassKilograms > 0d
                && HasInstalledTemplate
                && Math.Abs(projectileMassKilograms - InstalledProjectileMassKilograms)
                    <= MassComparisonToleranceKilograms;
        }

        internal bool MatchesRecordedProjectileDiameter(double projectileDiameterMetres)
        {
            return IsFinite(projectileDiameterMetres)
                && projectileDiameterMetres > 0d
                && HasInstalledTemplate
                && Math.Abs(projectileDiameterMetres - InstalledProjectileDiameterMetres)
                    <= DiameterComparisonToleranceMetres;
        }

        internal bool InstalledMassMatchesNominal(double nominalProjectileMassKilograms)
        {
            return IsFinite(nominalProjectileMassKilograms)
                && nominalProjectileMassKilograms > 0d
                && HasInstalledTemplate
                && Math.Abs(InstalledProjectileMassKilograms - nominalProjectileMassKilograms)
                    <= MassComparisonToleranceKilograms;
        }

        internal bool InstalledMassMatchesLocale()
        {
            return HasInstalledTemplate
                && Math.Abs(InstalledProjectileMassKilograms - LocaleProjectileMassKilograms)
                    <= MassComparisonToleranceKilograms;
        }

        private static string Required(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ThrowMissingText(parameterName);
            }
            return value;
        }

        private static void ValidatePositive(double value, string parameterName)
        {
            if (!IsFinite(value) || value <= 0d)
            {
                ThrowPositive(parameterName);
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        [DoesNotReturn]
        private static void ThrowInvalidIdentityStatus(ProtocolAmmunitionIdentityStatus value)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unsupported protocol ammunition identity status.");
        }

        [DoesNotReturn]
        private static void ThrowInstalledMappingCannotBeUnavailable()
        {
            throw new ArgumentException(
                "An installed mapping must identify an exact or variant designation.");
        }

        [DoesNotReturn]
        private static void ThrowMissingText(string parameterName)
        {
            throw new ArgumentException("A nonempty value is required.", parameterName);
        }

        [DoesNotReturn]
        private static void ThrowPositive(string parameterName)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Value must be finite and positive.");
        }
    }
}
