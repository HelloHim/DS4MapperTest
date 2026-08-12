namespace DS4MapperTest.Universal.Editor
{
    // Resolves a symbolic glyph key for a universal input given the
    // selected controller's capabilities. No per-button artwork exists in
    // this repository, so glyph keys are opaque strings the editor can use
    // to decide "do I have a picture for this" and otherwise degrade to the
    // text label alone. The one generic image asset already shipped with
    // the app is offered as a safe decorative fallback, never per-button
    // art that would need to be invented.
    public static class ControllerGlyphProvider
    {
        public const string GenericFallbackGlyphKey = "generic";
        public const string GenericFallbackImageResourcePath = "/images/gamepad-solid.png";

        public static string GetGlyphKey(UniversalInputId inputId, ControllerCapabilities capabilities)
        {
            return capabilities == null
                ? $"{GenericFallbackGlyphKey}:{inputId}"
                : capabilities.GetGlyphKey(inputId);
        }

        // No per-input image assets exist in the repository today, so every
        // glyph key currently resolves to "no image" and the caller must
        // fall back to the text label. This is a single, honest choke point
        // for that decision so a future asset pipeline only needs to change
        // this method, not every call site.
        public static bool TryResolveImageResourcePath(string glyphKey, out string resourcePath)
        {
            resourcePath = null;
            return false;
        }
    }
}
