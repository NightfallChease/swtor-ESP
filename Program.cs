using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using ClickableTransparentOverlay;
using ImGuiNET;
using Memory;
using SharpGen.Runtime;
using WindowsInput;

namespace swtor_ESP
{
    internal class Program : Overlay
    {
        //set invariant culture
        [DllImport("User32.dll", CharSet = CharSet.Unicode)]
        public static extern int MessageBox(IntPtr h, string m, string c, int type);
        
        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);
        
        // System metrics constants
        private const int SM_CXSCREEN = 0; // Width of the screen
        private const int SM_CYSCREEN = 1; // Height of the screen
        
        // Static screen dimensions
        public static int ScreenWidth = GetSystemMetrics(SM_CXSCREEN);
        public static int ScreenHeight = GetSystemMetrics(SM_CYSCREEN);
        public static float AspectRatio = (float)ScreenWidth / ScreenHeight;
        
        Program() : base(ScreenWidth, ScreenHeight) { }
        public static Program p = new Program();
        public static Mem m = new Mem();
        public static List<Entity> entList = new List<Entity> { };
        public static Entity selectedEnt = new Entity();
        public static InputSimulator sim = new InputSimulator();
        public bool isESPEnabled = false;
        public static string cameraAddrStr = "swtor.exe+01BFC168";
        public static string localPlayerAddrPtr = "swtor.exe+01BAED28,0x8";
        public static string localPlayerAddrStr = "";
        public static Vector3 localPlayerPos = new Vector3 { };
        public static Vector3 localPlayerSavedPos = new Vector3 { };
        public static string entlistAOB = "F3 44 0F 10 51 68 F3";
        public static UIntPtr codeCaveAddr = 0x0;
        public static UIntPtr entBaseAddr = 0x0;
        public static byte[] entlistHookBytes = { 0x48, 0x89, 0x0D, 0x0B, 0x00, 0x00, 0x00, 0xF3, 0x44, 0x0F, 0x10, 0x51, 0x68 };
        public static uint entbaseOffset = 0x12;
        public static string entBaseAddrStr = "";
        public static string entlistAddrStr = "";
        public static bool entlistHooked = false;
        public static Vector3 camPos = new Vector3 { };
        public static float espMaxDistance = 15f;
        public static bool distanceESP = false;
        public static bool baseAddrESP = false;
        public static bool boxESP = false;
        public static Vector4 espColor = new Vector4(1, 0, 0, 1);
        public static bool useESPColor = false;
        public static bool useDistanceColor = false;
        public static ImDrawListPtr drawlist;
        public static bool entSelection = false;
        public static string clientModelBackup = "";

