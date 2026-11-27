using DinamicScreenSound;

Verify("no eligible windows", [], null);
Verify("one window", ["Display 1"], "Display 1");
Verify("windows on the same display", ["Display 2", "display 2"], "Display 2");
Verify("windows on different displays", ["Display 1", "Display 2"], null);
Verify("a moving or minimized window", ["Display 1", null], null);

var displays = new Dictionary<string, DesktopDisplay>
{
    ["Display 1"] = new("laptop", @"\\.\DISPLAY1", true, new(0, 0, 1920, 1080)),
    ["Display 2"] = new("monitor", @"\\.\DISPLAY2", false, new(-1920, 0, 1920, 1080))
};
var external = displays["Display 2"] with { Primary = true, Bounds = new(0, 0, 1920, 1080) };
if (external.Number != "2" || external.Label != "Display 2 (primary) (0, 0)" || external.Key != "monitor")
{
    throw new InvalidOperationException("A primary display change must preserve the number used in the row and its assignment key.");
}
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
var videoWindow = new WindowIdentity(101, 10, 1000);
var secondaryWindow = new WindowIdentity(102, 10, 1000);
DesktopWindow[] split = [new(videoWindow, "Display 2", "Video"), new(secondaryWindow, "Display 1", "Other")];
CheckOutput("selected window resolves a split", DesktopRouter.GetRoutingDisplays(split, videoWindow),
    new("monitor-audio", "monitor"));
CheckOutput("selected window follows back to the primary display",
    DesktopRouter.GetRoutingDisplays([split[0] with { DisplayName = "Display 1" }, split[1]], videoWindow),
    new("speakers", "laptop"));
CheckOutput("selected moving or minimized window holds the output",
    DesktopRouter.GetRoutingDisplays([split[0] with { DisplayName = null }, split[1]], videoWindow), null);
CheckOutput("a temporarily hidden selected window holds the output",
    DesktopRouter.GetRoutingDisplays([split[1]], videoWindow), null);
CheckOutput("clearing the selected window restores the split rule",
    DesktopRouter.GetRoutingDisplays(split, null), null);
exceptions["brave.exe"] = "speakers";
CheckOutput("fixed output overrides a selected window", DesktopRouter.GetRoutingDisplays(split, videoWindow),
    new("speakers", null));
exceptions.Clear();
CheckConflict("windows on different configured displays", ["Display 1", "Display 2"], true);
CheckConflict("windows on the same display", ["Display 1", "display 1"], false);
CheckConflict("a window with no stable display yet", ["Display 1", null], false);
assignments.Clear();
CheckConflict("windows with no display assignments", ["Display 1", "Display 2"], false);

Console.WriteLine("Desktop routing checks passed.");

void CheckConflict(string scenario, string?[] windows, bool expected)
{
    if (DesktopRouter.HasConflictingDisplays(windows, displays, assignments) != expected)
    {
        throw new InvalidOperationException($"Unexpected shared-audio status for {scenario}.");
    }
}

void CheckOutput(string scenario, IEnumerable<string?> windows, DesktopRouter.OutputTarget? expected)
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
