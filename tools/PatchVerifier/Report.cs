namespace PatchVerifier;

/// <summary>Collects findings and prints them as plain text (no colors, stable prefixes).</summary>
public sealed class Report
{
    public int Errors { get; private set; }
    public int Warnings { get; private set; }

    public void Section(string title) => Console.WriteLine($"== {title}");

    public void Ok(string message) => Console.WriteLine($"  ok    {message}");

    public void Info(string message) => Console.WriteLine($"  info  {message}");

    public void Warn(string message)
    {
        Warnings++;
        Console.WriteLine($"  WARN  {message}");
    }

    public void Error(string message)
    {
        Errors++;
        Console.WriteLine($"  ERROR {message}");
    }
}
