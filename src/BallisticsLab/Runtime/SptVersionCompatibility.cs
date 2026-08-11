using System;

namespace BallisticsLab.Runtime
{
    internal static class SptVersionCompatibility
    {
        internal const string CorePluginGuid = "com.SPT.core";
        internal const string SupportedCoreVersionText = "4.1.2";
        internal const string SupportedEftVersionText = "0.16.9.40743";
        internal const string VerifiedAssemblyHash = "3D1B0C637467B773EF64C31A321B6C7CDEFC05025B88DB4E482489FA6F59FEBA";

        internal static bool IsExactSupportedCoreVersion(Version version)
        {
            return version != null && version.Equals(new Version(4, 1, 2));
        }
    }
}

