using System;
using System.Net;
using System.Threading.Tasks;

namespace GravityWells.Services
{
    // Works out *why* ads can't be reached after a failed load. The SDK only reports a generic
    // internal error when an ad-blocking DNS (AdGuard, NextDNS...), a VPN or a hosts file
    // sinks its servers, so the game checks the two things that tell the cases apart: does the
    // ad host resolve to a real address, and does an unrelated well-known host resolve at all.
    public static class AdNetworkProbe
    {
        private const string AdHost = "googleads.g.doubleclick.net";
        private const string ControlHost = "www.google.com";

        // Pure so it can be unit tested. Returns None when nothing looks wrong at the DNS level
        // (the caller then falls back to the SDK's own error code).
        public static AdFailure Classify(IPAddress[] adHostAddresses, bool controlHostResolved)
        {
            bool adResolved = adHostAddresses != null && adHostAddresses.Length > 0;

            if (!adResolved)
            {
                // The ad host has no answer. If an unrelated host resolves, the name itself is
                // being refused; if nothing resolves we are simply offline.
                return controlHostResolved ? AdFailure.Blocked : AdFailure.Offline;
            }

            // Ad blockers answer with a dead end: 0.0.0.0, ::, or loopback.
            foreach (IPAddress address in adHostAddresses)
            {
                if (!IsSinkAddress(address))
                {
                    return AdFailure.None;
                }
            }
            return AdFailure.Blocked;
        }

        public static bool IsSinkAddress(IPAddress address)
        {
            return address.Equals(IPAddress.Any)
                || address.Equals(IPAddress.IPv6Any)
                || IPAddress.IsLoopback(address);
        }

        // Runs the lookups off the main thread (DNS has no timeout we control) and reports from
        // that thread; callers marshal back to the main thread themselves.
        public static void Run(Action<AdFailure> onResult)
        {
            Task.Run(() =>
            {
                IPAddress[] adAddresses = TryResolve(AdHost);
                bool controlResolved = TryResolve(ControlHost) != null;
                AdFailure result;
                try
                {
                    result = Classify(adAddresses, controlResolved);
                }
                catch (Exception)
                {
                    result = AdFailure.None;
                }
                onResult(result);
            });
        }

        private static IPAddress[] TryResolve(string host)
        {
            try
            {
                return Dns.GetHostAddresses(host);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
