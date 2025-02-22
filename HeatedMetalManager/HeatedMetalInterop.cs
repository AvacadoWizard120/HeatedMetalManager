using System.Runtime.InteropServices;

namespace HeatedMetalManager
{
    public static class HeatedMetalInterop
    {
        private static IntPtr _dllHandle = IntPtr.Zero;

        // Function delegates to match the DLL exports
        public delegate IntPtr HMVersionDelegate();
        public delegate uint HMVersionIntDelegate();

        // Public accessors for the delegates
        public static HMVersionDelegate HMVersion { get; private set; }
        public static HMVersionIntDelegate HMVersionInt { get; private set; }

        // Initialize the DLL and functions
        public static void Initialize(string dllPath)
        {
            if (_dllHandle != IntPtr.Zero) return; // Already loaded

            _dllHandle = LoadLibrary(dllPath);
            if (_dllHandle == IntPtr.Zero)
            {
                throw new DllNotFoundException($"Failed to load DLL: {dllPath}");
            }

            // Get function pointers
            IntPtr versionPtr = GetProcAddress(_dllHandle, "HMVersion");
            IntPtr versionIntPtr = GetProcAddress(_dllHandle, "HMVersionInt");

            if (versionPtr == IntPtr.Zero || versionIntPtr == IntPtr.Zero)
            {
                throw new EntryPointNotFoundException("Exported functions not found");
            }

            // Convert pointers to delegates
            HMVersion = Marshal.GetDelegateForFunctionPointer<HMVersionDelegate>(versionPtr);
            HMVersionInt = Marshal.GetDelegateForFunctionPointer<HMVersionIntDelegate>(versionIntPtr);
        }

        // Cleanup
        public static void Unload()
        {
            if (_dllHandle != IntPtr.Zero)
            {
                FreeLibrary(_dllHandle);
                _dllHandle = IntPtr.Zero;
            }
        }

        // Windows API imports
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
    }
}