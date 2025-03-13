using System;

namespace JKClient {
	public class JOClientGame : ClientGame {
		public JOClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum)
			: base(client, serverMessageNum, serverCommandSequence, clientNum) {}
		protected override int GetConfigstringIndex(Configstring index) {
			if (index <= Configstring.Items) {
				return (int)index;
			}
			switch (index) {
			case Configstring.Sounds:
				return (int)ConfigstringJO.Sounds;
			case Configstring.Players:
				return (int)ConfigstringJO.Players;
			}
			return 0;
		}
		protected override EntityEvent GetEntityEvent(int entityEvent) {
			if (Enum.IsDefined(typeof(EntityEventJO), entityEvent)) {
				switch ((EntityEventJO)entityEvent) {
				default:
					break;
				}
			}
			return EntityEvent.None;
		}
		protected override int GetEntityFlag(EntityFlag entityFlag) {
			if (Enum.IsDefined(typeof(EntityFlagJO), (int)entityFlag)) {
				switch (entityFlag) {
				case EntityFlag.TeleportBit:
					return (int)EntityFlagJO.TeleportBit;
				case EntityFlag.PlayerEvent:
					return (int)EntityFlagJO.PlayerEvent;
				}
			}
			return 0;
		}
		protected override int GetEntityType(EntityType entityType) {
			switch (entityType) {
			default:
				return (int)entityType;
			case EntityType.Grapple:
				return (int)EntityTypeJO.Grapple;
			case EntityType.Events:
				return (int)EntityTypeJO.Events;
			case EntityType.NPC:
			case EntityType.Terrain:
			case EntityType.FX:
				throw new JKClientException($"Invalid entity type: {entityType}");
			}
		}
		public enum ConfigstringJO {
			Sounds = 288,
			Players = 544
		}
		[Flags]
		public enum EntityFlagJO : int {
			TeleportBit = (1<<3),
			PlayerEvent = (1<<5)
		}
		public enum EntityEventJO : int {
			None
		}
		public enum EntityTypeJO : int {
			General,
			Player,
			Item,
			Missile,
			Special,
			Holocron,
			Mover,
			Beam,
			Portal,
			Speaker,
			PushTrigger,
			TeleportTrigger,
			Invisible,
			Grapple,
			Team,
			Body,
			Events
		}
	}
}
