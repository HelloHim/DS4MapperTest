using System.IO;

namespace DS4MapperTest.SdlDiagnostics
{
    internal static class SdlNativeLibraryLocator
    {
        public const string NativeLibraryFileName = "SDL3.dll";

        public static string GetExpectedOutputPath(string baseDirectory) =>
            Path.Combine(baseDirectory, NativeLibraryFileName);
    }
}
