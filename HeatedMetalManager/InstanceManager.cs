namespace HeatedMetalManager
{
    public static class InstanceManager
    {
        private static Mutex _mutex;
        private const string MutexName = "{B9E76C7D-8D6A-4A3C-9E1A-3F3A5D7F2B1E}";

        public static bool Start()
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            return createdNew;
        }

        public static void Cleanup()
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
    }
}
