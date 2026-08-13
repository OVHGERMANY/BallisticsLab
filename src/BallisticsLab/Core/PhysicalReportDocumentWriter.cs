using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace BallisticsLab.Core
{
    internal static class PhysicalReportDocumentWriter
    {
        internal static string Build(
            string shotRecordsJsonArray,
            IReadOnlyList<PhysicalTransitionRecord> transitions)
        {
            return Build(shotRecordsJsonArray, transitions, null, null);
        }

        internal static string Build(
            string shotRecordsJsonArray,
            IReadOnlyList<PhysicalTransitionRecord> transitions,
            CampaignDefinition? campaignDefinition,
            CampaignRunSnapshot? campaignSnapshot)
        {
            if (string.IsNullOrWhiteSpace(shotRecordsJsonArray))
            {
                throw new ArgumentException("Shot-record JSON array is required.", nameof(shotRecordsJsonArray));
            }
            if (transitions == null)
            {
                ThrowNullTransitions(nameof(transitions));
            }

            var builder = new StringBuilder(shotRecordsJsonArray.Length + 256);
            builder.Append("{\"schema\":")
                .Append(LabBuild.ReportSchema)
                .Append(",\"pluginVersion\":")
                .Append(LabPolicies.Json(LabBuild.PluginVersion))
                .Append(",\"records\":")
                .Append(shotRecordsJsonArray)
                .Append(",\"physicalTransitions\":");
            PhysicalTransitionJsonWriter.AppendArray(builder, transitions);
            builder.Append(",\"campaign\":");
            CampaignJsonWriter.AppendOrNull(builder, campaignDefinition, campaignSnapshot);
            builder.Append(",\"protocolScreeningResult\":");
            ProtocolScreeningResultDocumentWriter.AppendOrNull(
                builder,
                campaignDefinition,
                campaignSnapshot);
            builder.Append('}');
            return builder.ToString();
        }

        [DoesNotReturn]
        private static void ThrowNullTransitions(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }
    }
}
