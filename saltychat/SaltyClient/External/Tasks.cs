using CitizenFX.Core;
using CitizenFX.Core.Native;
using System;
using System.Threading.Tasks;

namespace RedM.External
{
    public class Tasks
    {
        private Ped Ped { get; }

        internal Tasks(Ped ped)
        {
            Ped = ped;
        }

        /// <summary>
        /// Custom addition for facial animation native calls
        /// </summary>
        /// <param name="animDict"></param>
        /// <param name="animName"></param>
        public async Task PlayFacialAnimation(string animDict, string animName)
        {
            if (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, animDict))
            {
                Function.Call(Hash.REQUEST_ANIM_DICT, animDict);
            }

            var end = DateTime.UtcNow.AddMilliseconds(1000f);
            while (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, animDict))
            {
                if (DateTime.UtcNow >= end)
                {
                    return;
                }
                await BaseScript.Delay(0);
            }

            Function.Call(Hash.SET_FACIAL_IDLE_ANIM_OVERRIDE, Ped.Handle, animName, animDict);
        }

        /// <summary>
        /// Custom addition for facial animation native calls
        /// </summary>
        /// <param name="animDict"></param>
        /// <param name="animName"></param>
        public void ClearFacialAnimation()
        {
            Function.Call(Hash.CLEAR_FACIAL_IDLE_ANIM_OVERRIDE, Ped.Handle);
        }
    }
}
