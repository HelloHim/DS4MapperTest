namespace DS4MapperTest.StickActions
{
    /// <summary>
    /// Selects which representation of Counter Movement Release Press' Counter Press Start
    /// Delay is authoritative for the visible UI and the runtime effective range. Mirrors
    /// CounterPressLengthMode's three representations (Fixed, percentage variance around a
    /// fixed value, and an explicit Minimum/Maximum range); kept as a separate enum rather
    /// than reused because the start delay's default representation and default values
    /// differ from the press length's.
    /// </summary>
    public enum CounterPressStartDelayMode
    {
        Fixed,
        WaitVariancePercentage,
        MinimumAndMaximum,
    }
}
