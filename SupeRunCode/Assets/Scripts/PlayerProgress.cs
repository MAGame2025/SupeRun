using System;
using System.Collections.Generic;

[Serializable]
public class PlayerProgress
{
    // Meta / permanent progression
    public int metaGold = 0;

    public List<string> unlockedWeapons = new List<string>();
    public List<string> unlockedCharacters = new List<string>();
    public List<string> unlockedItems = new List<string>();

    public List<string> completedLevels = new List<string>();
    public List<string> completedChallenges = new List<string>();

    // Minimal stats to finish the assignment now
    public int totalRuns = 0;
    public int totalDeaths = 0;
    public int bestLevelReached = 1;
    public int lastRunLevelReached = 1;
}
