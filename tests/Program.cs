using DinamicScreenSound;

Verify("no eligible windows", [], null);
Verify("one window", ["Display 1"], "Display 1");
Verify("windows on the same display", ["Display 2", "display 2"], "Display 2");
Verify("windows on different displays", ["Display 1", "Display 2"], null);
Verify("a moving or minimized window", ["Display 1", null], null);
Console.WriteLine("Desktop routing checks passed.");

static void Verify(string scenario, string?[] windows, string? expected)
{
    if (!string.Equals(DesktopRouter.GetSharedDisplay(windows), expected, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Unexpected routing decision for {scenario}.");
    }
}
