import * as alt from 'alt-client';
import * as game from 'natives';

export default class Fingerpointing {
  constructor() {
    this.active = false;
    this.interval = null;
    this.cleanStart = false;
    this.localPlayer = alt.Player.local;
  }

  start() {
    if (!this.active) {
      this.active = true;

      alt.Utils.requestAnimDict("anim@mp_point").then(() => {
        game.setPedCurrentWeaponVisible(this.localPlayer.scriptID, false, true, true, true);
        game.setPedConfigFlag(this.localPlayer.scriptID, 36, true);
        game.taskMoveNetworkByName(this.localPlayer.scriptID, "task_mp_pointing", 0.5, false, "anim@mp_point", 24);
        game.removeAnimDict("anim@mp_point");
        this.cleanStart = true;
        this.interval = alt.setInterval(this.process.bind(this), 0);
      }).catch(() => { alt.log('Promise returned reject Pointing') });

    }
  }

  stop() {
    if (this.active) {
      if (this.interval) {
        alt.clearInterval(this.interval);
      }
      this.interval = null;

      this.active = false;

      if (this.cleanStart) {
        this.cleanStart = false;
        game.requestTaskMoveNetworkStateTransition(this.localPlayer.scriptID, "Stop");

        if (!game.isPedInjured(this.localPlayer.scriptID)) {
          game.clearPedSecondaryTask(this.localPlayer.scriptID);
        }
        if (!game.isPedInAnyVehicle(this.localPlayer.scriptID, true)) {
          game.setPedCurrentWeaponVisible(this.localPlayer.scriptID, true, true, true, true);
        }
        game.setPedConfigFlag(this.localPlayer.scriptID, 36, false);
        game.clearPedSecondaryTask(this.localPlayer.scriptID);
      }
    }
  }

  getRelativePitch() {
    let camRot = game.getGameplayCamRot(2);
    return camRot.x - game.getEntityPitch(this.localPlayer.scriptID);
  }

  process() {
    if (this.active) {

      game.isTaskMoveNetworkActive(this.localPlayer.scriptID);

      let camPitch = this.getRelativePitch();

      if (camPitch < -70.0) {
        camPitch = -70.0;
      }
      else if (camPitch > 42.0) {
        camPitch = 42.0;
      }
      camPitch = (camPitch + 70.0) / 112.0;

      let camHeading = game.getGameplayCamRelativeHeading();

      let cosCamHeading = Math.cos(camHeading);
      let sinCamHeading = Math.sin(camHeading);

      if (camHeading < -180.0) {
        camHeading = -180.0;
      }
      else if (camHeading > 180.0) {
        camHeading = 180.0;
      }
      camHeading = (camHeading + 180.0) / 360.0;

      let coords = game.getOffsetFromEntityInWorldCoords(this.localPlayer.scriptID, (cosCamHeading * -0.2) - (sinCamHeading *
        (0.4 * camHeading + 0.3)), (sinCamHeading * -0.2) + (cosCamHeading * (0.4 * camHeading + 0.3)), 0.6);

      let ray = game.startShapeTestCapsule(coords.x, coords.y, coords.z - 0.2, coords.x, coords.y, coords.z + 0.2, 1.0, 95, this.localPlayer.scriptID, 7);
      let [_, blocked, coords1, coords2, entity] = game.getShapeTestResult(ray, false, null, null, null);
      //alt.log("Blocked: " + blocked);
      //alt.log("Entity: " + game.getEntityType(entity));

      game.setTaskMoveNetworkSignalFloat(this.localPlayer.scriptID, "Pitch", camPitch);
      game.setTaskMoveNetworkSignalFloat(this.localPlayer.scriptID, "Heading", camHeading * -1.0 + 1.0);
      game.setTaskMoveNetworkSignalBool(this.localPlayer.scriptID, "isBlocked", blocked);
      game.setTaskMoveNetworkSignalBool(this.localPlayer.scriptID, "isFirstPerson", game.getCamViewModeForContext(game.getCamActiveViewModeContext()) === 4);

    }
  }
}