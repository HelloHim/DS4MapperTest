namespace DS4MapperTest.Universal
{
    public static class UniversalLiveInputRoutingOptions
    {
        public static bool NintendoFaceButtonSwapEnabled { get; set; }

        public static void Apply(AppSettingsStore settings)
        {
            NintendoFaceButtonSwapEnabled = settings?.NintendoFaceButtonSwapEnabled == true;
        }
    }
}
