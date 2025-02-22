using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HeatedMetalManager
{
    public class DllExportReader
    {
        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SymInitialize(IntPtr hProcess, string? UserSearchPath, bool fInvadeProcess);

        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SymCleanup(IntPtr hProcess);

        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern bool SymEnumSymbols(
            IntPtr hProcess,
            ulong BaseOfDll,
            string Mask,
            EnumSymbolsCallback EnumSymbolsCallback,
            IntPtr UserContext
        );

        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ulong SymLoadModuleEx(
            IntPtr hProcess,
            IntPtr hFile,
            string ImageName,
            string ModuleName,
            ulong BaseOfDll,
            uint DllSize,
            IntPtr Data,
            uint Flags
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate bool EnumSymbolsCallback(
            [In] ref SYMBOL_INFO SymbolInfo,
            uint SymbolSize,
            IntPtr UserContext
        );

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct SYMBOL_INFO
        {
            public uint SizeOfStruct;
            public uint TypeIndex;
            public ulong Reserved1;
            public ulong Reserved2;
            public uint Index;
            public uint Size;
            public ulong ModBase;
            public uint Flags;
            public ulong Value;
            public ulong Address;
            public uint Register;
            public uint Scope;
            public uint Tag;
            public uint NameLen;
            public uint MaxNameLen;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
            public string Name;
        }

        private readonly string dllPath;
        private readonly List<Tuple<string, ulong>> exports = new();

        public DllExportReader(string dllPath)
        {
            this.dllPath = dllPath;
        }

        public List<Tuple<string, ulong>> GetExports()
        {
            exports.Clear();
            IntPtr currentProcess = Process.GetCurrentProcess().Handle;

            try
            {
                // Initialize symbol handler
                if (!SymInitialize(currentProcess, null, false))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to initialize symbol handler");
                }

                // Load the target DLL module
                ulong baseAddress = SymLoadModuleEx(
                    currentProcess,
                    IntPtr.Zero,
                    dllPath,
                    Path.GetFileNameWithoutExtension(dllPath),
                    0,
                    0,
                    IntPtr.Zero,
                    0
                );

                if (baseAddress == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to load module");
                }

                // Create callback for symbol enumeration
                EnumSymbolsCallback callback = (ref SYMBOL_INFO symbolInfo, uint symbolSize, IntPtr userContext) =>
                {
                    exports.Add(Tuple.Create(symbolInfo.Name, symbolInfo.Address)); // Store name and address
                    return true;
                };

                // Enumerate symbols using the module name (without path)
                string moduleName = Path.GetFileNameWithoutExtension(dllPath);
                if (!SymEnumSymbols(currentProcess, baseAddress, $"{moduleName}!*", callback, IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to enumerate symbols");
                }

                return exports;
            }
            finally
            {
                SymCleanup(currentProcess);
            }
        }

        // Updated PrintExports method
        public void PrintExports()
        {
            try
            {
                var exports = GetExports();
                Debug.WriteLine($"Exports found in {Path.GetFileName(dllPath)}:");
                foreach (var export in exports)
                {
                    Debug.WriteLine($"  {export.Item1} @ 0x{export.Item2:X}"); // Format address as hex
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading exports: {ex.Message}");
            }
        }

    }
}