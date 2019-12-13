using System;
using System.Threading.Tasks;
using CitizenFX.Core.Native;

namespace RedM.External
{
    public sealed class Player : INativeValue, IEquatable<Player>
    {
        public int Handle { get; private set; }

        private Ped _ped;

        public Player(int handle)
        {
            Handle = handle;
        }

        public bool Exists()
        {
            return Function.Call<bool>(Hash.NETWORK_IS_PLAYER_ACTIVE, Handle);
        }

        public Ped Character
        {
            get {
                if (!ReferenceEquals(_ped, null) && _ped.Handle == Handle)
                {
                    return _ped;
                }
                _ped = new Ped(API.GetPlayerPed(Handle));
                return _ped;
            }
        }

        public string Name => Function.Call<string>(Hash.GET_PLAYER_NAME, Handle);
        public int ServerId => Function.Call<int>(Hash.GET_PLAYER_SERVER_ID, Handle);

        public async Task<bool> ChangeModel(PedHash hash, int timeout = 3000)
        {
            var model = new Model(hash);

            await model.Request(timeout);

            if (!model.IsLoaded)
            {
                return false;
            }

            Function.Call(Hash.SET_PLAYER_MODEL, Handle, hash, false);
            Function.Call((Hash)0x283978A15512B2FE, Character.Handle, true);
            model.MarkAsNoLongerNeeded();
            return true;
        }

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
                return !IsDead;
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
                return API.IsPlayerDead(Handle);
            }
        }

        public bool Equals(Player other)
        {
            return other != null && ReferenceEquals(this, other) && other.Handle == Handle;
        }

        public override bool Equals(object obj)
        {
            return obj is Player other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Handle.GetHashCode();
        }

        public override ulong NativeValue
        {
            get => (ulong)Handle;
            set => Handle = unchecked((int)value);
        }
    }
}
