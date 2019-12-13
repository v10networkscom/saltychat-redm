/*
License (MIT)

Copyright 2019 Mooshe
https://github.com/MoosheTV/redm-external

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Security;
using CitizenFX.Core;
using CitizenFX.Core.Native;

namespace RedM.External
{
    public class Entity : PoolObject, IEquatable<Entity>, ISpatial
    {
        public Entity(int handle) : base(handle)
        {
        }

        public int Health
        {
            get => API.GetEntityHealth(Handle);
        }

        public int MaxHealth
        {
            get => Function.Call<int>(Hash.GET_ENTITY_MAX_HEALTH, Handle) - 100;
            set => API.SetEntityMaxHealth(Handle, value + 100);
        }

        public bool IsDead => API.IsEntityDead(Handle);

        public bool IsAlive => !IsDead;

        public Model Model => new Model(Function.Call<int>(Hash.GET_ENTITY_MODEL, Handle));

        public virtual Vector3 Position
        {
            get => Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, Handle);
            set => Function.Call(Hash.SET_ENTITY_COORDS, Handle, value.X, value.Y, value.Z, false, false, false, true);
        }

        public Vector3 PositionNoOffset
        {
            set => Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Handle, value.X, value.Y, value.Z, true, true, true);
        }

        public virtual Vector3 Rotation
        {
            get => Function.Call<Vector3>(Hash.GET_ENTITY_ROTATION, Handle);
            set => Function.Call(Hash.SET_ENTITY_ROTATION, Handle, value.X, value.Y, value.Z, 2, true);
        }

        public float Heading
        {
            get => Function.Call<float>(Hash.GET_ENTITY_HEADING, Handle);
            set => Function.Call(Hash.SET_ENTITY_ROTATION, Handle, value);
        }

        public Vector3 ForwardVector => Function.Call<Vector3>(Hash.GET_ENTITY_FORWARD_VECTOR, Handle);

        public bool IsPositionFrozen
        {
            set => Function.Call(Hash.FREEZE_ENTITY_POSITION, Handle, value);
        }

        public Vector3 Velocity
        {
            get => Function.Call<Vector3>(Hash.GET_ENTITY_VELOCITY, Handle);
            set => Function.Call(Hash.SET_ENTITY_VELOCITY, Handle, value.X, value.Y, value.Z);
        }

        public bool HasGravity
        {
            set => Function.Call(Hash.SET_ENTITY_HAS_GRAVITY, Handle, value);
        }

        public float HeightAboveGround => Function.Call<float>(Hash.GET_ENTITY_HEIGHT_ABOVE_GROUND, Handle);

        public int LodDistance
        {
            get => Function.Call<int>(Hash.GET_ENTITY_LOD_DIST, Handle);
            set => Function.Call(Hash.SET_ENTITY_LOD_DIST, Handle, value);
        }

        public bool IsVisible
        {
            get => Function.Call<bool>(Hash.IS_ENTITY_VISIBLE, Handle);
            set => Function.Call(Hash.SET_ENTITY_VISIBLE, Handle, value);
        }

        public bool IsInvincible
        {
            set => Function.Call(Hash.SET_ENTITY_INVINCIBLE, Handle, value);
        }

        public bool CanRagdoll
        {
            get => Function.Call<bool>(Hash.CAN_PED_RAGDOLL, Handle);
            set => Function.Call(Hash.SET_PED_CAN_RAGDOLL, Handle, value);
        }

        public EntityType EntityType => Function.Call<EntityType>(Hash.GET_ENTITY_TYPE, Handle);
        public bool IsOccluded => Function.Call<bool>(Hash.IS_ENTITY_OCCLUDED, Handle);

        public bool IsOnScreen => Function.Call<bool>(Hash.IS_ENTITY_ON_SCREEN, Handle);

        public bool IsUpright => Function.Call<bool>(Hash.IS_ENTITY_UPRIGHT, Handle);

        public bool IsUpsideDown => Function.Call<bool>(Hash.IS_ENTITY_UPSIDEDOWN, Handle);

        public bool IsInAir => Function.Call<bool>(Hash.IS_ENTITY_IN_AIR, Handle);

        public bool IsInWater => Function.Call<bool>(Hash.IS_ENTITY_IN_WATER, Handle);

        public bool IsOnFire => Function.Call<bool>(Hash.IS_ENTITY_ON_FIRE, Handle);

        public bool IsPersistent
        {
            get => Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, Handle);
        }

        public float Speed => Function.Call<float>(Hash.GET_ENTITY_SPEED, Handle);

        public Vector3 SpeedVector => Function.Call<Vector3>(Hash.GET_ENTITY_SPEED_VECTOR, Handle);

        public Vector3 GetOffsetPosition(Vector3 relativeCoords)
        {
            return Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_IN_WORLD_COORDS, Handle, relativeCoords.X,
                relativeCoords.Y, relativeCoords.Z);
        }

        public Vector3 GetPositionOffset(Vector3 worldCoords)
        {
            return Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_GIVEN_WORLD_COORDS, Handle, worldCoords.X,
                worldCoords.Y, worldCoords.Z);
        }

        public override bool Exists()
        {
            return Function.Call<bool>(Hash.DOES_ENTITY_EXIST, Handle);
        }

        public override void Delete()
        {
            Function.Call(Hash.DELETE_ENTITY, Handle);
        }

        public static Entity FromNetworkId(int netId)
        {
            return FromHandle(Function.Call<int>(Hash.NETWORK_GET_ENTITY_FROM_NETWORK_ID, netId));
        }

        public static Entity FromHandle(int handle)
        {
            switch (Function.Call<EntityType>(Hash.GET_ENTITY_TYPE, handle))
            {
                case EntityType.Ped:
                    return new Ped(handle);
                default:
                    return null;
            }
        }

        public bool Equals(Entity entity)
        {
            return !ReferenceEquals(entity, null) && Handle == entity.Handle;
        }
        public override bool Equals(object obj)
        {
            return !ReferenceEquals(obj, null) && obj.GetType() == GetType() && Equals((Entity)obj);
        }

        public override int GetHashCode()
        {
            return Handle.GetHashCode();
        }

        public static bool operator ==(Entity left, Entity right)
        {
            return left?.Equals(right) ?? ReferenceEquals(right, null);
        }
        public static bool operator !=(Entity left, Entity right)
        {
            return !(left == right);
        }
    }

    public enum EntityType
    {
        Ped = 1,
        Vehicle,
        Object
    }
}
