using System;

namespace GravityWells.Meta
{
    // The coin balance. Every earn and spend goes through here so the balance can never go negative.
    public sealed class EconomyService
    {
        private readonly SaveService _save;

        public EconomyService(SaveService save)
        {
            _save = save;
        }

        public int Coins => _save.Data.Coins;

        public event Action<int> Changed;

        public bool CanAfford(int cost) => cost <= _save.Data.Coins;

        public void Earn(int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            long total = (long)_save.Data.Coins + amount;
            _save.Data.Coins = total > int.MaxValue ? int.MaxValue : (int)total;
            _save.Save();
            Changed?.Invoke(_save.Data.Coins);
        }

        public bool TrySpend(int cost)
        {
            if (cost < 0 || cost > _save.Data.Coins)
            {
                return false;
            }
            _save.Data.Coins -= cost;
            _save.Save();
            Changed?.Invoke(_save.Data.Coins);
            return true;
        }
    }
}
