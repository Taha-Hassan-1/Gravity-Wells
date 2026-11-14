using System;
using System.Text;

namespace GravityWells.Core
{
    // A short code that names one puzzle: its seed, the campaign-level number its size comes from, and
    // optionally a move count to beat. Anyone who types the code regenerates the identical board, so a
    // puzzle can be shared as plain text with no server. Twelve characters of Crockford base32 carry
    // 48 bits of data and a 12-bit check, so a mistyped character is caught rather than quietly
    // giving a different puzzle. Shown as GW-XXXX-XXXX-XXXX.
    public readonly struct PuzzleCode
    {
        public readonly int Seed;
        public readonly int Level;        // 1..255, feeds DifficultyCurve for the board's size and density
        public readonly int TargetMoves;  // 0 = no target

        public PuzzleCode(int seed, int level, int targetMoves)
        {
            Seed = seed;
            Level = level;
            TargetMoves = targetMoves;
        }

        public bool HasTarget => TargetMoves > 0;
    }

    public static class PuzzleCodes
    {
        // Crockford base32: no I, L, O or U, so a code survives being read aloud or copied by eye.
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        private const int Digits = 12;
        private const string Prefix = "GW";

        public static string Encode(int seed, int level, int targetMoves)
        {
            ulong payload = Pack(seed, Clamp(level, 1, 255), Clamp(targetMoves, 0, 255));
            ulong value = (payload << 12) | Checksum(payload);

            var chars = new char[Digits];
            for (int i = Digits - 1; i >= 0; i--)
            {
                chars[i] = Alphabet[(int)(value & 31UL)];
                value >>= 5;
            }

            var text = new StringBuilder(Prefix.Length + 1 + Digits + 2);
            text.Append(Prefix);
            for (int i = 0; i < Digits; i++)
            {
                if (i % 4 == 0)
                {
                    text.Append('-');
                }
                text.Append(chars[i]);
            }
            return text.ToString();
        }

        // Accepts any case, with or without dashes, spaces and the UT prefix, and reads O as 0 and
        // I or L as 1, the way people mis-type them. False for anything that does not check out.
        public static bool TryDecode(string text, out PuzzleCode code)
        {
            code = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var cleaned = new StringBuilder(Digits);
            foreach (char raw in text)
            {
                if (char.IsWhiteSpace(raw) || raw == '-' || raw == '_')
                {
                    continue;
                }
                cleaned.Append(char.ToUpperInvariant(raw));
            }

            string body = cleaned.ToString();
            if (body.Length == Digits + Prefix.Length && body.StartsWith(Prefix, StringComparison.Ordinal))
            {
                body = body.Substring(Prefix.Length);
            }
            if (body.Length != Digits)
            {
                return false;
            }

            ulong value = 0;
            for (int i = 0; i < Digits; i++)
            {
                char c = body[i];
                c = c == 'O' ? '0' : (c == 'I' || c == 'L') ? '1' : c;
                int digit = Alphabet.IndexOf(c);
                if (digit < 0)
                {
                    return false;
                }
                value = (value << 5) | (uint)digit;
            }

            ulong payload = value >> 12;
            if ((value & 0xFFFUL) != Checksum(payload))
            {
                return false;
            }

            int seed = unchecked((int)(uint)(payload >> 16));
            int level = (int)((payload >> 8) & 0xFF);
            int moves = (int)(payload & 0xFF);
            if (level < 1)
            {
                return false;
            }

            code = new PuzzleCode(seed, level, moves);
            return true;
        }

        // The text a player sends: the code, what was achieved, and where to enter it.
        public static string ShareMessage(string code, int moves)
        {
            string achievement = moves > 0 ? $"I solved this Gravity Wells puzzle in {moves} moves. Can you beat it?" : "Try this Gravity Wells puzzle!";
            return $"{achievement}\nPuzzle code: {code}\nIn Gravity Wells open Modes > Puzzle Challenge and enter the code.";
        }

        private static ulong Pack(int seed, int level, int moves) =>
            ((ulong)unchecked((uint)seed) << 16) | ((ulong)level << 8) | (ulong)moves;

        // FNV-1a over the six payload bytes, folded to 12 bits: a typo in any single character changes it.
        private static ulong Checksum(ulong payload)
        {
            uint hash = 2166136261u;
            for (int shift = 40; shift >= 0; shift -= 8)
            {
                hash ^= (uint)((payload >> shift) & 0xFF);
                hash = unchecked(hash * 16777619u);
            }
            return (ulong)((hash ^ (hash >> 12) ^ (hash >> 24)) & 0xFFFu);
        }

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    }
}
