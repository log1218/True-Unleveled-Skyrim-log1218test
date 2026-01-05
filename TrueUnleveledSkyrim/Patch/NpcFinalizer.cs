using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Plugins.Cache;
using System.Linq;

namespace TrueUnleveledSkyrim.Patch
{
    internal static class NpcFinalizer
    {
        public static void Patch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var linkCache = state.LoadOrder.PriorityOrder.ToImmutableLinkCache();

            foreach (var npcGetter in state.LoadOrder.PriorityOrder.Npc().WinningOverrides())
            {
                if (npcGetter.IsDeleted)
                    continue;

                var npc = npcGetter.DeepCopy();
                bool changed = false;

                // ゾーン最大レベルを取得
                ushort zoneMaxLevel = GetZoneMaxLevel(npc, linkCache);

                // レベルが最大未満なら上書き
                if (npc.Level != zoneMaxLevel)
                {
                    npc.Level = zoneMaxLevel;
                    changed = true;
                }

                // 動的計算を無効化
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
                        // ZonesPatcher で設定された最大レベルを優先
                        if (TUSConstants.ZoneMaxLevels.TryGetValue(zone.FormKey, out var maxLevel))
                            return maxLevel;

                        // zone.MaxLevel が設定されている場合はそれを使用
                        if (zone.MaxLevel.HasValue)
                            return zone.MaxLevel.Value;
                    }
                }
            }
            catch
            {
                // 安全策: 例外が出たらデフォルトにフォールバック
            }

            return 50; // デフォルト最大レベル
        }
    }
}
