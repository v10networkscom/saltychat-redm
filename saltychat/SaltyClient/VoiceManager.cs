using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using RedM.External;
using SaltyShared;

namespace SaltyClient
{
    internal class VoiceManager : BaseScript
    {
        #region Properties / Fields
        private static bool _isEnabled;
        private static bool _isConnected;
        private static bool _isIngame;

        private static string _teamSpeakName;
        private static string _serverUniqueIdentifier;
        private static string _soundPack;
        private static ulong _ingameChannel;
        private static string _ingameChannelPassword;

        public static VoiceClient[] VoiceClients => VoiceManager._voiceClients.Values.ToArray();
        private static Dictionary<int, VoiceClient> _voiceClients = new Dictionary<int, VoiceClient>();

        public static float VoiceRange { get; private set; } = SharedData.VoiceRanges[1];
        public static string PrimaryRadioChannel;
        public static string SecondaryRadioChannel;

        public static PlayerList PlayerList { get; } = new PlayerList();

        public static bool IsTalking { get; private set; }
        public static bool IsMicrophoneMuted { get; private set; }
        public static bool IsSoundMuted { get; private set; }
        #endregion

        #region CTOR
        public VoiceManager()
        {
            API.RegisterNuiCallbackType(NuiEvent.SaltyChat_OnConnected);
            API.RegisterNuiCallbackType(NuiEvent.SaltyChat_OnDisconnected);
            API.RegisterNuiCallbackType(NuiEvent.SaltyChat_OnError);
            API.RegisterNuiCallbackType(NuiEvent.SaltyChat_OnMessage);
        }
        #endregion

        #region Events
        [EventHandler("onClientResourceStart")]
        private void OnResourceStart(string resourceName)
        {
            if (resourceName != API.GetCurrentResourceName())
                return;
            
            BaseScript.TriggerServerEvent(Event.SaltyChat_Initialize);
        }

        [EventHandler("onClientResourceStop")]
        private void OnResourceStop(string resourceName)
        {
            if (resourceName != API.GetCurrentResourceName())
                return;

            VoiceManager._isEnabled = false;
            VoiceManager._isConnected = false;

            lock (VoiceManager._voiceClients)
            {
                VoiceManager._voiceClients.Clear();
            }

            VoiceManager.PrimaryRadioChannel = null;
        }
        #endregion

        #region Remote Events (Handling)
        [EventHandler(Event.SaltyChat_Initialize)]
        private void OnInitialize(string teamSpeakName, string serverUniqueIdentifier, string soundPack, string ingameChannel, string ingameChannelPassword)
        {
            VoiceManager._teamSpeakName = teamSpeakName;
            VoiceManager._serverUniqueIdentifier = serverUniqueIdentifier;
            VoiceManager._soundPack = soundPack;

            if (!UInt64.TryParse(ingameChannel, out ulong channelId))
            {
                Debug.WriteLine("[Salty Chat] Could not parse ingame channel");
                return;
            }

            VoiceManager._ingameChannel = channelId;
            VoiceManager._ingameChannelPassword = ingameChannelPassword;

            VoiceManager._isEnabled = true;

            if (VoiceManager._isConnected)
                this.InitializePlugin();
            else
                this.ExecuteCommand("connect", "127.0.0.1:8088");

            //VoiceManager.DisplayDebug(true);
        }

        [EventHandler(Event.SaltyChat_UpdateClient)]
        private void OnClientUpdate(string handle, string teamSpeakName, float voiceRange)
        {
            if (!Int32.TryParse(handle, out int serverId))
                return;

            if (Game.Player.ServerId == serverId)
                return;

            lock (VoiceManager._voiceClients)
            {
                if (VoiceManager._voiceClients.TryGetValue(serverId, out VoiceClient client))
                {
                    client.TeamSpeakName = teamSpeakName;
                    client.VoiceRange = voiceRange;
                }
                else
                {
                    VoiceManager._voiceClients.Add(serverId, new VoiceClient(serverId, VoiceManager.PlayerList[serverId], teamSpeakName, voiceRange));
                }
            }
        }

