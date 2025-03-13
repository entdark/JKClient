using System;

namespace JKClient {
	public class Q3ClientGame : ClientGame {
		public Q3ClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum)
			: base(client, serverMessageNum, serverCommandSequence, clientNum) {}
		protected override int GetConfigstringIndex(Configstring index) {
			if (index <= Configstring.Items) {
				return (int)index;
			}
			switch (index) {
			case Configstring.Sounds:
				return (int)ConfigstringQ3.Sounds;
			case Configstring.Players:
				return (int)ConfigstringQ3.Players;
			}
			return 0;
		}
		protected override EntityEvent GetEntityEvent(int entityEvent) {
			if (Enum.IsDefined(typeof(EntityEventQ3), entityEvent)) {
				switch ((EntityEventQ3)entityEvent) {
				default:
					break;
				}
			}
			return EntityEvent.None;
		}
		protected override int GetEntityFlag(EntityFlag entityFlag) {
			if (Enum.IsDefined(typeof(EntityFlagQ3), (int)entityFlag)) {
				switch (entityFlag) {
				case EntityFlag.TeleportBit:
					return (int)EntityFlagQ3.TeleportBit;
				case EntityFlag.PlayerEvent:
					return (int)EntityFlagQ3.PlayerEvent;
				}
			}
			return 0;
		}
		protected override int GetEntityType(EntityType entityType) {
			if (entityType >= EntityType.Mover && entityType <= EntityType.Invisible) {
				entityType-=2;
			}
			switch (entityType) {
			default:
				return (int)entityType;
			case EntityType.Grapple:
				return (int)EntityTypeQ3.Grapple;
			case EntityType.Team:
				return (int)EntityTypeQ3.Team;
			case EntityType.Events:
				return (int)EntityTypeQ3.Events;
			case EntityType.Special:
			case EntityType.Holocron:
			case EntityType.NPC:
				throw new JKClientException($"Invalid entity type: {entityType}");
			}
		}
		public enum ConfigstringQ3 {
			GameVersion = 20,
			Sounds = 288,
			Players = 544
		}
		[Flags]
		public enum EntityFlagQ3 : int {
			TeleportBit = (1<<2),
			PlayerEvent = (1<<4)
		}
		public enum EntityEventQ3 : int {
			None
		}
		public enum EntityTypeQ3 : int {
			General,
			Player,
			Item,
			Missile,
			Mover,
			Beam,
			Portal,
			Speaker,
			PushTrigger,
			TeleportTrigger,
			Invisible,
			Grapple,
			Team,
			Events
		}
	}
}
