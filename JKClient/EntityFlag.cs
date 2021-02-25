using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient {
	//not flags because values can be repeated
	internal enum EntityFlag : int {
		TeleportBit = (1<<3),
		PlayerEvent = (1<<5)
	}
	[Flags]
	internal enum EntityFlagJA : int {
		TeleportBit = (1<<3),
		PlayerEvent = (1<<5)
	}
	[Flags]
	internal enum EntityFlagJO : int {
		TeleportBit = (1<<3),
		PlayerEvent = (1<<5)
	}
}
