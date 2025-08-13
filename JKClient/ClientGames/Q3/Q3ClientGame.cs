using System;
using System.Linq;

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
			if (Enum.IsDefined(typeof(EntityEventQ3), entityEvent)
				&& Enum.TryParse(((EntityEventQ3)entityEvent).ToString(), out EntityEvent result)) {
				return result;
			}
			return EntityEvent.None;
		}
		protected override int GetEntityFlag(EntityFlag entityFlag) {
			if (Enum.TryParse(entityFlag.ToString(), out EntityFlagQ3 result)) {
				return (int)result;
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
		protected override int GetPowerup(Powerup powerup) {
			if (Enum.TryParse(powerup.ToString(), out PowerupQ3 result)) {
				return (int)result;
			}
			return 0;
		}
		protected override int GetWeapon(Weapon weapon) {
			if (Enum.TryParse(weapon.ToString(), out WeaponQ3 result)) {
				return (int)result;
			}
			return 0;
		}
		public override Weapon GetWeapon(ref ClientEntity cent, out bool firing) {
			firing = false;
			if (Enum.IsDefined(typeof(WeaponQ3), cent.CurrentState.Weapon)) {
				string ws = ((WeaponQ3)cent.CurrentState.Weapon).ToString();
				firing = (cent.CurrentState.EntityFlags & (int)EntityFlagQ3.Firing) != 0;
				return Enum.GetValues(typeof(Weapon)).Cast<Weapon>().FirstOrDefault(w => w.ToString() == ws);
			}
			return Weapon.None;
		}
		public override Team GetFlagTeam(ref ClientEntity cent) {
			if (cent.CurrentState.EntityType == this.GetEntityType(EntityType.Item)) {
				if (cent.CurrentState.ModelIndex == 34) {
					return Team.Red;
				} else if (cent.CurrentState.ModelIndex == 35) {
					return Team.Blue;
				}
			} else {
				return base.GetFlagTeam(ref cent);
			}
			return Team.Spectator;
		}
		protected override EntityEvent HandleEvent(EntityEventData eventData) {
			var ev = base.HandleEvent(eventData);
			this.Client.ExecuteEntityEvent(new EntityEventArgs(ev, ref eventData.Cent));
			return ev;
		}
		public enum ConfigstringQ3 {
			GameVersion = 20,
			Sounds = 288,
			Players = 544
		}
		[Flags]
		public enum EntityFlagQ3 : int {
			Dead = (1<<0),
			TeleportBit = (1<<2),
			PlayerEvent = (1<<4),
			NoDraw = (1<<7),
			Firing = (1<<8)
		}
		public enum EntityEventQ3 : int {
			None,
			FireWeapon = 23,
			BulletHitFlesh = 48,
			BulletHitWall = 49,
			MissileHit = 50,
			MissileMiss = 51,
			MissileMissMetal = 52,
			Railtrail = 53,
			Shotgun = 54
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
		public enum PowerupQ3 : int {
			None,
			Quad,
			Battlesuit,
			Haste,
			Invis,
			Regen,
			Flight,
			RedFlag,
			BlueFlag,
			NeutralFlag,
			Scout,
			Guard,
			Doubler,
			AmmoRegen,
			Invulnerability,
			NumPowerups
		}
		public enum WeaponQ3 : int {
			None,
			Gauntlet,
			Machinegun,
			Shotgun,
			GrenadeLauncher,
			RocketLauncher,
			Lightning,
			Railgun,
			Plasmagun,
			BFG,
			GrapplingHook,
			NumWeapons
		}
	}
}
