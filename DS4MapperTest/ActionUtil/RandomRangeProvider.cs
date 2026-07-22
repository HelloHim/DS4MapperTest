using System;

namespace DS4MapperTest.ActionUtil
{
    /// <summary>
    /// Default runtime IRandomRangeProvider. Uses the shared thread-safe Random.Shared
    /// instance rather than allocating a new Random per call, since callers of this type
    /// (such as Counter Movement Release Press) sample from a high-frequency input mapper.
    /// This is a timing-variation feature, not a security function, so cryptographic
    /// randomness is deliberately not used.
    /// </summary>
    public sealed class RandomRangeProvider : IRandomRangeProvider
    {
        public static readonly RandomRangeProvider Instance = new RandomRangeProvider();

        public int NextInclusive(int minimum, int maximum)
        {
            if (minimum >= maximum)
            {
                return minimum;
            }

            // Random.Next's upper bound is exclusive, so +1 makes the sampled range inclusive.
            return Random.Shared.Next(minimum, maximum + 1);
        }
    }
}
