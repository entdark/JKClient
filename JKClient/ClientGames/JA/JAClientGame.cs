using System;
using System.Linq;

namespace JKClient {
	public class JAClientGame : ClientGame {
		public const int SiegeRoundBeginTime = 5000;
		public int SiegeRoundState { get; private protected set; }
		public int BeatingSiegeTime { get; private protected set; }
		public int SiegeRoundBeganTime { get; private protected set; }
		public int SiegeRoundTime { get; set; }
		public JAClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum)
			: base(client, serverMessageNum, serverCommandSequence, clientNum) {
			this.ParseSiegeState(this.Client.GetConfigstring(this.GetConfigstringIndex(Configstring.SiegeState)));
			if (this.Client.ServerInfo.GameType == GameType.Siege) {
				this.BeatingSiegeTime = this.Client.GetConfigstring(this.GetConfigstringIndex(Configstring.SiegeTimeOverride)).Atoi();
			}
		}
		protected virtual void ParseSiegeState(string str) {
			const char SiegeStatesDivider = '|';
			if (string.IsNullOrEmpty(str)) {
				return;
			}
			if (str.Contains(SiegeStatesDivider)) {
				string []states = str.Split(SiegeStatesDivider);
				this.SiegeRoundState = states[0].Atoi();
				this.SiegeRoundTime = states[1].Atoi();
				if (this.SiegeRoundState == 0 || this.SiegeRoundState == 2) {
					this.SiegeRoundBeganTime = this.SiegeRoundTime;
				}
			} else {
				this.SiegeRoundState = str.Atoi();
				this.SiegeRoundTime = this.Time;
			}
		}
		protected override void ConfigstringModified(Command command) {
			int num = command[1].Atoi();
			int csSiegeState = this.GetConfigstringIndex(Configstring.SiegeState);
			if (num >= csSiegeState && num < csSiegeState+1) {
				this.ParseSiegeState(this.Client.GetConfigstring(num));
			}
			int csSiegeTimeOverride = this.GetConfigstringIndex(Configstring.SiegeTimeOverride);
			if (num >= csSiegeTimeOverride && num < csSiegeTimeOverride+1) {
				this.BeatingSiegeTime = this.Client.GetConfigstring(num).Atoi();
			}
			base.ConfigstringModified(command);
		}
		protected override int GetConfigstringIndex(Configstring index) {
			if (index <= Configstring.Items) {
				return (int)index;
			}
			switch (index) {
			case Configstring.SiegeState:
				return (int)ConfigstringJA.SiegeState;
			case Configstring.SiegeTimeOverride:
				return (int)ConfigstringJA.SiegeTimeOverride;
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
			SiegeState = 293,
			SiegeTimeOverride = 295,
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
