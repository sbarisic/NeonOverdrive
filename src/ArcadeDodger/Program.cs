using ArcadeDodger;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("Arcade Dodger supports Windows only.");
    return;
}
new GameApp().Run(args.Contains("--smoke-test"));
