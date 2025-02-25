namespace HeatedMetalManager
{
    public class HeliosConfig
    {
        public string Username { get; set; }
        public string ProfileID { get; set; }
        public string SavePath { get; set; }
        public int ProductID { get; set; }
        public string Email { get; set; }
        public string Language { get; set; } = "en-US";
        public bool Offline { get; set; } = true;
        public bool UseGameProductID { get; set; } = true;
    }
}
