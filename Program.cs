using System.Globalization;
using System.Numerics;
using ClickableTransparentOverlay;
using ImGuiNET;
using Memory;
using SharpDX.Direct3D11;
using System.Threading;
using System.Runtime.CompilerServices;

namespace swtor_ESP
{
    internal class Program : Overlay
    {
        Program() : base(2560, 1440) { }
        public static Program p = new Program();
        public static Mem m = new Mem();
        public static List<Entity> entList = new List<Entity> { };
        public bool isESPEnabled = false;
        public static string cameraAddrStr = "swtor.exe+01BFB168";
        //new entStuff
        public static string entListPtr = "swtor.exe+0x01BAE188,0x00,0x3F8,0x1A0";
        public static string entListPtrAddr = "";
        //old entHook
        public static string entlistAOB = "48 8B 01 48 8B 40 58 FF 15 ?? ?? ?? ?? 48 8B C8";
        public static UIntPtr codeCaveAddr = 0x0;
        public static UIntPtr entBaseAddr = 0x0;
        public static byte[] entlistHookBytes = { 0x48, 0x89, 0x0D, 0x0C, 0x00, 0x00, 0x00, 0x48, 0x8B, 0x01, 0x48, 0x8B, 0x40, 0x58 };
        public static uint entbaseOffset = 0x13;
        public static string entBaseAddrStr = "";
        public static string entlistAddrStr = "";
        public static bool entlistHooked = false;
        public static Vector3 camPos = new Vector3 { };

        static void Main()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Console.WriteLine("SWTOR-ESP Test");
            int PID = m.GetProcIdFromName("swtor.exe");
            if (PID == 0)
            {
                Console.WriteLine("Process not found. Exiting...");
                return;
            }
            Console.WriteLine($"Process found: {PID}");
            m.OpenProcess(PID);
            Console.WriteLine("Process opened successfully.");
            p.Run();
            Thread.Sleep(200);
            while (true) 
            {
                if(entlistAddrStr == "")
                {
                    AOBScan();
                }
                if (!entlistHooked)
                {
                    EntHook();
                }
                if(entlistAddrStr != "")
                {
                    //ReadEnts(); //old hook
                    AddEntsToList();
                }
            }
        }
        public static void AddEntsToList()
        {
            if (string.IsNullOrEmpty(entListPtrAddr) || entListPtrAddr == "00")
                return;
            for (int i = 0; i < 100; i++)
            {
                // Each entity pointer is probably at entListPtrAddr + i * 0x10 (common in game engines)
                long entBase = m.ReadMemory<long>($"{entListPtrAddr}+{i * 0x10:X}");
                if (entBase == 0)
                    continue;

                string entBaseAddrStr = entBase.ToString("X2");

                if (entList.Any(ent => ent.baseAddrStr == entBaseAddrStr))
                    continue;

                Entity nent = new Entity();
                nent.baseAddrStr = entBaseAddrStr;

                entList.Add(nent);
            }
            //Console.WriteLine($"Read {entList.Count} entities");
        }
        public static void UpdateEnts()
        {
            try
            {
                foreach (Entity ent in entList)
                {
                    ent.coords.X = m.ReadFloat($"{ent.baseAddrStr}+0x68");
                    ent.coords.Y = m.ReadFloat($"{ent.baseAddrStr}+0x6C");
                    ent.coords.Z = m.ReadFloat($"{ent.baseAddrStr}+0x70");
                    ent.magnitude = Vector3.Distance(camPos, ent.coords); // Calculate distance to camera
                }
            }
            catch { }

        }
        public static void EntHook()
        {
            //if(entlistAddrStr != "")
            //{
            //    UIntPtr codeCaveAddr = m.CreateCodeCave(entlistAddrStr, entlistHookBytes, 7, 240);
            //    entBaseAddr = codeCaveAddr + entbaseOffset;
            //    entBaseAddrStr = ConvertUintToStr(entBaseAddr);
            //    entlistHooked = true;
            //}
            if (entlistAddrStr != "")
            {
                var ptrAddr = m.Get64BitCode(entListPtr);
                entListPtrAddr = ptrAddr.ToString("X2");
                //Console.WriteLine(ptrAddr.ToString("X2"));
            }
        }
        public static void ReadEnts()
        {
            entBaseAddrStr = ConvertUintToStr(entBaseAddr);
            UInt64 entBuffer = (UInt64)m.ReadLong(entBaseAddrStr);
            entBaseAddrStr = ConvertUintToStr(entBuffer);
            //Console.WriteLine(entBaseAddrStr);
            Thread.Sleep(100);
        }
        protected override void Render()
        {
            DrawMenu();
            UpdateEnts();
            DrawBoxESP();
            //DrawTracelineESP();
            DrawBoxAtOrigin();
        }
        static void AOBScan()
        {
            entlistAddrStr = m.AoBScan(entlistAOB).Result.Sum().ToString("X2");
        }

