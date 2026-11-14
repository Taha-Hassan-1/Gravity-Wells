# 14 — Services, ads and analytics (Phase 5)

## What it does

* **Real ads on a device.** `AdMobAdService` (Google Mobile Ads 11.6.0, added with the owner's OK) and `AdMobConsentService` (Google's UMP consent) are registered in `Bootstrap` under `#if UNITY_ANDROID && !UNITY_EDITOR`. The device order is `GameConfig.AdProviderOrder = "admob"`; the Editor uses the stub. The stub is never in the device order. The ad ids are Google's public **test** ids until the owner supplies real ones (`BuildScript` warns while they are test ids).
* **Interstitial at "Next level" only.** `GameManager.NextLevel` goes through `AdFlow.ShowInterstitialThen`. The existing `AdPolicy` decides: level 5 or later, three levels and 45 s since the last ad, never within 3 s of a level start, nothing before level 3 in the first session, nothing with Remove ads. Never during a drag or a flight, and the daily has none.
* **Rewarded: hint, extra well, double coins.** The new button on the result card ("Double coins +N") pays the win's coins again for a whole rewarded ad, once per win.
* **Analytics.** `GameAnalytics` (Game/) turns level events into names from `AnalyticsEvents`; `Bootstrap` sends them to a `CompositeAnalyticsService` (console + `FileAnalyticsService`, a capped two-file log in the data folder). `ExceptionReporter` turns unhandled exceptions into `app_exception` events (20 per session at most, duplicates once).

Events: `session_start`, `level_start` (mode, level, wells = par), `launch` (level, wells, gates_lit, outcome), `level_complete` (mode, level, wells, par, launches, seconds, stars), `level_abandon` (level, launches, seconds, best_gates_lit), `hint_used` (paid_with coins/ad), `extra_well_used`, `coins_doubled`, `daily_started`, `daily_completed`, `ad_shown`, `ad_failed`, `iap_purchased`, `app_exception`.

## Rules

* Gameplay never touches an SDK; everything goes through `IAdService`, `IConsentService`, `IAnalyticsService`.
* A level left unsolved logs one `level_abandon`; a won level never does. The clock for `seconds` stops at the win, not when the player leaves the result card.
* A mediation or publisher SDK is one more `Register` line and one more analytics sink in `Bootstrap`.

## Not done

* Play Billing (Remove ads is still a stub purchase). Real AdMob ids, privacy policy URL, signing keys: the owner's.
* The Android resolve and a real ad on a phone: never run (no phone, no Android build yet).
