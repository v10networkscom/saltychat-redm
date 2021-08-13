using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using SaltyShared;
using Newtonsoft.Json;
using RedM.External;

namespace SaltyClient
{
    internal class VoiceManager : BaseScript
    {
        #region Properties / Fields
        public bool IsEnabled { get; private set; }
        public bool IsConnected { get; private set; }
        private GameInstanceState _pluginState = GameInstanceState.NotInitiated;
        public GameInstanceState PlguinState
        {
            get => this._pluginState;
            set
            {
                this._pluginState = value;

                BaseScript.TriggerEvent(Event.SaltyChat_PluginStateChanged, (int)value);
            }
        }
        public bool IsNuiReady { get; private set; }

        public string TeamSpeakName { get; private set; }
        public bool IsAlive => Game.Player.State[State.SaltyChat_IsAlive] == true;
        public Configuration Configuration { get; private set; }

        public VoiceClient[] VoiceClients => this._voiceClients.Values.ToArray();
        private Dictionary<int, VoiceClient> _voiceClients = new Dictionary<int, VoiceClient>();

        public Tower[] RadioTowers { get; private set; }

        public string WebSocketAddress { get; private set; } = "lh.v10.network:38088";

        private float _voiceRange = 0f;
        public float VoiceRange
        {
            get => this._voiceRange;
            private set
            {
                this._voiceRange = value;

                BaseScript.TriggerEvent(Event.SaltyChat_VoiceRangeChanged, value, Array.IndexOf(this.Configuration.VoiceRanges, value), this.Configuration.VoiceRanges.Length);
            }
        }

        public bool _canSendRadioTraffic = true;
        public bool CanSendRadioTraffic
        {
            get => this._canSendRadioTraffic;
            private set
            {
                if (this._canSendRadioTraffic == value || !this.Configuration.EnableRadioHardcoreMode)
                    return;

                this._canSendRadioTraffic = value;

                if (!value)
                {
                    foreach (RadioTraffic radioTraffic in this.RadioTrafficStates.ToArray())
                    {
                        if (radioTraffic.Name == this.TeamSpeakName)
                        {
                            if (radioTraffic.RadioChannelName == this.PrimaryRadioChannel)
                                this.OnPrimaryRadioReleased();
                            else if (radioTraffic.RadioChannelName == this.SecondaryRadioChannel)
                                this.OnSecondaryRadioReleased();
                        }
                    }
                }
            }
        }
        public bool _canReceiveRadioTraffic = true;
        public bool CanReceiveRadioTraffic
        {
            get => this._canReceiveRadioTraffic;
            private set
            {
                if (this._canReceiveRadioTraffic == value || !this.Configuration.EnableRadioHardcoreMode)
                    return;

                this._canReceiveRadioTraffic = value;

                if (value)
                {
                    foreach (RadioTraffic radioTraffic in this.RadioTrafficStates.ToArray().Where(r => r.Name != this.TeamSpeakName))
                    {
                        this.ExecuteCommand(
                            new PluginCommand(
                                Command.RadioCommunicationUpdate,
                                this.Configuration.ServerUniqueIdentifier,
                                new RadioCommunication(
                                    radioTraffic.Name,
                                    radioTraffic.SenderRadioType,
                                    radioTraffic.ReceiverRadioType,
                                    false,
                                    radioTraffic.RadioChannelName == this.PrimaryRadioChannel || radioTraffic.RadioChannelName == this.SecondaryRadioChannel,
                                    radioTraffic.RadioChannelName == this.SecondaryRadioChannel,
                                    radioTraffic.Relays,
                                    this.RadioVolume
                                )
                            )
                        );
                    }
                }
                else
                {
                    foreach (RadioTraffic radioTraffic in this.RadioTrafficStates.ToArray().Where(r => r.Name != this.TeamSpeakName))
                    {
                        this.ExecuteCommand(
                            new PluginCommand(
                                Command.StopRadioCommunication,
                                this.Configuration.ServerUniqueIdentifier,
                                new RadioCommunication(
                                    radioTraffic.Name,
                                    RadioType.None,
                                    RadioType.None,
                                    false,
                                    radioTraffic.RadioChannelName == this.PrimaryRadioChannel || radioTraffic.RadioChannelName == this.SecondaryRadioChannel,
                                    radioTraffic.RadioChannelName == this.SecondaryRadioChannel
                                )
                            )
                        );
                    }
                }
            }
        }
        public string PrimaryRadioChannel { get; private set; }
        public string SecondaryRadioChannel { get; private set; }
        public List<RadioTraffic> RadioTrafficStates { get; private set; } = new List<RadioTraffic>();
        private bool IsUsingMegaphone { get; set; }

        public bool IsMicrophoneMuted { get; private set; }
        public bool IsMicrophoneEnabled { get; private set; }
        public bool IsSoundMuted { get; private set; }
        public bool IsSoundEnabled { get; private set; }

        public float RadioVolume { get; private set; } = 1.0f;
        public bool IsRadioSpeakerEnabled { get; set; }

        public static PlayerList PlayerList { get; private set; }
        #endregion

        #region Delegates
        public delegate float GetVoiceRangeDelegate();
        public delegate string GetRadioChannelDelegate(bool primary);
        public delegate float GetRadioVolumeDelegate();
        public delegate bool GetRadioSpeakerDelegate();
        public delegate int GetPluginStateDelegate();
        #endregion

