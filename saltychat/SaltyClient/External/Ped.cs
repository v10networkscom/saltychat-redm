/*
License (MIT)

Copyright 2019 Mooshe
https://github.com/MoosheTV/redm-external

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using CitizenFX.Core;
using CitizenFX.Core.Native;

namespace RedM.External
{
    public class Ped : Entity
    {
        public Ped(int handle) : base(handle)
        {

        }

        public Gender Gender => Function.Call<bool>(Hash.IS_PED_MALE, Handle) ? Gender.Male : Gender.Female;
        public bool IsJumping => Function.Call<bool>(Hash.IS_PED_JUMPING, Handle);
        public bool IsInMeleeCombat => Function.Call<bool>(Hash.IS_PED_IN_MELEE_COMBAT, Handle);
        public bool IsInCombat => Function.Call<bool>(Hash.IS_PED_IN_COMBAT, Handle);
        public bool IsClimbing => Function.Call<bool>(Hash.IS_PED_CLIMBING, Handle);
        public bool IsPlayer => Function.Call<bool>(Hash.IS_PED_A_PLAYER, Handle);
        public bool IsHuman => Function.Call<bool>(Hash.IS_PED_HUMAN, Handle);
        public bool IsFleeing => Function.Call<bool>(Hash.IS_PED_FLEEING, Handle);
        public bool IsGettingUp => Function.Call<bool>(Hash.IS_PED_GETTING_UP, Handle);
        public bool IsGettingIntoVehicle => Function.Call<bool>(Hash.IS_PED_GETTING_INTO_A_VEHICLE, Handle);
        public bool IsInVehicle => Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, Handle);
        public bool IsOnFoot => Function.Call<bool>(Hash.IS_PED_ON_FOOT, Handle);
        public bool IsOnMount => Function.Call<bool>(Hash.IS_PED_ON_MOUNT, Handle);
    }

    public enum Gender
    {
        Male,
        Female
    }

    public enum PedCore
    {
        Health = 0,
        Stamina,
        DeadEye
    }

}
