namespace DS4MapperTest.StickActions
{
    /// <summary>
    /// Selects which representation of Counter Movement Release Press' Opposite Tap Start
    /// Delay is authoritative for the visible UI and the runtime effective range. Mirrors
    /// OppositeTapLengthMode's three representations (Fixed, percentage variance around a
    /// fixed value, and an explicit Minimum/Maximum range); kept as a separate enum rather
    /// than reused because the start delay's default representation and default values
    /// differ from the tap length's.
    /// </summary>
    public enum OppositeTapStartDelayMode
    {
        Fixed,
        WaitVariancePercentage,
        MinimumAndMaximum,
    }
}
