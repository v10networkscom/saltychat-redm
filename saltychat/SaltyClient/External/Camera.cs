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
    public sealed class Camera : PoolObject
    {

        public Camera(int handle) : base(handle)
        {
        }

        public bool IsActive
        {
            get => Function.Call<bool>(Hash.IS_CAM_ACTIVE, Handle);
            set => Function.Call(Hash.SET_CAM_ACTIVE, Handle, value);
        }

        public Vector3 Position
        {
            get => Function.Call<Vector3>(Hash.GET_CAM_COORD, Handle);
            set => Function.Call((Hash)0xF9EE7D419EE49DE6, Handle, value.X, value.Y, value.Z);
        }

        public Vector3 Rotation
        {
            get => Function.Call<Vector3>(Hash.GET_CAM_ROT, Handle, 2);
            set => Function.Call((Hash)0x63DFA6810AD78719, Handle, value.X, value.Y, value.Z, 2);
        }
        public bool IsInterpolating => Function.Call<bool>(Hash.IS_CAM_INTERPOLATING, Handle);

        public Vector3 ForwardVector => GameMath.RotationToDirection(Rotation);

        public Vector3 Direction
        {
            get => ForwardVector;
            set {
                value.Normalize();
                Vector3 vector1 = new Vector3(value.X, value.Y, 0f);
                Vector3 vector2 = new Vector3(value.Z, vector1.Length(), 0f);
                Vector3 vector3 = Vector3.Normalize(vector2);
                Rotation = new Vector3((float)(System.Math.Atan2(vector3.X, vector3.Y) * 57.295779513082323f), Rotation.Y, (float)(System.Math.Atan2(value.X, value.Y) * -57.295779513082323f));
            }
        }

        public float FieldOfView
        {
            get => Function.Call<float>(Hash.GET_CAM_FOV, Handle);
            set => Function.Call(Hash.SET_CAM_FOV, Handle, value);
        }

        public void PointAt(int entityHandle, Vector3 offset = default)
        {
            Function.Call(Hash.POINT_CAM_AT_ENTITY, Handle, entityHandle, offset.X, offset.Y, offset.Z, true);
        }

        public void PointAt(Vector3 target)
        {
            Function.Call(Hash.POINT_CAM_AT_COORD, Handle, target.X, target.Y, target.Z);
        }

        public void InterpTo(Camera to, int duration, bool easePosition = true, bool easeRotation = true)
        {
            Function.Call(Hash.SET_CAM_ACTIVE_WITH_INTERP, to.Handle, Handle, duration, easePosition ? 1 : 0, easeRotation ? 1 : 0);
        }

        public override bool Exists()
        {
            return Function.Call<bool>(Hash.DOES_CAM_EXIST, Handle);
        }

        public override void Delete()
        {
            Function.Call(Hash.DESTROY_CAM, Handle);
        }
    }

    public static class GameplayCamera
    {
        public static Vector3 Position => API.GetGameplayCamCoord();

        public static Vector3 Rotation => Function.Call<Vector3>(Hash.GET_GAMEPLAY_CAM_ROT, 2);

        public static float FieldOfView => Function.Call<float>(Hash.GET_GAMEPLAY_CAM_FOV);

        public static Vector3 ForwardVector
        {
            get {
                var rotation = (float)(Math.PI / 180f) * Rotation;
                return Vector3.Normalize(new Vector3((float)-Math.Sin(rotation.Z) * (float)Math.Abs(Math.Cos(rotation.X)), (float)Math.Cos(rotation.Z) * (float)Math.Abs(Math.Cos(rotation.X)), (float)Math.Sin(rotation.X)));
            }
        }

        public static float RelativePitch => Function.Call<float>(Hash.GET_GAMEPLAY_CAM_RELATIVE_PITCH);

        public static float RelativeHeading => Function.Call<float>(Hash.GET_GAMEPLAY_CAM_RELATIVE_HEADING);

        public static bool IsRendering => Function.Call<bool>(Hash.IS_GAMEPLAY_CAM_RENDERING);

        public static bool IsLookingBehind => Function.Call<bool>(Hash.IS_GAMEPLAY_CAM_LOOKING_BEHIND);

        public static bool IsShaking => Function.Call<bool>(Hash.IS_GAMEPLAY_CAM_SHAKING);
    }
}
