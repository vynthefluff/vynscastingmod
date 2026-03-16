using System;
using System.Collections.Generic;
using BepInEx;
using GorillaLocomotion;
using GorillaNetworking;
using OVR.OpenVR;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using vynscastingmod.Objects;

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

        public static Main instance;
        
        public void LateUpdate() // Testing lateUpdate, should fix camera jittering.
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
                
                
                // This codes taken from my old casting mod btw, so sorry its janky.
                
                if (loadedFonts.IsNullOrEmpty())
                {
                    
                    HashSet<string> fontListNames = new HashSet<string>();

                    TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();

                    foreach (TMP_FontAsset f in fonts)
                    {
                        if (f == null) 
                            continue;

                        if (!fontListNames.Contains(f.name))
                        {
                            fontListNames.Add(f.name);
                            loadedFonts.Add(f);
                        }
                    }

                }
                
                loadedFont = loadedFonts[nameTagFont - 1];
                

                instance = this;
                initialized = true;
            }
            
            Application.targetFrameRate = int.MaxValue; // Gtag's fps is capped at 144 by default - no thanks.
            uiNotificationTimer += Time.deltaTime; // Counts the timer up every frame in seconds - this same method of timing will be used when i add a timer overlay.
            PhotonNetworkController.Instance.disableAFKKick = true; // ensure we dont get kicked for not moving.

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
            
            if(Keyboard.current.vKey.wasPressedThisFrame)
            {
                bool muted = GorillaComputer.instance.pttType == "PUSH TO TALK";
                GorillaComputer.instance.pttType = muted ? "PUSH TO MUTE" : "PUSH TO TALK";
                Notify(muted ? "Unmuted!" : "Muted!");
            }

            float offset = xOffset + yOffset + zOffset; // save our combined offset before checking for keypresses, better than calling Notify() 7 times.
            
            if(Keyboard.current.dKey.isPressed) xOffset += 1 * Time.deltaTime;
            if(Keyboard.current.aKey.isPressed) xOffset -= 1 * Time.deltaTime;
            if(Keyboard.current.wKey.isPressed) zOffset += 1 * Time.deltaTime;
            if(Keyboard.current.sKey.isPressed) zOffset -= 1 * Time.deltaTime;
            if(Keyboard.current.eKey.isPressed) yOffset += 1 * Time.deltaTime;
            if(Keyboard.current.qKey.isPressed) yOffset -= 1 * Time.deltaTime;
            if (Keyboard.current.rKey.isPressed) xOffset = yOffset = zOffset = 0;
            
            float postOffset = xOffset + yOffset + zOffset;

            if (postOffset != offset) Notify($"Changed offsets!\nX Offset: {xOffset}\nY Offset: {yOffset}\nZ Offset: {zOffset}");
            

            if (Keyboard.current.pKey.wasPressedThisFrame)
            {
                headLock = !headLock;
                Notify(headLock ? "Enabled headlock!" : "Disabled headlock!");
            }


            // code looks horrible but idk - makes it easier to change smoothing at high values.
            float smoothing = moveSmoothing + rotSmoothing;
            if (Keyboard.current.minusKey.isPressed) moveSmoothing -= moveSmoothing < 0.8f ? 0.0025f : 0.0005f;
            if (Keyboard.current.equalsKey.isPressed) moveSmoothing += moveSmoothing < 0.8f ? 0.0025f : 0.0005f;
            if (Keyboard.current.leftBracketKey.isPressed) rotSmoothing -= rotSmoothing < 0.8f ? 0.0025f : 0.0005f;
            if (Keyboard.current.rightBracketKey.isPressed) rotSmoothing += rotSmoothing < 0.8f ? 0.0025f : 0.0005f;
            float postSmoothing = moveSmoothing + rotSmoothing;
            
            moveSmoothing = Mathf.Clamp(moveSmoothing, 0, 1);
            rotSmoothing = Mathf.Clamp(rotSmoothing, 0, 1);
            if (postSmoothing != smoothing) Notify($"Changed smoothing!\nMovement: {moveSmoothing}\nRotation: {rotSmoothing}");
            
            float lastRiglerp = rigLerpingMultiplier;
            if (Keyboard.current.commaKey.isPressed) rigLerpingMultiplier -= 0.5f * Time.deltaTime;
            if (Keyboard.current.periodKey.isPressed) rigLerpingMultiplier += 0.5f * Time.deltaTime;
            
            rigLerpingMultiplier = Mathf.Clamp(rigLerpingMultiplier, 1, 10);
            
            if(rigLerpingMultiplier != lastRiglerp) Notify($"Changed rig lerping!\nLerping: {rigLerpingMultiplier}");

            float lastFov = camera.fieldOfView;
            if(Keyboard.current.semicolonKey.isPressed) camera.fieldOfView -= 5 * Time.deltaTime;
            if(Keyboard.current.quoteKey.isPressed) camera.fieldOfView += 5 * Time.deltaTime;
            
            if(camera.fieldOfView != lastFov) Notify($"Changed FOV: {camera.fieldOfView}");

            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                nametagsEnabled = !nametagsEnabled;
                Notify(nametagsEnabled ? "Enabled nametags!" : "Disabled nametags!");
            }
            
            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                nameTagFont++;
                if (nameTagFont > loadedFonts.Count) nameTagFont = 1;
                loadedFont = loadedFonts[nameTagFont - 1];
                Notify($"Set nametag font to: {nameTagFont}");
            }
        }

        private void HandleRigModifiers()
        {
            float lerpDelta = Time.deltaTime * 120; //always gonna have 120fps-like lerping
            
            target.lerpValueBody = (0.155f * rigLerpingMultiplier) * lerpDelta;
            target.lerpValueFingers = (0.155f * rigLerpingMultiplier) * lerpDelta;

            if (nametagsEnabled)
            {
                loadedRigs.ForEach(rig =>
                {
                    rig.GetOrAddComponent<NametagObject>(out var unused);
                });
            }
            
        }
        private void HandleCameraMovement()
        {
            Transform targetTransform = headLock ? target.head.rigTarget : target.transform;
            Transform cameraTransform = camera.transform;
            
            Vector3 targetPosition = targetTransform.position;
            
            targetPosition += targetTransform.up * (yOffset * target.scaleFactor);
            targetPosition += targetTransform.right * (xOffset * target.scaleFactor);
            targetPosition += targetTransform.forward * (zOffset * target.scaleFactor);
            
            Quaternion targetRotation = headLock ? targetTransform.rotation : Quaternion.LookRotation(targetTransform.position-cameraTransform.position);
            
            float lerpDelta = Time.deltaTime * 120; //always gonna have 120fps-like lerping
            
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetPosition, (1-moveSmoothing) * lerpDelta);
            cameraTransform.rotation = Quaternion.Lerp(cameraTransform.rotation, targetRotation, (1-rotSmoothing) * lerpDelta);
        }

        #region GUI
        

        private void Notify(string message)
        {
            uiNotificationTimer = 0;
            uiNotificationText = message;
        }

        private void RenderOverlays()
        {
            if (centeredText == null)
            {
                centeredText = new GUIStyle(GUI.skin.label);
                centeredText.alignment = TextAnchor.UpperCenter;
                centeredText.fontSize = 18;
            }
            if (!uiNotificationText.IsNullOrEmpty() && uiNotificationTimer < 3) // only show notif text under 3 secs
            {
                Color color = Color.white;
                color.a = 1 - (uiNotificationTimer / 3);
                centeredText.normal.textColor = color;
                GUI.Label(new Rect(0,5, Screen.width, Screen.height-5), uiNotificationText, centeredText);
            }
            
            centeredText.normal.textColor = Color.white;
        }
        
        public void OnGUI()
        {
            if (!initialized) return;

            RenderOverlays();
            
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
            labelText += "V -> Push To Talk\n";
            labelText += "F1 -> Toggle Nametags\n\n\n";

            
            labelText += "Settings:\n\n";

            labelText += $"X Offset: {xOffset}\n";
            labelText += $"Y Offset: {yOffset}\n";
            labelText += $"Z Offset: {zOffset}\n";
            labelText += $"Headlock: {headLock}\n";
            labelText += $"Move Lerping: {moveSmoothing}\n";
            labelText += $"Rot Lerping: {rotSmoothing}\n";
            labelText += $"Rig Lerping: {rigLerpingMultiplier}\n";
            labelText += $"FOV: {camera.fieldOfView}\n";
            labelText += $"Nametags: {nametagsEnabled}\n";
            
            GUI.Label(new Rect(5,75, Screen.width-10, Screen.height-75), labelText);
            // Adding UI soon, no need for now with da binds :3
        }
        
        #endregion

        #region Camera variables
        
        private GUIStyle centeredText = null;
        
        private List<VRRig> loadedRigs = new List<VRRig>();
        private VRRig target;
        private VRRig offlineRig;
        private bool initialized = false, isUiOpen = true;
        private float uiNotificationTimer = 0;
        private string uiNotificationText = "";
        private List<TMP_FontAsset> loadedFonts = new List<TMP_FontAsset>();
            
        public Camera camera;
        
        private string roomToJoin = "LUCIO";
        
        
        #endregion


        #region Setting variables

        private float xOffset = 0, yOffset = 0, zOffset = 0;
        private float moveSmoothing = 0, rotSmoothing = 0, rigLerpingMultiplier = 1;
        private bool headLock = true;

        public bool nametagsEnabled = false;
        public int nameTagFont = 6;
        public TMP_FontAsset loadedFont;

        #endregion
    }
}