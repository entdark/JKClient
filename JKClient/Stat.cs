﻿using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient {
	internal enum Stat : int {
		Health,
		HoldableItem,
		HoldableItems,
		PersistantPowerup,
		Weapons = 4,
		Armor,
		DeadYaw,
		ClientsReady,
		MaxHealth,
		Max = 16
	}
}
