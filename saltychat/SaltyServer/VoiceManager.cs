using System;
using System.Collections.Generic;
using System.Linq;
using SaltyShared;
using CitizenFX.Core;
using CitizenFX.Core.Native;

namespace SaltyServer
{
    public class VoiceManager : BaseScript
    {
        #region Properties / Fields
        public static bool Enabled { get; private set; }
        public static string ServerUniqueIdentifier { get; private set; }
        public static string RequiredUpdateBranch { get; private set; }
        public static string MinimumPluginVersion { get; private set; }
        public static string SoundPack { get; private set; }
        public static string IngameChannel { get; private set; }
        public static string IngameChannelPassword { get; private set; }

        public static Vector3[] RadioTowers { get; private set; } = new Vector3[0];

        public static VoiceClient[] VoiceClients => VoiceManager._voiceClients.Values.ToArray();
        private static Dictionary<Player, VoiceClient> _voiceClients = new Dictionary<Player, VoiceClient>();

        public static RadioChannel[] RadioChannels => VoiceManager._radioChannels.ToArray();
        private static List<RadioChannel> _radioChannels = new List<RadioChannel>();
        #endregion

        #region CTOR
        public VoiceManager()
        {
            // Phone Exports
            this.Exports.Add("EstablishCall", new Action<int, int>(this.EstablishCall));
            this.Exports.Add("EndCall", new Action<int, int>(this.EndCall));

            // Radio Exports
            this.Exports.Add("SetPlayerRadioSpeaker", new Action<int, bool>(this.SetPlayerRadioSpeaker));
            this.Exports.Add("SetPlayerRadioChannel", new Action<int, string>(this.SetPlayerRadioChannel));
            this.Exports.Add("RemovePlayerRadioChannel", new Action<int, string>(this.RemovePlayerRadioChannel));
            this.Exports.Add("SetRadioTowers", new Action<dynamic>(this.SetRadioTowers));
        }
        #endregion

