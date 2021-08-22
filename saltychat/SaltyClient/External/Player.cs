/*
License (MIT)

Copyright 2019 Mooshe
https://github.com/MoosheTV/redm-external

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Threading.Tasks;
using CitizenFX.Core.Native;

namespace RedM.External
{
    public sealed class Player : INativeValue, IEquatable<Player>
    {
        public int Handle { get; private set; }
        public override ulong NativeValue
        {
            get { return (ulong)this.Handle; }
            set { this.Handle = unchecked((int)value); }
        }

        private Ped _ped;

        public Player(int handle)
        {
            this.Handle = handle;
        }

        public bool Exists()
        {
            return API.NetworkIsPlayerActive(this.Handle);
        }

        public Ped Character
        {
            get {
                if (!ReferenceEquals(_ped, null) && _ped.Handle == this.Handle)
                {
                    return this._ped;
                }

                this._ped = new Ped(API.GetPlayerPed(this.Handle));
                return this._ped;
            }
        }

        public string Name => API.GetPlayerName(this.Handle);
        public int ServerId => API.GetPlayerServerId(this.Handle);

        public StateBag State => new StateBag("player:" + this.ServerId);

        /// <summary>
		/// Gets a value indicating whether this <see cref="Player"/> is alive.
		/// </summary>
		/// <value>
		///   <c>true</c> if this <see cref="Player"/> is alive; otherwise, <c>false</c>.
		/// </value>
		public bool IsAlive
        {
            get
            {
                return !this.IsDead;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Player"/> is dead.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this <see cref="Player"/> is dead; otherwise, <c>false</c>.
        /// </value>
        public bool IsDead
        {
            get
            {
                return API.IsPlayerDead(this.Handle);
            }
        }

        public bool Equals(Player player)
        {
            return !ReferenceEquals(player, null) && this.Handle == player.Handle;
        }

        public override bool Equals(object obj)
        {
            return !ReferenceEquals(obj, null) && obj.GetType() == this.GetType() && this.Equals((Entity)obj);
        }

        public override int GetHashCode()
        {
            return this.Handle.GetHashCode();
        }

        public static bool operator ==(Player left, Player right)
        {
            return ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.Equals(right);
        }

        public static bool operator !=(Player left, Player right)
        {
            return !(left == right);
        }
    }
}
