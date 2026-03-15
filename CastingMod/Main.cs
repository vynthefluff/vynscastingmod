using System;
using System.Collections.Generic;
using BepInEx;
using GorillaLocomotion;
using GorillaNetworking;
using OVR.OpenVR;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

/*
 
 *  Vyn's casting mod - developed by Frapster/vyn (@vynthefluff on github)
 *  Sorry for not writing many comments - I normally don't purposefully open-
 *  source my projects, so I rarely remember to xD.
 *  Enjoy the casting mod :3
 
*/

namespace vynscastingmod
{
    [BepInPlugin(modId, modName, modVer)]
    public class Main : BaseUnityPlugin
    {
        public const string modId = "com.vyn.castingClient";
        public const string modName = "CastingClient";
        public const string modVer = "1.0.0";
        
        public void Update()
        {
            if (!initialized)
            {
                try
                {
                    if (OpenVR.IsHmdPresent())
                    {
                        Destroy(this); // If user is in VR, destroy the casting mod.
                        return;
                    }
                }
                catch (Exception) { } // Normally if OpenVR isnt initialised, they're running in the oculus version without a headset connected, so they likely arent in VR.


                if (GTPlayer.Instance == null) return;
                offlineRig = GorillaTagger.Instance.offlineVRRig;
                loadedRigs.Add(offlineRig);
                
                Destroy(GorillaTagger.Instance.thirdPersonCamera);
                camera = new GameObject("CastingClient").AddComponent<Camera>();

                camera.cameraType = CameraType.Game;
                camera.fieldOfView = 90;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 2500;
                
                Application.targetFrameRate = int.MaxValue; // Gtag's fps is capped at 144 by default - no thanks.
                initialized = true;
            }

            HandleLoadedRigs();
            HandleTargetSwitching();
            HandleCastingBinds();
            HandleCameraMovement();
        }

        private void HandleLoadedRigs()
        {
            // Recently, Another Axiom altered the usage of GorillaParent, and there is no more a vrrigs list there, so we handle our own.
            if (!PhotonNetwork.InRoom)
            {
                if (loadedRigs.Count > 1)
                {
                    loadedRigs.Clear();
                    loadedRigs.Add(offlineRig);
                }
                return;
            }

            if (loadedRigs.Count != PhotonNetwork.CurrentRoom.PlayerCount)
            {
                loadedRigs.Clear();
                loadedRigs.AddRange(GameObject.FindObjectsOfType<VRRig>());
                loadedRigs.Reverse(); // reverse order so 0 is always our offlineRig.
            }
        }

        private void setTarget(int targetNum)
        {
            target.lerpValueBody = 0.155f;
            target.lerpValueFingers = 0.155f;

            target = loadedRigs[targetNum];
        }
        
        private void setTarget(VRRig rig)
        {
            target.lerpValueBody = 0.155f;
            target.lerpValueFingers = 0.155f;

            target = rig;
        }

