using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Oxide.Core.Plugins;
using ProtoBuf;

namespace Oxide.Plugins
{
    [Info("RustyBearDebug", "Diablo", "1.0.7")]
    class RustyBearDebug : RustPlugin
    {
        void Loaded()
        {
            permission.RegisterPermission("rustybeardebug.use", this);
        }

        [ChatCommand("findentity"), Permission("rustybeardebug.use")]
        void CmdFindEnt(BasePlayer player, string command, string[] args)
        {
            var ent = BaseEntity.serverEntities.Where(x => x.ShortPrefabName == args[1]);

            if (ent == null)
            {
                SendReply(player, "Entity Not Found");
                return;
            }

            var extractedEnt = ent is BaseEntity;

            try
            {
                switch (args[0])
                {
                    case "base":
                        SendReplyInt(args, extractedEnt.GetType().BaseType.FullName, player);
                        return;
                    case "fullname":
                        SendReplyInt(args, extractedEnt.GetType().FullName, player);
                        return;
                    default:
                        SendReply(player, "Sub Command Not Found");
                        return;
                }
            }
            catch (Exception)
            {
                SendReply(player, "Command Failed!");
                throw;
            }
        }

        void SendReplyInt(string[] searchObj, string searchResult, BasePlayer player)
        {
            SendReply(player, $"{searchObj[0]} is: {searchResult}");
        }
    }
}