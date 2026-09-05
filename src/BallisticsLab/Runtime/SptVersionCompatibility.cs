using System;

namespace BallisticsLab.Runtime
{
    internal static class SptVersionCompatibility
    {
        internal const string CorePluginGuid = "com.SPT.core";
        internal const string SupportedCoreVersionText = "4.1.4";
        internal const string SupportedEftVersionText = "0.16.9.5.40743";
        internal const string VerifiedAssemblyHash = "EE25CEE1259777B38ED8B3E7841FDC2DB3C98540B1469FA539B1FF183476E436";

        internal static bool IsExactSupportedCoreVersion(Version? version)
        {
            return version != null && version.Equals(new Version(SupportedCoreVersionText));
        }
    }
}

