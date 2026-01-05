using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using System.Collections.Generic;
using System.Linq;

namespace TrueUnleveledSkyrim.Patch
{
    internal static class NPCs
    {
        public static void Patch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var vanillaCache = state.LoadOrder
                .Where(x => x.ModKey.Name == "Skyrim.esm")
                .ToImmutableLinkCache();

            foreach (var npcGetter in state.LoadOrder.PriorityOrder
                         .Npc()
                         .WinningOverrides())
            {
                if (npcGetter.IsDeleted)
                    continue;

                // 固有NPCを除外
                if (ExcludedNPCs.Contains(npcGetter.FormKey))
                    continue;

                var npc = npcGetter.DeepCopy();
                bool changed = false;

                // ===== 能力値・スキル・Perk系のみ =====
                changed |= RebalanceClassValues(npc, state, state.LinkCache);
                changed |= RelevelNPCSkills(npc, state.LinkCache);
                changed |= DistributeNPCPerks(npc, state.LinkCache, vanillaCache);
                changed |= ChangeEquipment(npc, state, state.LinkCache);

                if (changed)
                {
                    state.PatchMod.Npcs.Set(npc);
                }
            }
        }

        // ===============================
        // 除外対象（固有NPC）
        // ===============================
        private static readonly HashSet<FormKey> ExcludedNPCs = new()
        {
            FormKey.Factory("00032D9E:Skyrim.esm"), // Alduin 例
        };

        // ===============================
        // クラス・能力値再構築
        // ===============================
        private static bool RebalanceClassValues(
            Npc npc,
            IPatcherState<ISkyrimMod, ISkyrimModGetter> state,
            ILinkCache linkCache)
        {
            if (npc.Class.IsNull)
                return false;

            var classGetter = npc.Class.Resolve(linkCache);
            if (classGetter == null)
                return false;

            var newClass = classGetter.DeepCopy();
            bool changed = false;

            // スキル比率の正規化
            PopulateSkillWeights(newClass);

            if (!state.PatchMod.Classes.ContainsKey(newClass.FormKey))
            {
                state.PatchMod.Classes.Set(newClass);
                npc.Class.SetTo(newClass.FormKey);
                changed = true;
            }

            return changed;
        }

        private static void PopulateSkillWeights(Class newClass)
        {
            var weights = newClass.SkillWeights;
            if (weights == null || weights.Count == 0)
                return;

            var total = weights.Sum(x => x.Weight);
            if (total <= 0)
                return;

            foreach (var entry in weights)
            {
                entry.Weight /= total;
            }
        }

        // ===============================
        // スキル再配布
        // ===============================
        private static bool RelevelNPCSkills(
            Npc npc,
            ILinkCache linkCache)
        {
            if (npc.Class.IsNull)
                return false;

            var classGetter = npc.Class.Resolve(linkCache);
            if (classGetter == null)
                return false;

            bool changed = false;

            foreach (var skill in npc.Skills)
            {
                if (GetTreeFromSkill(skill.Skill, linkCache, out var actorValue))
                {
                    var weight = classGetter.SkillWeights.FirstOrDefault(x => x.Skill == skill.Skill);
                    if (weight != null)
                    {
                        skill.Level = (ushort)(15 + (weight.Weight * 50));
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private static bool GetTreeFromSkill(Skill activeSkill, ILinkCache linkCache, out IActorValueInformationGetter? actorValue)
        {
            switch (activeSkill)
            {
                case Skill.Alchemy: actorValue = Skyrim.ActorValueInformation.AVAlchemy.TryResolve(linkCache); return true;
                case Skill.Alteration: actorValue = Skyrim.ActorValueInformation.AVAlteration.TryResolve(linkCache); return true;
                case Skill.Archery: actorValue = Skyrim.ActorValueInformation.AVMarksman.TryResolve(linkCache); return true;
                case Skill.Block: actorValue = Skyrim.ActorValueInformation.AVBlock.TryResolve(linkCache); return true;
                case Skill.Conjuration: actorValue = Skyrim.ActorValueInformation.AVConjuration.TryResolve(linkCache); return true;
                case Skill.Destruction: actorValue = Skyrim.ActorValueInformation.AVDestruction.TryResolve(linkCache); return true;
                case Skill.Enchanting: actorValue = Skyrim.ActorValueInformation.AVEnchanting.TryResolve(linkCache); return true;
                case Skill.HeavyArmor: actorValue = Skyrim.ActorValueInformation.AVHeavyArmor.TryResolve(linkCache); return true;
                case Skill.Illusion: actorValue = Skyrim.ActorValueInformation.AVMysticism.TryResolve(linkCache); return true;
                case Skill.LightArmor: actorValue = Skyrim.ActorValueInformation.AVLightArmor.TryResolve(linkCache); return true;
                case Skill.Lockpicking: actorValue = Skyrim.ActorValueInformation.AVLockpicking.TryResolve(linkCache); return true;
                case Skill.OneHanded: actorValue = Skyrim.ActorValueInformation.AVOneHanded.TryResolve(linkCache); return true;
                case Skill.Pickpocket: actorValue = Skyrim.ActorValueInformation.AVPickpocket.TryResolve(linkCache); return true;
                case Skill.Restoration: actorValue = Skyrim.ActorValueInformation.AVRestoration.TryResolve(linkCache); return true;
                case Skill.Smithing: actorValue = Skyrim.ActorValueInformation.AVSmithing.TryResolve(linkCache); return true;
                case Skill.Sneak: actorValue = Skyrim.ActorValueInformation.AVSneak.TryResolve(linkCache); return true;
                case Skill.Speech: actorValue = Skyrim.ActorValueInformation.AVSpeechcraft.TryResolve(linkCache); return true;
                case Skill.TwoHanded: actorValue = Skyrim.ActorValueInformation.AVTwoHanded.TryResolve(linkCache); return true;
                default: actorValue = null; return false;
            }
        }

        // ===============================
        // Perk 配布
        // ===============================
        private static bool DistributeNPCPerks(
            Npc npc,
            ILinkCache linkCache,
            ILinkCache vanillaCache)
        {
            bool changed = false;

            if (npc.Perks.Count > 0)
            {
                npc.Perks.Clear();
                changed = true;
            }

            if (!npc.Class.IsNull)
            {
                var classGetter = npc.Class.Resolve(linkCache);
                if (classGetter != null)
                {
                    foreach (var perk in classGetter.Perks)
                    {
                        npc.Perks.Add(new NpcPerk
                        {
                            Perk = perk.Perk,
                            Rank = perk.Rank
                        });
                        changed = true;
                    }
                }
            }

            return changed;
        }

        // ===============================
        // 装備調整（能力値連動のみ）
        // ===============================
        private static bool ChangeEquipment(
            Npc npc,
            IPatcherState<ISkyrimMod, ISkyrimModGetter> state,
            ILinkCache linkCache)
        {
            if (npc.DefaultOutfit.IsNull)
                return false;

            var outfit = npc.DefaultOutfit.Resolve(linkCache);
            if (outfit == null)
                return false;

            var newOutfit = outfit.DeepCopy();
            bool changed = false;

            if (!state.PatchMod.Outfits.ContainsKey(newOutfit.FormKey))
            {
                state.PatchMod.Outfits.Set(newOutfit);
                npc.DefaultOutfit.SetTo(newOutfit.FormKey);
                changed = true;
            }

            return changed;
        }
    }
}
