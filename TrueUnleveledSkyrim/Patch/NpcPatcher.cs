using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;

namespace TrueUnleveledSkyrim.Patch
{
    internal static class NpcPatcher
    {
        public static void Patch(
            IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            foreach (var npcGetter in state.LoadOrder
                         .PriorityOrder
                         .Npc()
                         .WinningOverrides())
            {
                var npc = npcGetter.DeepCopy();
                bool changed = false;

                // Calculate from PC Level Çñ≥å¯âª
                if (npc.Configuration.Flags.HasFlag(
                        NpcConfiguration.Flag.CalcFromPCLevel))
                {
                    npc.Configuration.Flags &=
                        ~NpcConfiguration.Flag.CalcFromPCLevel;
                    changed = true;
                }

                // ÉåÉxÉãå≈íËÅiMin = MaxÅj
                if (npc.Configuration.MinLevel
                    != npc.Configuration.MaxLevel)
                {
                    npc.Configuration.MinLevel =
                        npc.Configuration.MaxLevel;
                    changed = true;
                }

                if (changed)
                {
                    state.PatchMod.Npcs.Set(npc);
                }
            }
        }
    }
}
