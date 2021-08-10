using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SaltyShared;
using Newtonsoft.Json;

namespace SaltyServer
{
    public class RadioChannel
    {
        #region Props/Fields
        internal string Name { get; }

        internal RadioChannelMember[] Members => this._members.ToArray();
        private List<RadioChannelMember> _members = new List<RadioChannelMember>();
        #endregion

        #region CTOR
        internal RadioChannel(string name, params RadioChannelMember[] members)
        {
            this.Name = name;

            if (members != null)
                this._members.AddRange(members);
        }
        #endregion

        #region Methods
        internal bool IsMember(VoiceClient voiceClient)
        {
            return this.Members.Any(m => m.VoiceClient == voiceClient);
        }

        internal void AddMember(VoiceClient voiceClient, bool isPrimary)
        {
            lock (this._members)
            {
                if (!this._members.Any(m => m.VoiceClient == voiceClient))
                {
                    this._members.Add(new RadioChannelMember(this, voiceClient, isPrimary));

                    voiceClient.TriggerEvent(Event.SaltyChat_SetRadioChannel, this.Name, isPrimary);

                    foreach (RadioChannelMember member in this._members.Where(m => m.IsSending))
                    {
                        voiceClient.TriggerEvent(Event.SaltyChat_IsSending, member.VoiceClient.Player.Handle, member.VoiceClient.TeamSpeakName, this.Name, true, false, member.VoiceClient.Player.GetPosition());
                    }
                }
            }
        }

        internal void RemoveMember(VoiceClient voiceClient)
        {
            lock (this._members)
            {
                RadioChannelMember member = this._members.FirstOrDefault(m => m.VoiceClient == voiceClient);

                if (member != null)
                {
                    if (member.IsSending)
                    {
                        CitizenFX.Core.Vector3 position = member.VoiceClient.Player.GetPosition();

                        if (member.VoiceClient.IsRadioSpeakerEnabled)
                        {
                            foreach (VoiceClient client in VoiceManager.Instance.VoiceClients)
                            {
                                client.TriggerEvent(Event.SaltyChat_IsSendingRelayed, voiceClient.Player.Handle, voiceClient.TeamSpeakName, this.Name, false, true, position, false, new string[0]);
                            }
                        }
                        else
                        {
                            foreach (RadioChannelMember channelMember in this._members)
                            {
                                channelMember.VoiceClient.TriggerEvent(Event.SaltyChat_IsSending, voiceClient.Player.Handle, voiceClient.TeamSpeakName, this.Name, false, true, position);
                            }
                        }
                    }

                    this._members.Remove(member);

                    foreach (RadioChannelMember channelMember in this._members.Where(m => m.IsSending))
                    {
                        voiceClient.TriggerEvent(Event.SaltyChat_IsSending, channelMember.VoiceClient.Player.Handle, channelMember.VoiceClient.TeamSpeakName, this.Name, false, false, channelMember.VoiceClient.Player.GetPosition());
                    }

                    voiceClient.TriggerEvent(Event.SaltyChat_SetRadioChannel, null, member.IsPrimary);
                }
            }
        }

        internal void SetSpeaker(VoiceClient voiceClient, bool isEnabled)
        {
            if (!this.TryGetMember(voiceClient, out RadioChannelMember radioChannelMember) || radioChannelMember.IsSpeakerEnabled == isEnabled)
                return;

            radioChannelMember.IsSpeakerEnabled = isEnabled;
            RadioChannelMember[] channelMembers = this.Members;
            IEnumerable<RadioChannelMember> sendingMembers = channelMembers.Where(m => m.IsSending);

            if (sendingMembers.Count() == 0)
                return;

            if (isEnabled || channelMembers.Any(m => m.IsSpeakerEnabled))
            {
                foreach (RadioChannelMember sendingMember in sendingMembers)
                {
                    this.Send(sendingMember.VoiceClient, true);
                }
            }
            else
            {
                foreach (RadioChannelMember sendingMember in sendingMembers)
                {
                    CitizenFX.Core.Vector3 position = sendingMember.VoiceClient.Player.GetPosition();

                    foreach (VoiceClient remoteClient in VoiceManager.Instance.VoiceClients.Where(v => !channelMembers.Any(m => m.VoiceClient == v)))
                    {
                        remoteClient.TriggerEvent(Event.SaltyChat_IsSendingRelayed, sendingMember.VoiceClient.Player.Handle, sendingMember.VoiceClient.TeamSpeakName, this.Name, false, false, position, false, new string[0]);
                    }
                }
            }
        }

        internal void Send(VoiceClient voiceClient, bool isSending)
        {
            if (!this.TryGetMember(voiceClient, out RadioChannelMember radioChannelMember))
                return;

            if (VoiceManager.Instance.Configuration.EnableRadioHardcoreMode && isSending && this.Members.Any(m => m.VoiceClient != voiceClient && m.IsSending))
            {
                voiceClient.TriggerEvent(Event.SaltyChat_ChannelInUse, this.Name);

                return;
            }

            bool stateChanged = radioChannelMember.IsSending != isSending;
            radioChannelMember.IsSending = isSending;

            RadioChannelMember[] channelMembers = this.Members;
            RadioChannelMember[] onSpeaker = channelMembers.Where(m => m.IsSpeakerEnabled && m.VoiceClient != voiceClient).ToArray();

            CitizenFX.Core.Vector3 position = voiceClient.Player.GetPosition();

            if (onSpeaker.Length > 0)
            {
                string[] channelMemberNames = onSpeaker.Select(m => m.VoiceClient.TeamSpeakName).ToArray();

                foreach (VoiceClient remoteClient in VoiceManager.Instance.VoiceClients)
                {
                    remoteClient.TriggerEvent(Event.SaltyChat_IsSendingRelayed, voiceClient.Player.Handle, voiceClient.TeamSpeakName, this.Name, isSending, stateChanged, position, this.IsMember(remoteClient), channelMemberNames);
                }
            }
            else
            {
                foreach (RadioChannelMember member in channelMembers)
                {
                    member.VoiceClient.TriggerEvent(Event.SaltyChat_IsSending, voiceClient.Player.Handle, voiceClient.TeamSpeakName, this.Name, isSending, stateChanged, position);
                }
            }
        }
        #endregion

        #region Helper
        internal bool TryGetMember(VoiceClient voiceClient, out RadioChannelMember radioChannelMember)
        {
            radioChannelMember = this.Members.FirstOrDefault(m => m.VoiceClient == voiceClient);

            return radioChannelMember != null;
        }
        #endregion
    }

    public class RadioChannelMember
    {
        internal RadioChannel RadioChannel { get; }
        internal VoiceClient VoiceClient { get; }
        internal bool IsPrimary { get; }
        internal bool IsSending { get; set; }
        internal bool IsSpeakerEnabled { get; set; }

        internal RadioChannelMember(RadioChannel radioChannel, VoiceClient voiceClient, bool isPrimary)
        {
            this.RadioChannel = radioChannel;
            this.VoiceClient = voiceClient;
            this.IsPrimary = isPrimary;
            this.IsSpeakerEnabled = voiceClient.IsRadioSpeakerEnabled;
        }
    }
}
