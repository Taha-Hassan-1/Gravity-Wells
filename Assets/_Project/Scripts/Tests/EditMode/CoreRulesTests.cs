using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace GravityWells.Tests
{
    // The architectural rules of CLAUDE.md that a compiler cannot enforce, checked by reading the
    // source text of Core/.
    public class CoreRulesTests
    {
        private const string CoreFolder = "Assets/_Project/Scripts/Core";

        private static string[] CoreFiles()
        {
            string[] files = Directory.GetFiles(CoreFolder, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(files.Length, 10, "Core source not found; tests must run with the project folder as the working directory");
            return files;
        }

        // Comments are dropped first, so a sentence that merely names a rule does not trip it.
        private static string CodeOf(string path)
        {
            string text = File.ReadAllText(path);
            text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(text, @"//[^\n]*", string.Empty);
        }

        [Test]
        public void NoTrigonometry_InAnyFileThatTouchesTheSimulatorOrTheGenerator()
        {
            var forbidden = new Regex(@"\b(Mathf|Math|MathF)\s*\.\s*(Sin|Cos|Tan|Asin|Acos|Atan|Atan2|Pow|Exp|Log|Log10)\b");
            int checkedFiles = 0;

            foreach (string path in CoreFiles())
            {
                string code = CodeOf(path);
                if (!code.Contains("FlightSimulator") && !code.Contains("LevelGenerator"))
                {
                    continue;
                }
                checkedFiles++;
                Match match = forbidden.Match(code);
                Assert.IsFalse(match.Success, $"{path} uses {match.Value}: only + - * / and square root are allowed there");
            }

            Assert.GreaterOrEqual(checkedFiles, 2, "the simulator and the generator must both have been checked");
        }

        [Test]
        public void Core_UsesNothingFromUnityButVector2AndMathf()
        {
            var unityType = new Regex(
                @"\b(MonoBehaviour|GameObject|Transform|Debug\s*\.\s*Log\w*|Resources\s*\.|Time\s*\.\s*\w+|Physics2?D?\s*\.|Coroutine|" +
                @"IEnumerator|Color32?|Vector3|Vector4|Quaternion|Rect|Texture2D|Sprite|Application\s*\.)\b");

            foreach (string path in CoreFiles())
            {
                string name = Path.GetFileName(path);
                // Copied shared code that predates this rule check and uses Color for palette maths
                // (allowed in the sibling games too): colour is a value type with no scene behind it.
                if (name == "LineColors.cs")
                {
                    continue;
                }
                Match match = unityType.Match(CodeOf(path));
                Assert.IsFalse(match.Success, $"{path} uses {match.Value}; Core may use only Vector2 and Mathf from UnityEngine");
            }
        }

        [Test]
        public void Core_NeverUsesUnitysRandom()
        {
            var unityRandom = new Regex(@"UnityEngine\s*\.\s*Random|(?<![\w.])Random\s*\.\s*(Range|value|insideUnitCircle|InitState)\b");
            foreach (string path in CoreFiles())
            {
                Match match = unityRandom.Match(CodeOf(path));
                Assert.IsFalse(match.Success, $"{path} uses {match.Value}; Core takes a seeded System.Random");
            }
        }

        [Test]
        public void OnlyTheGeneratorItsMeasureAndTheLevelItself_ReadTheStoredSolution()
        {
            foreach (string path in CoreFiles())
            {
                string name = Path.GetFileName(path);
                if (name == "LevelGenerator.cs" || name == "Forgiveness.cs" || name == "LevelDefinition.cs")
                {
                    continue;
                }
                Assert.IsFalse(CodeOf(path).Contains("SolutionWells"), path + " reads the stored solution; the solver and the hint must not");
            }

            foreach (string folder in new[] { "Game", "Meta", "UI", "Services" })
            {
                string root = "Assets/_Project/Scripts/" + folder;
                foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    Assert.IsFalse(CodeOf(path).Contains("SolutionWells"), path + " reads the stored solution");
                }
            }
        }
    }
}
