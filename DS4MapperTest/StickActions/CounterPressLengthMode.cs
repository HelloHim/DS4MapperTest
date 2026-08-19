namespace DS4MapperTest.StickActions
{
    /// <summary>
    /// Selects which representation of Counter Movement Release Press' Counter Press Length
    /// is authoritative for the visible UI and the runtime effective range. All three keep
    /// the same underlying settings synchronised, so switching modes never changes a value,
    /// only which representation is shown and which one drives GetEffectiveCounterPressLengthRange.
    /// Kept as a strongly typed enum rather than compared display strings so the mode is
    /// unambiguous through serialisation, migration and the UI.
    /// </summary>
    public enum CounterPressLengthMode
    {
        Fixed,
        WaitVariancePercentage,
        MinimumAndMaximum,
    }
}
