using CalamityMod.Events;
using CalamityMod.Systems;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BRBoss = CalamityMod.Events.BossRushEvent.Boss;
using Calamity = CalamityMod.CalamityMod;

namespace BossRushPlus
{
	public class BossRushPlus : Mod
	{
		public record BossRushBossData(BRBoss Value, int IndexToAddAt);

		private readonly List<ILHook> hooks = [];

		public static BindingFlags Flags => BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic;

		public override void Load()
		{
			MethodInfo propertyInfo = typeof(BossRushEvent).GetMethod("get_MusicToPlay", Flags);
			hooks.Add(new(propertyInfo, ILEdit_BRMusicToPlay));

			MethodInfo propertyInfo2 = typeof(BossRushScene).GetMethod("get_Priority", Flags);
			hooks.Add(new(propertyInfo2, ILEdit_BRMusicPriority));

			foreach (ILHook hook in hooks)
				hook.Apply();
		}

		public override void Unload()
		{
			foreach (ILHook hook in hooks)
			{
				hook.Undo();
				hook.Dispose();
			}
			hooks.Clear();
		}

		private void ILEdit_BRMusicToPlay(ILContext il)
		{
			var cursor = new ILCursor(il);
			cursor.EmitDelegate(() =>
			{
				if (ModLoader.HasMod("CalamityModMusic"))
				{
					if (BossRushEvent.CurrentTier <= 3)
						return ModContent.GetInstance<Calamity>().GetMusicFromMusicMod($"BossRushTier{BossRushEvent.CurrentTier}") ?? 0;
					else if (BossRushEvent.CurrentTier <= 5)
						return MusicLoader.GetMusicSlot($"BossRushPlus/Assets/Sounds/Music/BossRushTier{BossRushEvent.CurrentTier}");
				}

				switch (BossRushEvent.CurrentTier)
				{
					case 1:
						return MusicID.Boss1;
					case 2:
						return MusicID.Boss4;
					case 3:
						return MusicID.Boss2;
					case 4:
						return MusicLoader.GetMusicSlot($"BossRushPlus/Assets/Sounds/Music/BossRushTier4");
					case 5:
						return MusicLoader.GetMusicSlot($"BossRushPlus/Assets/Sounds/Music/BossRushTier5");
					default:
						break;
				}
				return 0;
			});
			cursor.Emit(OpCodes.Ret);
		}

		private void ILEdit_BRMusicPriority(ILContext il)
		{
			var cursor = new ILCursor(il);
			cursor.EmitDelegate(() =>
			{
				return (SceneEffectPriority)99;
			});
			cursor.Emit(OpCodes.Ret);
		}

		// Done here to ensure that the list already exists.
		public override void PostSetupContent()
		{
			List<BossRushBossData> bossesInfo = [];
			int endCount = BossRushEvent.Bosses.Count;
			var mlBoss = BossRushEvent.Bosses.Find(boss => boss.EntityID == NPCID.MoonLordCore);
			int mlIndex = BossRushEvent.Bosses.IndexOf(mlBoss);

			if (ModLoader.TryGetMod("CatalystMod", out var catalyst))
			{
				BRBoss astrageldon = new(catalyst.Find<ModNPC>("Astrageldon").Type, BossRushEvent.TimeChangeContext.Night, permittedNPCs:
					[
						catalyst.Find<ModNPC>("NovaSlime").Type,
						catalyst.Find<ModNPC>("NovaSlimer").Type,
					]);
				bossesInfo.Add(new(astrageldon, mlIndex));
				endCount++;
			}

			if (ModLoader.TryGetMod("NoxusBoss", out var noxusMod))
			{
				BRBoss noxus = new(noxusMod.Find<ModNPC>("EntropicGod").Type);
				bossesInfo.Add(new(noxus, endCount));
				endCount++;

				BRBoss nameless = new(noxusMod.Find<ModNPC>("NamelessDeityBoss").Type, spawnContext: _ =>
				{
					// Set him to be downed allowing for the skip. If you dont skip it, he kicks you out and you dont get the rock but consider that a final challenge or smth lol.
					var saveSystem = noxusMod.Find<ModSystem>("WorldSaveSystem");
					var propertyInfo = saveSystem.GetType().GetProperty("HasDefeatedNamelessDeity", Flags);
					propertyInfo.SetValue(null, true);
					NPC.SpawnOnPlayer(BossRushEvent.ClosestPlayerToWorldCenter, noxusMod.Find<ModNPC>("NamelessDeityBoss").Type);
				});
				bossesInfo.Add(new(nameless, endCount));
				endCount++;
			}

			foreach (var info in bossesInfo)
			{
				BossRushEvent.Bosses.Insert(info.IndexToAddAt, info.Value);
			}
		}
	}
}