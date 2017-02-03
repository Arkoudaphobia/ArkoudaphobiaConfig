using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("RustyBearDebug", "Diablo", "1.0.0")]
    class RustyBearDebug : RustPlugin
    {
        void Loaded()
        {
            permission.RegisterPermission("rustybeardebug.use", this);
        }

        [Command("FindEntity"), Permission("rustybeardebug.use")]
        void CmdFindEnt(BasePlayer player, string command, string[] args)
        {
            if (args[0] == "base")
            {
                var entbase = BaseEntity.serverEntities.Where(x => x.ShortPrefabName == args[1]);

                if (entbase == null)
                {
                    SendReply(player, "Entity Not FOund");
                }
                else
                {
                    SendReply(player, $"BaseType  {entbase}");
                }
            }
        }
    }
}