        [EventHandler(Event.SaltyChat_RemoveClient)]
        private void OnClientRemove(string handle)
        {
            if (!Int32.TryParse(handle, out int serverId))
                return;

            lock (VoiceManager._voiceClients)
            {
                if (VoiceManager._voiceClients.TryGetValue(serverId, out VoiceClient client))
                {
                    this.ExecuteCommand(new PluginCommand(Command.RemovePlayer, VoiceManager._serverUniqueIdentifier, new PlayerState(client.TeamSpeakName)));

                    VoiceManager._voiceClients.Remove(serverId);
                }
            }
        }
        #endregion

        #region Remote Events (Phone)
        [EventHandler(Event.SaltyChat_EstablishCall)]
        private void OnEstablishCall(string handle)
        {
            if (!Int32.TryParse(handle, out int serverId))
                return;

            if (VoiceManager._voiceClients.TryGetValue(serverId, out VoiceClient client))
            {
                //Vector3 playerPosition = Game.PlayerPed.Position;
                //Vector3 remotePlayerPosition = client.Player.Character.Position;

                // ToDo: Find alternative - Natives not available in RDR3
                //int signalDistortion = API.GetZoneScumminess(API.GetZoneAtCoords(playerPosition.X, playerPosition.Y, playerPosition.Z));
                //signalDistortion += API.GetZoneScumminess(API.GetZoneAtCoords(remotePlayerPosition.X, remotePlayerPosition.Y, remotePlayerPosition.Z));
                
                int signalDistortion = 0;

                this.ExecuteCommand(
                    new PluginCommand(
                        Command.PhoneCommunicationUpdate,
                        VoiceManager._serverUniqueIdentifier,
                        new PhoneCommunication(
                            client.TeamSpeakName,
                            signalDistortion
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

            if (VoiceManager._voiceClients.TryGetValue(serverId, out VoiceClient client))
            {
                this.ExecuteCommand(
                    new PluginCommand(
                        Command.StopPhoneCommunication,
                        VoiceManager._serverUniqueIdentifier,
                        new PhoneCommunication(
                            client.TeamSpeakName
                        )
                    )
                );
            }
        }
        #endregion

        #region Remote Events (Radio)
        [EventHandler(Event.SaltyChat_SetRadioChannel)]
        private void OnSetRadioChannel(string radioChannel, bool isPrimary)
        {
            if (isPrimary)
            {
                VoiceManager.PrimaryRadioChannel = radioChannel;

                if (String.IsNullOrEmpty(radioChannel))
                    this.PlaySound("leaveRadioChannel", false, "radio");
                else
                    this.PlaySound("enterRadioChannel", false, "radio");
            }
            else
            {
                VoiceManager.SecondaryRadioChannel = radioChannel;

                if (String.IsNullOrEmpty(radioChannel))
                    this.PlaySound("leaveRadioChannel", false, "radio");
                else
                    this.PlaySound("enterRadioChannel", false, "radio");
            }
        }

        [EventHandler(Event.SaltyChat_IsSending)]
        private void OnPlayerIsSending(string handle, string radioChannel, bool isSending, bool stateChange)
        {
            this.OnPlayerIsSendingRelayed(handle, radioChannel, isSending, stateChange, true, new List<dynamic>());
        }

        [EventHandler(Event.SaltyChat_IsSendingRelayed)]
        private void OnPlayerIsSendingRelayed(string handle, string radioChannel, bool isSending, bool stateChange, bool direct, List<dynamic> relays)
        {
            if (!Int32.TryParse(handle, out int serverId))
                return;

            if (serverId == Game.Player.ServerId)
            {
                this.PlaySound("selfMicClick", false, "MicClick");
            }
            else if (VoiceManager._voiceClients.TryGetValue(serverId, out VoiceClient client))
            {
                if (isSending)
                {
                    this.ExecuteCommand(
                        new PluginCommand(
                            Command.RadioCommunicationUpdate,
                            VoiceManager._serverUniqueIdentifier,
                            new RadioCommunication(
                                client.TeamSpeakName,
                                RadioType.LongRange,
                                RadioType.LongRange,
                                stateChange,
                                direct,
                                VoiceManager.SecondaryRadioChannel == radioChannel,
                                relays.Select(r => (string)r).ToArray()
                            )
                        )
                    );
                }
                else
                {
                    this.ExecuteCommand(
                        new PluginCommand(
                            Command.RadioCommunicationUpdate,
                            VoiceManager._serverUniqueIdentifier,
                            new RadioCommunication(
                                client.TeamSpeakName,
                                RadioType.None,
                                RadioType.None,
                                stateChange,
                                VoiceManager.SecondaryRadioChannel == radioChannel
                            )
                        )
                    );
                }
            }
        }

        [EventHandler(Event.SaltyChat_UpdateRadioTowers)]
        private void OnUpdateRadioTowers(dynamic towers)
        {
            List<Vector3> towerPositions = new List<Vector3>();

            foreach (dynamic tower in towers)
            {
                towerPositions.Add(new Vector3(tower[0], tower[1], tower[2]));
            }

            this.ExecuteCommand(
                new PluginCommand(
                    Command.RadioTowerUpdate,
                    VoiceManager._serverUniqueIdentifier,
                    new RadioTower(
                        towerPositions.ToArray()
                    )
                )
            );
        }
        #endregion

        #region NUI Events
        [EventHandler("__cfx_nui:" + NuiEvent.SaltyChat_OnConnected)]
        private void OnConnected(dynamic dummy, dynamic cb)
        {
            VoiceManager._isConnected = true;

            if (VoiceManager._isEnabled)
                this.InitializePlugin();

            cb("");
        }

        [EventHandler("__cfx_nui:" + NuiEvent.SaltyChat_OnDisconnected)]
        private void OnDisconnected(dynamic dummy, dynamic cb)
        {
            VoiceManager._isConnected = false;

            cb("");
        }

        [EventHandler("__cfx_nui:" + NuiEvent.SaltyChat_OnMessage)]
        private void OnMessage(dynamic message, dynamic cb)
        {
            PluginCommand pluginCommand = PluginCommand.Deserialize(message);

            if (pluginCommand.Command == Command.Ping && pluginCommand.ServerUniqueIdentifier == VoiceManager._serverUniqueIdentifier)
            {
                this.ExecuteCommand(new PluginCommand(VoiceManager._serverUniqueIdentifier));

                cb("");
                return;
            }

            if (!pluginCommand.TryGetState(out PluginState pluginState))
            {
                cb("");
                return;
            }

            if (pluginState.IsReady != VoiceManager._isIngame)
            {
                BaseScript.TriggerServerEvent(Event.SaltyChat_CheckVersion, pluginState.UpdateBranch, pluginState.Version);

                VoiceManager._isIngame = pluginState.IsReady;
            }

            if (pluginState.IsTalking != VoiceManager.IsTalking)
            {
                VoiceManager.IsTalking = pluginState.IsTalking;

                BaseScript.TriggerEvent(Event.SaltyChat_TalkStateChanged, VoiceManager.IsTalking);
            }

            if (pluginState.IsMicrophoneMuted != VoiceManager.IsMicrophoneMuted)
            {
                VoiceManager.IsMicrophoneMuted = pluginState.IsMicrophoneMuted;

                BaseScript.TriggerEvent(Event.SaltyChat_MicStateChanged, VoiceManager.IsMicrophoneMuted);
            }

            if (pluginState.IsSoundMuted != VoiceManager.IsSoundMuted)
            {
                VoiceManager.IsSoundMuted = pluginState.IsSoundMuted;

                BaseScript.TriggerEvent(Event.SaltyChat_SoundStateChanged, VoiceManager.IsSoundMuted);
            }

            cb("");
        }

        [EventHandler("__cfx_nui:" + NuiEvent.SaltyChat_OnError)]
        private void OnError(dynamic message, dynamic cb)
        {
            PluginCommand pluginCommand = PluginCommand.Deserialize(message);

            if (pluginCommand.TryGetError(out PluginError pluginError))
                Debug.WriteLine($"[Salty Chat] Error: {pluginError.Error} - Message: {pluginError.Message}");

            cb("");
        }
        #endregion

        #region Tick
        [Tick]
        private async Task OnControlTick()
        {
            Game.DisableControlThisFrame(0, Control.OpenJournal);
            Game.DisableControlThisFrame(0, Control.PushToTalk);
            Game.DisableControlThisFrame(0, Control.OpenSatchelMenu);

            if (Game.Player.IsAlive)
            {
                if (Game.IsControlJustPressed(0, Control.OpenJournal))
                {
                    this.ToggleVoiceRange();
                }

                if (VoiceManager.PrimaryRadioChannel != null)
                {
                    if (Game.IsControlJustPressed(0, Control.PushToTalk))
                        BaseScript.TriggerServerEvent(Event.SaltyChat_IsSending, VoiceManager.PrimaryRadioChannel, true);
                    else if (Game.IsControlJustReleased(0, Control.PushToTalk))
                        BaseScript.TriggerServerEvent(Event.SaltyChat_IsSending, VoiceManager.PrimaryRadioChannel, false);
                }

                if (VoiceManager.SecondaryRadioChannel != null)
                {
                    if (Game.IsControlJustPressed(0, Control.OpenSatchelMenu))
                        BaseScript.TriggerServerEvent(Event.SaltyChat_IsSending, VoiceManager.SecondaryRadioChannel, true);
                    else if (Game.IsControlJustReleased(0, Control.OpenSatchelMenu))
                        BaseScript.TriggerServerEvent(Event.SaltyChat_IsSending, VoiceManager.SecondaryRadioChannel, false);
                }
            }

            await Task.FromResult(0);
        }

        [Tick]
        private async Task OnStateUpdateTick()
        {
            if (VoiceManager._isConnected && VoiceManager._isIngame)
            {
                Vector3 playerPosition = Game.PlayerPed.Position;

                foreach (VoiceClient client in VoiceManager.VoiceClients)
                {
                    if (client.Player == null)
                    {
                        client.Player = VoiceManager.PlayerList[client.ServerId];

                        if (client.Player == null)
                            continue;
                    }

                    Ped ped = client.Player.Character;

                    if (!ped.Exists())
                        continue;

                    this.ExecuteCommand(
                        new PluginCommand(
                            Command.PlayerStateUpdate,
                            VoiceManager._serverUniqueIdentifier,
                            new PlayerState(
                                client.TeamSpeakName,
                                ped.Position,
                                client.VoiceRange,
                                client.Player.IsAlive
                            )
                        )
                    );
                }

                this.ExecuteCommand(
                    new PluginCommand(
                        Command.SelfStateUpdate,
                        VoiceManager._serverUniqueIdentifier,
                        new PlayerState(
                            playerPosition,
                            API.GetGameplayCamRot(0).Z
                        )
                    )
                );
            }

            await BaseScript.Delay(250);
        }
        #endregion

        #region Methods
        private void InitializePlugin()
        {
            this.ExecuteCommand(
                new PluginCommand(
                    Command.Initiate,
                    new GameInstance(
                        VoiceManager._serverUniqueIdentifier,
                        VoiceManager._teamSpeakName,
                        VoiceManager._ingameChannel,
                        VoiceManager._ingameChannelPassword,
                        VoiceManager._soundPack
                    )
                )
            );
        }

        /// <summary>
        /// Toggles voice range through <see cref="SharedData.VoiceRanges"/>
        /// </summary>
        public void ToggleVoiceRange()
        {
            int index = Array.IndexOf(SharedData.VoiceRanges, VoiceManager.VoiceRange);

            if (index < 0)
            {
                VoiceManager.VoiceRange = SharedData.VoiceRanges[1];
            }
            else if (index + 1 >= SharedData.VoiceRanges.Length)
            {
                VoiceManager.VoiceRange = SharedData.VoiceRanges[0];
            }
            else
            {
                VoiceManager.VoiceRange = SharedData.VoiceRanges[index + 1];
            }

            BaseScript.TriggerServerEvent(Event.SaltyChat_SetVoiceRange, VoiceManager.VoiceRange);

            // ToDo
            //CitizenFX.Core.UI.Screen.ShowNotification($"New voice range is {VoiceManager._voiceRange} metres.");

            System.Drawing.Color color = System.Drawing.Color.FromArgb(255, 255, 255);

            BaseScript.TriggerEvent("chat:addMessage", new
            {
                color = new[] { color.R, color.G, color.B },
                args = new[] { "Salty Chat", $"New voice range is {VoiceManager.VoiceRange} metres." }
            });
        }

        /// <summary>
        /// Plays a file from soundpack specified in <see cref="VoiceManager._soundPack"/>
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
                    VoiceManager._serverUniqueIdentifier,
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
                    VoiceManager._serverUniqueIdentifier,
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

        private void ExecuteCommand(PluginCommand pluginCommand)
        {
            this.ExecuteCommand("runCommand", Util.ToJson(pluginCommand));
        }

        private void DisplayDebug(bool show)
        {
            this.ExecuteCommand("showBody", show);
        }
        #endregion
    }
}
