﻿using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Linq;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    [Info("Better Chat Mute", "LaserHydra", "1.0.4", ResourceId = 2272)]
    [Description("Mute plugin, made for use with Better Chat")]
    internal class BetterChatMute : CovalencePlugin
    {
        private static Dictionary<string, MuteInfo> mutes = new Dictionary<string, MuteInfo>();
        private bool hasExpired = false;

        #region Classes

        public class MuteInfo
        {
            public DateTime ExpireDate = DateTime.MinValue;

            [JsonIgnore]
            public bool Timed => ExpireDate != DateTime.MinValue;

            [JsonIgnore]
            public bool Expired => Timed && ExpireDate < DateTime.UtcNow;

            public static bool IsMuted(IPlayer player) => mutes.ContainsKey(player.Id);

            public static readonly MuteInfo NonTimed = new MuteInfo(DateTime.MinValue);

            public MuteInfo()
            {
            }

            public MuteInfo(DateTime expireDate)
            {
                ExpireDate = expireDate;
            }
        }

        #endregion

        #region Hooks

        private void Loaded()
        {
            permission.RegisterPermission("betterchatmute.permanent", this);

            LoadData(ref mutes);
            SaveData(mutes);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "You don't have permission to use this command.",
                ["Muted"] = "{player} was muted by {initiator}.",
                ["Muted Time"] = "{player} was muted by {initiator} for {time}.",
                ["Unmuted"] = "{player} was unmuted by {initiator}.",
                ["Not Muted"] = "{player} is currently not muted.",
                ["Mute Expired"] = "{player} is no longer muted.",
                ["Invalid Time Format"] = "Invalid time format. Example: 1d2h3m4s = 1 day, 2 hours, 3 min, 4 sec",
                ["Nobody Muted"] = "There is nobody muted at the moment.",
                ["Invalid Syntax Mute"] = "/mute <player|steamid> [time: 1d1h1m1s]",
                ["Invalid Syntax Unmute"] = "/unmute <player|steamid>",
                ["Player Name Not Found"] = "Could not find player with name '{name}'",
                ["Player ID Not Found"] = "Could not find player with ID '{id}'",
                ["Multiple Players Found"] = "Multiple matching players found: \n{matches}",
                ["Time Muted Player Joined"] = "{player} is temporarily muted. Remaining time: {time}",
                ["Time Muted Player Chat"] = "You may not chat, you are temporarily muted. Remaining time: {time}",
                ["Muted Player Joined"] = "{player} is permanently muted.",
                ["Muted Player Chat"] = "You may not chat, you are permanently muted."
            }, this);

            timer.Repeat(10, 0, () =>
            {
                List<string> expired = mutes.Where(m => m.Value.Expired).Select(m => m.Key).ToList();

                foreach (string id in expired)
                {
                    mutes.Remove(id);
                    PublicMessage("Mute Expired", new KeyValuePair<string, string>("player", players.FindPlayerById(id)?.Name));

                    if (!hasExpired)
                        hasExpired = true;
                }

                if (hasExpired)
                {
                    SaveData(mutes);
                    hasExpired = false;
                }
            });
        }

        private object OnUserChat(IPlayer player, string message) => HandleChat(player);

        private object OnBetterChat(IPlayer player, string message)
        {
            object result = HandleChat(player);

            if (result is bool && !(bool)result)
            {
                if (mutes[player.Id].Timed)
                    player.Reply(lang.GetMessage("Time Muted Player Chat", this, player.Id).Replace("{time}", FormatTime(mutes[player.Id].ExpireDate - DateTime.UtcNow)));
                else
                    player.Reply(lang.GetMessage("Muted Player Chat", this, player.Id));
            }

            return result;
        }

        private void OnUserInit(IPlayer player)
        {
            UpdateMuteStatus(player);

            if (MuteInfo.IsMuted(player))
            {
                if (mutes[player.Id].Timed)
                    PublicMessage("Time Muted Player Joined",
                        new KeyValuePair<string, string>("player", player.Name), 
                        new KeyValuePair<string, string>("time", FormatTime(mutes[player.Id].ExpireDate - DateTime.UtcNow)));
                else
                    PublicMessage("Muted Player Joined", new KeyValuePair<string, string>("player", player.Name));
            }
        }

        #endregion

        #region Commands

        [Command("mutelist"), Permission("betterchatmute.use")]
        private void CmdMuteList(IPlayer player, string cmd, string[] args)
        {
            if (mutes.Count == 0)
                player.Reply(lang.GetMessage("Nobody Muted", this, player.Id));
            else
                player.Reply(string.Join(Environment.NewLine, mutes.Select(kvp => $"{players.FindPlayerById(kvp.Key).Name}: {FormatTime(kvp.Value.ExpireDate - DateTime.UtcNow)}").ToArray()));
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
                    if (!permission.UserHasPermission(player.Id, "betterchatmute.permanent"))
                    {
                        player.Reply(lang.GetMessage("No Permission", this, player.Id));
                        return;
                    }

                    target = GetPlayer(args[0], player);

                    if (target == null)
                        return;

                    mutes[target.Id] = MuteInfo.NonTimed;
                    SaveData(mutes);

                    Interface.CallHook("OnBetterChatMuted", target, player);

                    PublicMessage("Muted",
                        new KeyValuePair<string, string>("initiator", player.Name),
                        new KeyValuePair<string, string>("player", target.Name));

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

                    Interface.CallHook("OnBetterChatTimeMuted", target, player, expireDate);

                    PublicMessage("Muted Time",
                        new KeyValuePair<string, string>("initiator", player.Name),
                        new KeyValuePair<string, string>("player", target.Name),
                        new KeyValuePair<string, string>("time", FormatTime(expireDate - DateTime.UtcNow)));

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

            Interface.CallHook("OnBetterChatUnmuted", target, player);

            PublicMessage("Unmuted",
                new KeyValuePair<string, string>("initiator", player.Name),
                new KeyValuePair<string, string>("player", target.Name));
        }

        #endregion

        #region Helpers

        private void PublicMessage(string key, params KeyValuePair<string, string>[] replacements)
        {
            string message = lang.GetMessage(key, this);

            foreach (var replacement in replacements)
                message = message.Replace($"{{{replacement.Key}}}", replacement.Value);

            server.Broadcast(message);
            Puts(message);
        }

        private object HandleChat(IPlayer player)
        {
            UpdateMuteStatus(player);

            var result = Interface.CallHook("OnBetterChatMuteHandle", player, MuteInfo.IsMuted(player) ? JObject.FromObject(mutes[player.Id]) : null);

            if (result != null)
                return null;

            if (MuteInfo.IsMuted(player))
                return false;

            return null;
        }

        private void UpdateMuteStatus(IPlayer player)
        {
            if (MuteInfo.IsMuted(player) && mutes[player.Id].Expired)
            {
                mutes.Remove(player.Id);
                SaveData(mutes);
            }
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

            date = DateTime.UtcNow + new TimeSpan(days, hours, minutes, seconds);

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