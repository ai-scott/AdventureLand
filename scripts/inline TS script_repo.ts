// Enemy AI - Direct TypeScript for Event Sheet Actions
// This code is designed to be copied directly into "Add TypeScript" actions

// Example usage in event sheet TypeScript actions:

/*
INITIALIZATION (in En_Ooze_Base "On created" event):
Copy this into an "Add TypeScript" action:

if (!(globalThis as any).enemyData) {
  (globalThis as any).enemyData = new Map();
}

const enemy = runtime.objects.En_Ooze_Base.getPickedInstances()[0];
const mask = runtime.objects.En_Ooze_Mask.getPickedInstances()[0];

(globalThis as any).enemyData.set(enemy.uid, {
  maskUid: mask.uid,
  type: "Ooze",
  state: "Idle", 
  stateTimer: Math.floor(Math.random() * 60),
  direction: enemy.instVars.Direction || "Down"
});

console.log(`Initialized Ooze AI: ${enemy.uid}`);

*/

/*
UPDATE (in "Every tick" → "For each En_Ooze_Base" event):
First create a local variable called "aiResult" in your event
Then copy this into an "Add TypeScript" action:

const enemyData = (globalThis as any).enemyData;
if (!enemyData) return;

const enemy = runtime.objects.En_Ooze_Base.getPickedInstances()[0];
const player = runtime.objects.Player_Base.getFirstInstance();
if (!player) return;

const data = enemyData.get(enemy.uid);
if (!data) return;

// Decrement timer
data.stateTimer = Math.max(0, data.stateTimer - 1);

// Ooze AI logic
let result = "none";

if (data.state === "Idle" && data.stateTimer <= 0) {
  data.state = "Hop";
  data.stateTimer = 30;
  
  const angle = Math.atan2(player.y - enemy.y, player.x - enemy.x);
  const speed = 150;
  
  result = `hop|${data.maskUid}|Hop_${data.direction}|${Math.cos(angle) * speed}|${Math.sin(angle) * speed}`;
}
else if (data.state === "Hop" && data.stateTimer <= 0) {
  data.state = "Idle";
  data.stateTimer = 30 + Math.floor(Math.random() * 30);
  
  result = `idle|${data.maskUid}|Idle_${data.direction}|0|0`;
}

localVars.aiResult = result;

*/

/*
CRAB UPDATE (in "Every tick" → "For each En_Crab_Base" event):
First create a local variable called "aiResult" in your event
Then copy this into an "Add TypeScript" action:

const enemyData = (globalThis as any).enemyData;
if (!enemyData) return;

const enemy = runtime.objects.En_Crab_Base.getPickedInstances()[0];
const player = runtime.objects.Player_Base.getFirstInstance();
if (!player) return;

const data = enemyData.get(enemy.uid);
if (!data) return;

// Decrement timer
data.stateTimer = Math.max(0, data.stateTimer - 1);

// Crab AI logic
let result = "none";
const distance = Math.sqrt(Math.pow(player.x - enemy.x, 2) + Math.pow(player.y - enemy.y, 2));

if (data.state === "Idle" && data.stateTimer <= 0) {
  if (distance < 32) {
    data.state = "Attack";
    data.stateTimer = 60;
    result = `attack|${data.maskUid}|Attack_${data.direction}|0|0`;
  } else if (distance < 120) {
    data.state = "Walk";
    data.stateTimer = 30 + Math.floor(Math.random() * 30);
    
    const angle = Math.atan2(player.y - enemy.y, player.x - enemy.x);
    const angleDeg = ((angle * 180 / Math.PI) + 360) % 360;
    
    if (angleDeg >= 315 || angleDeg < 45) data.direction = "Right";
    else if (angleDeg >= 45 && angleDeg < 135) data.direction = "Down";
    else if (angleDeg >= 135 && angleDeg < 225) data.direction = "Left";
    else data.direction = "Up";
    
    result = `walk|${data.maskUid}|Walk_${data.direction}|${Math.cos(angle) * 20}|${Math.sin(angle) * 20}|${data.direction}`;
  } else {
    data.state = "Walk";
    data.stateTimer = 30 + Math.floor(Math.random() * 30);
    
    const directions = ["Up", "Down", "Left", "Right"];
    data.direction = directions[Math.floor(Math.random() * directions.length)];
    
    let vx = 0, vy = 0;
    if (data.direction === "Up") { vx = 0; vy = -20; }
    else if (data.direction === "Down") { vx = 0; vy = 20; }
    else if (data.direction === "Left") { vx = -20; vy = 0; }
    else if (data.direction === "Right") { vx = 20; vy = 0; }
    
    result = `walk|${data.maskUid}|Walk_${data.direction}|${vx}|${vy}|${data.direction}`;
  }
}
else if ((data.state === "Walk" || data.state === "Attack") && data.stateTimer <= 0) {
  data.state = "Idle";
  data.stateTimer = 12 + Math.floor(Math.random() * 30);
  result = `idle|${data.maskUid}|Idle_${data.direction}|0|0`;
}

localVars.aiResult = result;

*/

// This file serves as documentation and code snippets
// The actual TypeScript goes directly into event sheet actions
console.log("Enemy AI code snippets loaded - copy code from comments into TypeScript actions");