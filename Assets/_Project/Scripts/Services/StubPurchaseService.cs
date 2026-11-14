using System;
using System.Collections.Generic;

namespace GravityWells.Services
{
    // Lets the purchase flow be exercised end to end in the Editor and development builds.
    // In a release build it reports Unavailable: a stub that granted "Remove ads" for free
    // would be a real bug if it ever shipped without a billing implementation behind it.
    public sealed class StubPurchaseService : IPurchaseService
    {
        public void Initialize() { }

        public string GetLocalizedPrice(string productId) => null;

        public void Purchase(string productId, Action<PurchaseOutcome> onComplete)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            onComplete?.Invoke(PurchaseOutcome.Success);
#else
            onComplete?.Invoke(PurchaseOutcome.Unavailable);
#endif
        }

        public void Restore(Action<IReadOnlyCollection<string>> onOwnedProducts)
        {
            onOwnedProducts?.Invoke(new string[0]);
        }
    }
}
