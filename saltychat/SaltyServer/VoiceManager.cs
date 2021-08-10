using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SaltyShared;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using Newtonsoft.Json;

namespace SaltyServer
{
    public class VoiceManager : BaseScript
    {
        #region Properties / Fields
        public static VoiceManager Instance { get; private set; }

        public float[][] RadioTowers { get; private set; } = new float[0][];

        public VoiceClient[] VoiceClients => this._voiceClients.Values.ToArray();
        private Dictionary<Player, VoiceClient> _voiceClients = new Dictionary<Player, VoiceClient>();

        public PhoneCall[] PhoneCalls => this._phoneCalls.ToArray();
        private List<PhoneCall> _phoneCalls = new List<PhoneCall>();

        public RadioChannel[] RadioChannels => this._radioChannels.ToArray();
        private List<RadioChannel> _radioChannels = new List<RadioChannel>();

        public Configuration Configuration { get; private set; }
        #endregion

        #region CTOR
        public VoiceManager()
        {
            VoiceManager.Instance = this;

            // General Exports
            this.Exports.Add("SetPlayerAlive", new Action<int, bool>(this.SetPlayerAlive));

            // Phone Exports
            this.Exports.Add("AddPlayerToCall", new Action<string, int>(this.AddPlayerToCall));
            this.Exports.Add("AddPlayersToCall", new Action<string, List<dynamic>>(this.AddPlayersToCall));
            this.Exports.Add("RemovePlayerFromCall", new Action<string, int>(this.RemovePlayerFromCall));
            this.Exports.Add("RemovePlayersFromCall", new Action<string, List<dynamic>>(this.RemovePlayersFromCall));
            this.Exports.Add("SetPhoneSpeaker", new Action<int, bool>(this.SetPlayerPhoneSpeaker));

            // Phone Exports (Obsolete)
            this.Exports.Add("EstablishCall", new Action<int, int>(this.EstablishCall));
            this.Exports.Add("EndCall", new Action<int, int>(this.EndCall));

            // Radio Exports
            this.Exports.Add("SetPlayerRadioSpeaker", new Action<int, bool>(this.SetPlayerRadioSpeaker));
            this.Exports.Add("SetPlayerRadioChannel", new Action<int, string, bool>(this.SetPlayerRadioChannel));
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

            this.Configuration = JsonConvert.DeserializeObject<Configuration>(API.LoadResourceFile(API.GetCurrentResourceName(), "config.json"));
        }

        [EventHandler("onResourceStop")]
        private void OnResourceStop(string resourceName)
        {
            if (resourceName != API.GetCurrentResourceName())
                return;

            this.Configuration.VoiceEnabled = false;

            // Clear all voice clients
            lock (this._voiceClients)
            {
                this._voiceClients.Clear();
            }

            // Clear all phone calls
            lock (this._phoneCalls)
            {
                this._phoneCalls.Clear();
            }

            // Clear all radio channels
            lock (this._radioChannels)
            {
                this._radioChannels.Clear();
            }
        }

