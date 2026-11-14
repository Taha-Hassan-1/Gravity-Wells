using System.IO;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace GravityWells.Tests
{
    // The two UI fonts ship as baked (static) atlases, so a character missing from the bake
    // would silently fall back to another typeface. These fail first.
    public class UIFontTests
    {
        private static readonly string[] FontNames = { "VarelaRound-Regular SDF", "LilitaOne-Regular SDF" };

        private static TMP_FontAsset Load(string name)
        {
            var font = Resources.Load<TMP_FontAsset>("UI/Fonts/" + name);
            Assert.IsNotNull(font, name + " is missing from Resources/UI/Fonts");
            return font;
        }

        [Test]
        public void Fonts_AreStatic_SoPlayingNeverRewritesTheAssets([ValueSource(nameof(FontNames))] string name)
        {
            TMP_FontAsset font = Load(name);

            Assert.AreEqual(AtlasPopulationMode.Static, font.atlasPopulationMode);
            Assert.IsNotNull(font.atlasTexture);
            Assert.Greater(font.characterTable.Count, 90);
        }

        [Test]
        public void Fonts_HaveEveryPrintableAsciiCharacter([ValueSource(nameof(FontNames))] string name)
        {
            TMP_FontAsset font = Load(name);
            var missing = new StringBuilder();
            for (char c = ' '; c <= '~'; c++)
            {
                if (!font.HasCharacter(c))
                {
                    missing.Append(c);
                }
            }

            Assert.AreEqual(string.Empty, missing.ToString(), name + " has no glyph for these");
        }

        // Guards the assumption the bake rests on: the game's own text is plain ASCII. If a
        // string ever gains another character, add it to UIFontBuilder.Characters and re-bake.
        [Test]
        public void GameText_UsesOnlyCharactersTheFontsHave()
        {
            TMP_FontAsset body = Load(FontNames[0]);
            TMP_FontAsset display = Load(FontNames[1]);
            string scripts = Path.Combine(Application.dataPath, "_Project", "Scripts");
            var problems = new StringBuilder();

            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string relative = file.Substring(scripts.Length).Replace('\\', '/');
                if (relative.StartsWith("/Tests/") || relative.StartsWith("/Editor/"))
                {
                    continue;
                }
                foreach (char c in StringLiteralCharacters(File.ReadAllText(file)))
                {
                    if (c > '~' && !(body.HasCharacter(c) && display.HasCharacter(c)))
                    {
                        problems.Append(relative).Append(": U+").Append(((int)c).ToString("X4")).Append('\n');
                    }
                }
            }

            Assert.AreEqual(string.Empty, problems.ToString());
        }

        // The characters inside "..." literals, skipping // comments. Rough on purpose: a false
        // positive only asks for a look, and escapes other than \uXXXX are all ASCII.
        private static string StringLiteralCharacters(string source)
        {
            var found = new StringBuilder();
            foreach (string line in source.Split('\n'))
            {
                bool inString = false;
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (inString)
                    {
                        if (c == '\\' && i + 1 < line.Length)
                        {
                            if (line[i + 1] == 'u' && i + 5 < line.Length
                                && int.TryParse(line.Substring(i + 2, 4), System.Globalization.NumberStyles.HexNumber, null, out int code))
                            {
                                found.Append((char)code);
                                i += 5;
                            }
                            else
                            {
                                i++;
                            }
                        }
                        else if (c == '"')
                        {
                            inString = false;
                        }
                        else
                        {
                            found.Append(c);
                        }
                    }
                    else if (c == '"')
                    {
                        inString = true;
                    }
                    else if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                    {
                        break;
                    }
                }
            }
            return found.ToString();
        }
    }
}
