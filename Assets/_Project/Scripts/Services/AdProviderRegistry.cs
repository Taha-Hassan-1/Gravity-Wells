using System;
using System.Collections.Generic;

namespace GravityWells.Services
{
    // The list of ad networks this build knows how to create. Bootstrap registers one factory per
    // adapter; nothing is constructed (so no SDK is touched) until a provider is actually put in
    // the active order. Adding a network is one adapter class plus one Register line here.
    public sealed class AdProviderRegistry
    {
        private readonly Dictionary<string, Func<IAdService>> _factories =
            new Dictionary<string, Func<IAdService>>(StringComparer.OrdinalIgnoreCase);

        public void Register(string name, Func<IAdService> factory)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A provider needs a name.", nameof(name));
            }
            _factories[name.Trim()] = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public bool IsRegistered(string name) => name != null && _factories.ContainsKey(name.Trim());

        public IAdService Create(string name) => _factories[name.Trim()]();
    }
}
