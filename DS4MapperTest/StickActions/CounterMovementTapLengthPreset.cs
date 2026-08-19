namespace DS4MapperTest.StickActions
{
    /// <summary>
    /// Named presets for Counter Movement Release Press' Counter Tap Length Variance range.
    /// Kept as a strongly typed enum rather than compared display strings so the preset is
    /// unambiguous through serialisation, migration and the UI.
    /// </summary>
    public enum CounterMovementTapLengthPreset
    {
        Custom,
        CS2,
    }
}