        #region CTOR
        public VoiceManager()
        {
            // NUI Callbacks
            API.RegisterNuiCallbackType(NuiEvent.SaltyChat_OnConnected);
            API.RegisterNuiCallbackType(NuiEvent.SaltyChat_OnDisconnected);
            API.RegisterNuiCallbackType(NuiEvent.SaltyChat_OnError);
            API.RegisterNuiCallbackType(NuiEvent.SaltyChat_OnMessage);
            API.RegisterNuiCallbackType(NuiEvent.SaltyChat_OnNuiReady);

            // Proximity Getter Exports
            GetVoiceRangeDelegate getVoiceRangeDelegate = new GetVoiceRangeDelegate(this.GetVoiceRange);
            this.Exports.Add("GetVoiceRange", getVoiceRangeDelegate);

            // Radio Getter Exports
            GetRadioChannelDelegate getRadioChannelDelegate = new GetRadioChannelDelegate(this.GetRadioChannel);
            this.Exports.Add("GetRadioChannel", getRadioChannelDelegate);

            GetRadioVolumeDelegate getRadioVolumeDelegate = new GetRadioVolumeDelegate(this.GetRadioVolume);
            this.Exports.Add("GetRadioVolume", getRadioVolumeDelegate);

            GetRadioSpeakerDelegate getRadioSpeakerDelegate = new GetRadioSpeakerDelegate(this.GetRadioSpeaker);
            this.Exports.Add("GetRadioSpeaker", getRadioSpeakerDelegate);

            // Radio Setter Exports
            this.Exports.Add("SetRadioChannel", new Action<string, bool>(this.SetRadioChannel));
            this.Exports.Add("SetRadioVolume", new Action<float>(this.SetRadioVolume));
            this.Exports.Add("SetRadioSpeaker", new Action<bool>(this.SetRadioSpeaker));

            // Misc Exports
            GetPluginStateDelegate getPluginStateDelegate = new GetPluginStateDelegate(this.GetPluginState);
            this.Exports.Add("GetPluginState", getPluginStateDelegate);

            this.Exports.Add("PlaySound", new Action<string, bool, string>(this.PlaySound));

            VoiceManager.PlayerList = new PlayerList();
        }
        #endregion

        #region Events
        [EventHandler("onClientResourceStop")]
        private void OnResourceStop(string resourceName)
        {
            if (resourceName != API.GetCurrentResourceName())
                return;

            this.IsEnabled = false;
            this.IsConnected = false;

            lock (this._voiceClients)
            {
                this._voiceClients.Clear();
            }

            this.PrimaryRadioChannel = null;
            this.SecondaryRadioChannel = null;
        }
        #endregion

        #region Remote Events (Handling)
        [EventHandler(Event.SaltyChat_Initialize)]
        private void OnInitialize(string teamSpeakName, float voiceRange, dynamic towers)
        {
            this.TeamSpeakName = teamSpeakName;
            this.VoiceRange = voiceRange;

            this.OnUpdateRadioTowers(towers);

            this.IsEnabled = true;

            if (this.IsConnected)
                this.InitializePlugin();
            else if (this.IsNuiReady)
                this.ExecuteCommand("connect", this.WebSocketAddress);
            else
                Debug.WriteLine("[Salty Chat] Got server response, but NUI wasn't ready");

            //VoiceManager.DisplayDebug(true);
        }

        [EventHandler(Event.SaltyChat_RemoveClient)]
        private void OnClientRemove(string handle)
        {
            if (!Int32.TryParse(handle, out int serverId))
                return;

            lock (this._voiceClients)
            {
                if (this._voiceClients.TryGetValue(serverId, out VoiceClient client))
                {
                    this.ExecuteCommand(new PluginCommand(Command.RemovePlayer, this.Configuration.ServerUniqueIdentifier, new PlayerState(client.TeamSpeakName)));

                    this._voiceClients.Remove(serverId);
                }
            }
        }
        #endregion

        #region Remote Events (Phone)
        [EventHandler(Event.SaltyChat_EstablishCall)]
        private void OnEstablishCall(string handle, string teamSpeakName, dynamic position)
        {
            this.OnEstablishCallRelayed(handle, teamSpeakName, position, true, new List<dynamic>());
        }

        [EventHandler(Event.SaltyChat_EstablishCallRelayed)]
        private void OnEstablishCallRelayed(string handle, string teamSpeakName, dynamic position, bool direct, List<dynamic> relays)
        {
            if (!Int32.TryParse(handle, out int serverId))
                return;

            if (this.GetOrCreateVoiceClient(serverId, teamSpeakName, out VoiceClient client))
            {
                if (client.DistanceCulled)
                {
                    client.LastPosition = new CitizenFX.Core.Vector3(position[0], position[1], position[2]);
                    client.SendPlayerStateUpdate(this);
                }

                CitizenFX.Core.Vector3 playerPosition = Game.PlayerPed.Position;
                CitizenFX.Core.Vector3 remotePlayerPosition = client.LastPosition;

                //int signalDistortion = API.GetZoneScumminess(API.GetZoneAtCoords(playerPosition.X, playerPosition.Y, playerPosition.Z));
                //signalDistortion += API.GetZoneScumminess(API.GetZoneAtCoords(remotePlayerPosition.X, remotePlayerPosition.Y, remotePlayerPosition.Z));

                int signalDistortion = 0;

                this.ExecuteCommand(
                    new PluginCommand(
                        Command.PhoneCommunicationUpdate,
                        this.Configuration.ServerUniqueIdentifier,
                        new PhoneCommunication(
                            client.TeamSpeakName,
                            signalDistortion,
                            direct,
                            relays.Cast<string>().ToArray()
                        )
                    )
                );
            }
        }