        static void Main()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            Thread memoryThread = new Thread(MemoryStuff);
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Console.WriteLine("SWTOR-ESP Test");
            Console.WriteLine($"Detected screen resolution: {ScreenWidth}x{ScreenHeight}");
            Console.WriteLine($"Aspect ratio: {AspectRatio:F2}");
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
            memoryThread.Start();
            while (true) 
            {
                Thread.Sleep(3);
            }
        }
        protected override void Render()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            DrawMenu();
            DrawESP();
            ClickCheck();
        }
        static void MemoryStuff()
        {
            while (true)
            {
                if (entlistAddrStr == "")
                {
                    AOBScan();
                }
                if (!entlistHooked)
                {
                    EntHook();
                }
                if (entlistAddrStr != "")
                {
                    ReadEnts();
                    AddEntsToList();
                    UpdateEnts();
                }
                if (selectedEnt.baseAddrStr != "")
                {
                    selectedEnt.modelConfig = m.ReadLong($"{selectedEnt.baseAddrStr}+0x288").ToString();
                }
                Thread.Sleep(10);
            }
        }
        void ClickCheck()
        {
            if (!entSelection) return;
            if (sim.InputDeviceState.IsKeyDown(WindowsInput.Native.VirtualKeyCode.LBUTTON))
            {
                if (entList.Count <= 0) return;
                Vector2 mousePos = ImGui.GetMousePos();
                foreach(Entity ent in entList)
                {
                    if (mousePos.X >= ent.rectMin.X && mousePos.X <= ent.rectMax.X && mousePos.Y >= ent.rectMin.Y && mousePos.Y <= ent.rectMax.Y)
                    {
                        foreach (Entity ent2 in entList)
                        {
                            ent2.entESPColor = new Vector4(0, 0, 0, 0);
                        }
                        ent.entESPColor = new Vector4(0f, 0.931f, 1f, 1f);
                        selectedEnt = ent;
                    }
                }
            }
        }
        void DrawESP()
        {
            if (!p.isESPEnabled)
                return;

            ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(ScreenWidth, ScreenHeight), ImGuiCond.Always);
            ImGui.Begin("ESP Overlay",
                ImGuiWindowFlags.NoTitleBar
                | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.NoMouseInputs
                | ImGuiWindowFlags.NoScrollbar
            );
            drawlist = ImGui.GetWindowDrawList();

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
            float[,] proj = CreateProjectionMatrix(60f, AspectRatio, 0.1f, 1000f);
            float[,] viewProj = MultiplyMatrices(view, proj);

            // Loop through entities instead of drawing a single fixed boxw
            try {
                foreach (Entity ent in entList)
                {
                    if (ent.coords == Vector3.Zero)
                        continue;

                    Vector2 screenCoords = WorldToScreen(ent.coords, viewProj, ScreenWidth, ScreenHeight);

                    if (screenCoords.X != -99 && ent.magnitude < espMaxDistance)
                    {
                        //draw box
                        if (boxESP)
                        {
                            if (useDistanceColor)
                            {
                                // Interpolate between blue (far) and red (near)
                                float t = Math.Clamp(ent.magnitude / espMaxDistance, 0f, 1f); // Normalize magnitude
                                Vector4 coldColor = new Vector4(0, 0, 1, 1); // Blue for far
                                Vector4 warmColor = new Vector4(1, 0, 0, 1); // Red for near
                                espColor = Vector4.Lerp(warmColor, coldColor, t); // Interpolate
                            }
                            ent.rectMin = screenCoords - new Vector2(70 / ent.magnitude, 330 / ent.magnitude);
                            ent.rectMax = screenCoords + new Vector2(70 / ent.magnitude, 50 / ent.magnitude);
                            if (ent.entESPColor != new Vector4(0, 0, 0, 0))
                            {
                                drawlist.AddRect(
                                 screenCoords - new Vector2(70 / ent.magnitude, 330 / ent.magnitude),
                                 screenCoords + new Vector2(70 / ent.magnitude, 50 / ent.magnitude),
                                 ImGui.ColorConvertFloat4ToU32(ent.entESPColor));
                            }
                            else
                            {
                                drawlist.AddRect(
                                screenCoords - new Vector2(70 / ent.magnitude, 330 / ent.magnitude),
                                screenCoords + new Vector2(70 / ent.magnitude, 50 / ent.magnitude),
                                ImGui.ColorConvertFloat4ToU32(espColor));
                            }
                        }
                        //draw text
                        if (distanceESP)
                        {
                            drawlist.AddText(screenCoords, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)), $"{ent.playermagnitude}");
                        }
                        if (baseAddrESP)
                        {
                            drawlist.AddText(screenCoords, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)), $"{ent.baseAddrStr}");
                        }
                    }
                }
            }
            catch { }
            ImGui.End();
        }
        void DrawMenu()
        {
            ImGui.Begin("Nightfall's SWTOR ESP");
            ImGui.Checkbox("Enable ESP", ref p.isESPEnabled);
            if (p.isESPEnabled)
            {
                ImGui.Checkbox("Draw Distance", ref distanceESP);
                ImGui.Checkbox("Draw BaseAddr", ref baseAddrESP);
                ImGui.Checkbox("Draw Box", ref boxESP);
                ImGui.SliderFloat("Max Distance", ref espMaxDistance, 1f, 15f);
                ImGui.Checkbox("ESP Color", ref useESPColor);
                if (useESPColor)
                {
                    ImGui.Checkbox("Color by distance", ref useDistanceColor);
                    ImGui.ColorPicker4("EspColor", ref espColor);
                }
                ImGui.Checkbox("Make entities selectable", ref entSelection);
                if (entSelection)
                {
                    ImGui.Text($"Selected entity base addr: {selectedEnt.baseAddrStr}");
                    ImGui.Text($"Selected entity model config: {selectedEnt.modelConfig}");
                    if(ImGui.Button("Copy Model"))
                    {
                        changeModel();
                    }
                }
            }
            if (ImGui.Button("Exit"))
            {
                ImGui.End();
                Environment.Exit(0);
            }
            ImGui.End();
        }
        void changeModel()
        {
            string currentModel = m.ReadLong($"{localPlayerAddrStr}+0x288").ToString();
            if (clientModelBackup == "")
            {
                clientModelBackup = currentModel;
            }
            if (currentModel != selectedEnt.modelConfig)
            {
                m.WriteMemory($"{localPlayerAddrStr}+0x288", "long", $"{selectedEnt.modelConfig}");
            }
            else
            {
                m.WriteMemory($"{localPlayerAddrStr}+0x288", "long", $"{clientModelBackup}");
            }
            Thread.Sleep(300);
        }
        public static void AddEntsToList()
        {
            if (string.IsNullOrEmpty(entBaseAddrStr) || entBaseAddrStr == "00")
                return;
            if (entBaseAddrStr == localPlayerAddrStr)
                return;

            if (!entList.Any(ent => ent.baseAddrStr == entBaseAddrStr))
            {
                Entity newEnt = new Entity { baseAddrStr = entBaseAddrStr };
                entList.Add(newEnt);
                Console.WriteLine("Added Entity: " + entBaseAddrStr);
                Console.WriteLine($"Coords: X-{newEnt.coords.X.ToString()}, Y-{newEnt.coords.Y.ToString()}, Z-{newEnt.coords.Z.ToString()}");
            }
        }
        public static void UpdateEnts()
        {
            try
            {
                foreach (Entity ent in entList)
                {
                    localPlayerPos.X = m.ReadFloat($"{localPlayerAddrStr}+0x68");
                    localPlayerPos.Y = m.ReadFloat($"{localPlayerAddrStr}+0x6C");
                    localPlayerPos.Z = m.ReadFloat($"{localPlayerAddrStr}+0x70");
                    ent.coords.X = m.ReadFloat($"{ent.baseAddrStr}+0x68");
                    ent.coords.Y = m.ReadFloat($"{ent.baseAddrStr}+0x6C");
                    ent.coords.Z = m.ReadFloat($"{ent.baseAddrStr}+0x70");
                    ent.magnitude = Vector3.Distance(camPos, ent.coords); // Calculate distance to camera
                    ent.playermagnitude = Vector3.Distance(localPlayerPos, ent.coords); // Calculate distance to camera
                }
            }
            catch { }
        }
        public static void EntHook()
        {
            if(entlistAddrStr != "")
            {
                UIntPtr codeCaveAddr = m.CreateCodeCave(entlistAddrStr, entlistHookBytes, 6, 240);
                entBaseAddr = codeCaveAddr + entbaseOffset;
                entBaseAddrStr = ConvertUintToStr(entBaseAddr);
                entlistHooked = true;
            }
            //if (entlistAddrStr != "")
            //{
            //    var ptrAddr = m.Get64BitCode(entListPtr);
            //    entListPtrAddr = ptrAddr.ToString("X2");
            //    //Console.WriteLine(ptrAddr.ToString("X2"));
            //}
        }
        public static void ReadEnts()
        {
            try 
            {
                localPlayerAddrStr = m.ReadLong(localPlayerAddrPtr).ToString("X2");
            }
            catch { }
            entBaseAddrStr = ConvertUintToStr(entBaseAddr);
            long entBuffer = m.ReadMemory<long>(entBaseAddrStr);
            entBaseAddrStr = entBuffer.ToString("X2");
            Console.WriteLine(entBaseAddrStr);
            Thread.Sleep(100);
        }
        static void AOBScan()
        {
            entlistAddrStr = m.AoBScan(entlistAOB).Result.Sum().ToString("X2");
            if(entlistAddrStr.Length == 0)
            {
                MessageBox(0, "Failed to find aob!", "Error", 0);
                Environment.Exit(0);
            }
        }
       
        static Vector2 WorldToScreen(Vector3 pos, float[,] viewProj, int screenWidth, int screenHeight)
        {
            float x = pos.X * viewProj[0, 0] + pos.Y * viewProj[1, 0] + pos.Z * viewProj[2, 0] + viewProj[3, 0];
            float y = pos.X * viewProj[0, 1] + pos.Y * viewProj[1, 1] + pos.Z * viewProj[2, 1] + viewProj[3, 1];
            float z = pos.X * viewProj[0, 2] + pos.Y * viewProj[1, 2] + pos.Z * viewProj[2, 2] + viewProj[3, 2];
            float w = pos.X * viewProj[0, 3] + pos.Y * viewProj[1, 3] + pos.Z * viewProj[2, 3] + viewProj[3, 3];

            if (w < 0.0001f)
                return new Vector2(-9999f, -9999f);

            x /= w;
            y /= w;

            float screenX = (x + 1f) * 0.5f * screenWidth;
            float screenY = (1f - y) * 0.5f * screenHeight;

            return new Vector2(screenX, screenY);
        }
        static float[,] CreateProjectionMatrix(float fovDegrees, float aspect, float near, float far)
        {
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
            proj[3, 3] = 0;

            return proj;
        }
        static float[,] CreateViewMatrix(Vector3 camPos, float yaw, float pitchNorm)
        {
            // SWTOR pitch is effectively sin(pitch): -1 = up, +1 = down
            float sinPitch = Math.Clamp(pitchNorm, -0.9999f, 0.9999f); // tiny clamp avoids cos=0 exactly
            float cosPitch = (float)Math.Sqrt(1f - sinPitch * sinPitch);

            float cy = (float)Math.Cos(yaw);
            float sy = (float)Math.Sin(yaw);

            // Forward (your Y-up, Z-forward convention)
            Vector3 forward = Vector3.Normalize(new Vector3(
                cosPitch * sy,  // X
                sinPitch,       // Y (up)
                cosPitch * cy   // Z (forward)
            ));

            // Stable horizon-right: depends only on yaw, never collinear with forward at vertical pitch
            Vector3 right = Vector3.Normalize(new Vector3(
                cy,  // +X when yaw=0
                0f,
                -sy
            ));

            // Up from forward × right keeps the same handedness you had
            Vector3 up = Vector3.Normalize(Vector3.Cross(forward, right));

            float[,] view = new float[4, 4];

            view[0, 0] = right.X; view[1, 0] = right.Y; view[2, 0] = right.Z; view[3, 0] = -Vector3.Dot(right, camPos);
            view[0, 1] = up.X; view[1, 1] = up.Y; view[2, 1] = up.Z; view[3, 1] = -Vector3.Dot(up, camPos);
            view[0, 2] = forward.X; view[1, 2] = forward.Y; view[2, 2] = forward.Z; view[3, 2] = -Vector3.Dot(forward, camPos);
            view[0, 3] = 0; view[1, 3] = 0; view[2, 3] = 0; view[3, 3] = 1;

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
        private static void OnProcessExit(object sender, EventArgs e)
        {
            try
            {
                m.WriteMemory(entlistAddrStr, "bytes", "F3 44 0F 10 51 68");
            }
            catch
            {
                Console.WriteLine("Restoring code failed! Please restart the game.");
            }
        }
    }
}
