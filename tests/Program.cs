using DinamicScreenSound;

Verify("no eligible windows", [], null);
Verify("one window", ["Display 1"], "Display 1");
Verify("windows on the same display", ["Display 2", "display 2"], "Display 2");
Verify("windows on different displays", ["Display 1", "Display 2"], null);
Verify("a moving or minimized window", ["Display 1", null], null);

var displays = new Dictionary<string, DesktopDisplay>
{
    ["Display 1"] = new("laptop", "Display 1", "Primary display"),
    ["Display 2"] = new("monitor", "Display 2", "External display")
};
var assignments = new Dictionary<string, string?> { ["laptop"] = "speakers", ["monitor"] = "monitor-audio" };
var exceptions = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["brave.exe"] = "headphones" };
var available = new HashSet<string> { "speakers", "monitor-audio", "headphones" };

CheckOutput("fixed output takes priority", ["Display 2"], new("headphones", null));
CheckOutput("fixed output with windows on two displays", ["Display 1", "Display 2"], new("headphones", null));
CheckOutput("fixed output without a visible window", [], new("headphones", null));
available.Remove("headphones");
CheckOutput("disconnected fixed output falls back to default", ["Display 2"], new(null, null));
exceptions["brave.exe"] = null;
CheckOutput("fixed Windows default", ["Display 2"], new(null, null));
exceptions.Clear();
CheckOutput("display routing resumes after removing the exception", ["Display 2"], new("monitor-audio", "monitor"));
CheckOutput("ambiguous windows keep their current output", ["Display 1", "Display 2"], null);

Console.WriteLine("Desktop routing checks passed.");

void CheckOutput(string scenario, string?[] windows, DesktopRouter.OutputTarget? expected)
{
    var actual = DesktopRouter.ResolveOutput("BRAVE.EXE", windows, displays, assignments, exceptions, available);
    if (actual != expected)
    {
        throw new InvalidOperationException($"Unexpected output for {scenario}.");
    }
}

static void Verify(string scenario, string?[] windows, string? expected)
{
    if (!string.Equals(DesktopRouter.GetSharedDisplay(windows), expected, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Unexpected routing decision for {scenario}.");
    }
}
