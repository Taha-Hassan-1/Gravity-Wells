namespace GravityWells.Core
{
    // Every designer-tunable value lives here as a named constant (no Inspector). Simulation and
    // difficulty constants are added with the phases that use them.
    public static class GameConfig
    {
        // --- Field ---
        // Normalised, portrait. The origin is the bottom-left corner.
        public const float FieldWidth = 1f;
        public const float FieldHeight = 1.5f;
        public const float FieldMargin = 0.05f;             // how far outside the field the ship may go before it is lost

        // --- Flight (starting guesses; tuned from LevelBatchTester data and agreed with the owner before locking) ---
        public const int StepsPerSecond = 120;
        public const float TimeStep = 1f / StepsPerSecond;
        public const float MaxFlightSeconds = 8f;
        public const int TrajectoryCapacity = (int)(MaxFlightSeconds * StepsPerSecond) + 1;
        public const float LaunchSpeed = 0.35f;             // field widths per second
        public const float WellStrength = 0.02f;
        public const float Softening = 0.03f;               // keeps the pull finite close to a well
        public const float WellCoreRadius = 0.035f;         // touching this ends the flight
        public const float MinWellDistanceFromStart = 0.12f;
        public const int ExtraWellBudget = 2;               // the player may use par + this many wells
        public const int MaxGates = 8;                      // capacity of the per-gate buffers (the curve tops out at 5)
        public const int MaxWells = 8;                      // par 4 + 2 spare + the rewarded Extra Well, with room to spare

        // --- Level generation (starting guesses, as above) ---
        public const int GeneratorAttemptsPerRound = 300;
        public const int GeneratorRelaxedAttempts = 100000; // last resort after every gate count failed; never reached in testing
        public const float StartEdgeInset = 0.12f;
        public const float StartMaxHeightFraction = 1f / 3f; // the start sits in the lower third
        public const float MinStraightFlightSeconds = 1.2f;  // an unbent launch must stay on the field this long
        public const float WellFieldInset = 0.1f;
        public const float MinSolutionWellSpacing = 0.22f;
        public const int RepulsorFirstLevel = 35;
        public const float RepulsorChance = 0.3f;
        public const float PathEndInset = 0.08f;
        public const float MinSolutionFlightSeconds = 2.5f;
        public const float MaxSolutionFlightSeconds = 7f;
        public const float SolutionPathWellClearanceCores = 3f; // the solution path stays this many core radii from every well
        public const float MinTotalTurning = 1.5708f;        // a quarter turn, in radians, summed along the path
        public const float MinGoalDistanceFromStart = 0.3f;
        public const int MaxPathSelfCrossings = 1;
        public const float GateArcMin = 0.15f;               // gates sit between these fractions of the path length
        public const float GateArcMax = 0.85f;
        public const float MinGateArcSpacing = 0.15f;
        public const float GateClearance = 0.1f;             // from every solution well and from the goal
        public const int AsteroidFirstLevel = 12;
        public const float AsteroidMinRadius = 0.03f;
        public const float AsteroidMaxRadius = 0.05f;
        public const float AsteroidClearance = 0.07f;
        public const float FirstLevelGoalRadius = 0.09f;     // level 1 has a wide goal

        // --- Forgiveness (the difficulty number: how much error a solution tolerates) ---
        public const float ForgivenessRadius = 0.02f;
        public const int ForgivenessSamples = 24;
        public const float MinForgiveness = 0.2f;

        // --- View and input ---
        // The camera frames the field plus this margin, inside the space left by the HUD (top) and the well tray
        // and Launch button (bottom). Fractions of the screen.
        public const float ViewMargin = 0.04f;              // world units around the field
        public const float ViewSideMargin = 0.02f;
        public const float ViewTopReserve = 0.13f;
        public const float ViewBottomReserve = 0.21f;
        public const float TouchRadiusDp = 48f;             // how near a touch must be to a placed well to grab it
        public const float MinTouchRadiusWorldUnits = 0.07f; // the field is 1 unit wide, so this is about 7% of it
        public const float DragLiftWorldUnits = 0f;         // lift of a dragged well above the finger; to be judged on a phone
        public const float PlaybackSpeed = 1f;
        public const float FastPlaybackSpeed = 3f;          // a tap during a flight
        public const float WinHoldSeconds = 0.9f;           // the pause on the finished flight before the result card
        public const int PathDotStride = 6;                 // one dot every this many steps (0.05 s)
        public const float PathDotRadius = 0.0055f;
        public const float WellViewRadius = 0.11f;          // the pull ring drawn around a well

        // --- Meta ---
        public const float ThemeCrossfadeDuration = 0.3f;

        // --- Ads & monetization ---
        public const int InterstitialMinLevelsBetween = 3;
        public const float InterstitialMinSecondsBetween = 45f;
        public const int InterstitialMinLevel = 5;
        public const float InterstitialMinSecondsAfterLevelStart = 3f;
        public const int FirstSessionNoAdsBeforeLevel = 3;
        public const string RemoveAdsProductId = "remove_ads";
        public const string RemoveAdsFallbackPrice = "$2.99"; // shown only until the store reports the real localized price

        // Ad networks to use, highest priority first (comma separated, names as registered in
        // Bootstrap). The stub is deliberately absent from the device order: it always "completes",
        // so it would hand out rewards for free if it ever served in a release build.
        public const string AdProviderOrder = "admob";
        public const string EditorAdProviderOrder = "stub";

        // --- AdMob ---
        // Google's public TEST app/ad-unit ids: they always fill with a clearly marked test ad and can
        // never earn (or violate policy). Replace all three with the real ids from the AdMob console
        // before a public release; BuildScript warns while any of them is still a test id.
        public const string AdMobAndroidAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string AdMobInterstitialUnitId = "ca-app-pub-3940256099942544/1033173712";
        public const string AdMobRewardedUnitId = "ca-app-pub-3940256099942544/5224354917";
        private const string AdMobTestPublisherPrefix = "ca-app-pub-3940256099942544";

        public static bool IsAdMobTestId(string id)
        {
            return !string.IsNullOrEmpty(id) && id.StartsWith(AdMobTestPublisherPrefix, System.StringComparison.Ordinal);
        }
    }
}
