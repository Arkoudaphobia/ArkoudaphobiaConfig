using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using System;

namespace Oxide.Plugins
{
    [Info("Better Chat Mute", "LaserHydra", "1.0.1", ResourceId = 2272)]
    [Description("Mute plugin, made for use with Better Chat")]
    internal class BetterChatMute : CovalencePlugin
    {
        private static Dictionary<string, MuteInfo> mutes = new Dictionary<string, MuteInfo>();

        #region Classes

        public class Date
        {
            public string _value = "00/00/00/01/01/0001";
            private DateTime _cachedDateTime = DateTime.MinValue;

            internal DateTime value
            {
                get
                {
                    return _cachedDateTime;
                }
                set
                {
                    _value = $"{value.Second}/{value.Minute}/{value.Hour}/{value.Day}/{value.Month}/{value.Year}";

                    int[] date = (from val in _value.Split('/') select Convert.ToInt32(val)).ToArray();
                    _cachedDateTime = new DateTime(date[5], date[4], date[3], date[2], date[1], date[0]);
                }
            }
        }

        public class MuteInfo
        {
            public Date ExpireDate = new Date { value = DateTime.MinValue };

            [JsonIgnore]
            public bool Timed => ExpireDate.value != DateTime.MinValue;

            [JsonIgnore]
            public bool Expired => Timed && ExpireDate.value < DateTime.Now;

            public static bool IsMuted(IPlayer player) => mutes.ContainsKey(player.Id);

            public static readonly MuteInfo NonTimed = new MuteInfo(DateTime.MinValue);

            public MuteInfo()
            {
            }

            public MuteInfo(DateTime expireDate)
            {
                ExpireDate.value = expireDate;
            }
        }

        #endregion

        #region Hooks

        private void Loaded()
        {
            LoadData(ref mutes);
            SaveData(mutes);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Muted"] = "{player} was muted.",
                ["Muted Time"] = "{player} was muted for {time}.",
                ["Unmuted"] = "{player} was unmuted.",
                ["Not Muted"] = "{player} is currently not muted.",
                ["You Are Muted"] = "You may not chat, you are muted!",
                ["Mute Expired"] = "{player} is no longer muted.",
                ["Invalid Time Format"] = "Invalid time format. Example: 1d2h3m4s = 1 day, 2 hours, 3 min, 4 sec",
                ["Nobody Muted"] = "There is nobody muted at the moment.",
                ["Invalid Syntax Mute"] = "/mute <player|steamid> [time: 1d1h1m1s]",
                ["Invalid Syntax Unmute"] = "/unmute <player|steamid>",
                ["Player Name Not Found"] = "Could not find player with name '{name}'",
                ["Player ID Not Found"] = "Could not find player with ID '{id}'",
                ["Multiple Players Found"] = "Multiple matching players found: \n{matches}"
            }, this);

