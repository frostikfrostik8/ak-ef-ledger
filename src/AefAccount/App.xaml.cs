using System.IO;
using System.Windows;
using AefAccount.Services;

namespace AefAccount;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Арт ищем до создания MainWindow: биндинги карточек читают OperatorArt сразу.
        var artDir = e.Args
            .FirstOrDefault(arg => arg.StartsWith("--art-dir=", StringComparison.OrdinalIgnoreCase))
            ?.Split('=', 2)[1];
        ArtPack.Initialize(artDir);
        var artCacheRoot = Path.Combine(DataService.FindRepoRoot(), "data", "art_cache");
        OperatorArt.Initialize(artCacheRoot);
        OperatorArtCache.Initialize(artCacheRoot);

        base.OnStartup(e);
    }
}