        [EventHandler(Event.SaltyChat_EndCall)]
        private void OnEndCall(string handle)
        {
            if (!Int32.TryParse(handle, out int serverId))
                return;

            if (this._voiceClients.TryGetValue(serverId, out VoiceClient client))
            {
                this.ExecuteCommand(
                    new PluginCommand(
                        Command.StopPhoneCommunication,
                        this.Configuration.ServerUniqueIdentifier,
                        new PhoneCommunication(
                            client.TeamSpeakName
                        )
                    )
                );
            }
        }
        #endregion

        #region Remote Events (Radio)
        [EventHandler(Event.SaltyChat_SetRadioSpeaker)]
        private void OnSetRadioSpeaker(bool isRadioSpeakerEnabled)
        {
            this.IsRadioSpeakerEnabled = isRadioSpeakerEnabled;
        }

        [EventHandler(Event.SaltyChat_ChannelInUse)]
        private void OnChannelBlocked(string channelName)
        {
            this.PlaySound("offMicClick", false, "radio"); // offMicClick is just a placeholder, need to add a new sound to the default sound pack

            if (channelName == this.PrimaryRadioChannel)
                this.OnPrimaryRadioReleased();
            else if (channelName == this.SecondaryRadioChannel)
                this.OnSecondaryRadioReleased();
        }

        [EventHandler(Event.SaltyChat_SetRadioChannel)]
        private void OnSetRadioChannel(string radioChannel, bool isPrimary)
        {
            if (isPrimary)
            {
                this.PrimaryRadioChannel = radioChannel;

                if (String.IsNullOrEmpty(radioChannel))
                    this.PlaySound("leaveRadioChannel", false, "radio");
                else
                    this.PlaySound("enterRadioChannel", false, "radio");
            }
            else
            {
                this.SecondaryRadioChannel = radioChannel;

                if (String.IsNullOrEmpty(radioChannel))
                    this.PlaySound("leaveRadioChannel", false, "radio");
                else
                    this.PlaySound("enterRadioChannel", false, "radio");
            }
        }

        [EventHandler(Event.SaltyChat_IsSending)]
        private void OnPlayerIsSending(string handle, string teamSpeakName, string radioChannel, bool isSending, bool stateChange, dynamic position)
        {
            this.OnPlayerIsSendingRelayed(handle, teamSpeakName, radioChannel, isSending, stateChange, position, true, new List<dynamic>());
        }

        [EventHandler(Event.SaltyChat_IsSendingRelayed)]
        private void OnPlayerIsSendingRelayed(string handle, string teamSpeakName, string radioChannel, bool isSending, bool stateChange, dynamic position, bool direct, List<dynamic> relays)
        {
            if (!Int32.TryParse(handle, out int serverId))
                return;

            lock (this.RadioTrafficStates)
            {
                RadioTraffic radioTraffic = this.RadioTrafficStates.FirstOrDefault(r => r.Name == teamSpeakName && r.RadioChannelName == radioChannel);

                if (isSending && radioTraffic == null)
                    this.RadioTrafficStates.Add(new RadioTraffic(teamSpeakName, isSending, radioChannel, (RadioType)this.Configuration.RadioType, (RadioType)this.Configuration.RadioType, relays.Select(r => (string)r).ToArray()));
                else if (radioTraffic != null)
                    this.RadioTrafficStates.Remove(radioTraffic);
            }

            if (serverId == Game.Player.ServerId)
            {
                if (isSending)
                {
                    this.ExecuteCommand(
                        new PluginCommand(
                            Command.RadioCommunicationUpdate,
                            this.Configuration.ServerUniqueIdentifier,
                            new RadioCommunication(
                                this.TeamSpeakName,
                                (RadioType)this.Configuration.RadioType,
                                (RadioType)this.Configuration.RadioType,
                                stateChange,
                                direct,
                                this.SecondaryRadioChannel == radioChannel,
                                relays.Select(r => (string)r).ToArray(),
                                this.RadioVolume
                            )
                        )
                    );
                }
                else
                {
                    this.ExecuteCommand(
                        new PluginCommand(
                            Command.StopRadioCommunication,
                            this.Configuration.ServerUniqueIdentifier,
                            new RadioCommunication(
                                this.TeamSpeakName,
                                RadioType.None,
                                RadioType.None,
                                stateChange,
                                direct,
                                this.SecondaryRadioChannel == radioChannel
                            )
                        )
                    );
                }
            }
            else if (this.GetOrCreateVoiceClient(serverId, teamSpeakName, out VoiceClient client))
            {
                if (client.DistanceCulled)
                {
                    client.LastPosition = new CitizenFX.Core.Vector3(position[0], position[1], position[2]);
                    client.SendPlayerStateUpdate(this);
                }

                if (isSending && this.CanReceiveRadioTraffic)
                {
                    this.ExecuteCommand(
                        new PluginCommand(
                            Command.RadioCommunicationUpdate,
                            this.Configuration.ServerUniqueIdentifier,
                            new RadioCommunication(
                                client.TeamSpeakName,
                                (RadioType)this.Configuration.RadioType,
                                (RadioType)this.Configuration.RadioType,
                                stateChange,
                                direct,
                                this.SecondaryRadioChannel == radioChannel,
                                relays.Select(r => (string)r).ToArray(),
                                this.RadioVolume
                            )
                        )
                    );
                }
                else if (!isSending)
                {
                    this.ExecuteCommand(
                        new PluginCommand(
                            Command.StopRadioCommunication,
                            this.Configuration.ServerUniqueIdentifier,
                            new RadioCommunication(
                                client.TeamSpeakName,
                                RadioType.None,
                                RadioType.None,
                                stateChange,
                                direct,
                                this.SecondaryRadioChannel == radioChannel
                            )
                        )
                    );
                }
            }
        }