        static void DrawMenu()
        {
            ImGui.Begin("SWTOR ESP");
            ImGui.Text("Hello, SWTOR!");
            ImGui.Checkbox("Enable ESP", ref p.isESPEnabled);
            ImGui.End();
        }
        static void DrawBoxESP()
        {
            if (!p.isESPEnabled)
                return;

            ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(2560, 1440), ImGuiCond.Always);
            ImGui.Begin("ESP Overlay",
                ImGuiWindowFlags.NoTitleBar
                | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.NoMouseInputs
                | ImGuiWindowFlags.NoScrollbar
            );
            ImDrawListPtr drawlist = ImGui.GetWindowDrawList();

            // Read camera position
            float camX = m.ReadFloat($"{cameraAddrStr},0x208");
            float camY = m.ReadFloat($"{cameraAddrStr},0x20C");
            float camZ = m.ReadFloat($"{cameraAddrStr},0x210");

            // Read camera angles
            float yaw = m.ReadFloat($"{cameraAddrStr},0x218");
            float pitchNorm = m.ReadFloat($"{cameraAddrStr},0x290");

            // Build view/projection matrices
            camPos = new Vector3(camX, camY, camZ);
            float[,] view = CreateViewMatrix(camPos, yaw, pitchNorm);
            float[,] proj = CreateProjectionMatrix(60f, 2560f / 1440f, 0.1f, 1000f);
            float[,] viewProj = MultiplyMatrices(view, proj);

