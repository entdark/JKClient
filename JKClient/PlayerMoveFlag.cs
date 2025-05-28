using System;

namespace JKClient {
	[Flags]
	internal enum PlayerMoveFlag : int {
		Ducked = (1<<0),
		JumpHeld = (1<<1),
		Rolling = (1<<2),
		BackwardJump = (1<<3),
		BackwardRun = (1<<4),
		TimeLand = (1<<5),
		TimeKnockback = (1<<6),
		FixMins = (1<<7),
		TimeWaterjump = (1<<8),
		Respawned = (1<<9),
		UseItemHeld = (1<<10),
		GrapplePull = (1<<11),
		UpdateAnim = (1<<11),
		Follow = (1<<12),
		Scoreboard = (1<<13),
		Invulexpand = (1<<14),
		StuckToWall = (1<<14),
		AllTimes = (TimeWaterjump|TimeLand|TimeKnockback)
	}
}