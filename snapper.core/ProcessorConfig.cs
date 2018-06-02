namespace snapper.core
{
    public class ProcessorConfig
    {
        public int PauseSeconds { get; set; }

        public int KeystrokeDelayMilliseconds { get; set; }

        public string RootFolderPath { get; set; }

        public string UsernamePattern { get; set; }

        public bool Debug { get; set; }
    }
}
