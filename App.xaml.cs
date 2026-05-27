using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace Verdict;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    static App()
    {
        try
        {
            System.IO.File.WriteAllText(@"C:\Users\charlesp\Verdict\bin\Debug\net9.0-windows\app_startup.log", "STATIC_CTOR_OK");
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(@"C:\Users\charlesp\Verdict\bin\Debug\net9.0-windows\app_error.log", $"STATIC_CTOR_FAIL:{ex.Message}");
        }
    }

    public App()
    {
        try
        {
            System.IO.File.WriteAllText(@"C:\Users\charlesp\Verdict\bin\Debug\net9.0-windows\app_startup.log", "INSTANCE_CTOR_OK");
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(@"C:\Users\charlesp\Verdict\bin\Debug\net9.0-windows\app_error.log", $"INSTANCE_CTOR_FAIL:{ex.Message}");
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            System.IO.File.WriteAllText(@"C:\Users\charlesp\Verdict\bin\Debug\net9.0-windows\app_startup.log", "ONSTARTUP_OK");
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(@"C:\Users\charlesp\Verdict\bin\Debug\net9.0-windows\app_error.log", $"ONSTARTUP_FAIL:{ex.Message}");
        }
        base.OnStartup(e);
    }
}
