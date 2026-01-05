using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using System.Linq;

namespace TrueUnleveledSkyrim.Patch
{
    internal static class LeveledNpcPatcher
    {
        public static void Patch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            foreach (var lvlnGetter in state.LoadOrder.PriorityOrder
                         .LeveledNpc()
                         .WinningOverrides())
            {
                var lvln = lvlnGetter.DeepCopy();
                bool changed = false;

                // ChanceNone 無効化（スポーン欠損防止）
                if (lvln.ChanceNone != 0)
                {
                    lvln.ChanceNone = 0;
                    changed = true;
                }

                // Flags 全無効化（PC レベル・条件分岐・RNG 排除）
                if (lvln.Flags != 0)
                {
                    lvln.Flags = 0;
                    changed = true;
                }

                // ★ Tier 内単調さ対策：
                // Entry は複数残すが、すべて同一条件に正規化
                foreach (var entry in lvln.Entries)
                {
                    if (entry.Level != 1 || entry.Count != 1)
                    {
                        entry.Level = 1;
                        entry.Count = 1;
                        changed = true;
                    }
                }

                // 念のため空 Entry を除外
                if (lvln.Entries.Count == 0)
                    continue;

                if (changed)
                {
                    state.PatchMod.LeveledNpcs.Set(lvln);
                }
            }
        }
    }
}