            timer.Repeat(10, 0, () =>
            {
                List<string> expired = mutes.Where(m => m.Value.Expired).Select(m => m.Key).ToList();

                foreach (string id in expired)
                {
                    mutes.Remove(id);
                    server.Broadcast(lang.GetMessage("Mute Expired", this).Replace("{player}", players.FindPlayerById(id)?.Name));
                    Puts(lang.GetMessage("Mute Expired", this).Replace("{player}", players.FindPlayerById(id)?.Name));
                }
            });
        }

        private object OnUserChat(IPlayer player, string message) => CheckMuted(player);

        private object OnBetterChat(IPlayer player, string message)
        {
            object result = CheckMuted(player);

            if (result is bool && !(bool)result)
                player.Reply(lang.GetMessage("You Are Muted", this, player.Id));

            return result;
        }

        #endregion

        #region Commands

        [Command("mutelist"), Permission("betterchatmute.use")]
        private void CmdMuteList(IPlayer player, string cmd, string[] args)
        {
            if (mutes.Count == 0)
                player.Reply(lang.GetMessage("Nobody Muted", this, player.Id));
            else
                player.Reply(string.Join(Environment.NewLine, mutes.Select(kvp => $"{players.FindPlayerById(kvp.Key).Name}: {FormatTime(kvp.Value.ExpireDate.value - DateTime.Now)}").ToArray()));
        }

        [Command("mute"), Permission("betterchatmute.use")]
        private void CmdMute(IPlayer player, string cmd, string[] args)
        {
            if (args.Length == 0)
            {
                player.Reply(lang.GetMessage("Invalid Syntax Mute", this, player.Id));
                return;
            }

            IPlayer target;

            switch (args.Length)
            {
                case 1:
                    target = GetPlayer(args[0], player);

                    if (target == null)
                        return;

                    mutes[target.Id] = MuteInfo.NonTimed;
                    SaveData(mutes);

                    server.Broadcast(lang.GetMessage("Muted", this).Replace("{player}", target.Name));
                    Puts(lang.GetMessage("Muted", this).Replace("{player}", target.Name));
                    break;

                case 2:
                    target = GetPlayer(args[0], player);

                    if (target == null)
                        return;

                    DateTime expireDate;

                    if (!TryGetDateTime(args[1], out expireDate))
                    {
                        player.Reply(lang.GetMessage("Invalid Time Format", this, player.Id));
                        return;
                    }

                    mutes[target.Id] = new MuteInfo(expireDate);
                    SaveData(mutes);

                    server.Broadcast(lang.GetMessage("Muted Time", this).Replace("{player}", target.Name).Replace("{time}", FormatTime(expireDate - DateTime.Now)));
                    Puts(lang.GetMessage("Muted Time", this).Replace("{player}", target.Name).Replace("{time}", FormatTime(expireDate - DateTime.Now)));
                    break;

                default:
                    player.Reply(lang.GetMessage("Invalid Syntax Mute", this, player.Id));
                    break;
            }
        }

        [Command("unmute"), Permission("betterchatmute.use")]
        private void CmdUnmute(IPlayer player, string cmd, string[] args)
        {
            if (args.Length != 1)
            {
                player.Reply(lang.GetMessage("Invalid Syntax Unmute", this, player.Id));
                return;
            }

            IPlayer target = GetPlayer(args[0], player);

            if (target == null)
                return;

            if (!MuteInfo.IsMuted(target))
            {
                player.Reply(lang.GetMessage("Not Muted", this, player.Id).Replace("{player}", target.Name));
                return;
            }

            mutes.Remove(target.Id);
            SaveData(mutes);

            server.Broadcast(lang.GetMessage("Unmuted", this).Replace("{player}", target.Name));
            Puts(lang.GetMessage("Unmuted", this).Replace("{player}", target.Name));
        }

        #endregion

        #region Helpers

        private object CheckMuted(IPlayer player)
        {
            if (MuteInfo.IsMuted(player))
            {
                if (mutes[player.Id].Expired)
                {
                    mutes.Remove(player.Id);
                    SaveData(mutes);
                    return null;
                }

                return false;
            }

            return null;
        }

        private IPlayer GetPlayer(string nameOrID, IPlayer player)
        {
            if (IsParseableTo<ulong>(nameOrID) && nameOrID.StartsWith("7656119") && nameOrID.Length == 17)
            {
                IPlayer result = players.All.ToList().Find((p) => p.Id == nameOrID);

                if (result == null)
                    player.Reply(lang.GetMessage("Player ID Not Found", this, player.Id).Replace("{id}", nameOrID));

                return result;
            }

            List<IPlayer> foundPlayers = new List<IPlayer>();

            foreach (IPlayer current in players.Connected)
            {
                if (current.Name.ToLower() == nameOrID.ToLower())
                    return current;

                if (current.Name.ToLower().Contains(nameOrID.ToLower()))
                    foundPlayers.Add(current);
            }

            switch (foundPlayers.Count)
            {
                case 0:
                    player.Reply(lang.GetMessage("Player Name Not Found", this, player.Id).Replace("{name}", nameOrID));
                    break;

                case 1:
                    return foundPlayers[0];

                default:
                    string[] names = (from current in foundPlayers select current.Name).ToArray();
                    player.Reply(lang.GetMessage("Multiple Players Found", this, player.Id).Replace("{matches}", string.Join(", ", names)));
                    break;
            }

            return null;
        }

        private bool IsParseableTo<T>(object s)
        {
            try
            {
                var parsed = (T)Convert.ChangeType(s, typeof(T));
                return true;
            }
            catch
            {
                return false;
            }
        }

        #region DateTime

        string FormatTime(TimeSpan time) => $"{(time.Days == 0 ? string.Empty : $"{time.Days} day(s)")}{(time.Days != 0 && time.Hours != 0 ? $", " : string.Empty)}{(time.Hours == 0 ? string.Empty : $"{time.Hours} hour(s)")}{(time.Hours != 0 && time.Minutes != 0 ? $", " : string.Empty)}{(time.Minutes == 0 ? string.Empty : $"{time.Minutes} minute(s)")}{(time.Minutes != 0 && time.Seconds != 0 ? $", " : string.Empty)}{(time.Seconds == 0 ? string.Empty : $"{time.Seconds} second(s)")}";

        private bool TryGetDateTime(string source, out DateTime date)
        {
            int seconds = 0, minutes = 0, hours = 0, days = 0;

            Match s = new Regex(@"(\d+?)s", RegexOptions.IgnoreCase).Match(source);
            Match m = new Regex(@"(\d+?)m", RegexOptions.IgnoreCase).Match(source);
            Match h = new Regex(@"(\d+?)h", RegexOptions.IgnoreCase).Match(source);
            Match d = new Regex(@"(\d+?)d", RegexOptions.IgnoreCase).Match(source);

            if (s.Success)
                seconds = Convert.ToInt32(s.Groups[1].ToString());

            if (m.Success)
                minutes = Convert.ToInt32(m.Groups[1].ToString());

            if (h.Success)
                hours = Convert.ToInt32(h.Groups[1].ToString());

            if (d.Success)
                days = Convert.ToInt32(d.Groups[1].ToString());

            source = source.Replace(seconds + "s", string.Empty);
            source = source.Replace(minutes + "m", string.Empty);
            source = source.Replace(hours + "h", string.Empty);
            source = source.Replace(days + "d", string.Empty);

            if (!string.IsNullOrEmpty(source) || (!s.Success && !m.Success && !h.Success && !d.Success))
            {
                date = default(DateTime);
                return false;
            }

            date = DateTime.Now + new TimeSpan(days, hours, minutes, seconds);

            return true;
        }

        #endregion

        #region Data & Config Helper

        private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
                return;

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        private string DataFileName => Title.Replace(" ", "");

        private void LoadData<T>(ref T data, string filename = null) => data = Core.Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? DataFileName);

        private void SaveData<T>(T data, string filename = null) => Core.Interface.Oxide.DataFileSystem.WriteObject(filename ?? DataFileName, data);

        #endregion

        #endregion
    }
}