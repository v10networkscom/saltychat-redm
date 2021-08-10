using CitizenFX.Core;
using SaltyShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SaltyServer
{
    public class PhoneCall
    {
        #region Props/Fields
        public string Identifier { get; set; }

        public PhoneCallMember[] Members => this._members.ToArray();
        public List<PhoneCallMember> _members = new List<PhoneCallMember>();
        #endregion

        #region CTOR
        public PhoneCall(string identifier)
        {
            this.Identifier = identifier;
        }
        #endregion

        #region Methods
        public bool IsMember(VoiceClient voiceClient)
        {
            return this.Members.Any(m => m.VoiceClient == voiceClient);
        }

        public void AddMember(VoiceClient voiceClient)
        {
            List<PhoneCallMember> callMembers = this.Members.ToList();
            PhoneCallMember callMember = new PhoneCallMember(this, voiceClient);

            if (callMembers.Any(m => m.VoiceClient == voiceClient))
                return;

            lock (this._members)
            {
                this._members.Add(callMember);
            }

            callMembers.Add(callMember);

            if (callMembers.Count == 1)
                return;

            string handle = voiceClient.Player.Handle;
            string tsName = voiceClient.TeamSpeakName;
            Vector3 position = voiceClient.Player.GetPosition();
            string[] relays = callMembers.Where(m => m.IsSpeakerEnabled).Select(m => m.VoiceClient.TeamSpeakName).ToArray();

            foreach (PhoneCallMember member in callMembers.Where(m => m.VoiceClient != voiceClient))
            {
                voiceClient.TriggerEvent(Event.SaltyChat_EstablishCall, member.VoiceClient.Player.Handle, member.VoiceClient.TeamSpeakName, member.VoiceClient.Player.GetPosition());

                if (relays.Length == 0)
                    member.VoiceClient.TriggerEvent(Event.SaltyChat_EstablishCall, handle, tsName, position);
            }

            if (relays.Length > 0)
            {
                foreach (VoiceClient client in VoiceManager.Instance.VoiceClients)
                {
                    client.TriggerEvent(
                        Event.SaltyChat_EstablishCallRelayed,
                        handle,
                        tsName,
                        position,
                        callMembers.Any(m => m.VoiceClient == client),
                        relays
                    );
                }
            }
        }

        public void RemoveMember(VoiceClient voiceClient)
        {
            List<PhoneCallMember> callMembers = this.Members.ToList();
            PhoneCallMember callMember = callMembers.FirstOrDefault(m => m.VoiceClient == voiceClient);

            if (callMember == null)
                return;

            lock (this._members)
            {
                this._members.Remove(callMember);
            }

            callMembers.Remove(callMember);

            string handle = voiceClient.Player.Handle;
            string[] relays = callMembers.Where(m => m.IsSpeakerEnabled).Select(m => m.VoiceClient.TeamSpeakName).ToArray();

            // if removed member was the only one with speaker enabled
            if (relays.Length == 0 && callMember.IsSpeakerEnabled)
            {
                foreach (VoiceClient client in VoiceManager.Instance.VoiceClients)
                {
                    // if client is the removed player: end all calls of other call members
                    if (client == voiceClient)
                    {
                        foreach (PhoneCallMember member in callMembers)
                        {
                            voiceClient.TriggerEvent(Event.SaltyChat_EndCall, member.VoiceClient.Player.Handle);
                        }
                    }
                    // if client is a remaining member: end the call of removed member
                    else if (callMembers.Any(m => m.VoiceClient == client))
                    {
                        client.TriggerEvent(Event.SaltyChat_EndCall, handle);
                    }
                    // if anyone else: end the relayed calls
                    else
                    {
                        foreach (PhoneCallMember member in callMembers)
                        {
                            client.TriggerEvent(Event.SaltyChat_EndCall, member.VoiceClient.Player.Handle);
                        }
                    }
                }
            }
            // if any remaining member has speaker enabled
            else if (relays.Length > 0)
            {
                foreach (VoiceClient client in VoiceManager.Instance.VoiceClients)
                {
                    // end the call for removed player
                    client.TriggerEvent(Event.SaltyChat_EndCall, handle);

                    // update relays in case removed player also relayed them
                    if (callMember.IsSpeakerEnabled || client == voiceClient)
                    {
                        foreach (PhoneCallMember member in callMembers)
                        {
                            client.TriggerEvent(
                                Event.SaltyChat_EstablishCallRelayed,
                                member.VoiceClient.Player.Handle,
                                member.VoiceClient.TeamSpeakName,
                                member.VoiceClient.Player.GetPosition(),
                                callMembers.Any(m => m.VoiceClient == client),
                                relays
                            );
                        }
                    }
                }
            }
            // if no one had speaker enabled
            else
            {
                foreach (PhoneCallMember member in callMembers)
                {
                    voiceClient.TriggerEvent(Event.SaltyChat_EndCall, member.VoiceClient.Player.Handle);

                    member.VoiceClient.TriggerEvent(Event.SaltyChat_EndCall, handle);
                }
            }
        }

        public void SetSpeaker(VoiceClient voiceClient, bool isEnabled)
        {
            PhoneCallMember[] callMembers = this.Members;
            PhoneCallMember callMember = callMembers.FirstOrDefault(m => m.VoiceClient == voiceClient);

            if (callMember == null || callMember.IsSpeakerEnabled == isEnabled)
                return;

            string[] replays = callMembers.Where(m => m.IsSpeakerEnabled).Select(m => m.VoiceClient.TeamSpeakName).ToArray();

            if (replays.Length == 0)
            {
                foreach (VoiceClient client in VoiceManager.Instance.VoiceClients)
                {
                    if (client == voiceClient || callMembers.Any(m => m.VoiceClient == client))
                    {
                        continue;
                    }
                    else
                    {
                        foreach (PhoneCallMember member in callMembers)
                        {
                            client.TriggerEvent(Event.SaltyChat_EndCall, member.VoiceClient.Player.Handle);
                        }
                    }
                }
            }
            else
            {
                foreach (VoiceClient client in VoiceManager.Instance.VoiceClients)
                {
                    if (client == voiceClient || callMembers.Any(m => m.VoiceClient == client))
                    {
                        continue;
                    }
                    else
                    {
                        foreach (PhoneCallMember member in callMembers)
                        {
                            client.TriggerEvent(Event.SaltyChat_EstablishCallRelayed, member.VoiceClient.Player.Handle, member.VoiceClient.TeamSpeakName, member.VoiceClient.Player.GetPosition(), false, replays);
                        }
                    }
                }
            }
        }
        #endregion

        #region Helper
        public bool TryGetMember(VoiceClient voiceClient, out PhoneCallMember member)
        {
            member = this.Members.FirstOrDefault(m => m.VoiceClient == voiceClient);

            return member != null;
        }
        #endregion
    }

    public class PhoneCallMember
    {
        internal PhoneCall PhoneCall { get; }
        internal VoiceClient VoiceClient { get; }
        internal bool IsSpeakerEnabled { get; set; }

        internal PhoneCallMember(PhoneCall phoneCall, VoiceClient voiceClient)
        {
            this.PhoneCall = phoneCall;
            this.VoiceClient = voiceClient;
            this.IsSpeakerEnabled = false;
        }
    }
}
