public static class SRRunState
{
    public static bool IsLevelComplete { get; private set; }

    public static void MarkLevelComplete()
    {
        IsLevelComplete = true;
    }

    public static void Reset()
    {
        IsLevelComplete = false;
    }
}
