using System;
using System.Collections.Generic;
using System.Text;
using CitizenFX.Core;

namespace SaltyServer
{
    internal static class Util
    {
        // Sometimes OneSync has issues getting the player ped, in which case we return (0, 0, 0)
        internal static Vector3 GetPosition(this Player player)
        {
            Ped ped = player.Character;

            if (ped == null)
                return Vector3.Zero;
            else
                return ped.Position;
        }
    }
}
