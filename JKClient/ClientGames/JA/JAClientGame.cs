using System;
using System.Linq;
using System.Numerics;

namespace JKClient {
	public class JAClientGame : ClientGame {
		public const int SiegeRoundBeginTime = 5000;
		protected readonly int PermanentEntitiesNum;
		private protected readonly ClientEntity []PermanentEntities;
		public int SiegeRoundState { get; private protected set; }
		public int BeatingSiegeTime { get; private protected set; }
		public int SiegeRoundBeganTime { get; private protected set; }
		public int SiegeRoundTime { get; set; }
		public JAClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum)
			: base(client, serverMessageNum, serverCommandSequence, clientNum) {
			this.PermanentEntities = new ClientEntity[Common.MaxGEntities];
			Common.MemSet(this.PermanentEntities, 0);
			for (int i = 0; i < Common.MaxGEntities; i++) {
				ref var cent = ref this.Entities[i];
				if (this.Client.GetDefaultState(i, ref cent.CurrentState, (1<<7))) {
					cent.NextState = cent.CurrentState;
					cent.LerpOrigin = cent.CurrentState.Origin;
					cent.LerpAngles = cent.CurrentState.Angles;
					cent.CurrentValid = true;
					this.PermanentEntities[this.PermanentEntitiesNum++] = cent;
				}
			}
			this.ParseSiegeState(this.GetConfigstring(Configstring.SiegeState));
			if (this.Client.ServerInfo.GameType == GameType.Siege) {
				this.BeatingSiegeTime = this.GetConfigstring(Configstring.SiegeTimeOverride).Atoi();
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
		private protected override void AddPacketEntities() {
			if (this.Snap.PlayerState.VehicleNum != 0) {
				ref var veh = ref this.Entities[this.Snap.PlayerState.VehicleNum];
				if (veh.CurrentState.Owner == this.Snap.PlayerState.ClientNum) {
					this.PlayerStateToEntityState(ref this.Snap.VehiclePlayerState, ref veh.CurrentState);
					veh.CurrentState.EntityType = this.GetEntityType(EntityType.NPC);
					veh.CurrentState.PositionTrajectory.Type = TrajectoryType.Interpolate;
				}
				this.AddClientEntity(ref veh);
				veh.LastVehTime = this.Time;
			}
			this.AddClientEntity(ref this.Entities[this.Snap.PlayerState.ClientNum]);
			for (int num = 0; num < this.Snap.NumEntities; num++) {
				int number = this.Snap.Entities[num].Number;
				if (number != this.Snap.PlayerState.ClientNum) {
					ref var cent = ref this.Entities[number];
					if (cent.CurrentState.EntityType == this.GetEntityType(EntityType.Player) && cent.CurrentState.VehicleNum != 0) {
						int j = 0;
						while (j < this.Snap.NumEntities) {
							if (this.Snap.Entities[j].Number == cent.CurrentState.VehicleNum) {
								ref var veh = ref this.Entities[this.Snap.Entities[j].Number];
								this.AddClientEntity(ref veh);
								veh.LastVehTime = this.Time;
							}
							j++;
						}
					} else if (cent.CurrentState.EntityType == this.GetEntityType(EntityType.NPC) && cent.CurrentState.VehicleNum != 0 && cent.LastVehTime == this.Time) {
						continue;
					}
					this.AddClientEntity(ref cent);
				}
			}
		}
		private protected override bool CalcEntityLerpPositions(ref ClientEntity cent) {
			if (base.CalcEntityLerpPositions(ref cent)) {
				return true;
			}
			ref var currentState = ref cent.CurrentState;
			if (cent.Interpolate && currentState.PositionTrajectory.Type == TrajectoryType.LinearStop
				&& (currentState.Number < this.Client.MaxClients || currentState.EntityType == this.GetEntityType(EntityType.NPC))) {
				this.InterpolateEntityPosition(ref cent);
				return true;
			} else if (cent.Interpolate && currentState.EntityType == this.GetEntityType(EntityType.NPC) && currentState.NPCClass == NPCClass.Vehicle) {
				this.InterpolateEntityPosition(ref cent);
				return true;
			}
			return false;
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
			if (Enum.IsDefined(typeof(EntityEventJA), entityEvent)
				&& Enum.TryParse(((EntityEventJA)entityEvent).ToString(), out EntityEvent result)) {
				return result;
			}
			return EntityEvent.None;
		}
		protected override int GetEntityFlag(EntityFlag entityFlag) {
			if (Enum.TryParse(entityFlag.ToString(), out EntityFlagJA result)) {
				return (int)result;
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
		protected override int GetPowerup(Powerup powerup) {
			if (Enum.TryParse(powerup.ToString(), out PowerupJA result)) {
				return (int)result;
			}
			return 0;
		}
		protected override int GetWeapon(Weapon weapon) {
			if (Enum.TryParse(weapon.ToString(), out WeaponJA result)) {
				return (int)result;
			}
			return 0;
		}
		public override Weapon GetWeapon(ref ClientEntity cent, out bool altFire) {
			altFire = (cent.CurrentState.EntityFlags & (int)EntityFlagJA.AltFiring) != 0;
			if (Enum.IsDefined(typeof(WeaponJA), cent.CurrentState.Weapon)) {
				string ws = ((WeaponJA)cent.CurrentState.Weapon).ToString();
				return Enum.GetValues(typeof(Weapon)).Cast<Weapon>().FirstOrDefault(w => w.ToString() == ws);
			}
			return Weapon.None;
		}
		protected override EntityEvent HandleEvent(EntityEventData eventData) {
			ref var es = ref eventData.Cent.CurrentState;
			if (es.EntityType == this.GetEntityType(EntityType.NPC)) {
				return EntityEvent.None;
			}
			var ev = base.HandleEvent(eventData);
			switch (ev) {
			case EntityEvent.PlayEffect:
				switch (es.EventParm) {
				case 5: //ExplosionTripMine
					es.Weapon = (int)Weapon.TripMine;
					break;
				case 6: //ExplosionDetPack
					es.Weapon = (int)Weapon.DetPack;
					es.EntityFlags |= (int)EntityFlagJA.AltFiring;
					break;
				case 7: //ExplosionFlechette
					es.Weapon = (int)Weapon.Flechette;
					es.EntityFlags |= (int)EntityFlagJA.AltFiring;
					break;
				case 9: //ExplosionDemp2Alt
					es.Weapon = (int)Weapon.Demp2;
					es.EntityFlags |= (int)EntityFlagJA.AltFiring;
					break;
				case 10: //ExplosionTurret
					es.Weapon = (int)Weapon.Turret;
					break;
				default:
					es.Weapon = 0;
					break;
				}
				break;
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
			this.Client.ExecuteEntityEvent(new EntityEventArgs(ev, ref eventData.Cent));
			return ev;
		}
		public override bool IsVehicle(ref ClientEntity cent, ref ClientEntity player) {
			int owner = cent.CurrentState.Owner;
			bool isVehicle = cent.CurrentState.EntityType == this.GetEntityType(EntityType.NPC) && cent.CurrentState.NPCClass == NPCClass.Vehicle
				&& owner >= 0 && owner < this.Client.MaxClients;
			if (isVehicle) {
				player = this.Entities[owner];
			}
			return isVehicle;
		}
		public override Team GetFlagTeam(ref ClientEntity cent) {
			if (cent.CurrentState.EntityType == this.GetEntityType(EntityType.Item)) {
				if (cent.CurrentState.ModelIndex == 46) {
					return Team.Red;
				} else if (cent.CurrentState.ModelIndex == 47) {
					return Team.Blue;
				} else if (cent.CurrentState.ModelIndex == 48) {
					return Team.Free;
				}
			} else {
				return base.GetFlagTeam(ref cent);
			}
			return Team.Spectator;
		}
		public enum ConfigstringJA {
			SiegeState = 293,
			SiegeTimeOverride = 295,
			Sounds = 811,
			Players = 1131
		}
		[Flags]
		public enum EntityFlagJA : int {
			Dead = (1<<1),
			TeleportBit = (1<<3),
			PlayerEvent = (1<<5),
			NoDraw = (1<<8),
			AltFiring = (1<<10)
		}
		public enum EntityEventJA : int {
			None,
			DisruptorMainShot = 35,
			DisruptorSniperShot = 36,
			PlayEffect = 68,
			VoiceCommandSound = 75,
			ConcAltImpact = 84,
			MissileHit = 85,
			MissileMiss = 86,
			MissileMissMetal = 87
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
		public enum PowerupJA : int {
			None,
			Quad,
			Battlesuit,
			Pull,
			RedFlag,
			BlueFlag,
			NeutralFlag,
			ShieldHit,
			SpeedBurst,
			Disint4,
			Speed,
			Cloaked,
			ForceEnlightenedLight,
			ForceEnlightenedDark,
			ForceBoon,
			Ysalamiri,
			NumPowerups
		}
		public enum WeaponJA : int {
			None,
			StunBaton,
			Melee,
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
			Concussion,
			BryarOld,
			EmplacedGun,
			Turret,
			NumWeapons
		}
	}
}
