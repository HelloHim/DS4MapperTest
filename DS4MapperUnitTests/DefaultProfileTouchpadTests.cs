using System.Text.Json;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class DefaultProfileTouchpadTests
    {
        private static string RepoRoot
        {
            get
            {
                string? current = AppContext.BaseDirectory;
                while (!string.IsNullOrEmpty(current))
                {
                    if (Directory.Exists(Path.Combine(current, "template_profiles")))
                    {
                        return current;
                    }

                    current = Directory.GetParent(current)?.FullName;
                }

                throw new DirectoryNotFoundException(
                    "Could not locate template_profiles from the test output directory.");
            }
        }

        [TestMethod]
        public void SteamControllerPlayStationDefaults_DoNotBindTouchpadInputs()
        {
            AssertProfileOmitsInputs(
                Path.Combine(RepoRoot, "template_profiles", "SteamController",
                    "Default - DS4.json"),
                "LeftTouchpad", "RightTouchpad", "LeftPadClick", "RightPadClick");
            AssertProfileOmitsInputs(
                Path.Combine(RepoRoot, "template_profiles", "SteamController",
                    "Default - DualSense Edge.json"),
                "LeftTouchpad", "RightTouchpad", "LeftPadClick", "RightPadClick");
        }

        [TestMethod]
        public void SteamControllerPlayStationDefaults_MapBackToShareInsteadOfTouchClick()
        {
            AssertMappedButtonOutput(
                Path.Combine(RepoRoot, "template_profiles", "SteamController",
                    "Default - DS4.json"),
                "Back",
                "X360_Back");
            AssertMappedButtonOutput(
                Path.Combine(RepoRoot, "template_profiles", "SteamController",
                    "Default - DualSense Edge.json"),
                "Back",
                "X360_Back");
            AssertMappedButtonOutput(
                Path.Combine(RepoRoot, "template_profiles", "SteamControllerTriton",
                    "Default - DS4.json"),
                "Select",
                "X360_Back");
            AssertMappedButtonOutput(
                Path.Combine(RepoRoot, "template_profiles", "SteamControllerTriton",
                    "Default - DualSense Edge.json"),
                "Select",
                "X360_Back");
        }

        private static void AssertProfileOmitsInputs(string profilePath,
            params string[] omittedInputs)
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(profilePath));
            JsonElement mappings =
                doc.RootElement.GetProperty("Mappings")[0].GetProperty("InputMappings");

            HashSet<string> mappedInputs = mappings
                .EnumerateArray()
                .Select(item => item.GetProperty("Input").GetString() ?? string.Empty)
                .ToHashSet(StringComparer.Ordinal);

            foreach (string input in omittedInputs)
            {
                Assert.IsFalse(mappedInputs.Contains(input),
                    $"{Path.GetFileName(profilePath)} should not map {input}.");
            }
        }

        private static void AssertMappedButtonOutput(string profilePath, string input,
            string expectedPadOutput)
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(profilePath));
            JsonElement root = doc.RootElement;
            JsonElement inputMappings =
                root.GetProperty("Mappings")[0].GetProperty("InputMappings");

            int actionId = inputMappings
                .EnumerateArray()
                .Where(item => string.Equals(item.GetProperty("Input").GetString(), input,
                    StringComparison.Ordinal))
                .Select(item => item.GetProperty("Action").ValueKind ==
                    JsonValueKind.String
                    ? int.Parse(item.GetProperty("Action").GetString()!)
                    : item.GetProperty("Action").GetInt32())
                .Single();

            JsonElement mappedAction =
                root.GetProperty("ActionSets")[0]
                    .GetProperty("ActionLayers")[0]
                    .GetProperty("MappedActions")
                    .EnumerateArray()
                    .Single(item => item.GetProperty("Id").ValueKind == JsonValueKind.String
                        ? int.Parse(item.GetProperty("Id").GetString()!) == actionId
                        : item.GetProperty("Id").GetInt32() == actionId);

            string actualPadOutput = mappedAction
                .GetProperty("Functions")[0]
                .GetProperty("OutputActions")[0]
                .GetProperty("PadOutput")
                .GetString() ?? string.Empty;

            Assert.AreEqual(expectedPadOutput, actualPadOutput,
                $"{Path.GetFileName(profilePath)} should map {input} to {expectedPadOutput}.");
        }
    }
}
