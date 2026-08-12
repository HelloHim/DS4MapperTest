namespace DS4MapperTest.Universal
{
    public sealed class ControllerDisplayInfo
    {
        public const string GenericGlyphFamily = "generic";
        public const string UnknownControllerName = "Unknown Controller";

        public string DisplayName { get; }
        public string ControllerFamily { get; }
        public string GlyphFamily { get; }

        public ControllerDisplayInfo(string displayName, string controllerFamily = "", string glyphFamily = "")
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? UnknownControllerName : displayName;
            ControllerFamily = controllerFamily ?? string.Empty;
            GlyphFamily = string.IsNullOrWhiteSpace(glyphFamily) ? GenericGlyphFamily : glyphFamily;
        }

        public static ControllerDisplayInfo Unknown()
        {
            return new ControllerDisplayInfo(UnknownControllerName);
        }

        public string GetFallbackGlyphKey(UniversalInputId inputId)
        {
            return $"{GlyphFamily}:{inputId}";
        }
    }
}
