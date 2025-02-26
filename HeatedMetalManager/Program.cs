namespace HeatedMetalManager
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            if (!InstanceManager.Start())
            {
                MessageBox.Show("Another instance is already running.");
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new OuterForm());

            InstanceManager.Cleanup();
        }
    }
}