namespace FSClient.Shared.Providers
{
    public class LoginDialogOutput
    {
        public string? Login { get; set; }

        public AuthStatus Status { get; set; }

        public string? Password { get; set; }
    }
}
