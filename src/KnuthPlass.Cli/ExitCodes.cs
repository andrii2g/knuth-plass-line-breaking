namespace KnuthPlass.Cli;

public static class ExitCodes
{
    public const int Success = 0;
    public const int UsageOrInputError = 2;
    public const int NoFeasibleLayout = 3;
    public const int IoError = 4;
    public const int UnexpectedError = 5;
}
