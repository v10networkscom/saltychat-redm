using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SaltyShared;

namespace SaltyServer
{
    public class VoiceClient
    {
        #region Props/Fields
        internal CitizenFX.Core.Player Player { get; set; }
        internal string TeamSpeakName { get; set; }

        private float _voiceRange;
        internal float VoiceRange
        {
            get => this._voiceRange;
            set
            {
                this._voiceRange = value;

                this.Player.State.Set(State.SaltyChat_VoiceRange, value, true);
            }
        }

        private bool _isAlive;
        internal bool IsAlive
        {
            get => this._isAlive;
            set
            {
                this._isAlive = value;

                this.Player.State.Set(State.SaltyChat_IsAlive, value, true);
            }
        }

        private bool _isRadioSpeakerEnabled = false;
        internal bool IsRadioSpeakerEnabled
        {
            get => this._isRadioSpeakerEnabled;
            set
            {
                this._isRadioSpeakerEnabled = value;

                this.Player.TriggerEvent(Event.SaltyChat_SetRadioSpeaker, this._isRadioSpeakerEnabled);

                foreach (RadioChannelMember radioChannelMembership in VoiceManager.Instance.GetPlayerRadioChannelMembership(this))
                {
                    radioChannelMembership.RadioChannel.SetSpeaker(this, value);
                }
            }
        }
        #endregion

        #region CTOR
        internal VoiceClient(CitizenFX.Core.Player player, string teamSpeakName, float voiceRange, bool isAlive)
        {
            this.Player = player;
            this.TeamSpeakName = teamSpeakName;
            this.VoiceRange = voiceRange;
            this.IsAlive = isAlive;

            this.Player.State.Set(State.SaltyChat_TeamSpeakName, this.TeamSpeakName, true);
        }
        #endregion

        #region Methods
        internal void TriggerEvent(string eventName, params object[] args) => this.Player.TriggerEvent(eventName, args);

        internal void SetPhoneSpeakerEnabled(bool isEnabled)
        {
            foreach (PhoneCallMember phoneCallMembership in VoiceManager.Instance.GetPlayerPhoneCallMembership(this))
            {
                phoneCallMembership.PhoneCall.SetSpeaker(this, isEnabled);
            }
        }
        #endregion
    }
}
