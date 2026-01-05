using System;
using System.Linq;

using Noggog;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Plugins.Order;

using TrueUnleveledSkyrim.Config;

namespace TrueUnleveledSkyrim.Patch
{
    internal static class ZonesPatcher
    {
        private static ZoneList? ZonesByKeyword;
        private static ZoneList? ZonesByID;

        /// <summary>
        /// STR 用：EncounterZone を完全固定・決定論化
        /// </summary>
        private static void UnlevelZone(
            EncounterZone encZone,
            ZoneEntry zoneDefinition)
        {
            // PC レベル参照を完全無効化
            encZone.Flags.SetFlag(
                EncounterZone.Flag.MatchPcBelowMinimumLevel,
                false);
            encZone.Flags.SetFlag(
                EncounterZone.Flag.MatchPcAboveMaximumLevel,
                false);

            // Combat Boundary 制御
            if (zoneDefinition.EnableCombatBoundary is not null)
            {
                encZone.Flags.SetFlag(
                    EncounterZone.Flag.DisableCombatBoundary,
                    !(bool)zoneDefinition.EnableCombatBoundary);
            }

            // ★ STR 用：完全固定（MaxLevel を採用）
            sbyte fixedLevel = (sbyte)zoneDefinition.MaxLevel;
            encZone.MinLevel = fixedLevel;
            encZone.MaxLevel = fixedLevel;
        }

        private static bool PatchZonesByKeyword(
            EncounterZone encZone,
            ILinkCache linkCache)
        {
            if (!encZone.Location.TryResolve<ILocationGetter>(
                    linkCache, out var location))
                return false;

            for (int i = ZonesByKeyword!.Zones.Count - 1; i >= 0; i--)
            {
                var zoneDefinition = ZonesByKeyword.Zones[i];

                foreach (var kwLink in location.Keywords.EmptyIfNull())
                {
                    if (!kwLink.TryResolve<IKeywordGetter>(
                            linkCache, out var keyword)
                        || keyword.EditorID is null)
                        continue;

                    if (zoneDefinition.Keys.Any(k =>
                            keyword.EditorID.Equals(
                                k, StringComparison.OrdinalIgnoreCase))
                        && !zoneDefinition.ForbiddenKeys.Any(k =>
                            keyword.EditorID.Equals(
                                k, StringComparison.OrdinalIgnoreCase)))
                    {
                        UnlevelZone(encZone, zoneDefinition);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool PatchZonesByID(EncounterZone encZone)
        {
            if (encZone.EditorID is null)
                return false;

            for (int i = ZonesByID!.Zones.Count - 1; i >= 0; i--)
            {
                var zoneDefinition = ZonesByID.Zones[i];

                if (zoneDefinition.Keys.Any(k =>
                        encZone.EditorID.Equals(
                            k, StringComparison.OrdinalIgnoreCase))
                    && !zoneDefinition.ForbiddenKeys.Any(k =>
                        encZone.EditorID.Equals(
                            k, StringComparison.OrdinalIgnoreCase)))
                {
                    UnlevelZone(encZone, zoneDefinition);
                    return true;
                }
            }

            return false;
        }

        public static void PatchZones(
            IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            // Zone 定義ロード
            if (Patcher.ModSettings.Value.Zones.UseMorrowlootZoneBalance)
            {
                ZonesByKeyword = JsonHelper.LoadConfig<ZoneList>(
                    TUSConstants.ZoneTyesKeywordMLUPath);
                ZonesByID = JsonHelper.LoadConfig<ZoneList>(
                    TUSConstants.ZoneTyesEDIDMLUPath);
            }
            else
            {
                ZonesByKeyword = JsonHelper.LoadConfig<ZoneList>(
                    TUSConstants.ZoneTyesKeywordPath);
                ZonesByID = JsonHelper.LoadConfig<ZoneList>(
                    TUSConstants.ZoneTyesEDIDPath);
            }

            uint processedRecords = 0;

            // 除外プラグインキャッシュ
            var forbiddenCache =
                LoadOrder.Import<ISkyrimModGetter>(
                        state.DataFolderPath,
                        Patcher.ModSettings.Value.Zones.PluginFilter,
                        GameRelease.SkyrimSE)
                    .ListedOrder
                    .ToImmutableLinkCache();

            foreach (var zoneGetter in state.LoadOrder
                         .PriorityOrder
                         .EncounterZone()
                         .WinningOverrides())
            {
                if (forbiddenCache.TryResolve(
                        zoneGetter.ToLink(), out _))
                    continue;

                var zoneCopy = zoneGetter.DeepCopy();

                bool wasChanged =
                    PatchZonesByID(zoneCopy)
                    || PatchZonesByKeyword(
                        zoneCopy, Patcher.LinkCache);

                processedRecords++;

                if (processedRecords % 100 == 0)
                {
                    Console.WriteLine(
                        $"Processed {processedRecords} encounter zones.");
                }

                if (wasChanged)
                {
                    state.PatchMod.EncounterZones.Set(zoneCopy);
                }
            }

            // 難易度倍率（同期安全）
            SetGameSetting(
                state,
                Skyrim.GameSetting.fLeveledActorMultEasy,
                Patcher.ModSettings.Value.Zones.EasySpawnLevelMult);

            SetGameSetting(
                state,
                Skyrim.GameSetting.fLeveledActorMultMedium,
                Patcher.ModSettings.Value.Zones.NormalSpawnLevelMult);

            SetGameSetting(
                state,
                Skyrim.GameSetting.fLeveledActorMultHard,
                Patcher.ModSettings.Value.Zones.HardSpawnLevelMult);

            SetGameSetting(
                state,
                Skyrim.GameSetting.fLeveledActorMultVeryHard,
                Patcher.ModSettings.Value.Zones.VeryHardSpawnLevelMult);

            Console.WriteLine(
                $"Processed {processedRecords} encounter zones in total.\n");
        }

        private static void SetGameSetting(
            IPatcherState<ISkyrimMod, ISkyrimModGetter> state,
            IFormLinkGetter<IGameSettingGetter> setting,
            float value)
        {
            if (setting.TryResolve(
                    Patcher.LinkCache,
                    out var resolved)
                && resolved is GameSettingFloat gs)
            {
                var copy = gs.DeepCopy();
                copy.Data = value;
                state.PatchMod.GameSettings.Set(copy);
            }
        }
    }
}