        #region Server Events
        [EventHandler("onResourceStart")]
        private void OnResourceStart(string resourceName)
        {
            if (resourceName != API.GetCurrentResourceName())
                return;

            VoiceManager.Enabled = API.GetConvar("VoiceEnabled", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

            if (VoiceManager.Enabled)
            {
                VoiceManager.ServerUniqueIdentifier = API.GetConvar("ServerUniqueIdentifier", String.Empty);
                VoiceManager.RequiredUpdateBranch = API.GetConvar("RequiredUpdateBranch", String.Empty);
                VoiceManager.MinimumPluginVersion = API.GetConvar("MinimumPluginVersion", String.Empty);
                VoiceManager.SoundPack = API.GetConvar("SoundPack", String.Empty);
                VoiceManager.IngameChannel = API.GetConvar("IngameChannel", String.Empty);
                VoiceManager.IngameChannelPassword = API.GetConvar("IngameChannelPassword", String.Empty);
            }
        }

        [EventHandler("onResourceStop")]
        private void OnResourceStop(string resourceName)
        {
            if (resourceName != API.GetCurrentResourceName())
                return;

            VoiceManager.Enabled = false;

            lock (VoiceManager._voiceClients)
            {
                VoiceManager._voiceClients.Clear();
            }

            lock (VoiceManager._radioChannels)
            {
                foreach (RadioChannel radioChannel in VoiceManager._radioChannels)
                {
                    foreach (RadioChannelMember member in radioChannel.Members)
                    {
                        radioChannel.RemoveMember(member.VoiceClient);
                    }
                }

                VoiceManager._radioChannels.Clear();
            }
        }

        [EventHandler("playerDropped")]
        private void OnPlayerDisconnected([FromSource] Player player, string reason)
        {
            if (!VoiceManager._voiceClients.TryGetValue(player, out VoiceClient client))
                return;

            foreach (RadioChannel radioChannel in VoiceManager.RadioChannels.Where(c => c.IsMember(client)))
            {
                radioChannel.RemoveMember(client);
            }

            lock (VoiceManager._voiceClients)
            {
                VoiceManager._voiceClients.Remove(player);
            }

            BaseScript.TriggerClientEvent(Event.SaltyChat_RemoveClient, player.Handle);
        }
        #endregion

        #region Exports (Phone)
        private void EstablishCall(int callerNetId, int partnerNetId)
        {
            Player caller = this.Players[callerNetId];
            Player callPartner = this.Players[partnerNetId];

            caller.TriggerEvent(Event.SaltyChat_EstablishCall, callPartner.Handle);
            callPartner.TriggerEvent(Event.SaltyChat_EstablishCall, caller.Handle);
        }

        private void EndCall(int callerNetId, int partnerNetId)
        {
            Player caller = this.Players[callerNetId];
            Player callPartner = this.Players[partnerNetId];

            caller.TriggerEvent(Event.SaltyChat_EndCall, callPartner.Handle);
            callPartner.TriggerEvent(Event.SaltyChat_EndCall, caller.Handle);
        }
        #endregion

        #region Exports (Radio)
        private void SetPlayerRadioSpeaker(int netId, bool toggle)
        {
            Player player = this.Players[netId];

            VoiceManager.SetRadioSpeaker(player, toggle);
        }

        private void SetPlayerRadioChannel(int netId, string radioChannelName)
        {
            Player player = this.Players[netId];

            VoiceManager.JoinRadioChannel(player, radioChannelName);
        }

        private void RemovePlayerRadioChannel(int netId, string radioChannelName)
        {
            Player player = this.Players[netId];

            VoiceManager.LeaveRadioChannel(player, radioChannelName);
        }

        private void SetRadioTowers(dynamic towers)
        {
            List<Vector3> towerPositions = new List<Vector3>();

            foreach (dynamic tower in towers)
            {
                towerPositions.Add(new Vector3(tower[0], tower[1], tower[2]));
            }

            VoiceManager.RadioTowers = towerPositions.ToArray();

            BaseScript.TriggerClientEvent(Event.SaltyChat_UpdateRadioTowers, VoiceManager.RadioTowers);
        }
        #endregion

        #region Remote Events (Salty Chat)
        [EventHandler(Event.SaltyChat_Initialize)]
        private void OnInitialize([FromSource] Player player)
        {
            if (!VoiceManager.Enabled)
                return;

            VoiceClient voiceClient;

            lock (VoiceManager._voiceClients)
            {
                voiceClient = new VoiceClient(player, VoiceManager.GetTeamSpeakName(), SharedData.VoiceRanges[1]);
                VoiceManager._voiceClients.Add(player, voiceClient);
            }

            player.TriggerEvent(Event.SaltyChat_Initialize, voiceClient.TeamSpeakName, VoiceManager.ServerUniqueIdentifier, VoiceManager.SoundPack, VoiceManager.IngameChannel, VoiceManager.IngameChannelPassword);
        }

        [EventHandler(Event.SaltyChat_CheckVersion)]
        private void OnCheckVersion([FromSource] Player player, string updateChannel, string version)
        {
            if (!VoiceManager._voiceClients.TryGetValue(player, out VoiceClient client))
                return;

            if (!VoiceManager.IsVersionAccepted(updateChannel, version))
            {
                player.Drop($"[Salty Chat] Required Branch: {VoiceManager.RequiredUpdateBranch} | Required Version: {VoiceManager.MinimumPluginVersion}");
                return;
            }

            foreach (VoiceClient voiceClient in VoiceManager._voiceClients.Values.ToArray().Where(c => c.Player != player))
            {
                player.TriggerEvent(Event.SaltyChat_UpdateClient, voiceClient.Player.Handle, voiceClient.TeamSpeakName, voiceClient.VoiceRange);

                voiceClient.Player.TriggerEvent(Event.SaltyChat_UpdateClient, player.Handle, client.TeamSpeakName, client.VoiceRange);
            }

            player.TriggerEvent(Event.SaltyChat_UpdateRadioTowers, VoiceManager.RadioTowers);
        }

        [EventHandler(Event.SaltyChat_SetVoiceRange)]
        private void OnSetVoiceRange([FromSource] Player player, float voiceRange)
        {
            if (!VoiceManager._voiceClients.TryGetValue(player, out VoiceClient client))
                return;

            if (Array.IndexOf(SharedData.VoiceRanges, voiceRange) >= 0)
            {
                client.VoiceRange = voiceRange;

                BaseScript.TriggerClientEvent(Event.SaltyChat_UpdateClient, player.Handle, client.TeamSpeakName, client.VoiceRange);
            }
        }
        #endregion

        #region Commands (Radio)
#if DEBUG
        [Command("speaker")]
        private void OnSetRadioSpeaker(Player player, string[] args)
        {
            if (args.Length < 1)
            {
                player.SendChatMessage("Usage", "/speaker {true/false}");
                return;
            }

            bool toggle = String.Equals(args[0], "true", StringComparison.OrdinalIgnoreCase);

            VoiceManager.SetRadioSpeaker(player, toggle);

            player.SendChatMessage("Speaker", $"The speaker is now {(toggle ? "on" : "off")}.");
        }

        [Command("joinradio")]
        private void OnJoinRadioChannel(Player player, string[] args)
        {
            if (args.Length < 1)
            {
                player.SendChatMessage("Usage", "/joinradio {radioChannelName}");
                return;
            }

            VoiceManager.JoinRadioChannel(player, args[0]);

            player.SendChatMessage("Radio", $"You joined channel \"{args[0]}\".");
        }

        [Command("leaveradio")]
        private void OnLeaveRadioChannel(Player player, string[] args)
        {
            if (args.Length < 1)
            {
                player.SendChatMessage("Usage", "/leaveradio {radioChannelName}");
                return;
            }

            VoiceManager.LeaveRadioChannel(player, args[0]);

            player.SendChatMessage("Radio", $"You left channel \"{args[0]}\".");
        }
#endif
        #endregion

        #region Remote Events (Radio)
        [EventHandler(Event.SaltyChat_IsSending)]
        private void OnSendingOnRadio([FromSource] Player player, bool isSending)
        {
            if (!VoiceManager._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            foreach (RadioChannel radioChannel in VoiceManager.RadioChannels)
            {
                if (radioChannel.IsMember(voiceClient))
                {
                    radioChannel.Send(voiceClient, isSending);

                    return;
                }
            }
        }
        #endregion

        #region Methods
        public static string GetTeamSpeakName()
        {
            string name;

            do
            {
                name = Guid.NewGuid().ToString().Replace("-", "");

                if (name.Length > 30)
                {
                    name = name.Remove(29, name.Length - 30);
                }
            }
            while (VoiceManager._voiceClients.Values.Any(c => c.TeamSpeakName == name));

            return name;
        }

        public static RadioChannel GetRadioChannel(string name, bool create)
        {
            RadioChannel radioChannel;

            lock (VoiceManager._radioChannels)
            {
                radioChannel = VoiceManager.RadioChannels.FirstOrDefault(r => r.Name == name);

                if (radioChannel == null && create)
                {
                    radioChannel = new RadioChannel(name);

                    VoiceManager._radioChannels.Add(radioChannel);
                }
            }

            return radioChannel;
        }

        public static bool IsVersionAccepted(string branch, string version)
        {
            if (!String.IsNullOrWhiteSpace(VoiceManager.RequiredUpdateBranch) && VoiceManager.RequiredUpdateBranch != branch)
            {
                return false;
            }

            if (!String.IsNullOrWhiteSpace(VoiceManager.MinimumPluginVersion))
            {
                try
                {
                    string[] minimumVersionArray = VoiceManager.MinimumPluginVersion.Split('.');
                    string[] versionArray = version.Split('.');

                    int lengthCounter = 0;

                    if (versionArray.Length >= minimumVersionArray.Length)
                    {
                        lengthCounter = minimumVersionArray.Length;
                    }
                    else
                    {
                        lengthCounter = versionArray.Length;
                    }

                    for (int i = 0; i < lengthCounter; i++)
                    {
                        int min = Convert.ToInt32(minimumVersionArray[i]);
                        int cur = Convert.ToInt32(versionArray[i]);

                        if (cur > min)
                        {
                            return true;
                        }
                        else if (min > cur)
                        {
                            return false;
                        }
                    }
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }
        #endregion

        #region Methods (Radio)
        public static void SetRadioSpeaker(Player player, bool toggle)
        {
            if (!VoiceManager._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            voiceClient.RadioSpeaker = toggle;
        }

        public static void JoinRadioChannel(Player player, string radioChannelName)
        {
            if (!VoiceManager._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            foreach (RadioChannel channel in VoiceManager.RadioChannels)
            {
                if (channel.IsMember(voiceClient))
                    return;
            }

            RadioChannel radioChannel = VoiceManager.GetRadioChannel(radioChannelName, true);

            radioChannel.AddMember(voiceClient);
        }

        public static void LeaveRadioChannel(Player player, string radioChannelName)
        {
            if (!VoiceManager._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            RadioChannel radioChannel = VoiceManager.GetRadioChannel(radioChannelName, false);

            if (radioChannel != null)
            {
                radioChannel.RemoveMember(voiceClient);

                if (radioChannel.Members.Length == 0)
                {
                    VoiceManager._radioChannels.Remove(radioChannel);
                }
            }
        }

        public static void SendingOnRadio(Player player, bool isSending)
        {
            if (!VoiceManager._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            foreach (RadioChannel radioChannel in VoiceManager.RadioChannels)
            {
                if (radioChannel.IsMember(voiceClient))
                {
                    radioChannel.Send(voiceClient, isSending);

                    return;
                }
            }
        }
        #endregion
    }
}
