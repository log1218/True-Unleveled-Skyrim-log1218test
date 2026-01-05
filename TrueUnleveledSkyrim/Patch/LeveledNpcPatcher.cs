using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Plugins.Cache;
using System.Linq;

namespace TrueUnleveledSkyrim.Patch
{
    internal static class LeveledNpcPatcher
    {
        public static void Patch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var linkCache = state.LoadOrder.PriorityOrder.ToImmutableLinkCache();

            // 1. ワールドNPCのレベル飽和
            foreach (var npcGetter in state.LoadOrder.PriorityOrder.Npc().WinningOverrides())
            {
                if (npcGetter.IsDeleted)
                    continue;

                var npc = npcGetter.DeepCopy();
                bool changed = false;

                ushort zoneMaxLevel = GetZoneMaxLevel(npc, linkCache);

                if (npc.Level < zoneMaxLevel)
                {
                    npc.Level = zoneMaxLevel;
                    changed = true;
                }

                if (npc.LevelMult != 1f)
                {
                    npc.LevelMult = 1f;
                    changed = true;
                }

                if (npc.LevelOffset != 0)
                {
                    npc.LevelOffset = 0;
                    changed = true;
                }

                if (changed)
                    state.PatchMod.Npcs.Set(npc);
            }

            // 2. LVLIリストの補正（ダンジョン内スポーン用）
            foreach (var lvliGetter in state.LoadOrder.PriorityOrder.LeveledCharacter().WinningOverrides())
            {
                if (lvliGetter.IsDeleted)
                    continue;

                var lvli = lvliGetter.DeepCopy();
                bool changed = false;

                // 各Entryのレベルをゾーン最大に
                foreach (var entry in lvli.Entries)
                {
                    ushort maxLevel = 50; // デフォルト
                    if (entry.Reference.IsNull == false)
                    {
                        var refNpc = entry.Reference.Resolve(linkCache);
                        if (refNpc != null)
                            maxLevel = GetZoneMaxLevel(refNpc, linkCache);
                    }

                    if (entry.Level < maxLevel)
                    {
                        entry.Level = maxLevel;
                        changed = true;
                    }
                }

                if (changed)
                    state.PatchMod.LeveledCharacters.Set(lvli);
            }
        }

        private static ushort GetZoneMaxLevel(Npc npc, ILinkCache linkCache)
        {
            try
            {
                if (npc.Location != null && !npc.Location.IsNull)
                {
                    var zone = npc.Location.Resolve(linkCache);
                    if (zone != null)
                    {
                        if (TUSConstants.ZoneMaxLevels.TryGetValue(zone.FormKey, out var maxLevel))
                            return maxLevel;
                        if (zone.MaxLevel.HasValue)
                            return zone.MaxLevel.Value;
                    }
                }
            }
            catch
            {
                // 安全策: 例外が出たらデフォルト
            }
            return 50;
        }
    }
}