            // Loop through entities instead of drawing a single fixed box
            try
            {
                foreach (Entity ent in entList)
                {
                    if (ent.coords == Vector3.Zero)
                        continue;

                    Vector2 screenCoords = WorldToScreen(ent.coords, viewProj, 2560, 1440);

                    if (screenCoords.X != -99)
                    {
                        drawlist.AddRect(
                            screenCoords - new Vector2(50 / ent.magnitude, 50 / ent.magnitude),
                            screenCoords + new Vector2(50, 50),
                            ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 1, 1))
                        );
                        drawlist.AddText(screenCoords, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)), $"{ent.magnitude}");
                    }
                }
            }
            catch { }

            ImGui.End();
        }
        static void DrawTracelineESP()
        {
            if (!p.isESPEnabled)
                return;

            ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(2560, 1440), ImGuiCond.Always);
            ImGui.Begin("ESP Overlay",
                ImGuiWindowFlags.NoTitleBar
                | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.NoMouseInputs
                | ImGuiWindowFlags.NoScrollbar
            );
            ImDrawListPtr drawlist = ImGui.GetWindowDrawList();

            // Read camera position
            float camX = m.ReadFloat($"{cameraAddrStr},0x208");
            float camY = m.ReadFloat($"{cameraAddrStr},0x20C");
            float camZ = m.ReadFloat($"{cameraAddrStr},0x210");

            // Read camera angles
            float yaw = m.ReadFloat($"{cameraAddrStr},0x218");
            float pitchNorm = m.ReadFloat($"{cameraAddrStr},0x290");

            // Build view/projection matrices
            Vector3 camPos = new Vector3(camX, camY, camZ);
            float[,] view = CreateViewMatrix(camPos, yaw, pitchNorm);
            float[,] proj = CreateProjectionMatrix(60f, 2560f / 1440f, 0.1f, 1000f);
            float[,] viewProj = MultiplyMatrices(view, proj);

            // Loop through entities instead of drawing a single fixed box
            foreach (Entity ent in entList)
            {
                if (ent.coords == Vector3.Zero)
                    continue;

                Vector2 screenCoords = WorldToScreen(ent.coords, viewProj, 2560, 1440);

                if (screenCoords.X != -99)
                {
                    drawlist.AddLine(
                        new Vector2(1280, 1440),
                        screenCoords + new Vector2(50, 50),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 1, 1))
                    );
                }
            }

            ImGui.End();
        }

        static Viewmatrix ReadViewmatrix()
        {
            float camX = m.ReadFloat($"{cameraAddrStr},0x208");
            float camY = m.ReadFloat($"{cameraAddrStr},0x20C");
            float camZ = m.ReadFloat($"{cameraAddrStr},0x210");

            float yaw = m.ReadFloat($"{cameraAddrStr},0x218");
            float pitch = m.ReadFloat($"{cameraAddrStr},0x290");

            float pitchRad = pitch * (float)(System.Math.PI / 2.0);

            float cosPitch = (float)System.Math.Cos(pitchRad);
            float sinPitch = (float)System.Math.Sin(pitchRad);
            float cosYaw = (float)System.Math.Cos(yaw);
            float sinYaw = (float)System.Math.Sin(yaw);

            Vector3 forward = new Vector3(
                cosPitch * sinYaw,
                -sinPitch,
                cosPitch * cosYaw
            );

            Vector3 eye = new Vector3(camX, camY, camZ);
            Vector3 target = eye + forward;
            Vector3 up = new Vector3(0, 1, 0);

            return CreateLookAtMatrix(eye, target, up);
        }

        static Viewmatrix CreateLookAtMatrix(Vector3 eye, Vector3 target, Vector3 up)
        {
            Vector3 zaxis = Vector3.Normalize(eye - target);
            Vector3 xaxis = Vector3.Normalize(Vector3.Cross(up, zaxis));
            Vector3 yaxis = Vector3.Cross(zaxis, xaxis);

            Viewmatrix vm = new Viewmatrix
            {
                m11 = xaxis.X,
                m12 = yaxis.X,
                m13 = zaxis.X,
                m14 = 0,

                m21 = xaxis.Y,
                m22 = yaxis.Y,
                m23 = zaxis.Y,
                m24 = 0,

                m31 = xaxis.Z,
                m32 = yaxis.Z,
                m33 = zaxis.Z,
                m34 = 0,

                m41 = -Vector3.Dot(xaxis, eye),
                m42 = -Vector3.Dot(yaxis, eye),
                m43 = -Vector3.Dot(zaxis, eye),
                m44 = 1
            };

            return vm;
        }

        static Vector2 WorldToScreen(Vector3 pos, float[,] viewProj, int screenWidth, int screenHeight)
        {
            float x = pos.X * viewProj[0, 0] + pos.Y * viewProj[1, 0] + pos.Z * viewProj[2, 0] + viewProj[3, 0];
            float y = pos.X * viewProj[0, 1] + pos.Y * viewProj[1, 1] + pos.Z * viewProj[2, 1] + viewProj[3, 1];
            float z = pos.X * viewProj[0, 2] + pos.Y * viewProj[1, 2] + pos.Z * viewProj[2, 2] + viewProj[3, 2];
            float w = pos.X * viewProj[0, 3] + pos.Y * viewProj[1, 3] + pos.Z * viewProj[2, 3] + viewProj[3, 3];

            if (w < 0.01f)
                return new Vector2(-99, -99);

            x /= w;
            y /= w;

            float screenX = (x + 1f) * 0.5f * screenWidth;
            float screenY = (1f - y) * 0.5f * screenHeight;

            return new Vector2(screenX, screenY);
        }

        static void DrawBoxAtOrigin()
        {
            Viewmatrix vm = ReadViewmatrix();

            // Convert Viewmatrix struct to float[,] for WorldToScreen
            float[,] view = ViewmatrixToArray(vm);
            float[,] proj = CreateProjectionMatrix(80f, 2560f / 1440f, 0.1f, 1000f);
            float[,] viewProj = MultiplyMatrices(view, proj);

            Vector3 origin = new Vector3(0, 0, 0);
            Vector2 screenCoords = WorldToScreen(origin, viewProj, 2560, 1440);

            if (screenCoords.X != -99 && screenCoords.Y != -99)
            {
                ImGui.GetWindowDrawList().AddRectFilled(
                    screenCoords - new Vector2(50, 50),
                    screenCoords + new Vector2(50, 50),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0f, 0f, 1f))
                );
            }
        }

        static float[,] ViewmatrixToArray(Viewmatrix vm)
        {
            float[,] m = new float[4, 4];
            m[0, 0] = vm.m11; m[0, 1] = vm.m12; m[0, 2] = vm.m13; m[0, 3] = vm.m14;
            m[1, 0] = vm.m21; m[1, 1] = vm.m22; m[1, 2] = vm.m23; m[1, 3] = vm.m24;
            m[2, 0] = vm.m31; m[2, 1] = vm.m32; m[2, 2] = vm.m33; m[2, 3] = vm.m34;
            m[3, 0] = vm.m41; m[3, 1] = vm.m42; m[3, 2] = vm.m43; m[3, 3] = vm.m44;
            return m;
        }

        static float[,] CreateProjectionMatrix(float fovDegrees, float aspect, float near, float far)
        {
            // FOV set to 80 degrees now
            float fovRad = fovDegrees * (float)System.Math.PI / 180f;
            float yScale = 1.25f / (float)System.Math.Tan(fovRad / 2f);
            float xScale = yScale / aspect;
            float zRange = far - near;

            float[,] proj = new float[4, 4];
            proj[0, 0] = xScale;
            proj[1, 1] = yScale;
            proj[2, 2] = -(far + near) / zRange;
            proj[2, 3] = -1f;
            proj[3, 2] = -(2f * far * near) / zRange;
            proj[3, 3] = 0;  // Notice corrected here, was 0 or 1 before? Usually 0 for D3D-style projection

            return proj;
        }

        static float[,] CreateViewMatrix(Vector3 camPos, float yaw, float pitchNorm)
        {
            float pitch = pitchNorm * (float)(System.Math.PI / 3f);

            float cosPitch = (float)System.Math.Cos(pitch);
            float sinPitch = (float)System.Math.Sin(pitch);
            float cosYaw = (float)System.Math.Cos(yaw);
            float sinYaw = (float)System.Math.Sin(yaw);

            Vector3 forward = new Vector3(
                cosPitch * sinYaw,
                sinPitch,
                cosPitch * cosYaw
            );

            Vector3 worldUp = new Vector3(0, 1, 0);
            Vector3 right = Vector3.Normalize(Vector3.Cross(worldUp, forward));
            Vector3 up = Vector3.Normalize(Vector3.Cross(forward, right));


            float[,] view = new float[4, 4];

            view[0, 0] = right.X;
            view[1, 0] = right.Y;
            view[2, 0] = right.Z;
            view[3, 0] = -Vector3.Dot(right, camPos);

            view[0, 1] = up.X;
            view[1, 1] = up.Y;
            view[2, 1] = up.Z;
            view[3, 1] = -Vector3.Dot(up, camPos);

            view[0, 2] = forward.X;
            view[1, 2] = forward.Y;
            view[2, 2] = forward.Z;
            view[3, 2] = -Vector3.Dot(forward, camPos);

            view[0, 3] = 0;
            view[1, 3] = 0;
            view[2, 3] = 0;
            view[3, 3] = 1;

            return view;
        }

        static float[,] MultiplyMatrices(float[,] a, float[,] b)
        {
            float[,] result = new float[4, 4];
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    float sum = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        sum += a[i, k] * b[k, j];
                    }
                    result[i, j] = sum;
                }
            }
            return result;
        }
        static string ConvertUintToStr(ulong conv)
        {
            UInt64 buf1 = (UInt64)conv;
            string retStr = buf1.ToString("X2");
            return retStr;
        }
    }
}
