namespace DS4MapperTest.ActionUtil
{
    /// <summary>
    /// Abstraction over inclusive integer range sampling. Used by timing-variance features
    /// such as Counter Movement Release Press so unit tests can substitute deterministic
    /// values instead of depending on real randomness, and so the runtime path is never
    /// forced to allocate a new Random per sample.
    /// </summary>
    public interface IRandomRangeProvider
    {
        /// <summary>
        /// Returns a value in the inclusive range [minimum, maximum]. If minimum equals
        /// maximum, implementations must return that value directly without sampling.
        /// </summary>
        int NextInclusive(int minimum, int maximum);
    }
}
