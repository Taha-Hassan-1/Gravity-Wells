using NUnit.Framework;
using GravityWells.Core;

namespace GravityWells.Tests
{
    public class AdMobConfigTests
    {
        [Test]
        public void GooglesPublicTestIds_AreRecognisedAsTestIds()
        {
            Assert.IsTrue(GameConfig.IsAdMobTestId("ca-app-pub-3940256099942544~3347511713"));
            Assert.IsTrue(GameConfig.IsAdMobTestId("ca-app-pub-3940256099942544/1033173712"));
            Assert.IsTrue(GameConfig.IsAdMobTestId("ca-app-pub-3940256099942544/5224354917"));
        }

        [Test]
        public void ARealPublisherId_IsNotATestId()
        {
            Assert.IsFalse(GameConfig.IsAdMobTestId("ca-app-pub-1234567890123456~1234567890"));
            Assert.IsFalse(GameConfig.IsAdMobTestId("ca-app-pub-1234567890123456/1234567890"));
        }

        [Test]
        public void EmptyOrMissingIds_AreNotTestIds()
        {
            Assert.IsFalse(GameConfig.IsAdMobTestId(null));
            Assert.IsFalse(GameConfig.IsAdMobTestId(string.Empty));
        }

        [Test]
        public void TheShippedDefaults_AreAllWellFormedAdMobIds()
        {
            // App ids use "~", ad unit ids use "/"; a typo here would fail the build or every ad request.
            StringAssert.StartsWith("ca-app-pub-", GameConfig.AdMobAndroidAppId);
            StringAssert.Contains("~", GameConfig.AdMobAndroidAppId);
            StringAssert.StartsWith("ca-app-pub-", GameConfig.AdMobInterstitialUnitId);
            StringAssert.Contains("/", GameConfig.AdMobInterstitialUnitId);
            StringAssert.StartsWith("ca-app-pub-", GameConfig.AdMobRewardedUnitId);
            StringAssert.Contains("/", GameConfig.AdMobRewardedUnitId);
            Assert.AreNotEqual(GameConfig.AdMobInterstitialUnitId, GameConfig.AdMobRewardedUnitId);
        }
    }
}
