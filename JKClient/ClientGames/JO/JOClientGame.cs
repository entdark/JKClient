using System;
using System.Linq;

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
			if (Enum.TryParse(entityFlag.ToString(), out EntityFlagJO result)) {
				return (int)result;
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
		protected override int GetPowerup(Powerup powerup) {
			if (Enum.TryParse(powerup.ToString(), out PowerupJO result)) {
				return (int)result;
			}
			return 0;
		}
		protected override int GetWeapon(Weapon weapon) {
			if (Enum.TryParse(weapon.ToString(), out WeaponJO result)) {
				return (int)result;
			}
			return 0;
		}
		public override Weapon GetWeapon(ref ClientEntity cent, out bool altFire) {
			altFire = (cent.CurrentState.EntityFlags & (int)EntityFlagJO.AltFiring) != 0;
			if (Enum.IsDefined(typeof(WeaponJO), cent.CurrentState.Weapon)) {
				string ws = ((WeaponJO)cent.CurrentState.Weapon).ToString();
				return Enum.GetValues(typeof(Weapon)).Cast<Weapon>().FirstOrDefault(w => w.ToString() == ws);
			}
			return Weapon.None;
		}
		protected override EntityEvent HandleEvent(EntityEventData eventData) {
			ref var es = ref eventData.Cent.CurrentState;
			var ev = base.HandleEvent(eventData);
			switch (ev) {
			case EntityEvent.PlayEffect:
				switch (es.EventParm) {
				case 5: //ExplosionTripMine
					es.Weapon = (int)Weapon.TripMine;
					break;
				case 6: //ExplosionDetPack
					es.Weapon = (int)Weapon.DetPack;
					es.EntityFlags |= (int)EntityFlagJO.AltFiring;
					break;
				case 7: //ExplosionFlechette
					es.Weapon = (int)Weapon.Flechette;
					es.EntityFlags |= (int)EntityFlagJO.AltFiring;
					break;
				case 9: //ExplosionDemp2Alt
					es.Weapon = (int)Weapon.Demp2;
					es.EntityFlags |= (int)EntityFlagJO.AltFiring;
					break;
				default:
					es.Weapon = 0;
					break;
				}
				break;
			}
			this.Client.ExecuteEntityEvent(new EntityEventArgs(ev, ref eventData.Cent));
			return ev;
		}
		public override Team GetFlagTeam(ref ClientEntity cent) {
			if (cent.CurrentState.EntityType == this.GetEntityType(EntityType.Item)) {
				if (cent.CurrentState.ModelIndex == 37) {
					return Team.Red;
				} else if (cent.CurrentState.ModelIndex == 38) {
					return Team.Blue;
				} else if (cent.CurrentState.ModelIndex == 39) {
					return Team.Free;
				}
			} else {
				return base.GetFlagTeam(ref cent);
			}
			return Team.Spectator;
		}
		public enum ConfigstringJO {
			Sounds = 288,
			Players = 544
		}
		[Flags]
		public enum EntityFlagJO : int {
			Dead = (1<<0),
			TeleportBit = (1<<3),
			PlayerEvent = (1<<5),
			NoDraw = (1<<7),
			AltFiring = (1<<9)
		}
		public enum EntityEventJO : int {
			None,
			DisruptorMainShot = 31,
			DisruptorSniperShot = 32,
			PlayEffect = 63,
			MissileHit = 73,
			MissileMiss = 74,
			MissileMissMetal = 75
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
		public enum PowerupJO : int {
			None,
			Quad,
			Battlesuit,
			Haste,
			RedFlag,
			BlueFlag,
			NeutralFlag,
			ShieldHit,
			SpeedBurst,
			Disint4,
			Speed,
			ForceLightning,
			ForceEnlightenedLight,
			ForceEnlightenedDark,
			ForceBoon,
			Ysalamiri,
			NumPowerups
		}
		public enum WeaponJO : int {
			None,
			StunBaton,
			Saber,
			BryarPistol,
			Blaster,
			Disruptor,
			Bowcaster,
			Repeater,
			Demp2,
			Flechette,
			RocketLauncher,
			Thermal,
			TripMine,
			DetPack,
			EmplacedGun,
			Turret,
			NumWeapons
		}
	}
}