        [EventHandler("playerDropped")]
        private void OnPlayerDisconnected([FromSource] Player player, string reason)
        {
            // Return if player wasn't a registered voice client
            if (!this._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            // Remove player from all phone calls
            foreach (PhoneCall phoneCall in this.PhoneCalls.Where(c => c.IsMember(voiceClient)))
            {
                this.LeavePhoneCall(voiceClient, phoneCall);
            }

            // Remove player from all radio channels
            this.LeaveRadioChannel(voiceClient);

            // Tell all players to remove the player
            BaseScript.TriggerClientEvent(Event.SaltyChat_RemoveClient, player.Handle);
        }
        #endregion

        #region RemoteEvents (Proximity)
        [EventHandler(Event.SaltyChat_SetVoiceRange)]
        private void OnSetVoiceRange([FromSource] Player player, float voiceRange)
        {
            if (!this._voiceClients.TryGetValue(player, out VoiceClient client))
                return;

            if (Array.IndexOf(this.Configuration.VoiceRanges, voiceRange) >= 0)
            {
                client.VoiceRange = voiceRange;
            }
        }
        #endregion

        #region Exports (General)
        private void SetPlayerAlive(int netId, bool isAlive)
        {
            Player player = this.Players[netId];

            lock (this._voiceClients)
            {
                if (this._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                {
                    voiceClient.IsAlive = isAlive;
                }
            }
        }
        #endregion

        #region Exports (Phone)
        private void AddPlayerToCall(string identifier, int playerHandle) => this.AddPlayersToCall(identifier, new List<dynamic>() { playerHandle });

        private void AddPlayersToCall(string identifier, List<dynamic> players)
        {
            PhoneCall phoneCall = this.GetPhoneCall(identifier, true);

            foreach (int playerHandle in players.Cast<int>())
            {
                VoiceClient voiceClient = this.VoiceClients.FirstOrDefault(c => c.Player.GetServerId() == playerHandle);

                if (voiceClient == null)
                    continue;

                this.JoinPhoneCall(voiceClient, phoneCall);
            }
        }

        private void RemovePlayerFromCall(string identifier, int playerHandle) => this.RemovePlayersFromCall(identifier, new List<dynamic>() { playerHandle });

        private void RemovePlayersFromCall(string identifier, List<dynamic> players)
        {
            PhoneCall phoneCall = this.GetPhoneCall(identifier, false);

            if (phoneCall == null)
                return;

            foreach (int playerHandle in players.Cast<int>())
            {
                VoiceClient voiceClient = this.VoiceClients.FirstOrDefault(c => c.Player.GetServerId() == playerHandle);

                if (voiceClient == null)
                    continue;

                this.LeavePhoneCall(voiceClient, phoneCall);
            }
        }

        private void SetPlayerPhoneSpeaker(int playerHandle, bool isEnabled)
        {
            Player player = this.Players[playerHandle];

            if (!this._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            voiceClient.SetPhoneSpeakerEnabled(isEnabled);
        }

        [Obsolete]
        private void EstablishCall(int callerNetId, int partnerNetId)
        {
            VoiceClient caller = this.VoiceClients.FirstOrDefault(c => c.Player.GetServerId() == callerNetId);
            VoiceClient callPartner = this.VoiceClients.FirstOrDefault(c => c.Player.GetServerId() == partnerNetId);

            if (caller == null || callPartner == null)
                return;

            caller.TriggerEvent(Event.SaltyChat_EstablishCall, partnerNetId, callPartner.TeamSpeakName, callPartner.Player.GetPosition());
            callPartner.TriggerEvent(Event.SaltyChat_EstablishCall, callerNetId, caller.TeamSpeakName, caller.Player.GetPosition());
        }

        [Obsolete]
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

            if (!this._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            voiceClient.IsRadioSpeakerEnabled = toggle;
        }

        private void SetPlayerRadioChannel(int netId, string radioChannelName, bool isPrimary)
        {
            Player player = this.Players[netId];

            if (!this._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            this.JoinRadioChannel(voiceClient, radioChannelName, isPrimary);
        }

        private void RemovePlayerRadioChannel(int netId, string radioChannelName)
        {
            Player player = this.Players[netId];

            if (!this._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            this.LeaveRadioChannel(voiceClient, radioChannelName);
        }

        private void SetRadioTowers(dynamic towers)
        {
            List<float[]> radioTowers = new List<float[]>();

            foreach (dynamic tower in towers)
            {
                if (tower.GetType() == typeof(Vector3))
                    radioTowers.Add(new float[] { tower.X, tower.Y, tower.Z });
                else if (tower.Count == 3)
                    radioTowers.Add(new float[] { (float)tower[0], (float)tower[1], (float)tower[2] });
                else if (tower.Count == 4)
                    radioTowers.Add(new float[] { (float)tower[0], (float)tower[1], (float)tower[2], (float)tower[3] });
            }

            this.RadioTowers = radioTowers.ToArray();

            BaseScript.TriggerClientEvent(Event.SaltyChat_UpdateRadioTowers, this.RadioTowers);
        }
        #endregion

        #region Remote Events (Salty Chat)
        [EventHandler(Event.SaltyChat_Initialize)]
        private void OnInitialize([FromSource] Player player)
        {
            if (!this.Configuration.VoiceEnabled)
                return;

            VoiceClient voiceClient;

            lock (this._voiceClients)
            {
                voiceClient = new VoiceClient(player, this.GetTeamSpeakName(player), this.Configuration.VoiceRanges[1], true);

                if (this._voiceClients.ContainsKey(player))
                    this._voiceClients[player] = voiceClient;
                else
                    this._voiceClients.Add(player, voiceClient);
            }

            player.TriggerEvent(Event.SaltyChat_Initialize, voiceClient.TeamSpeakName, voiceClient.VoiceRange, this.RadioTowers);
        }

        [EventHandler(Event.SaltyChat_CheckVersion)]
        private void OnCheckVersion([FromSource] Player player, string version)
        {
            if (!this._voiceClients.TryGetValue(player, out _))
                return;

            if (!this.IsVersionAccepted(version))
            {
                player.Drop($"[Salty Chat] You need to have version {this.Configuration.MinimumPluginVersion} or later.");
                return;
            }
        }
        #endregion

        #region Commands (Phone)
#if DEBUG
        [Command("joincall")]
        private void OnJoinPhoneCall(Player player, string[] args)
        {
            if (!this._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
            {
                return;
            }
            else if (args.Length < 1)
            {
                player.SendChatMessage("Usage", "/joincall {identifier}");
                return;
            }

            string identifier = args[0];

            this.JoinPhoneCall(voiceClient, identifier);

            player.SendChatMessage("PhoneCall", $"Joined call {identifier}");
        }

        [Command("leavecall")]
        private void OnLeavePhoneCall(Player player, string[] args)
        {
            if (!this._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
            {
                return;
            }
            else if (args.Length < 1)
            {
                player.SendChatMessage("Usage", "/leavecall {identifier}");
                return;
            }

            string identifier = args[0];

            this.LeavePhoneCall(voiceClient, identifier);

            player.SendChatMessage("PhoneCall", $"Left call {identifier}");
        }

        [Command("setphonespeaker")]
        private void OnSetPhoneSpeaker(Player player, string[] args)
        {
            if (args.Length < 1)
            {
                player.SendChatMessage("Usage", "/setphonespeaker {true/false}");
                return;
            }

            if (!this._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            bool toggle = String.Equals(args[0], "true", StringComparison.OrdinalIgnoreCase);

            voiceClient.SetPhoneSpeakerEnabled(toggle);

            player.SendChatMessage("PhoneSpeaker", $"The speaker is now {(toggle ? "on" : "off")}.");
        }
#endif
        #endregion

        #region Commands (Radio)
#if DEBUG
        [Command("setradiospeaker")]
        private void OnSetRadioSpeaker(Player player, string[] args)
        {
            if (args.Length < 1)
            {
                player.SendChatMessage("Usage", "/radiospeaker {true/false}");
                return;
            }

            if (!this._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            bool toggle = String.Equals(args[0], "true", StringComparison.OrdinalIgnoreCase);

            voiceClient.IsRadioSpeakerEnabled = toggle;

            player.SendChatMessage("RadioSpeaker", $"The speaker is now {(toggle ? "on" : "off")}.");
        }

        [Command("joinradio")]
        private void OnJoinRadioChannel(Player player, string[] args)
        {
            if (args.Length < 1)
            {
                player.SendChatMessage("Usage", "/joinradio {radioChannelName}");
                return;
            }

            if (!this._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            this.JoinRadioChannel(voiceClient, args[0], true);

            player.SendChatMessage("Radio", $"You joined channel \"{args[0]}\".");
        }

        [Command("joinsecradio")]
        private void OnJoinSecondaryRadioChannel(Player player, string[] args)
        {
            if (args.Length < 1)
            {
                player.SendChatMessage("Usage", "/joinsecradio {radioChannelName}");
                return;
            }

            if (!this._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            this.JoinRadioChannel(voiceClient, args[0], false);

            player.SendChatMessage("Radio", $"You joined secondary channel \"{args[0]}\".");
        }

        [Command("leaveradio")]
        private void OnLeaveRadioChannel(Player player, string[] args)
        {
            if (args.Length < 1)
            {
                player.SendChatMessage("Usage", "/leaveradio {radioChannelName}");
                return;
            }

            if (!this._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            this.LeaveRadioChannel(voiceClient, args[0]);

            player.SendChatMessage("Radio", $"You left channel \"{args[0]}\".");
        }
#endif
        #endregion

        #region Remote Events (Radio)
        [EventHandler(Event.SaltyChat_IsSending)]
        private void OnSendingOnRadio([FromSource] Player player, string radioChannelName, bool isSending)
        {
            if (!this._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            RadioChannel radioChannel = this.GetRadioChannel(radioChannelName, false);

            if (radioChannel == null || !radioChannel.IsMember(voiceClient))
                return;

            radioChannel.Send(voiceClient, isSending);
        }

        [EventHandler(Event.SaltyChat_SetRadioChannel)]
        private void OnJoinRadioChannel([FromSource] Player player, string radioChannelName, bool isPrimary)
        {
            if (!this._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            this.LeaveRadioChannel(voiceClient, isPrimary);

            if (!String.IsNullOrEmpty(radioChannelName))
            {
                this.JoinRadioChannel(voiceClient, radioChannelName, isPrimary);
            }
        }

        [EventHandler(Event.SaltyChat_SetRadioSpeaker)]
        private void OnSetRadioSpeaker([FromSource] Player player, bool isRadioSpeakerEnabled)
        {
            if (!this._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            voiceClient.IsRadioSpeakerEnabled = isRadioSpeakerEnabled;
        }
        #endregion

        #region Remote Events(Megaphoone)
        [EventHandler(Event.SaltyChat_IsUsingMegaphone)]
        private void OnIsUsingMegaphone([FromSource] Player player, bool isSending)
        {
            if (!this._voiceClients.TryGetValue(player, out VoiceClient voiceClient))
                return;

            Vector3 position = voiceClient.Player.GetPosition();

            foreach (VoiceClient remoteClient in this.VoiceClients)
            {
                remoteClient.TriggerEvent(Event.SaltyChat_IsUsingMegaphone, voiceClient.Player.Handle, voiceClient.TeamSpeakName, this.Configuration.MegaphoneRange, isSending, position);
            }
        }
        #endregion

        #region Methods (Phone)
        public PhoneCall GetPhoneCall(string identifier, bool create)
        {
            PhoneCall phoneCall;

            lock (this._phoneCalls)
            {
                phoneCall = this.PhoneCalls.FirstOrDefault(r => r.Identifier == identifier);

                if (phoneCall == null && create)
                {
                    phoneCall = new PhoneCall(identifier);

                    this._phoneCalls.Add(phoneCall);
                }
            }

            return phoneCall;
        }

        public void JoinPhoneCall(VoiceClient voiceClient, string identifier)
        {
            PhoneCall phoneCall = this.GetPhoneCall(identifier, true);

            this.JoinPhoneCall(voiceClient, phoneCall);
        }

        public void JoinPhoneCall(VoiceClient voiceClient, PhoneCall phoneCall)
        {
            phoneCall.AddMember(voiceClient);
        }

        public void LeavePhoneCall(VoiceClient voiceClient, string identifier)
        {
            PhoneCall phoneCall = this.GetPhoneCall(identifier, false);

            if (phoneCall != null)
                this.LeavePhoneCall(voiceClient, phoneCall);
        }

        public void LeavePhoneCall(VoiceClient voiceClient, PhoneCall phoneCall)
        {
            phoneCall.RemoveMember(voiceClient);

            if (phoneCall.Members.Length == 0)
            {
                lock (this._phoneCalls)
                {
                    this._phoneCalls.Remove(phoneCall);
                }
            }
        }

        public IEnumerable<PhoneCallMember> GetPlayerPhoneCallMembership(VoiceClient voiceClient)
        {
            foreach (PhoneCall phoneCall in this.PhoneCalls)
            {
                PhoneCallMember membership = phoneCall.Members.FirstOrDefault(m => m.VoiceClient == voiceClient);

                if (membership != null)
                {
                    yield return membership;
                }
            }
        }
        #endregion

        #region Methods (Radio)
        public RadioChannel GetRadioChannel(string name, bool create)
        {
            RadioChannel radioChannel;

            lock (this._radioChannels)
            {
                radioChannel = this.RadioChannels.FirstOrDefault(r => r.Name == name);

                if (radioChannel == null && create)
                {
                    radioChannel = new RadioChannel(name);

                    this._radioChannels.Add(radioChannel);
                }
            }

            return radioChannel;
        }

        public IEnumerable<RadioChannelMember> GetPlayerRadioChannelMembership(VoiceClient voiceClient)
        {
            foreach (RadioChannel radioChannel in this.RadioChannels)
            {
                RadioChannelMember membership = radioChannel.Members.FirstOrDefault(m => m.VoiceClient == voiceClient);

                if (membership != null)
                {
                    yield return membership;
                }
            }
        }

        public void JoinRadioChannel(VoiceClient voiceClient, string radioChannelName, bool isPrimary)
        {
            foreach (RadioChannel channel in this.RadioChannels)
            {
                if (channel.Members.Any(v => v.VoiceClient == voiceClient && v.IsPrimary == isPrimary))
                    return;
            }

            RadioChannel radioChannel = this.GetRadioChannel(radioChannelName, true);

            radioChannel.AddMember(voiceClient, isPrimary);
        }

        public void LeaveRadioChannel(VoiceClient voiceClient)
        {
            foreach (RadioChannelMember membership in this.GetPlayerRadioChannelMembership(voiceClient))
            {
                this.LeaveRadioChannel(voiceClient, membership.RadioChannel);
            }
        }

        public void LeaveRadioChannel(VoiceClient voiceClient, string radioChannelName)
        {
            foreach (RadioChannelMember membership in this.GetPlayerRadioChannelMembership(voiceClient).Where(m => m.RadioChannel.Name == radioChannelName))
            {
                this.LeaveRadioChannel(voiceClient, membership.RadioChannel);
            }
        }

        public void LeaveRadioChannel(VoiceClient voiceClient, bool primary)
        {
            foreach (RadioChannelMember membership in this.GetPlayerRadioChannelMembership(voiceClient).Where(m => m.IsPrimary == primary))
            {
                this.LeaveRadioChannel(voiceClient, membership.RadioChannel);
            }
        }

        public void LeaveRadioChannel(VoiceClient voiceClient, RadioChannel radioChannel)
        {
            radioChannel.RemoveMember(voiceClient);

            if (radioChannel.Members.Length == 0)
            {
                lock (this._radioChannels)
                {
                    this._radioChannels.Remove(radioChannel);
                }
            }
        }
        #endregion

        #region Methods (Misc)
        public string GetTeamSpeakName(Player player)
        {
            string name = this.Configuration.NamePattern;

            do
            {
                name = Regex.Replace(name, @"(\{serverid\})", player.Handle);
                name = Regex.Replace(name, @"(\{guid\})", Guid.NewGuid().ToString().Replace("-", ""));

                if (name.Length > 30)
                    name = name.Remove(29, name.Length - 30);
            }
            while (this._voiceClients.Values.Any(c => c.TeamSpeakName == name));

            return name;
        }

        public bool IsVersionAccepted(string version)
        {
            if (!String.IsNullOrWhiteSpace(this.Configuration.MinimumPluginVersion))
            {
                try
                {
                    string[] minimumVersionArray = this.Configuration.MinimumPluginVersion.Split('.');
                    string[] versionArray = version.Split('.');

                    int lengthCounter = 0;

                    if (versionArray.Length >= minimumVersionArray.Length)
                        lengthCounter = minimumVersionArray.Length;
                    else
                        lengthCounter = versionArray.Length;

                    for (int i = 0; i < lengthCounter; i++)
                    {
                        int min = Convert.ToInt32(minimumVersionArray[i]);
                        int cur = 0;

                        // regex match so we can have versions like 2.2.6p1
                        Match match = Regex.Match(versionArray[i], "^(\\d+)");

                        if (match.Success)
                            cur = Convert.ToInt32(match.Value);

                        if (cur > min)
                            return true;
                        else if (min > cur)
                            return false;
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
    }
}
