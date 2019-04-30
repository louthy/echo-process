using LanguageExt;
using Newtonsoft.Json;
using System;
using static LanguageExt.Prelude;

namespace Echo
{
    /// <summary>
    /// Supplementary Session ID
    /// </summary>
    /// <remarks>
    /// It enforces the rules for session IDs.
    /// </remarks>
    public struct SupplementarySessionId : IEquatable<SupplementarySessionId>, IComparable<SupplementarySessionId>, IComparable
    {
        /// <summary>
        /// Hashfield key for supplementary sessionid
        /// </summary>
        public const string Key = "sys-supp-session";

        public readonly string Value;

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="value">SupplementarySessionId</param>
        [JsonConstructor]
        public SupplementarySessionId(string value)
        {
            Value = value;
        }

        const int DefaultSessionIdSizeInBytes = 32;

        /// <summary>
        /// Make a cryptographically strong session ID
        /// </summary>
        /// <param name="sizeInBytes">Size in bytes.  This is not the final string length, the final length depends
        /// on the Base64 encoding of a byte-array sizeInBytes long.  As a guide a 64 byte session ID turns into
        /// an 88 character string.</param>
        public static SupplementarySessionId Generate(int sizeInBytes = DefaultSessionIdSizeInBytes) =>
            new SupplementarySessionId(randomBase64(sizeInBytes));

        public bool IsValid =>
            !String.IsNullOrEmpty(Value);

        public override string ToString() =>
            Value;

        public override int GetHashCode() =>
            Value.GetHashCode();

        public static SupplementarySessionId New(string value) =>
            new SupplementarySessionId(value);


        public bool Equals(SupplementarySessionId other) =>
            Value.Equals(other.Value);

        public int CompareTo(SupplementarySessionId other) =>
            String.Compare(Value, other.Value, StringComparison.Ordinal);

        public int CompareTo(object obj) =>
            obj == null
                ? -1
                : obj is SupplementarySessionId sid
                    ? CompareTo(sid)
                    : -1;

        public static bool operator == (SupplementarySessionId lhs, SupplementarySessionId rhs) =>
            lhs.Equals(rhs);

        public static bool operator != (SupplementarySessionId lhs, SupplementarySessionId rhs) =>
            !lhs.Equals(rhs);

        public override bool Equals(object obj) =>
            obj is SupplementarySessionId sid && Equals(sid);
    }
}
