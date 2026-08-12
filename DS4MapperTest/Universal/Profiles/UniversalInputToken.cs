using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DS4MapperTest.Universal.Profiles
{
    public static class UniversalInputToken
    {
        private static readonly IReadOnlyDictionary<UniversalInputId, string> tokensById =
            new ReadOnlyDictionary<UniversalInputId, string>(BuildTokens());

        private static readonly IReadOnlyDictionary<string, UniversalInputId> idsByToken =
            new ReadOnlyDictionary<string, UniversalInputId>(
                tokensById.ToDictionary(item => item.Value, item => item.Key, StringComparer.Ordinal));

        public static IReadOnlyDictionary<UniversalInputId, string> TokensById => tokensById;

        public static string Format(UniversalInputId id)
        {
            if (!tokensById.TryGetValue(id, out string token))
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown universal input id.");
            }

            return token;
        }

        public static bool TryParse(string token, out UniversalInputId id)
        {
            id = default;
            return !string.IsNullOrWhiteSpace(token) && idsByToken.TryGetValue(token, out id);
        }

        private static Dictionary<UniversalInputId, string> BuildTokens()
        {
            Dictionary<UniversalInputId, string> result = new Dictionary<UniversalInputId, string>();
            foreach (UniversalInputId id in Enum.GetValues(typeof(UniversalInputId)).Cast<UniversalInputId>())
            {
                result.Add(id, ToKebabCase(id.ToString()));
            }

            if (result.Values.Distinct(StringComparer.Ordinal).Count() != result.Count)
            {
                throw new InvalidOperationException("Universal profile input tokens must be unique.");
            }

            return result;
        }

        private static string ToKebabCase(string value)
        {
            List<char> chars = new List<char>(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (i > 0 && char.IsUpper(current))
                {
                    chars.Add('-');
                }

                chars.Add(char.ToLowerInvariant(current));
            }

            return new string(chars.ToArray());
        }
    }
}
