namespace PocketVault.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var application = new System.Windows.Application();
        application.Run(new MainWindow(args.FirstOrDefault()));
    }
}
