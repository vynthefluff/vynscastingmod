using System;
using System.Collections.Generic;
using BepInEx;
using GorillaLocomotion;
using GorillaNetworking;
using OVR.OpenVR;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

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
                camera.depth = Camera.main.depth + 1;
                
                
                UniversalAdditionalCameraData addCam = camera.AddComponent<UniversalAdditionalCameraData>();
                
                addCam.requiresDepthTexture = false;
                addCam.requiresColorTexture = false;
                addCam.renderPostProcessing = false;
                addCam.SetRenderer(0); 
                addCam.renderType = CameraRenderType.Base;
                
                initialized = true;
            }
            
            Application.targetFrameRate = int.MaxValue; // Gtag's fps is capped at 144 by default - no thanks.

            HandleLoadedRigs();
            HandleTargetSwitching();
            HandleCastingBinds();
            HandleRigModifiers();
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

            // code looks horrible but idk - makes it easier to change smoothing at high values.
            if (Keyboard.current.minusKey.isPressed) moveSmoothing -= moveSmoothing < 0.8f ? 0.0025f : 0.0005f;
            if (Keyboard.current.equalsKey.isPressed) moveSmoothing += moveSmoothing < 0.8f ? 0.0025f : 0.0005f;
            if (Keyboard.current.leftBracketKey.isPressed) rotSmoothing -= rotSmoothing < 0.8f ? 0.0025f : 0.0005f;
            if (Keyboard.current.rightBracketKey.isPressed) rotSmoothing += rotSmoothing < 0.8f ? 0.0025f : 0.0005f;
            
            if (Keyboard.current.commaKey.isPressed) rigLerpingMultiplier -= 3 * Time.deltaTime;
            if (Keyboard.current.periodKey.isPressed) rigLerpingMultiplier += 3 * Time.deltaTime;
            
            
            if(Keyboard.current.semicolonKey.isPressed) camera.fieldOfView -= 5 * Time.deltaTime;
            if(Keyboard.current.quoteKey.isPressed) camera.fieldOfView += 5 * Time.deltaTime;
            
            moveSmoothing = Mathf.Clamp(moveSmoothing, 0, 1);
            rotSmoothing = Mathf.Clamp(rotSmoothing, 0, 1);
        }

        private void HandleRigModifiers()
        {
            float lerpDelta = Time.deltaTime * 120; //always gonna have 120fps-like lerping
            
            target.lerpValueBody = (0.155f * rigLerpingMultiplier) * lerpDelta;
            target.lerpValueFingers = (0.155f * rigLerpingMultiplier) * lerpDelta;
        }
        private void HandleCameraMovement()
        {
            Transform targetTransform = headLock ? target.head.rigTarget : target.transform;
            Transform cameraTransform = camera.transform;
            
            Vector3 targetPosition = targetTransform.position;
            
            //headlock rly offsets shi for some reason:
            
            targetPosition += targetTransform.up * yOffset;
            targetPosition += targetTransform.right * xOffset;
            targetPosition += targetTransform.forward * zOffset;
            
            Quaternion targetRotation = headLock ? targetTransform.rotation : Quaternion.LookRotation(targetTransform.position-cameraTransform.position);
            
            float lerpDelta = Time.deltaTime * 120; //always gonna have 120fps-like lerping
            
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetPosition, (1-moveSmoothing) * lerpDelta);
            cameraTransform.rotation = Quaternion.Lerp(cameraTransform.rotation, targetRotation, (1-rotSmoothing) * lerpDelta);
        }

        public void OnGUI()
        {
            if (!isUiOpen) return;

            roomToJoin = GUI.TextField(new Rect(5, 5, 200, 30), roomToJoin).ToUpper();

            if (PhotonNetwork.InRoom)
            { 
                if (GUI.Button(new Rect(5, 40, 200, 30), "Leave Room")) PhotonNetwork.Disconnect();
            }
            else if(GUI.Button(new Rect(5, 40, 200, 30), "Join Room")) PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(roomToJoin, JoinType.Solo);

            string labelText = "Bindings:\n\n";

            labelText += "ESC -> Toggle UI (this)\n";
            labelText += "WASDQE -> Offset Camera Position\n";
            labelText += "R -> Reset Offsets\n";
            labelText += "P -> Toggle Headlock\n";
            labelText += "NUMKEYS -> Switch Target\n";
            labelText += "-= -> Decrease/Increase movement lerping\n";
            labelText += "[] -> Decrease/Increase rotation lerping\n";
            labelText += ",. -> Decrease/Increase rig lerping\n";
            labelText += ";' -> Decrease/Increase FOV\n";
            labelText += "V -> Push To Talk\n\n\n";

            
            labelText += "Settings:\n\n";

            labelText += $"X Offset: {xOffset}\n";
            labelText += $"Y Offset: {yOffset}\n";
            labelText += $"Z Offset: {zOffset}\n";
            labelText += $"Headlock: {headLock}\n";
            labelText += $"Move Lerping: {moveSmoothing}\n";
            labelText += $"Rot Lerping: {rotSmoothing}\n";
            labelText += $"Rig Lerping: {rigLerpingMultiplier}\n";
            labelText += $"FOV: {camera.fieldOfView}\n";
            
            GUI.Label(new Rect(5,75, Screen.width-10, Screen.height-75), labelText);
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
        private float moveSmoothing = 0, rotSmoothing = 0, rigLerpingMultiplier = 1;
        private bool headLock = true;
        

        #endregion
    }
}