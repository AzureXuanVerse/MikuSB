using MikuSB.Data;
using MikuSB.Database.Inventory;
using MikuSB.Enums.Item;
using MikuSB.Enums.Player;
using MikuSB.GameServer.Server.Packet.Send.Misc;
using MikuSB.Internationalization;

namespace MikuSB.GameServer.Command.Commands;

[CommandInfo("giveall", "Game.Command.GiveAll.Desc", "Game.Command.GiveAll.Usage", ["ga"], [PermEnum.Admin, PermEnum.Support])]
public class CommandGiveAll : ICommands
{
    [CommandMethod("weapon")]
    public async ValueTask GiveAllWeapon(CommandArg arg)
    {
        if (!await arg.CheckOnlineTarget()) return;
        if (await arg.GetOption('p') is not int particular) return;
        if (await arg.GetOption('l') is not int level) return;

        var detail = arg.GetInt(0);
        level = Math.Clamp(level, 1, 80);
        var player = arg.Target!.Player!;
        List<GameWeaponInfo> weapons = [];
        if (detail == -1)
        {
            // add all
            foreach (var config in GameData.WeaponData.Values)
            {
                var weapon = await player.InventoryManager!
                    .AddWeaponItem((ItemTypeEnum)config.Genre,config.Detail,config.Particular,config.Level,(uint)level,false);
                if (weapon != null) weapons.Add(weapon);
            }
        }
        else
        {
            var weapon = await player.InventoryManager!.AddWeaponItem(ItemTypeEnum.TYPE_WEAPON, (uint)detail,(uint)particular,1,(uint)level,false);
            if (weapon == null)
            {
                await arg.SendMsg(I18NManager.Translate("Game.Command.GiveAll.WeaponNotFound"));
                return;
            }
            weapons.Add(weapon);
        }
        if (weapons.Count > 0) await player.SendPacket(new PacketNtfCallScript(weapons));
        await arg.SendMsg(I18NManager.Translate("Game.Command.GiveAll.WeaponAdded", weapons.Count.ToString()));
    }
}