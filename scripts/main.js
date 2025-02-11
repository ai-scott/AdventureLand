// Import any other script files here, e.g.:
// import * as myModule from "./mymodule.js";
// Use a module to store our game's global variables
import Globals from "./globals.js";
runOnStartup(async (runtime) => {
    // Code to run on the loading screen.
    // Note layouts, objects etc. are not yet available.
    runtime.addEventListener("beforeprojectstart", () => OnBeforeProjectStart(runtime));
});
async function OnBeforeProjectStart(runtime) {
    // Code to run just before 'On start of layout' on
    // the first layout. Loading has finished and initial
    // instances are created and available to use here.
    runtime.addEventListener("tick", () => Tick(runtime));
    // Store the only Player and GameOverText instances as globals.
    // Note this is done every time the layout is started, since restarting
    // the layout re-creates all its instances.
    Globals.playerInstance = runtime.objects.Player_Base.getFirstInstance();
    Globals.pennyInstance = runtime.objects.Penny.getFirstInstance();
    Globals.boyInstance = runtime.objects.Player_Boy.getFirstInstance();
    Globals.girlInstance = runtime.objects.Player_Girl.getFirstInstance();
    //const player = runtime.objects.Player_Base.getFirstInstance()!;
    //const character = runtime.objects.Penny.getFirstInstance()!;
    console.log("Globals.pennyInstance" + Globals.pennyInstance);
    const characters = [Globals.pennyInstance, Globals.boyInstance, Globals.girlInstance];
    runtime.collisions.setCollisionCellSize(40, 40);
    //runtime.collisions.testOverlapAny(Globals.playerInstance, characters)	
    //let target = runtime.collisions.getCollisionCandidates(characters, playerRect);
}
function Tick(runtime) {
    // Code to run every tick	
    //runtime.collisions.getCollisionCandidates(characters, DOMRect).
    Globals.playerInstance = runtime.objects.Player_Base.getFirstInstance();
    Globals.pennyInstance = runtime.objects.Penny.getFirstInstance();
    Globals.boyInstance = runtime.objects.Player_Boy.getFirstInstance();
    Globals.girlInstance = runtime.objects.Player_Girl.getFirstInstance();
    /*
        Globals.pennyObj = runtime.objects.Penny;
        Globals.boyObj = runtime.objects.Player_Boy;
        Globals.girlObj = runtime.objects.Player_Girl;
    */
    /*

    runtime.collisions.testOverlapAny(Globals.playerInstance, characters)

    if(runtime.collisions.getCollisionCandidates(Globals.pennyInstance!, DOMRect)){

        console.log("Hi Penny!");
    }
*/
    /*
    let	playerRect : DOMRect;
    playerRect = new DOMRect((player.x-20), (player.y-20), 50, 50);
    runtime.collisions.testOverlapAny(Globals.playerInstance, characters)
    */
    const player = runtime.objects.Player_Base.getFirstInstance();
    const character = runtime.objects.Penny.getFirstInstance();
    if (player) {
        let playerRect;
        playerRect = new DOMRect((player.x - 20), (player.y - 20), 50, 50);
        const characters = [runtime.objects.Penny, runtime.objects.Player_Boy, runtime.objects.Player_Girl];
        const nearChars = runtime.collisions.getCollisionCandidates(characters, playerRect);
        let item;
        for (item of nearChars.values()) {
            //	console.log(item.objectType.name);
            //runtime.callFunction("beginDialogue",item.objectType.name,"quest");
        }
    }
    ;
    //console.log(character);
    //if(player.testOverlap(character)){
}
