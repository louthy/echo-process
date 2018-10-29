using LanguageExt;
using Newtonsoft.Json;
using System;
using static LanguageExt.Prelude;

namespace Echo
{
    /// <summary>
    /// Session ID
    /// </summary>
    /// <remarks>
    /// It enforces the rules for session IDs.
    /// </remarks>
    public class SessionId : Record<SessionId>
    {
        public readonly string Value;

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="value">SessionId</param>
        [JsonConstructor]
        public SessionId(string value)
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
        public static SessionId Generate(int sizeInBytes = DefaultSessionIdSizeInBytes) =>
            new SessionId(randomBase64(sizeInBytes));

        public bool IsValid =>
            !String.IsNullOrEmpty(Value);

        public override string ToString() =>
            Value;

        public static implicit operator SessionId(string value) =>
            new SessionId(value);

    }

    /// <summary>
    /// Supplementary session id new type
    /// </summary>
    public class SupplementarySessionId : SessionId
    {
        public static string Key => $"sys-supp-session";
        public SupplementarySessionId(string value) : base(value) { }

        public static SupplementarySessionId Generate() => new SupplementarySessionId(SessionId.Generate().Value);

    }
}
