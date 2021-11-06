﻿namespace SaltyShared
{
    public class Event
    {
        #region Plugin
        public const string SaltyChat_Initialize = "SaltyChat_Initialize";
        public const string SaltyChat_CheckVersion = "SaltyChat_CheckVersion";
        public const string SaltyChat_UpdateVoiceRange = "SaltyChat_UpdateVoiceRange";
        public const string SaltyChat_RemoveClient = "SaltyChat_RemoveClient";
        #endregion

        #region State Change
        public const string SaltyChat_PluginStateChanged = "SaltyChat_PluginStateChanged";
        public const string SaltyChat_TalkStateChanged = "SaltyChat_TalkStateChanged";
        public const string SaltyChat_VoiceRangeChanged = "SaltyChat_VoiceRangeChanged";
        public const string SaltyChat_MicStateChanged = "SaltyChat_MicStateChanged";
        public const string SaltyChat_MicEnabledChanged = "SaltyChat_MicEnabledChanged";
        public const string SaltyChat_SoundStateChanged = "SaltyChat_SoundStateChanged";
        public const string SaltyChat_SoundEnabledChanged = "SaltyChat_SoundEnabledChanged";
        public const string SaltyChat_RadioTrafficStateChanged = "SaltyChat_RadioTrafficStateChanged";
        #endregion

        #region Proximity
        public const string SaltyChat_SetVoiceRange = "SaltyChat_SetVoiceRange";
        #endregion

        #region Phone
        public const string SaltyChat_EstablishCall = "SaltyChat_EstablishCall";
        public const string SaltyChat_EstablishCallRelayed = "SaltyChat_EstablishCallRelayed";
        public const string SaltyChat_EndCall = "SaltyChat_EndCall";
        #endregion

        #region Radio
        public const string SaltyChat_RadioChannelMemberUpdated = "SaltyChat_RadioChannelMemberUpdated";
        public const string SaltyChat_SetRadioSpeaker = "SaltyChat_SetRadioSpeaker";
        public const string SaltyChat_ChannelInUse = "SaltyChat_ChannelInUse";
        public const string SaltyChat_IsSending = "SaltyChat_IsSending";
        public const string SaltyChat_IsSendingRelayed = "SaltyChat_IsSendingRelayed";
        public const string SaltyChat_SetRadioChannel = "SaltyChat_SetRadioChannel";
        public const string SaltyChat_UpdateRadioTowers = "SaltyChat_UpdateRadioTowers";
        #endregion

        #region Megaphone
        public const string SaltyChat_IsUsingMegaphone = "SaltyChat_IsUsingMegaphone ";
        #endregion
    }
}
