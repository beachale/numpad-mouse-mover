using System.Diagnostics;
using System.Runtime.InteropServices;

internal static class Program
{
    private const int WhKeyboardLl = 13;

    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private const int VkNumpad2 = 0x62;
    private const int VkNumpad4 = 0x64;
    private const int VkNumpad5 = 0x65;
    private const int VkNumpad6 = 0x66;
    private const int VkNumpad8 = 0x68;
    private const int VkAdd = 0x6B;
    private const int VkSubtract = 0x6D;
    private const int VkDecimal = 0x6E;
    private const int VkF12 = 0x7B;

    private const uint InputMouse = 0;
    private const uint MouseEventfMove = 0x0001;

    private const double DefaultSpeedPixelsPerSecond = 1100.0;
    private const double SpeedStepPixelsPerSecond = 150.0;
    private const double MinSpeedPixelsPerSecond = 100.0;
    private const double MaxSpeedPixelsPerSecond = 5000.0;

    private static readonly object StateLock = new();
    private static readonly LowLevelKeyboardProc KeyboardProc = HookCallback;

    private static bool running = true;
    private static bool enabled = true;
    private static bool suppressNumpad = true;
    private static double speedPixelsPerSecond = DefaultSpeedPixelsPerSecond;

    private static bool upPressed;
    private static bool downPressed;
    private static bool leftPressed;
    private static bool rightPressed;
    private static Axis lastAxis = Axis.Vertical;

    private static bool numpad5Down;
    private static bool addDown;
    private static bool subtractDown;
    private static bool decimalDown;
    private static bool f12Down;

    private static IntPtr hookHandle = IntPtr.Zero;

