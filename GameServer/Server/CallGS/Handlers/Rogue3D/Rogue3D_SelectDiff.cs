using MikuSB.Database.Player;
using MikuSB.GameServer.Game.Player;
using MikuSB.Proto;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace MikuSB.GameServer.Server.CallGS.Handlers.Rogue3D;

// Selects the Rogue3D difficulty.
// Persists CurDiff (sid=5) and GameplayId (sid=6) as player attributes (GroupId=124).
// param: {"nDiffId": int}
// Response: {} on success, {"sErr": "key"} on failure
[CallGSApi("Rogue3D_SelectDiff")]
public class Rogue3D_SelectDiff : ICallGSHandler
{
    private const uint GroupId = 124;
    private const uint CurDiffSid = 5;
    private const uint GameplayIdSid = 6;

    // First gameplay group per difficulty (from server_01_difficult.txt, GameplayGroup column)
    private static readonly Dictionary<uint, uint> DiffToGameplayGroup = new()
    {
        { 1,   100 },
        { 2,   200 },
        { 3,   300 },
        { 4,   400 },
        { 100, 811 },
    };

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var req = JsonSerializer.Deserialize<SelectDiffParam>(param);
        if (req == null)
        {
            await CallGSRouter.SendScript(connection, "Rogue3D_SelectDiff", "{}");
            return;
        }

        if (!DiffToGameplayGroup.TryGetValue(req.DiffId, out var gameplayGroup))
        {
            await CallGSRouter.SendScript(connection, "Rogue3D_SelectDiff", "{\"sErr\":\"rogue3.massage_gameProcessError\"}");
            return;
        }

        var player = connection.Player!;
        var sync = new NtfSyncPlayer();

        SetAttr(player, CurDiffSid, req.DiffId, sync);
        SetAttr(player, GameplayIdSid, gameplayGroup, sync);

        await CallGSRouter.SendScript(connection, "Rogue3D_SelectDiff", "{}", sync);
    }

    private static void SetAttr(PlayerInstance player, uint sid, uint val, NtfSyncPlayer sync)
    {
        var attr = player.Data.Attrs.FirstOrDefault(x => x.Gid == GroupId && x.Sid == sid);
        if (attr == null)
        {
            attr = new PlayerAttr { Gid = GroupId, Sid = sid };
            player.Data.Attrs.Add(attr);
        }
        attr.Val = val;
        sync.Custom[player.ToPackedAttrKey(GroupId, sid)] = val;
        sync.Custom[player.ToShiftedAttrKey(GroupId, sid)] = val;
    }
}

internal sealed class SelectDiffParam
{
    [JsonPropertyName("nDiffId")]
    public uint DiffId { get; set; }
}
