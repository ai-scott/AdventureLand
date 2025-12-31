// scripts/enemy-configs.ts - Enemy Configuration Data with Extended Types
// This is the main enemy configuration file for Adventure Land

// ===== TYPE DEFINITIONS =====
export interface BehaviorCondition {
  type: "distance" | "health" | "timer" | "random" | "hurt" | "invulnerable";
  operator: "<" | ">" | "<=" | ">=" | "==";
  value: number;
}

// Extended ActionConfig to support all action types from enemy-ai.ts
export interface ActionConfig {
  type: "move" | "animate" | "sound" | "invulnerable" | "set_effect";
  params: {
    // Movement params
    pattern?: "toward_player" | "away_from_player" | "random" | "stop" | "sideways_left" | "sideways_right" | "crab_toward_player" | "swoop_to_player" | "flee_to_nearest_tree" | "idle_in_tree";
    speed?: number;

    // Animation params
    name?: string;

    // Invulnerability params
    duration?: number;

    // Sound params
    sound?: string;

    // Effect params
    effect?: string;
    parameter?: string;
    value?: number;
    enabled?: boolean;
  };
}

export interface BehaviorConfig {
  name: string;
  duration: [number, number];
  weight: number;
  cooldown?: number;
  conditions?: BehaviorCondition[];
  actions: ActionConfig[];
}

export interface EnemyConfig {
  type: string;
  baseStats: {
    health: number;
    speed: number;
    viewDistance: number;
    attackDistance: number;
  };
  behaviors: BehaviorConfig[];
}

export interface EnemyData {
  maskUid: number;
  type: string;
  state: string;
  stateTimer: number;
  direction: string;
  config: EnemyConfig;
  currentBehavior: BehaviorConfig;
  lastPlayerDistance: number;
  behaviorCooldowns: Map<string, number>;
  behaviorStarted: boolean;
  isHurt: boolean;
  invulnerableTimer: number;
  sidewaysDirection: string;
  currentAnimation?: string; // Track current animation to prevent spam
}

// Helper function to get enemy config
export function getEnemyConfig(type: string): EnemyConfig | null {
  const configs: { [key: string]: EnemyConfig } = {
    "Ooze": OOZE_CONFIG,
    "Crab": CRAB_CONFIG,
    "Bat": BAT_CONFIG
  };
  return configs[type] || null;
}

