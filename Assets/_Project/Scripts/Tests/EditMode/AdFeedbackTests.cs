using System;
using System.Net;
using NUnit.Framework;
using GravityWells.Game;
using GravityWells.Services;

namespace GravityWells.Tests
{
    public class AdFeedbackTests
    {
        private static IPAddress[] Addresses(params string[] ips)
        {
            var result = new IPAddress[ips.Length];
            for (int i = 0; i < ips.Length; i++)
            {
                result[i] = IPAddress.Parse(ips[i]);
            }
            return result;
        }

        // ---- network probe ----

        [Test]
        public void AdHostSunkToZeroAddress_IsBlocked()
        {
            Assert.AreEqual(AdFailure.Blocked, AdNetworkProbe.Classify(Addresses("0.0.0.0"), controlHostResolved: true));
        }

        [Test]
        public void AdHostSunkToLoopbackOrIpv6Any_IsBlocked()
        {
            Assert.AreEqual(AdFailure.Blocked, AdNetworkProbe.Classify(Addresses("127.0.0.1"), true));
            Assert.AreEqual(AdFailure.Blocked, AdNetworkProbe.Classify(Addresses("::"), true));
            Assert.AreEqual(AdFailure.Blocked, AdNetworkProbe.Classify(Addresses("0.0.0.0", "::"), true));
        }

        [Test]
        public void AdHostWithNoAnswer_ButOtherHostsResolving_IsBlocked()
        {
            Assert.AreEqual(AdFailure.Blocked, AdNetworkProbe.Classify(null, controlHostResolved: true));
            Assert.AreEqual(AdFailure.Blocked, AdNetworkProbe.Classify(new IPAddress[0], controlHostResolved: true));
        }

        [Test]
        public void NothingResolving_IsOffline()
        {
            Assert.AreEqual(AdFailure.Offline, AdNetworkProbe.Classify(null, controlHostResolved: false));
        }

        [Test]
        public void AdHostWithARealAddress_IsNotAnNetworkProblem()
        {
            Assert.AreEqual(AdFailure.None, AdNetworkProbe.Classify(Addresses("142.250.185.34"), true));
            // one real address among sinks is still reachable
            Assert.AreEqual(AdFailure.None, AdNetworkProbe.Classify(Addresses("0.0.0.0", "142.250.185.34"), true));
        }

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
