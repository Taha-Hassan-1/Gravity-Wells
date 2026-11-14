using System;
using System.Collections.Generic;

namespace GravityWells.Services
{
    public enum PurchaseOutcome
    {
        Success,
        Cancelled,
        Failed,
        Unavailable, // no store on this build/device — never treat as a purchase
    }

    public interface IPurchaseService
    {
        void Initialize();

        // The store's localized price (e.g. "$2.99", "2,99 EUR"), or null until it's known.
        string GetLocalizedPrice(string productId);

        void Purchase(string productId, Action<PurchaseOutcome> onComplete);

        // Reports every non-consumable the store says this account already owns.
        void Restore(Action<IReadOnlyCollection<string>> onOwnedProducts);
    }
}