    private static void Main()
    {
        Console.WriteLine("Numpad Mouse Lock (Windows)");
        Console.WriteLine("8/2 = Up/Down, 4/6 = Left/Right");
        Console.WriteLine("Numpad + / - = Increase / decrease movement speed");
        Console.WriteLine("Numpad 5 = Toggle movement ON/OFF");
        Console.WriteLine("Numpad . = Toggle key suppression");
        Console.WriteLine("F12 = Exit");
        Console.WriteLine($"Speed: {speedPixelsPerSecond:0} px/s");
        Console.WriteLine();

        using Process currentProcess = Process.GetCurrentProcess();
        using ProcessModule? currentModule = currentProcess.MainModule;
        IntPtr moduleHandle = GetModuleHandle(currentModule?.ModuleName);

        hookHandle = SetWindowsHookEx(WhKeyboardLl, KeyboardProc, moduleHandle, 0);
        if (hookHandle == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to install keyboard hook. Win32 error: {error}");
        }

        Thread mover = new(MovementLoop) { IsBackground = true, Name = "NumpadMouseMover" };
        mover.Start();

        while (running && GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        lock (StateLock)
        {
            running = false;
        }

        mover.Join();

        if (hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(hookHandle);
            hookHandle = IntPtr.Zero;
        }
    }

    private static void MovementLoop()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        long previousTicks = stopwatch.ElapsedTicks;
        double xRemainder = 0.0;
        double yRemainder = 0.0;

        while (true)
        {
            bool localRunning;
            bool localEnabled;
            int x = 0;
            int y = 0;
            double localSpeedPixelsPerSecond;
            Axis localLastAxis;

            lock (StateLock)
            {
                localRunning = running;
                localEnabled = enabled;
                localSpeedPixelsPerSecond = speedPixelsPerSecond;
                localLastAxis = lastAxis;

                if (localEnabled)
                {
                    x = (rightPressed ? 1 : 0) - (leftPressed ? 1 : 0);
                    y = (downPressed ? 1 : 0) - (upPressed ? 1 : 0);
                }
            }

            if (!localRunning)
            {
                break;
            }

            if (x != 0 && y != 0)
            {
                if (localLastAxis == Axis.Horizontal)
                {
                    y = 0;
                }
                else
                {
                    x = 0;
                }
            }

            long nowTicks = stopwatch.ElapsedTicks;
            double dt = (nowTicks - previousTicks) / (double)Stopwatch.Frequency;
            previousTicks = nowTicks;

            xRemainder += x * localSpeedPixelsPerSecond * dt;
            yRemainder += y * localSpeedPixelsPerSecond * dt;

            int sendX = (int)Math.Truncate(xRemainder);
            int sendY = (int)Math.Truncate(yRemainder);
            xRemainder -= sendX;
            yRemainder -= sendY;

            if (sendX != 0 || sendY != 0)
            {
                SendRelativeMouseMove(sendX, sendY);
            }

            Thread.Sleep(1);
        }
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        int message = wParam.ToInt32();
        bool keyDown = message is WmKeyDown or WmSysKeyDown;
        bool keyUp = message is WmKeyUp or WmSysKeyUp;
        if (!keyDown && !keyUp)
        {
            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        KbdLlHookStruct hookData = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
        int vk = (int)hookData.VkCode;

        bool handled = false;
        bool suppress = false;

        lock (StateLock)
        {
            switch (vk)
            {
                case VkNumpad8:
                    upPressed = keyDown ? true : keyUp ? false : upPressed;
                    if (keyDown) lastAxis = Axis.Vertical;
                    handled = true;
                    suppress = suppressNumpad;
                    break;
                case VkNumpad2:
                    downPressed = keyDown ? true : keyUp ? false : downPressed;
                    if (keyDown) lastAxis = Axis.Vertical;
                    handled = true;
                    suppress = suppressNumpad;
                    break;
                case VkNumpad4:
                    leftPressed = keyDown ? true : keyUp ? false : leftPressed;
                    if (keyDown) lastAxis = Axis.Horizontal;
                    handled = true;
                    suppress = suppressNumpad;
                    break;
                case VkNumpad6:
                    rightPressed = keyDown ? true : keyUp ? false : rightPressed;
                    if (keyDown) lastAxis = Axis.Horizontal;
                    handled = true;
                    suppress = suppressNumpad;
                    break;
                case VkNumpad5:
                    if (keyDown && !numpad5Down)
                    {
                        enabled = !enabled;
                        Console.WriteLine(enabled ? "Movement enabled" : "Movement paused");
                    }

                    numpad5Down = keyDown && !keyUp;
                    handled = true;
                    suppress = true;
                    break;
                case VkAdd:
                    if (keyDown && !addDown)
                    {
                        speedPixelsPerSecond = Math.Min(
                            MaxSpeedPixelsPerSecond,
                            speedPixelsPerSecond + SpeedStepPixelsPerSecond);
                        Console.WriteLine($"Speed: {speedPixelsPerSecond:0} px/s");
                    }

                    addDown = keyDown && !keyUp;
                    handled = true;
                    suppress = true;
                    break;
                case VkSubtract:
                    if (keyDown && !subtractDown)
                    {
                        speedPixelsPerSecond = Math.Max(
                            MinSpeedPixelsPerSecond,
                            speedPixelsPerSecond - SpeedStepPixelsPerSecond);
                        Console.WriteLine($"Speed: {speedPixelsPerSecond:0} px/s");
                    }

                    subtractDown = keyDown && !keyUp;
                    handled = true;
                    suppress = true;
                    break;
                case VkDecimal:
                    if (keyDown && !decimalDown)
                    {
                        suppressNumpad = !suppressNumpad;
                        Console.WriteLine(suppressNumpad
                            ? "Numpad passthrough: OFF (keys suppressed)"
                            : "Numpad passthrough: ON (keys passed to game/apps)");
                    }

                    decimalDown = keyDown && !keyUp;
                    handled = true;
                    suppress = true;
                    break;
                case VkF12:
                    if (keyDown && !f12Down)
                    {
                        running = false;
                        PostQuitMessage(0);
                        Console.WriteLine("Exiting...");
                    }

                    f12Down = keyDown && !keyUp;
                    handled = true;
                    suppress = true;
                    break;
            }
        }

        if (handled && suppress)
        {
            return (IntPtr)1;
        }

        return CallNextHookEx(hookHandle, nCode, wParam, lParam);
    }

    private static void SendRelativeMouseMove(int dx, int dy)
    {
        INPUT[] inputs =
        [
            new INPUT
            {
                Type = InputMouse,
                Union = new InputUnion
                {
                    MouseInput = new MouseInput
                    {
                        Dx = dx,
                        Dy = dy,
                        MouseData = 0,
                        DwFlags = MouseEventfMove,
                        Time = 0,
                        DwExtraInfo = IntPtr.Zero
                    }
                }
            }
        ];

        _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private enum Axis
    {
        Horizontal,
        Vertical
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput MouseInput;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint DwFlags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr HWnd;
        public uint Message;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public POINT Pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hhk,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern sbyte GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage([In] ref MSG lpmsg);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);
}
