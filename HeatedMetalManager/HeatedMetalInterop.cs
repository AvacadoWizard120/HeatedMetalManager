using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HeatedMetalManager
{
    public static class HeatedMetalInterop
    {
        private static IntPtr _dllHandle = IntPtr.Zero;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr HMVersionDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate uint HMVersionIntDelegate();

        [DllImport("kernel32")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32", EntryPoint = "GetProcAddress")]
        private static extern IntPtr GetProcAddressOrdinal(IntPtr hModule, IntPtr ordinal);

        public static HMVersionDelegate HMVersion { get; private set; }
        public static HMVersionIntDelegate HMVersionInt { get; private set; }

        public static void Initialize(string dllPath)
        {
            if (_dllHandle != IntPtr.Zero) return;

            const uint DONT_RESOLVE_DLL_REFERENCES = 0x00000001;

            _dllHandle = LoadLibraryEx(dllPath, IntPtr.Zero, DONT_RESOLVE_DLL_REFERENCES);
            if (_dllHandle == IntPtr.Zero)
            {
                throw new DllNotFoundException($"Failed to load HeatedMetal.dll");
            }

            IntPtr versionPtr = GetProcAddressOrdinal(_dllHandle, (IntPtr)1);
            IntPtr versionIntPtr = GetProcAddressOrdinal(_dllHandle, (IntPtr)2);

            if (versionPtr == IntPtr.Zero || versionIntPtr == IntPtr.Zero)
            {
                throw new EntryPointNotFoundException("Required HeatedMetal functions not found");
            }

            HMVersion = Marshal.GetDelegateForFunctionPointer<HMVersionDelegate>(versionPtr);
            HMVersionInt = Marshal.GetDelegateForFunctionPointer<HMVersionIntDelegate>(versionIntPtr);
        }

        public static void Unload()
        {
            if (_dllHandle != IntPtr.Zero)
            {
                FreeLibrary(_dllHandle);
                _dllHandle = IntPtr.Zero;
            }
        }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);
    }
}