        [EventHandler(Event.SaltyChat_UpdateRadioTowers)]
        private void OnUpdateRadioTowers(dynamic towers)
        {
            List<Tower> radioTowers = new List<Tower>();

            foreach (dynamic tower in towers)
            {
                if (tower.GetType() == typeof(CitizenFX.Core.Vector3))
                    radioTowers.Add(new Tower(tower.X, tower.Y, tower.Z));
                else if (tower.Count == 3)
                    radioTowers.Add(new Tower(tower[0], tower[1], tower[2]));
                else if (tower.Count == 4)
                    radioTowers.Add(new Tower(tower[0], tower[1], tower[2], tower[3]));
            }

            this.RadioTowers = radioTowers.ToArray();

            this.ExecuteCommand(
                new PluginCommand(
                    Command.RadioTowerUpdate,
                    this.Configuration.ServerUniqueIdentifier,
                    new RadioTower(
                        this.RadioTowers
                    )
                )
            );
        }
        #endregion

        #region Remote Events(Megaphone)
        [EventHandler(Event.SaltyChat_IsUsingMegaphone)]
        private void OnIsUsingMegaphone(string handle, string teamSpeakName, float range, bool isSending, dynamic position)
        {
            if (!Int32.TryParse(handle, out int serverId))
                return;

            string name;

            if (serverId == Game.Player.ServerId)
            {
                name = this.TeamSpeakName;
            }
            else if (this.GetOrCreateVoiceClient(serverId, teamSpeakName, out VoiceClient client))
            {
                if (client.DistanceCulled)
                {
                    client.LastPosition = new CitizenFX.Core.Vector3(position[0], position[1], position[2]);
                    client.SendPlayerStateUpdate(this);
                }

                name = client.TeamSpeakName;
            }
            else
            {
                return;
            }

            this.ExecuteCommand(
                new PluginCommand(
                    isSending ? Command.MegaphoneCommunicationUpdate : Command.StopMegaphoneCommunication,
                    this.Configuration.ServerUniqueIdentifier,
                    new MegaphoneCommunication(
                        name,
                        range
                    )
                )
            );
        }
        #endregion

        #region Exports (Proximity)
        internal float GetVoiceRange() => this.VoiceRange;
        #endregion

        #region Exports (Radio)
        internal string GetRadioChannel(bool primary) => primary ? this.PrimaryRadioChannel : this.SecondaryRadioChannel;

        internal float GetRadioVolume() => this.RadioVolume;

        internal bool GetRadioSpeaker() => this.IsRadioSpeakerEnabled;

        internal void SetRadioChannel(string radioChannelName, bool primary)
        {
            if ((primary && this.PrimaryRadioChannel == radioChannelName) ||
                (!primary && this.SecondaryRadioChannel == radioChannelName))
                return;

            BaseScript.TriggerServerEvent(Event.SaltyChat_SetRadioChannel, radioChannelName, primary);
        }

        internal void SetRadioVolume(float volumeLevel)
        {
            if (volumeLevel < 0f)
                this.RadioVolume = 0f;
            else if (volumeLevel > 1.6f)
                this.RadioVolume = 1.6f;
            else
                this.RadioVolume = volumeLevel;
        }

        internal void SetRadioSpeaker(bool isRadioSpeakEnabled)
        {
            BaseScript.TriggerServerEvent(Event.SaltyChat_SetRadioSpeaker, isRadioSpeakEnabled);
        }
        #endregion

        #region Exports (Misc)
        internal int GetPluginState() => (int)this.PlguinState;
        #endregion

        #region NUI Events
        [EventHandler("__cfx_nui:" + NuiEvent.SaltyChat_OnNuiReady)]
        private void OnNuiReady(dynamic dummy, dynamic cb)
        {
            this.IsNuiReady = true;

            if (this.IsEnabled && this.TeamSpeakName != null && !this.IsConnected)
            {
                Debug.WriteLine("[Salty Chat] NUI is now ready, connecting...");

                this.ExecuteCommand("connect", this.WebSocketAddress);
            }

            cb("");
        }

        [EventHandler("__cfx_nui:" + NuiEvent.SaltyChat_OnConnected)]
        private void OnConnected(dynamic dummy, dynamic cb)
        {
            this.IsConnected = true;

            if (this.IsEnabled)
                this.InitializePlugin();

            cb("");
        }

        [EventHandler("__cfx_nui:" + NuiEvent.SaltyChat_OnDisconnected)]
        private void OnDisconnected(dynamic dummy, dynamic cb)
        {
            this.IsConnected = false;
            this.PlguinState = GameInstanceState.NotInitiated;

            cb("");
        }

