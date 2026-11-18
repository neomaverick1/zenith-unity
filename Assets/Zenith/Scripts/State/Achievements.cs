// Twelve achievements, checked after every move.
using System.Collections.Generic;

namespace Zenith
{
    public class Achievement
    {
        public string id, name, how;

        public Achievement(string id, string name, string how)
        {
            this.id = id;
            this.name = name;
            this.how = how;
        }
    }

    public static class Achievements
    {
        public static readonly Achievement[] All =
        {
            new Achievement("first", "First Light", "Fuse your first two stars"),
            new Achievement("t128", "Kindling", "Make a Sun (128)"),
            new Achievement("t512", "White Heat", "Make a White Star (512)"),
            new Achievement("t1024", "Blue Giant", "Make a Blue Giant (1024)"),
            new Achievement("nova", "Supernova", "Reach the goal of any board"),
            new Achievement("t4096", "Neutron", "Make a Neutron Star (4096)"),
            new Achievement("chain3", "Chain Reaction", "Fuse three pairs in one move"),
            new Achievement("chain4", "Cascade", "Fuse four or more pairs in one move"),
            new Achievement("pure", "No Regrets", "Make a Blue Giant without undo"),
            new Achievement("score20k", "Big Bang", "Score 20,000 in one game"),
            new Achievement("daily3", "Devotion", "Play the daily challenge on three days"),
            new Achievement("marathon", "Long Night", "Make 1,000 moves in one game"),
        };

        public static Achievement Find(string id)
        {
            foreach (var a in All) if (a.id == id) return a;
            return null;
        }

        /// <summary>Which achievements this moment earns, given the game, the merges just made and the records so far.</summary>
        public static List<string> Earned(Game g, int combo, Stats stats)
        {
            var have = stats.achievements;
            var outList = new List<string>();
            void Add(string id, bool ok)
            {
                if (ok && !have.Has(id)) outList.Add(id);
            }
            Add("first", g.merges > 0);
            Add("t128", g.best >= 128);
            Add("t512", g.best >= 512);
            Add("t1024", g.best >= 1024);
            Add("nova", g.won);
            Add("t4096", g.best >= 4096);
            Add("chain3", combo >= 3);
            Add("chain4", combo >= 4);
            Add("pure", g.best >= 1024 && !g.usedUndo);
            Add("score20k", g.score >= 20000);
            Add("daily3", stats.dailyBest.Count >= 3);
            Add("marathon", g.moves >= 1000);
            return outList;
        }
    }
}
