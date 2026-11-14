using System;
using System.Collections.Generic;
using NUnit.Framework;
using GravityWells.Core;

namespace GravityWells.Tests
{
    public class PuzzleCodeTests
    {
        // ---- encode / decode ----

        [Test]
        public void RoundTrip_ManyRandomPuzzles()
        {
            var random = new System.Random(99);
            for (int i = 0; i < 2000; i++)
            {
                int seed = random.Next(int.MinValue, int.MaxValue);
                int level = random.Next(1, 256);
                int moves = random.Next(0, 256);

                string text = PuzzleCodes.Encode(seed, level, moves);

                Assert.IsTrue(PuzzleCodes.TryDecode(text, out PuzzleCode code), text);
                Assert.AreEqual(seed, code.Seed);
                Assert.AreEqual(level, code.Level);
                Assert.AreEqual(moves, code.TargetMoves);
            }
        }

        [Test]
        public void Extremes_RoundTrip()
        {
            foreach (int seed in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
            {
                Assert.IsTrue(PuzzleCodes.TryDecode(PuzzleCodes.Encode(seed, 255, 255), out PuzzleCode code));
                Assert.AreEqual(seed, code.Seed);
                Assert.AreEqual(255, code.Level);
                Assert.AreEqual(255, code.TargetMoves);
            }
        }

        [Test]
        public void OutOfRangeLevelAndMoves_AreClamped_NotWrapped()
        {
            Assert.IsTrue(PuzzleCodes.TryDecode(PuzzleCodes.Encode(5, 9999, 9999), out PuzzleCode high));
            Assert.AreEqual(255, high.Level);
            Assert.AreEqual(255, high.TargetMoves);

            Assert.IsTrue(PuzzleCodes.TryDecode(PuzzleCodes.Encode(5, -4, -4), out PuzzleCode low));
            Assert.AreEqual(1, low.Level);
            Assert.AreEqual(0, low.TargetMoves);
            Assert.IsFalse(low.HasTarget);
        }

        [Test]
        public void Format_IsPrefixThenThreeGroupsOfFour_FromAFriendlyAlphabet()
        {
            string text = PuzzleCodes.Encode(123456, 7, 12);

            StringAssert.IsMatch("^GW-[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}$", text);
        }

        [Test]
        public void DifferentPuzzles_GiveDifferentCodes()
        {
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 500; seed++)
            {
                Assert.IsTrue(seen.Add(PuzzleCodes.Encode(seed, 10, 8)));
            }
            Assert.AreNotEqual(PuzzleCodes.Encode(1, 10, 8), PuzzleCodes.Encode(1, 10, 9));
            Assert.AreNotEqual(PuzzleCodes.Encode(1, 10, 8), PuzzleCodes.Encode(1, 11, 8));
        }

        // ---- forgiving input ----

        [Test]
        public void Decoding_IgnoresCase_Dashes_Spaces_AndTheOptionalPrefix()
        {
            string canonical = PuzzleCodes.Encode(424242, 20, 15);
            string bare = canonical.Replace("GW-", string.Empty).Replace("-", string.Empty);

            foreach (string variant in new[]
            {
                canonical, canonical.ToLowerInvariant(), "  " + canonical + "  ", bare, bare.ToLowerInvariant(),
                canonical.Replace("-", " "), canonical.Replace("-", string.Empty),
            })
            {
                Assert.IsTrue(PuzzleCodes.TryDecode(variant, out PuzzleCode code), "'" + variant + "'");
                Assert.AreEqual(424242, code.Seed, variant);
            }
        }

        [Test]
        public void Decoding_ReadsLookAlikeLettersAsTheDigitsTheyWereMeantAs()
        {
            // Find a code containing a 0 or a 1, then spell it with O and I/L as a tired typist might.
            string canonical = null;
            for (int seed = 0; seed < 1000 && canonical == null; seed++)
            {
                string candidate = PuzzleCodes.Encode(seed, 12, 9);
                if (candidate.IndexOf('0', 3) >= 0 && candidate.IndexOf('1', 3) >= 0)
                {
                    canonical = candidate;
                }
            }
            Assert.IsNotNull(canonical);

            string typo = canonical.Substring(0, 3) + canonical.Substring(3).Replace('0', 'O').Replace('1', 'L');

            Assert.IsTrue(PuzzleCodes.TryDecode(typo, out PuzzleCode a));
            Assert.IsTrue(PuzzleCodes.TryDecode(canonical, out PuzzleCode b));
            Assert.AreEqual(b.Seed, a.Seed);
        }

        // ---- catching mistakes ----

        [Test]
        public void ARandomSingleCharacterTypo_IsAlmostAlwaysCaught()
        {
            var random = new System.Random(5);
            const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
            int mutations = 0;
            int caught = 0;
            for (int i = 0; i < 300; i++)
            {
                string text = PuzzleCodes.Encode(random.Next(int.MinValue, int.MaxValue), random.Next(1, 256), random.Next(0, 256));
                string digits = text.Replace("GW-", string.Empty).Replace("-", string.Empty);

                for (int position = 0; position < digits.Length; position++)
                {
                    foreach (char replacement in alphabet)
                    {
                        if (replacement == digits[position])
                        {
                            continue;
                        }
                        mutations++;
                        char[] mutated = digits.ToCharArray();
                        mutated[position] = replacement;
                        if (!PuzzleCodes.TryDecode(new string(mutated), out PuzzleCode decoded)
                            || PuzzleCodes.Encode(decoded.Seed, decoded.Level, decoded.TargetMoves) != text)
                        {
                            caught++;
                        }
                    }
                }
            }

            // A 12-bit check lets about 1 in 4096 slips through; anything near that is the design working.
            Assert.GreaterOrEqual(caught / (double)mutations, 0.998, $"{mutations - caught} of {mutations} typos slipped through");
        }

        [Test]
        public void ChangingTheCheckCharacters_IsAlwaysCaught()
        {
            string text = PuzzleCodes.Encode(777, 30, 14);
            string digits = text.Replace("GW-", string.Empty).Replace("-", string.Empty);
            const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

            // The last two characters hold only check bits, so altering either can never decode.
            for (int position = 10; position < 12; position++)
            {
                foreach (char replacement in alphabet)
                {
                    if (replacement == digits[position])
                    {
                        continue;
                    }
                    char[] mutated = digits.ToCharArray();
                    mutated[position] = replacement;
                    Assert.IsFalse(PuzzleCodes.TryDecode(new string(mutated), out _), $"position {position} -> {replacement}");
                }
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("GW-ABCD")]
        [TestCase("GW-0000-0000-0000-0000")]
        [TestCase("hello world")]
        [TestCase("GW-UUUU-UUUU-UUUU")]
        [TestCase("GW-!!!!-????-****")]
        public void NonsenseIsRejected(string text)
        {
            Assert.IsFalse(PuzzleCodes.TryDecode(text, out _));
        }

        // ---- the share message ----

        [Test]
        public void ShareMessage_NamesTheCode_TheResult_AndWhereToEnterIt()
        {
            string code = PuzzleCodes.Encode(1, 5, 9);

            string message = PuzzleCodes.ShareMessage(code, 9);

            StringAssert.Contains(code, message);
            StringAssert.Contains("9 moves", message);
            StringAssert.Contains("Puzzle Challenge", message);
        }

        [Test]
        public void ShareMessage_WithoutMoves_InvitesRatherThanBoasts()
        {
            string message = PuzzleCodes.ShareMessage(PuzzleCodes.Encode(1, 5, 0), 0);

            StringAssert.DoesNotContain("moves", message.Split('\n')[0]);
        }
    }
}
