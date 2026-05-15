using Newtonsoft.Json;

namespace MikuSB.Data.Excel;

[ResourceEntity("item/templates/support_card.json")]
public class SupportCardExcel : ExcelResource
{
    public uint Genre { get; set; }
    public uint Detail { get; set; }
    public uint Particular { get; set; }
    public uint Level { get; set; }
    public uint Icon { get; set; }
    public uint ProvideExp { get; set; }
    public uint Color { get; set; }
    [JsonProperty("LevelLimitID")] public int LevelLimitId { get; set; }
    [JsonProperty("AffixPool")] public List<int> AffixPool { get; set; } = [];

    public uint MaxLevel => LevelLimitId switch
    {
        1007 => 10,
        1008 => 13,
        1009 => 16,
        _ => 10
    };

    // Number of affixes granted initially
    public int InitialAffixCount => Color >= 5 ? 2 : 1;

    // Total maximum affixes (including ones unlocked at max level)
    public int TotalAffixCount => Color >= 5 ? 3 : 2;

    public ulong TemplateId => GameResourceTemplateId.FromGdpl(Genre, Detail, Particular, Level);

    public override uint GetId() => Icon;

    public override void Loaded()
    {
        GameData.SupportCardData.Add(this);
    }
}