        [EventHandler("__cfx_nui:" + NuiEvent.SaltyChat_OnMessage)]
        private void OnMessage(dynamic message, dynamic cb)
        {
            cb("");

            PluginCommand pluginCommand = PluginCommand.Deserialize(message);

            if (pluginCommand.ServerUniqueIdentifier != this.Configuration.ServerUniqueIdentifier)
                return;

            switch (pluginCommand.Command)
            {
                case Command.PluginState:
                    {
                        if (pluginCommand.TryGetPayload(out PluginState pluginState))
                        {
                            BaseScript.TriggerServerEvent(Event.SaltyChat_CheckVersion, pluginState.Version);

                            this.ExecuteCommand(
                                new PluginCommand(
                                    Command.RadioTowerUpdate,
                                    this.Configuration.ServerUniqueIdentifier,
                                    new RadioTower(this.RadioTowers)
                                )
                            );
                        }

                        break;
                    }
                case Command.Reset:
                    {
                        this.PlguinState = GameInstanceState.NotInitiated;

                        this.InitializePlugin();

                        break;
                    }
                case Command.Ping:
                    {
                        if (this.PlguinState != GameInstanceState.NotInitiated)
                            this.ExecuteCommand(new PluginCommand(this.Configuration.ServerUniqueIdentifier));

                        break;
                    }
                case Command.InstanceState:
                    {
                        if (pluginCommand.TryGetPayload(out InstanceState instanceState))
                            this.PlguinState = instanceState.State;

                        break;
                    }
                case Command.SoundState:
                    {
                        if (pluginCommand.TryGetPayload(out SoundState soundState))
                        {
                            if (soundState.IsMicrophoneMuted != this.IsMicrophoneMuted)
                            {
                                this.IsMicrophoneMuted = soundState.IsMicrophoneMuted;

                                BaseScript.TriggerEvent(Event.SaltyChat_MicStateChanged, this.IsMicrophoneMuted);
                            }

                            if (soundState.IsMicrophoneEnabled != this.IsMicrophoneEnabled)
                            {
                                this.IsMicrophoneEnabled = soundState.IsMicrophoneEnabled;

                                BaseScript.TriggerEvent(Event.SaltyChat_MicEnabledChanged, this.IsMicrophoneEnabled);
                            }

                            if (soundState.IsSoundMuted != this.IsSoundMuted)
                            {
                                this.IsSoundMuted = soundState.IsSoundMuted;

                                BaseScript.TriggerEvent(Event.SaltyChat_SoundStateChanged, this.IsSoundMuted);
                            }

                            if (soundState.IsSoundEnabled != this.IsSoundEnabled)
                            {
                                this.IsSoundEnabled = soundState.IsSoundEnabled;

                                BaseScript.TriggerEvent(Event.SaltyChat_SoundEnabledChanged, this.IsSoundEnabled);
                            }
                        }

                        break;
                    }
                case Command.TalkState:
                    {
                        if (pluginCommand.TryGetPayload(out TalkState talkState))
                            this.SetPlayerTalking(talkState.Name, talkState.IsTalking);

                        break;
                    }
                case Command.RadioTrafficState:
                    {
                        if (pluginCommand.TryGetPayload(out RadioTrafficState radioTrafficState))
                            BaseScript.TriggerEvent(Event.SaltyChat_RadioTrafficStateChanged, radioTrafficState.Name, radioTrafficState.IsSending, radioTrafficState.IsPrimaryChannel, radioTrafficState.ActiveRelay);

                        break;
                    }
            }
        }

