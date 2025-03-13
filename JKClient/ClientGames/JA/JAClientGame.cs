using System;

namespace JKClient {
	public class JAClientGame : ClientGame {
		public JAClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum)
			: base(client, serverMessageNum, serverCommandSequence, clientNum) {}
		protected override int GetConfigstringIndex(Configstring index) {
			if (index <= Configstring.Items) {
				return (int)index;
			}
			switch (index) {
			case Configstring.Sounds:
				return (int)ConfigstringJA.Sounds;
			case Configstring.Players:
				return (int)ConfigstringJA.Players;
			}
			return 0;
		}
		protected override EntityEvent GetEntityEvent(int entityEvent) {
			if (Enum.IsDefined(typeof(EntityEventJA), entityEvent)) {
				switch ((EntityEventJA)entityEvent) {
				case EntityEventJA.VoiceCommandSound:
					return EntityEvent.VoiceCommandSound;
				}
			}
			return EntityEvent.None;
		}
		protected override int GetEntityFlag(EntityFlag entityFlag) {
			if (Enum.IsDefined(typeof(EntityFlagJA), (int)entityFlag)) {
				switch (entityFlag) {
				case EntityFlag.TeleportBit:
					return (int)EntityFlagJA.TeleportBit;
				case EntityFlag.PlayerEvent:
					return (int)EntityFlagJA.PlayerEvent;
				}
			}
			return 0;
		}
		protected override int GetEntityType(EntityType entityType) {
			switch (entityType) {
			default:
				return (int)entityType;
			case EntityType.Events:
				return (int)EntityTypeJA.Events;
			case EntityType.Grapple:
				throw new JKClientException($"Invalid entity type: {entityType}");
			}
		}
		protected override EntityEvent HandleEvent(EntityEventData eventData) {
			ref var es = ref eventData.Cent.CurrentState;
			if (es.EntityType == this.GetEntityType(EntityType.NPC)) {
				return EntityEvent.None;
			}
			var ev = base.HandleEvent(eventData);
			switch (ev) {
			case EntityEvent.VoiceCommandSound:
				if (es.GroundEntityNum >= 0 && es.GroundEntityNum < this.Client.MaxClients) {
					int clientNum = es.GroundEntityNum;
					string description = this.Client.GetConfigstring(this.GetConfigstringIndex(Configstring.Sounds) + es.EventParm);
					string message = $"<{this.ClientsInfo[clientNum].Name}^7{Common.EscapeCharacter}: {description}>";
					var command = new Command(new string[] { "chat", message });
					this.Client.ExecuteServerCommand(new CommandEventArgs(command));
				}
				break;
			}
			return ev;
		}
		public enum ConfigstringJA {
			Sounds = 811,
			Players = 1131
		}
		[Flags]
		public enum EntityFlagJA : int {
			TeleportBit = (1<<3),
			PlayerEvent = (1<<5)
		}
		public enum EntityEventJA : int {
			None,
			VoiceCommandSound = 75
		}
		public enum EntityTypeJA : int {
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
			NPC,
			Team,
			Body,
			Terrain,
			FX,
			Events
		}
	}
}