// ===== ENEMY CONFIGURATIONS =====
export const OOZE_CONFIG: EnemyConfig = {
  type: "Ooze",
  baseStats: {
    health: 2,
    speed: 15,
    viewDistance: 120,
    attackDistance: 0
  },
  behaviors: [
    {
      name: "idle",
      duration: [1.0, 2.0],
      weight: 4,
      actions: [
        { type: 'animate', params: { name: 'Idle_{direction}' } },
        { type: 'move', params: { pattern: 'toward_player', speed: 20 } }
      ]
    },
    {
      name: "hop",
      duration: [0.8, 1.2],
      weight: 2,
      cooldown: 2.0,
      conditions: [
        { type: 'distance', operator: '<', value: 250 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Hop_{direction}' } },
        { type: 'move', params: { pattern: 'toward_player', speed: 50 } },
        { type: 'sound', params: { sound: 'Slime_Jump' } }
      ]
    },
    {
      name: "hurt",
      duration: [0.5, 0.5],
      weight: 0,
      conditions: [
        { type: 'hurt', operator: '==', value: 1 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Hurt_{direction}' } },
        { type: 'move', params: { pattern: 'stop' } },
        { type: 'invulnerable', params: { duration: 1.0 } }
      ]
    }
  ]
};

export const CRAB_CONFIG: EnemyConfig = {
  type: "Crab",
  baseStats: {
    health: 3,
    speed: 20,
    viewDistance: 150,
    attackDistance: 32
  },
  behaviors: [
    {
      name: "patrol",
      duration: [2.0, 4.0],
      weight: 3,
      conditions: [
        { type: 'distance', operator: '>', value: 150 }  // Beyond viewDistance
      ],
      actions: [
        { type: 'animate', params: { name: 'Walk_{direction}' } },
        { type: 'move', params: { pattern: 'random', speed: 15 } }
      ]
    },
    {
      name: "cranky_chase",
      duration: [1.5, 3.0],
      weight: 6,
      conditions: [
        { type: 'distance', operator: '<', value: 150 },  // Within viewDistance
        { type: 'distance', operator: '>', value: 32 }    // But beyond attackDistance
      ],
      actions: [
        { type: 'animate', params: { name: 'Cranky_{direction}' } },
        { type: 'move', params: { pattern: 'crab_toward_player', speed: 35 } }
      ]
    },
    {
      name: "attack",
      duration: [0.5, 1.0],
      weight: 10,
      conditions: [
        { type: 'distance', operator: '<', value: 32 }  // Within attackDistance
      ],
      actions: [
        { type: 'animate', params: { name: 'Attack_{direction}' } },
        { type: 'move', params: { pattern: 'toward_player', speed: 50 } }
      ]
    },
    {
      name: "hurt_flash",
      duration: [0.2, 0.2],  // Exactly 0.2 seconds
      weight: 0,
      conditions: [
        { type: 'hurt', operator: '==', value: 1 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Hurt' } },
        { type: 'move', params: { pattern: 'stop' } },
        { type: 'invulnerable', params: { duration: 0.7 } }  // Total 0.7s (0.2 + 0.5) (removed SetColor effect since it doesn't exist)
      ]
    },
    {
      name: "retreat",
      duration: [2.0, 2.0],  // Even longer duration for guaranteed visibility
      weight: 99,   // High priority when conditions met, but not overwhelming during init
      cooldown: 1.0,  // Reduced from 2.0s for better responsiveness
      conditions: [
        { type: 'hurt', operator: '==', value: 0 },     // Not currently in hurt state
        { type: 'invulnerable', operator: '==', value: 1 }  // But still invulnerable (just after hurt)
      ],
      actions: [
        { type: 'animate', params: { name: 'Retreat_{direction}' } },
        { type: 'move', params: { pattern: 'away_from_player', speed: 100 } },  // MUCH faster for dramatic visibility
        { type: 'sound', params: { sound: 'Crab_Retreat' } }  // Audio feedback for retreat (removed SetColor effect since it doesn't exist)
      ]
    }
  ]
};

export const BAT_CONFIG: EnemyConfig = {
  type: "Bat",
  baseStats: {
    health: 12,
    speed: 32,
    viewDistance: 202,
    attackDistance: 88
  },
  behaviors: [
    {
      name: "idle_hanging",
      duration: [1.0, 2.0],  // Longer idle for visible rest at tree (was 0.4-0.7)
      weight: 1,  // Lowest - always available failsafe
      conditions: [],  // NO CONDITIONS - prevents empty behavior list
      actions: [
        { type: 'animate', params: { name: 'Idle' } },
        { type: 'move', params: { pattern: 'idle_in_tree' } }
      ]
    },
    {
      name: "swoop_attack",
      duration: [2.0, 2.5],  // 2.5s swoop - longer pursuit (was 1.5-2.0)
      weight: 15,  // Highest priority - always swoop when available
      cooldown: 3.5,  // 3.5s cooldown - ensures 1-2s idle after flee
      conditions: [
        { type: 'distance', operator: '<', value: 202 }  // Within viewDistance
      ],
      actions: [
        { type: 'animate', params: { name: 'Fly_Left' } },  // Bat uses _Left, mirroring handles right (switches to Attack_Left when close)
        { type: 'move', params: { pattern: 'swoop_to_player', speed: 64 } }  // Faster swoop speed - auto-triggers bite animation when < 40px
      ]
    },
    {
      name: "hurt_flash",
      duration: [0.1, 0.1],  // Brief hurt flash
      weight: 0,  // Not selected randomly
      conditions: [
        { type: 'hurt', operator: '==', value: 1 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Hurt_Left' } },  // Use _Left, mirroring handles direction
        { type: 'move', params: { pattern: 'stop' } },
        { type: 'invulnerable', params: { duration: 1.5 } }  // 1.5 seconds invulnerability
      ]
    },
    {
      name: "flee_to_tree",
      duration: [10.0, 10.0],  // Long max duration - always ends early when tree reached (line 723)
      weight: 0,  // NEVER randomly selected - only forced after swoop/hurt
      conditions: [],  // NO CONDITIONS - can flee regardless of player distance
      actions: [
        { type: 'animate', params: { name: 'Fly_Left' } },
        { type: 'move', params: { pattern: 'flee_to_nearest_tree', speed: 48 } }  // Return to tree
      ]
    }
  ]
};