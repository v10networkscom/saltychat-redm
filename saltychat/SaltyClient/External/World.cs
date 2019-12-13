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
using CitizenFX.Core;
using CitizenFX.Core.Native;

namespace RedM.External
{
    public static class World
    {

        private static WeatherType _currentWeather;
        public static WeatherType CurrentWeather
        {
            get => GetCurrentWeatherType();
            set {
                _currentWeather = value;
                Function.Call(Hash._SET_WEATHER_TYPE_TRANSITION, GetCurrentWeatherType(), value, 1f);
            }
        }

        private static WeatherType _nextWeather;
        public static WeatherType NextWeather
        {
            get {
                GetCurrentWeatherType();
                return _nextWeather;
            }
        }

        public static int CurrentDay => API.GetClockDayOfMonth();

        public static int CurrentMonth => API.GetClockMonth();

        public static int CurrentYear => API.GetClockYear();

        public static bool IsWaypointActive => Function.Call<bool>((Hash)0x202B1BBFC6AB5EE4);

        public static Vector3 WaypointPosition => Function.Call<Vector3>((Hash)0x29B30D07C3F7873B);


        public static TimeSpan CurrentTime
        {
            get => new TimeSpan(API.GetClockHours(), API.GetClockMinutes(), API.GetClockSeconds());
            set => API.SetClockTime(value.Hours, value.Minutes, value.Seconds);
        }

        public static void SetClockDate(int day, int month, int year)
        {
            API.SetClockDate(day, month, year);
        }

        private static WeatherType GetCurrentWeatherType()
        {
            var currentWeather = new OutputArgument();
            var nextWeather = new OutputArgument();
            var percent = new OutputArgument();
            Function.Call(Hash._GET_WEATHER_TYPE_TRANSITION, currentWeather, nextWeather, percent);
            _currentWeather = currentWeather.GetResult<WeatherType>();
            _nextWeather = nextWeather.GetResult<WeatherType>();
            var pct = percent.GetResult<float>();
            if (pct >= 0.5f)
            {
                return _nextWeather;
            }
            return _currentWeather;
        }

        public static Camera RenderingCamera
        {
            get => new Camera(API.GetRenderingCam());
            set {
                if (value == null)
                {
                    Function.Call(Hash.RENDER_SCRIPT_CAMS, false, false, 3000, true, false);
                }
                else
                {
                    value.IsActive = true;
                    Function.Call(Hash.RENDER_SCRIPT_CAMS, true, false, 3000, true, false);
                }
            }
        }

        public static Camera CreateCamera(Vector3 pos, Vector3 rot, float fov = -1f)
        {
            if (fov <= 0f)
            {
                fov = Function.Call<float>(Hash.GET_GAMEPLAY_CAM_FOV);
            }
            var handle = Function.Call<int>(Hash.CREATE_CAM_WITH_PARAMS, "DEFAULT_SCRIPTED_CAMERA", pos.X, pos.Y,
                pos.Z, rot.X, rot.Y, rot.Z, fov, true, 2);
            return new Camera(handle);
        }

        public static async Task<Ped> CreatePed(PedHash hash, Vector3 position, float heading = 0f, bool isNet = true, bool isMission = true)
        {
            var model = new Model(hash);
            if (!await model.Request(4000))
            {
                return null;
            }
            var id = Function.Call<int>((Hash)0xD49F9B0955C367DE, hash, position.X, position.Y, position.Z, heading,
                isNet, !isMission, 0, 0);
            Function.Call((Hash)0x283978A15512B2FE, id, true);
            return id == 0 ? null : (Ped)Entity.FromHandle(id);
        }

        public static Vector2 World3dToScreen2d(Vector3 pos)
        {
            OutputArgument outX = new OutputArgument(), outY = new OutputArgument();
            return Function.Call<bool>(Hash.GET_SCREEN_COORD_FROM_WORLD_COORD, pos.X, pos.Y, pos.Z, outX, outY) ?
                new Vector2(outX.GetResult<float>(), outY.GetResult<float>()) : Vector2.Zero;
        }
    }

    [Flags]
    public enum IntersectOptions
    {
        Everything = -1,
        Map = 1,
        MissionEntities,
        Peds1 = 12,
        Objects = 16,
        Unk1 = 32,
        Unk2 = 64,
        Unk3 = 128,
        Vegetation = 256,
        Unk4 = 512
    }

    public enum WeatherType : uint
    {
        Overcast = 0xBB898D2D,
        Rain = 0x54A69840,
        Fog = 0xD61BDE01,
        Snowlight = 0x23FB812B,
        Thunder = 0xB677829F,
        Blizzard = 0x27EA2814,
        Snow = 0xEFB6EFF6,
        Misty = 0x5974E8E5,
        Sunny = 0x614A1F91,
        HighPressure = 0xF5A87B65,
        Clearing = 0x6DB1A50D,
        Sleet = 0xCA71D7C,
        Drizzle = 0x995C7F44,
        Shower = 0xE72679D5,
        SnowClearing = 0x641DFC11,
        OvercastDark = 0x19D4F1D9,
        Thunderstorm = 0x7C1C4A13,
        Sandstorm = 0xB17F6111,
        Hurricane = 0x320D0951,
        Hail = 0x75A9E268,
        Whiteout = 0x2B402288,
        GroundBlizzard = 0x7F622122
    }
}
