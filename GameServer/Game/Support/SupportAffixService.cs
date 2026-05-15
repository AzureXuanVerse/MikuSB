using MikuSB.Data;

namespace MikuSB.GameServer.Game.Support;

public static class SupportAffixService
{
    // Returns (affixId, tier) - both 1-based. Returns (0,0) if pool not found.
    public static (uint AffixId, uint Tier) GenerateRandomAffix(int poolId)
    {
        if (!GameData.SupportAffixPoolData.TryGetValue(poolId, out var pool))
            return (0, 0);

        var groups = pool.Groups.ToList();
        if (groups.Count == 0)
            return (0, 0);

        var totalWeight = groups.Sum(x => x.Weight);
        var roll = Random.Shared.Next(totalWeight);
        var cumulative = 0;
        var selectedAffixs = groups[0].Affixs;

        foreach (var (affixIds, weight) in groups)
        {
            cumulative += weight;
            if (roll < cumulative)
            {
                selectedAffixs = affixIds;
                break;
            }
        }

        if (selectedAffixs.Count == 0)
            return (0, 0);

        var affixId = selectedAffixs[Random.Shared.Next(selectedAffixs.Count)];
        var tierCount = GameData.SupportAffixData.GetValueOrDefault(affixId)?.TierCount ?? 5;
        var tier = (uint)(Random.Shared.Next(tierCount) + 1);
        return ((uint)affixId, tier);
    }
}