        private void HandleTargetSwitching()
        {
            if (target == null) target = offlineRig;
            
            if (Keyboard.current.digit1Key.wasPressedThisFrame) setTarget(1);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) setTarget(2);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) setTarget(3);
            if (Keyboard.current.digit4Key.wasPressedThisFrame) setTarget(4);
            if (Keyboard.current.digit5Key.wasPressedThisFrame) setTarget(5);
            if (Keyboard.current.digit6Key.wasPressedThisFrame) setTarget(6);
            if (Keyboard.current.digit7Key.wasPressedThisFrame) setTarget(7);
            if (Keyboard.current.digit8Key.wasPressedThisFrame) setTarget(8);
            if (Keyboard.current.digit9Key.wasPressedThisFrame) setTarget(9);
            if (Keyboard.current.digit0Key.wasPressedThisFrame) setTarget(0);
        }

        private void HandleCastingBinds() // method was originally named HandleCameraOffsets, but i put perspective and other binds here too
        {
            if(Keyboard.current.escapeKey.wasPressedThisFrame) isUiOpen = !isUiOpen;
            
            if(Keyboard.current.dKey.isPressed) xOffset += 1 * Time.deltaTime;
            if(Keyboard.current.aKey.isPressed) xOffset -= 1 * Time.deltaTime;
            if(Keyboard.current.wKey.isPressed) zOffset += 1 * Time.deltaTime;
            if(Keyboard.current.sKey.isPressed) zOffset -= 1 * Time.deltaTime;
            if(Keyboard.current.eKey.isPressed) yOffset += 1 * Time.deltaTime;
            if(Keyboard.current.qKey.isPressed) yOffset -= 1 * Time.deltaTime;
            if (Keyboard.current.rKey.isPressed) xOffset = yOffset = zOffset = 0;
            
            if(Keyboard.current.pKey.wasPressedThisFrame) headLock = !headLock;

            if (Keyboard.current.minusKey.isPressed) moveSmoothing -= 0.01f;
            if (Keyboard.current.equalsKey.isPressed) moveSmoothing += 0.01f;
            if (Keyboard.current.leftBracketKey.isPressed) rotSmoothing -= 0.01f;
            if (Keyboard.current.rightBracketKey.isPressed) rotSmoothing += 0.01f;
            
            moveSmoothing = Mathf.Clamp(moveSmoothing, 0, 1);
            rotSmoothing = Mathf.Clamp(rotSmoothing, 0, 1);
        }

        private void HandleCameraMovement()
        {
            Transform targetTransform = headLock ? target.headConstraint : target.transform;
            Transform cameraTransform = camera.transform;
            Vector3 upTemp = headLock ? targetTransform.up : Vector3.up;
            
            Vector3 targetPosition = targetTransform.position;
            targetPosition += upTemp * yOffset;
            targetPosition += targetTransform.right * xOffset;
            targetPosition += targetTransform.forward * zOffset;
            
            Quaternion targetRotation = headLock ? targetTransform.rotation : Quaternion.LookRotation(cameraTransform.forward, targetTransform.position-cameraTransform.position);
            
            float lerpDelta = Time.deltaTime * 120; //always gonna have 120fps-like lerping
            
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetPosition, (1-moveSmoothing) * lerpDelta);
            cameraTransform.rotation = Quaternion.Lerp(cameraTransform.rotation, targetRotation, (1-rotSmoothing) * lerpDelta);
        }

        public void OnGUI()
        {
            if (!initialized) return;
            if (!isUiOpen) return;

            if (!PhotonNetwork.InRoom)
            {
                roomToJoin = GUI.TextField(new Rect(5, 5, 200, 30), roomToJoin, 16).ToUpper();
                if(GUI.Button(new Rect(5, 40, 200, 30), "Join Room")) PhotonNetworkController.Instance.AttemptToAutoJoinSpecificRoom(roomToJoin, JoinType.Solo);
                return;
            }

            string bindings = "ESC -> Toggle UI (this)\n\n";
            
            bindings += "WASDQE -> Offset Camera Position\n";
            bindings += "R -> Reset Offsets\n";
            bindings += "P -> Toggle Headlock\n";
            bindings += "NUMKEYS -> Switch Target\n\n";
            
            bindings += "-= -> Decrease/Increase movement smoothing\n";
            bindings += "[] -> Decrease/Increase rotation smoothing\n";
            
            GUI.Label(new Rect(5,5, Screen.width-10, Screen.height-10), bindings);
            // Adding UI soon, no need for now with da binds :3
        }

        #region Camera variables
        
        private List<VRRig> loadedRigs = new List<VRRig>();
        private VRRig target;
        private VRRig offlineRig;
        private bool initialized = false, isUiOpen = true;
        public Camera camera;
        
        private string roomToJoin = "LUCIO";
        
        #endregion


        #region Setting variables

        private float xOffset = 0, yOffset = 0, zOffset = 0;
        private float moveSmoothing = 0, rotSmoothing = 0;
        private bool headLock = true;
        

        #endregion
    }
}