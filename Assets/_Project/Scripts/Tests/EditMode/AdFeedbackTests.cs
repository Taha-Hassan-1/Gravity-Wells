using System;
using NUnit.Framework;
using GravityWells.Game;
using GravityWells.Services;

namespace GravityWells.Tests
{
    public class AdFeedbackTests
    {
        // ---- messages ----

        [Test]
        public void EveryFailureHasATitleAndBody_AndTheSpecificOnesDiffer()
        {
            var seenBodies = new System.Collections.Generic.HashSet<string>();
            foreach (AdFailure failure in Enum.GetValues(typeof(AdFailure)))
            {
                AdMessages.ForFailure(failure, out string title, out string body);
                Assert.IsNotEmpty(title, failure.ToString());
                Assert.IsNotEmpty(body, failure.ToString());
                seenBodies.Add(body);
            }

            AdMessages.ForFailure(AdFailure.Blocked, out string _, out string blocked);
            AdMessages.ForFailure(AdFailure.Offline, out string _, out string offline);
            Assert.AreNotEqual(blocked, offline);
        }

        [Test]
        public void BlockedMessage_NamesTheLikelyCauses()
        {
            AdMessages.ForFailure(AdFailure.Blocked, out string title, out string body);
            StringAssert.Contains("blocked", title.ToLowerInvariant());
            StringAssert.Contains("Private DNS", body);
        }
    }
}