        [EventHandler("__cfx_nui:" + NuiEvent.SaltyChat_OnError)]
        private async void OnError(dynamic message, dynamic cb)
        {
            cb("");

            try
            {
                PluginError pluginError = PluginError.Deserialize(message);

                switch (pluginError.Error)
                {
                    case Error.AlreadyInGame:
                        {
                            Debug.WriteLine($"[Salty Chat] Error: Seems like we are already in an instance, retry in 5 seconds...");

                            await BaseScript.Delay(5 * 1000);

                            this.InitializePlugin();

                            break;
                        }
                    default:
                        {
                            Debug.WriteLine($"[Salty Chat] Error: {pluginError.Error} - Message: {pluginError.Message}");

                            break;
                        }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[Salty Chat] Error: We received an error, but couldn't deserialize it:{Environment.NewLine}{e}");
            }
        }
        #endregion

        #region Keybindings
        private void OnVoiceRangePressed()
        {
            if (!this.IsEnabled)
                return;

            this.ToggleVoiceRange();
        }

        private void OnVoiceRangeReleased()
        {
            // Empty dummy, so /-voiceRange isn't spammed in the chat
        }

        private void OnPrimaryRadioPressed()
        {
            Ped playerPed = Game.PlayerPed;

            if (!this.IsEnabled || !playerPed.IsAlive || String.IsNullOrWhiteSpace(this.PrimaryRadioChannel) || !this.CanSendRadioTraffic)
                return;

            BaseScript.TriggerServerEvent(Event.SaltyChat_IsSending, this.PrimaryRadioChannel, true);
            //Game.PlayerPed.Task.PlayAnimation("random@arrests", "generic_radio_enter", -2f, -1, (AnimationFlags)50);
        }

        private void OnPrimaryRadioReleased()
        {
            if (!this.IsEnabled || String.IsNullOrWhiteSpace(this.PrimaryRadioChannel))
                return;

            BaseScript.TriggerServerEvent(Event.SaltyChat_IsSending, this.PrimaryRadioChannel, false);
            //Game.PlayerPed.Task.ClearAnimation("random@arrests", "generic_radio_enter");
        }

        private void OnSecondaryRadioPressed()
        {
            Ped playerPed = Game.PlayerPed;

            if (!this.IsEnabled || !playerPed.IsAlive || String.IsNullOrWhiteSpace(this.SecondaryRadioChannel) || !this.CanSendRadioTraffic)
                return;

            BaseScript.TriggerServerEvent(Event.SaltyChat_IsSending, this.SecondaryRadioChannel, true);
            //Game.PlayerPed.Task.PlayAnimation("random@arrests", "generic_radio_enter", -2f, -1, (AnimationFlags)50);
        }

        private void OnSecondaryRadioReleased()
        {
            if (!this.IsEnabled || String.IsNullOrWhiteSpace(this.SecondaryRadioChannel))
                return;

            BaseScript.TriggerServerEvent(Event.SaltyChat_IsSending, this.SecondaryRadioChannel, false);
            //Game.PlayerPed.Task.ClearAnimation("random@arrests", "generic_radio_enter");
        }

        /*
        private void OnMegaphonePressed()
        {
            Ped playerPed = Game.PlayerPed;

            if (!this.IsEnabled || !playerPed.IsAlive || !playerPed.IsInPoliceVehicle)
                return;

            Vehicle vehicle = playerPed.CurrentVehicle;

            if (vehicle.GetPedOnSeat(VehicleSeat.Driver) == playerPed || vehicle.GetPedOnSeat(VehicleSeat.Passenger) == playerPed)
            {
                BaseScript.TriggerServerEvent(Event.SaltyChat_IsUsingMegaphone, true);
                this.IsUsingMegaphone = true;
            }
        }

        private void OnMegaphoneReleased()
        {
            if (!this.IsEnabled || !this.IsUsingMegaphone)
                return;

            BaseScript.TriggerServerEvent(Event.SaltyChat_IsUsingMegaphone, false);
            this.IsUsingMegaphone = false;
        }
        */
        #endregion

        #region Tick
        [Tick]
        private async Task FirstTick()
        {
            this.Configuration = JsonConvert.DeserializeObject<Configuration>(API.LoadResourceFile(API.GetCurrentResourceName(), "config.json"));

            // Register commands and key mappings
            /*
            API.RegisterCommand("+voiceRange", new Action(this.OnVoiceRangePressed), false);
            API.RegisterCommand("-voiceRange", new Action(this.OnVoiceRangeReleased), false);
            API.RegisterKeyMapping("+voiceRange", "Toggle Voice Range", "keyboard", this.Configuration.ToggleRange);

            API.RegisterCommand("+primaryRadio", new Action(this.OnPrimaryRadioPressed), false);
            API.RegisterCommand("-primaryRadio", new Action(this.OnPrimaryRadioReleased), false);
            API.RegisterKeyMapping("+primaryRadio", "Use Primary Radio", "keyboard", this.Configuration.TalkPrimary);

            API.RegisterCommand("+secondaryRadio", new Action(this.OnSecondaryRadioPressed), false);
            API.RegisterCommand("-secondaryRadio", new Action(this.OnSecondaryRadioReleased), false);
            API.RegisterKeyMapping("+secondaryRadio", "Use Secondary Radio", "keyboard", this.Configuration.TalkSecondary);

            API.RegisterCommand("+megaphone", new Action(this.OnMegaphonePressed), false);
            API.RegisterCommand("-megaphone", new Action(this.OnMegaphoneReleased), false);
            API.RegisterKeyMapping("+megaphone", "Use Megaphone", "keyboard", this.Configuration.TalkMegaphone);
            */

            BaseScript.TriggerServerEvent(Event.SaltyChat_Initialize);

            this.Tick -= this.FirstTick;

            if (this.Configuration.VoiceEnabled)
            {
                this.Tick += this.OnControlTick;
                this.Tick += this.OnStateUpdateTick;
            }

            await Task.FromResult(0);
        }

        private async Task OnControlTick()
        {
            Ped playerPed = Game.PlayerPed;

            if (this.IsEnabled && playerPed != null)
            {
                Game.DisableControlThisFrame(0, Control.PushToTalk);
                Game.DisableControlThisFrame(0, (Control)this.Configuration.ToggleRange);
                Game.DisableControlThisFrame(0, (Control)this.Configuration.TalkPrimary);
                Game.DisableControlThisFrame(0, (Control)this.Configuration.TalkSecondary);

                if (Game.IsControlJustPressed(0, (Control)this.Configuration.ToggleRange))
                    this.OnVoiceRangePressed();

                if (Game.IsControlJustPressed(0, (Control)this.Configuration.TalkPrimary))
                    this.OnPrimaryRadioPressed();
                else if (Game.IsControlJustReleased(0, (Control)this.Configuration.TalkPrimary))
                    this.OnPrimaryRadioReleased();

                if (Game.IsControlJustPressed(0, (Control)this.Configuration.TalkSecondary))
                    this.OnSecondaryRadioPressed();
                else if (Game.IsControlJustReleased(0, (Control)this.Configuration.TalkSecondary))
                    this.OnSecondaryRadioReleased();

                /*
                if (!playerPed.IsInPoliceVehicle && this.IsUsingMegaphone)
                {
                    BaseScript.TriggerServerEvent(Event.SaltyChat_IsUsingMegaphone, false);
                    this.IsUsingMegaphone = false;
                }
                */
            }

            await Task.FromResult(0);
        }

        private async Task OnStateUpdateTick()
        {
            Ped playerPed = Game.PlayerPed;

            if (this.IsConnected && this.PlguinState == GameInstanceState.Ingame)
            {
                CitizenFX.Core.Vector3 playerPosition = playerPed.Position;
                int playerRoomId = API.GetRoomKeyFromEntity(playerPed.Handle);

                List<PlayerState> playerStates = new List<PlayerState>();
                List<int> updatedPlayers = new List<int>();

                foreach (Ped ped in World.GetAllPeds().Where(p => p.IsPlayer))
                {
                    if (ped == playerPed)
                        continue;

                    Player nPlayer = new Player(API.NetworkGetPlayerIndexFromPed(ped.Handle));

                    if (nPlayer == null || !this.GetOrCreateVoiceClient(nPlayer, out VoiceClient voiceClient))
                        continue;

                    if (voiceClient.DistanceCulled)
                        voiceClient.DistanceCulled = false;

                    voiceClient.LastPosition = ped.Position;
                    int? muffleIntensity = null;

                    if (voiceClient.IsAlive)
                    {
                        int nPlayerRoomId = API.GetRoomKeyFromEntity(ped.Handle);

                        if (nPlayerRoomId != playerRoomId && !API.HasEntityClearLosToEntity(playerPed.Handle, ped.Handle, 17))
                        {
                            muffleIntensity = 10;
                        }
                    }

                    playerStates.Add(
                        new PlayerState(
                            voiceClient.TeamSpeakName,
                            voiceClient.LastPosition,
                            voiceClient.VoiceRange,
                            voiceClient.IsAlive,
                            voiceClient.DistanceCulled,
                            muffleIntensity
                        )
                    );

                    updatedPlayers.Add(voiceClient.ServerId);
                }

                foreach (VoiceClient culledVoiceClient in this.VoiceClients.Where(c => !c.DistanceCulled && !updatedPlayers.Contains(c.ServerId)))
                {
                    culledVoiceClient.DistanceCulled = true;

                    playerStates.Add(
                        new PlayerState(
                            culledVoiceClient.TeamSpeakName,
                            culledVoiceClient.LastPosition,
                            culledVoiceClient.VoiceRange,
                            culledVoiceClient.IsAlive,
                            culledVoiceClient.DistanceCulled
                        )
                    );
                }

                this.ExecuteCommand(
                    new PluginCommand(
                        Command.BulkUpdate,
                        this.Configuration.ServerUniqueIdentifier,
                        new BulkUpdate(
                            playerStates,
                            new SelfState(
                                playerPosition,
                                API.GetGameplayCamRot(0).Z,
                                this.IsAlive
                            )
                        )
                    )
                );
            }

            if (this.IsAlive)
            {
                bool isUnderWater = playerPed.IsSwimmingUnderWater;
                bool isSwimming = isUnderWater || playerPed.IsSwimming;

                if (isUnderWater)
                {
                    this.CanSendRadioTraffic = false;
                    this.CanReceiveRadioTraffic = false;
                }
                else if (isSwimming && API.GetEntitySpeed(playerPed.Handle) <= 2f)
                {
                    this.CanSendRadioTraffic = true;
                    this.CanReceiveRadioTraffic = true;
                }
                else if (isSwimming)
                {
                    this.CanSendRadioTraffic = false;
                    this.CanReceiveRadioTraffic = true;
                }
                else
                {
                    this.CanSendRadioTraffic = true;
                    this.CanReceiveRadioTraffic = true;
                }
            }
            else
            {
                this.CanSendRadioTraffic = false;
                this.CanReceiveRadioTraffic = false;
            }

            await BaseScript.Delay(280);
        }
        #endregion

        #region Methods (Proximity)
        private void SetPlayerTalking(string teamSpeakName, bool isTalking)
        {
            if (teamSpeakName == this.TeamSpeakName)
            {
                BaseScript.TriggerEvent(Event.SaltyChat_TalkStateChanged, isTalking);

                API.SetPlayerTalkingOverride(Game.Player.Handle, isTalking);

                // Lip sync workaround for OneSync
                if (isTalking)
                    Game.PlayerPed.PlayFacialAnimation("face_human@gen_male@base", "mood_talking_normal");
                else
                    Game.PlayerPed.PlayFacialAnimation("face_human@gen_male@base", "mood_normal");
            }
            else
            {
                VoiceClient voiceClient = this.VoiceClients.FirstOrDefault(v => v.TeamSpeakName == teamSpeakName);

                if (voiceClient != null && voiceClient.Player != null)
                    API.SetPlayerTalkingOverride(voiceClient.Player.Handle, isTalking);
            }
        }

        /// <summary>
        /// Toggles voice range through <see cref="Voice.VoiceRanges"/>
        /// </summary>
        public void ToggleVoiceRange()
        {
            int index = Array.IndexOf(this.Configuration.VoiceRanges, this.VoiceRange);

            if (index < 0)
            {
                index = 1;
                this.VoiceRange = this.Configuration.VoiceRanges[index];
            }
            else if (index + 1 >= this.Configuration.VoiceRanges.Length)
            {
                index = 0;
                this.VoiceRange = this.Configuration.VoiceRanges[index];
            }
            else
            {
                index++;
                this.VoiceRange = this.Configuration.VoiceRanges[index];
            }

            BaseScript.TriggerServerEvent(Event.SaltyChat_SetVoiceRange, this.VoiceRange);

            if (this.Configuration.EnableVoiceRangeNotification)
            {
                Util.SendChatMessage("Salty Chat", this.Configuration.VoiceRangeNotification.Replace("{voicerange}", this.VoiceRange.ToString()));
            }
        }
        #endregion

        #region Methods (Plugin)
        private void InitializePlugin()
        {
            if (this.PlguinState != GameInstanceState.NotInitiated)
                return;

            this.ExecuteCommand(
                new PluginCommand(
                    Command.Initiate,
                    new GameInstance(
                        this.Configuration.ServerUniqueIdentifier,
                        this.TeamSpeakName,
                        this.Configuration.IngameChannelId,
                        this.Configuration.IngameChannelPassword,
                        this.Configuration.SoundPack,
                        this.Configuration.SwissChannelIds,
                        this.Configuration.RequestTalkStates,
                        this.Configuration.RequestRadioTrafficStates,
                        this.Configuration.UltraShortRangeDistance,
                        this.Configuration.ShortRangeDistance,
                        this.Configuration.LongRangeDistace
                    )
                )
            );
        }

        /// <summary>
        /// Plays a file from soundpack specified in <see cref="VoiceManager.SoundPack"/>
        /// </summary>
        /// <param name="fileName">filename (without .wav) of the soundfile</param>
        /// <param name="loop">use <see cref="true"/> to let the plugin loop the sound</param>
        /// <param name="handle">use your own handle instead of the filename, so you can play the sound multiple times</param>
        public void PlaySound(string fileName, bool loop = false, string handle = null)
        {
            if (String.IsNullOrWhiteSpace(handle))
                handle = fileName;

            this.ExecuteCommand(
                new PluginCommand(
                    Command.PlaySound,
                    this.Configuration.ServerUniqueIdentifier,
                    new Sound(
                        fileName,
                        loop,
                        handle
                    )
                )
            );
        }

        /// <summary>
        /// Stops and dispose the sound
        /// </summary>
        /// <param name="handle">filename or handle of the sound</param>
        public void StopSound(string handle)
        {
            this.ExecuteCommand(
                new PluginCommand(
                    Command.StopSound,
                    this.Configuration.ServerUniqueIdentifier,
                    new Sound(handle)
                )
            );
        }

        private void ExecuteCommand(string funtion, object parameters)
        {
            API.SendNuiMessage(
                Newtonsoft.Json.JsonConvert.SerializeObject(new { Function = funtion, Params = parameters })
            );
        }

        internal void ExecuteCommand(PluginCommand pluginCommand)
        {
            this.ExecuteCommand("runCommand", Util.ToJson(pluginCommand));
        }

        private void DisplayDebug(bool show)
        {
            this.ExecuteCommand("showBody", show);
        }
        #endregion

        #region Methods (Helper)
        public bool GetOrCreateVoiceClient(Player player, out VoiceClient voiceClient)
        {
            if (player == null)
            {
                voiceClient = null;
                return false;
            }

            lock (this._voiceClients)
            {
                if (this._voiceClients.TryGetValue(player.ServerId, out voiceClient))
                {
                    dynamic voiceRange = player.State[State.SaltyChat_VoiceRange];

                    voiceClient.VoiceRange = voiceRange ?? 0f;
                    voiceClient.IsAlive = player.State[State.SaltyChat_IsAlive] == true;
                }
                else
                {
                    string tsName = player.State[State.SaltyChat_TeamSpeakName];

                    if (tsName == null)
                        return false;

                    dynamic voiceRange = player.State[State.SaltyChat_VoiceRange];
                    bool isAlive = player.State[State.SaltyChat_IsAlive] == true;

                    voiceClient = new VoiceClient(player.ServerId, tsName, voiceRange ?? 0f, isAlive);

                    this._voiceClients.Add(voiceClient.ServerId, voiceClient);
                }
            }

            return voiceClient != null;
        }

        public bool GetOrCreateVoiceClient(int serverId, string teamSpeakName, out VoiceClient voiceClient)
        {
            Player player = VoiceManager.PlayerList[serverId];

            lock (this._voiceClients)
            {
                if (this._voiceClients.TryGetValue(serverId, out voiceClient))
                {
                    if (player != null)
                    {
                        dynamic voiceRange = player.State[State.SaltyChat_VoiceRange];

                        voiceClient.VoiceRange = voiceRange ?? 0f;
                        voiceClient.IsAlive = player.State[State.SaltyChat_IsAlive] == true;
                    }
                }
                else
                {
                    if (player != null)
                    {
                        string tsName = player.State[State.SaltyChat_TeamSpeakName];

                        if (tsName == null)
                            return false;

                        dynamic voiceRange = player.State[State.SaltyChat_VoiceRange];
                        bool isAlive = player.State[State.SaltyChat_IsAlive] == true;

                        voiceClient = new VoiceClient(player.ServerId, tsName, voiceRange ?? 0f, isAlive);
                    }
                    else
                    {
                        voiceClient = new VoiceClient(serverId, teamSpeakName, 0f, true) { DistanceCulled = true };
                    }

                    this._voiceClients.Add(voiceClient.ServerId, voiceClient);
                }
            }

            return voiceClient != null;
        }
        #endregion
    }
}
