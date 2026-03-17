using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
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
        public const string modName = "vyn's casting mod";
        public const string modVer = "3.0.1";

        public static Main instance;
        
        public void LateUpdate() // Testing lateUpdate, should fix camera jittering.
        {
            if (!initialized)
            {

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
                
                Destroy(Camera.main.GetComponent<AudioListener>());
                camera.AddComponent<AudioListener>();
                
                
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
                Overlays.InitOverlays();

                if (!File.Exists("config.uwu"))
                {
                    SaveConfig();
                    Application.OpenURL("https://discord.gg/KPhreBySxr");
                }else LoadConfig();

                // fetch latest ver from github gist
                HttpClient c = new HttpClient();
                var mango = c.GetStringAsync(
                    "https://gist.githubusercontent.com/vynthefluff/d6ba2812261548833f155fdc7671b75a/raw/");
                mango.Wait();
                string latestVersion = mango.Result;

                if (latestVersion != modVer)
                {
                    Application.OpenURL("https://github.com/vynthefluff/vynscastingmod/releases/");
                    outdatedBuild = true;
                    fetchedVer = latestVersion;
                }
                
                instance = this;
                initialized = true;
            }
            
            Application.targetFrameRate = int.MaxValue; // Gtag's fps is capped at 144 by default - no thanks.
            PhotonNetworkController.Instance.disableAFKKick = true; // ensure we dont get kicked for not moving.

            HandleLoadedRigs();
            HandleTargetSwitching();
            HandleTimers();
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

        private void HandleTimers()
        {
            uiNotificationTimer += Time.deltaTime; // Counts the timer up every frame in seconds - this same method of timing will be used when i add a timer overlay.

            if (isTimerRunning)
            {
                timeRunning += Time.deltaTime;
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                isTimerRunning = !isTimerRunning;
                if (!isTimerRunning && timeRunning > 0) timeToBeat = timeRunning;
                timeRunning = -10;
            }

            if (Keyboard.current.bKey.wasPressedThisFrame)
            {
                isTimerRunning = false;
                timeToBeat = -10;
                timeRunning = -10;
            }
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

            if (Keyboard.current.fKey.wasPressedThisFrame) rotSmoothing = moveSmoothing = 0;
            
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
            
            if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
                scoreOverlay++;
                if (scoreOverlay > 2) scoreOverlay = 0;
                Notify($"Set score overlay to: {scoreOverlay}");
            }
            
            if (Keyboard.current.f4Key.wasPressedThisFrame)
            {
                timeOfDay++;
                BetterDayNightManager.instance.SetTimeOfDay(timeOfDay);
                if (timeOfDay > 9) timeOfDay = 0;
                Notify($"Set time of day to: {timeOfDay}");
            }
            
            if (Keyboard.current.f5Key.wasPressedThisFrame)
            {
                cosmeticsHidden = !cosmeticsHidden;
                loadedRigs.ForEach(rig =>
                {
                    if (cosmeticsHidden)
                    {
                        rig.LocalUpdateCosmeticsWithTryon(CosmeticsController.CosmeticSet.EmptySet,
                            CosmeticsController.CosmeticSet.EmptySet, false);
                    }
                    else
                    {
                        NetworkView view = (NetworkView)typeof(VRRig).GetField("netView",
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(rig); // why would you make this internal lemmone :(

                        view.SendRPC("RPC_RequestCosmetics", rig.OwningNetPlayer);
                    }
                });
                Notify(cosmeticsHidden ? "Disabled cosemtics." : "Enabled cosemtics.");
            }
            
            
            // moving overlay thingy :3
            if (Keyboard.current.upArrowKey.isPressed) overlayY -= 3;
            if (Keyboard.current.downArrowKey.isPressed) overlayY += 3;
            if (Keyboard.current.leftArrowKey.isPressed) overlayX -= 3;
            if (Keyboard.current.rightArrowKey.isPressed) overlayX += 3;
            
            if(overlayX < 267) overlayX = 267;
            if(overlayX > Screen.width-267) overlayX = Screen.width-267;
            if(overlayY < 10) overlayY = 10;
            if (overlayY > Screen.height - 90) overlayY = Screen.height - 90;


            if (Keyboard.current.tKey.wasPressedThisFrame) team1Score--;
            if (Keyboard.current.yKey.wasPressedThisFrame) team1Score++;

            if (Keyboard.current.gKey.wasPressedThisFrame) team2Score--;
            if (Keyboard.current.hKey.wasPressedThisFrame) team2Score++;
            
            team1Score = Math.Clamp(team1Score, 0, 9);
            team2Score = Math.Clamp(team2Score, 0, 9);

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
            
            if(headLock) targetPosition += targetTransform.up * 0.15f; // actually puts cam at head height when going in head tracker
            
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

        private void DrawOutline(Rect position, String text, GUIStyle style){ //another horribly coded method from v2, ported with old overlays
            style.normal.textColor = Color.black;
            position.x--;
            GUI.Label(position, text, style);
            position.x +=2;
            GUI.Label(position, text, style);
            position.x--;
            position.y--;
            GUI.Label(position, text, style);
            position.y +=2;
            GUI.Label(position, text, style);
            position.y--;
            style.normal.textColor = Color.white;
            GUI.Label(position, text, style);
        }

        private void RenderOverlays()
        {
            int centerX = Screen.width / 2; // useful for overlays and other thingies like idk

            if (centeredText == null)
            {
                centeredText = new GUIStyle(GUI.skin.label);
            }

            if (outdatedBuild)
            {
                centeredText.fontSize = 12;
                centeredText.alignment = TextAnchor.UpperRight;
                centeredText.normal.textColor = Color.red;
                GUI.Label(new Rect(5,5,Screen.width-10,Screen.height-10), $"You are using an outdated build of vyn's casting mod\nLatest version: {fetchedVer}", centeredText);
            
                centeredText.normal.textColor = Color.white;
            }
            centeredText.alignment = TextAnchor.UpperCenter;
            centeredText.fontSize = 18;

            if (!uiNotificationText.IsNullOrEmpty() && uiNotificationTimer < 3) // only show notif text under 3 secs
            {
                Color color = Color.white;
                color.a = 1 - (uiNotificationTimer / 3);
                centeredText.normal.textColor = color;
                GUI.Label(new Rect(0, 5, Screen.width, Screen.height - 5), uiNotificationText, centeredText);
            }else if (!uiNotificationText.IsNullOrEmpty() && !uiNotificationText.Contains("Config") && uiNotificationTimer > 5)
            {
                uiNotificationText = "";
                SaveConfig();
                Notify("Autosaved Config.");
                uiNotificationTimer = 1.5f;
            }

            if (scoreOverlay == 0) return;

            centeredText.normal.textColor = Color.white;
            int meow = overlayX;
            if (overlayX - centerX < 25 && overlayX - centerX > -25) meow = centerX;

            switch (scoreOverlay)
            {
                case 1:
                    GUI.DrawTexture(new Rect(meow - 277, overlayY, 277 * 2, 45 * 2), Overlays.cgtDefault);
                    break;
                case 2:
                    GUI.DrawTexture(new Rect(meow - 277, overlayY, 277 * 2, 45 * 2), Overlays.cgtPink);
                    break;

            }


            centeredText.fontSize = 32;
            centeredText.alignment = TextAnchor.MiddleLeft;
            DrawOutline(new Rect(meow - 175, overlayY + 2, 173, 50), $"{team1Name}", centeredText);



            centeredText.fontSize = 48;
            DrawOutline(new Rect(meow - 225, overlayY + 12, 225, 50), $"{team1Score}", centeredText);
            
            
            
            centeredText.fontSize = 32;
            centeredText.alignment = TextAnchor.MiddleRight;
            DrawOutline(new Rect(meow, overlayY+2, 175, 50), $"{team2Name}", centeredText);
            
            
            centeredText.fontSize = 48;
            DrawOutline(new Rect(meow, overlayY+12, 225, 50), $"{team2Score}", centeredText);


            centeredText.alignment = TextAnchor.MiddleCenter;
            centeredText.fontSize = 30;

            TimeSpan time;
                
            if (timeRunning > 0)
            {
                time = TimeSpan.FromSeconds(timeRunning);

                try
                {
                    string aaa = $"{time.Minutes}:{time.Seconds}:{time.Milliseconds.ToString().Substring(0, 2)}";
                    DrawOutline(new Rect(meow - 150, overlayY+2, 300, 50), aaa,
                        centeredText);
                    lastTimeDisplay = aaa;
                }
                catch (Exception)
                {
                    DrawOutline(new Rect(meow - 150, overlayY+2, 300, 50), lastTimeDisplay,
                        centeredText);
                }
            }else 
                DrawOutline(new Rect(meow-150, overlayY+2, 300, 50), timeRunning.ToString("F1") + 
                                                                     (timeRunning.ToString("F1").Contains("-") ? "-" : ""), centeredText);
            
            time = TimeSpan.FromSeconds(timeToBeat);
            
            if (timeToBeat <= 0)
            {
                DrawOutline(new Rect(meow - 300, overlayY+42, 600, 50),
                    $"0:0:00",
                    centeredText);
                return;
            }
            try
            {
                DrawOutline(new Rect(meow - 300, overlayY+42, 600, 50),
                    $"{time.Minutes}:{time.Seconds}:{time.Milliseconds.ToString().Substring(0, 2)}",
                    centeredText);

            }
            catch (Exception)
            {
                DrawOutline(new Rect(meow - 300, Screen.height - 58, 600, 50),
                    $"{time.Minutes}:{time.Seconds}", centeredText);

            }
            

            centeredText.fontSize = 24;
        }

        private string lastTimeDisplay = ""; //  ig enuinely dont know why but sometimes the code above throws exceptions no matter what so im doing this stupid ass fix.

        public void OnGUI()
        {
            if (!initialized) return;

            RenderOverlays();

            if (!isUiOpen) return;

            roomToJoin = GUI.TextField(new Rect(5, 5, 200, 30), roomToJoin).ToUpper();
            if (scoreOverlay != 0)
            {
                team1Name = GUI.TextField(new Rect(210, 5, 200, 30), team1Name).ToUpper();
                team2Name = GUI.TextField(new Rect(415, 5, 200, 30), team2Name).ToUpper();
                
            }

            if (PhotonNetwork.InRoom)
            { 
                if (GUI.Button(new Rect(5, 40, 200, 30), "Leave Room")) PhotonNetwork.Disconnect();
            }
            else if(GUI.Button(new Rect(5, 40, 200, 30), "Join Room")) PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(roomToJoin, JoinType.Solo);

            string labelText = modName + " " + modVer + " - discord.gg/KPhreBySxr";
            labelText += "Bindings:\n\n";

            labelText += "ESC -> Toggle UI (this)\n";
            labelText += "WASDQE -> Offset Camera Position\n";
            labelText += "R -> Reset Offsets\n";
            labelText += "P -> Toggle Headlock\n";
            labelText += "NUMKEYS -> Switch Target, shift makes runners only\n";
            labelText += "-= -> Decrease/Increase movement lerping\n";
            labelText += "[] -> Decrease/Increase rotation lerping\n";
            labelText += "F -> Reset Smoothing\n";
            labelText += ",. -> Decrease/Increase rig lerping\n";
            labelText += ";' -> Decrease/Increase FOV\n";
            labelText += "V -> Push To Talk\n";
            labelText += "F1 -> Toggle Nametags\n";
            labelText += "F2 -> Change Nametag font\n";
            labelText += "F3 -> Switch Overlays\n";
            labelText += "F4 -> Change Time Of Day\n";
            labelText += "F5 -> Hide all cosmetics\n";
            labelText += "Arrows -> Move Overlay\n\n";
            
            
            labelText += "Space -> Start/lap timer\n";
            labelText += "B -> Reset round (lap & timer)\n";
            labelText += "TY -> Team 1 points\n";
            labelText += "GH -> Team 2 points\n\n\n";

            
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
            
            // gonna add team name inputs when done with overlays
        }
        
        #endregion

        #region Settings

        public void SaveConfig()
        {
            StreamWriter cfg = new StreamWriter("config.uwu");
            
            cfg.WriteLine(xOffset);
            cfg.WriteLine(yOffset);
            cfg.WriteLine(zOffset);
            
            cfg.WriteLine(moveSmoothing);
            cfg.WriteLine(rotSmoothing);
            cfg.WriteLine(rigLerpingMultiplier);
            
            cfg.WriteLine(headLock);
            
            cfg.WriteLine(nameTagFont);
            cfg.WriteLine(scoreOverlay);
            
            cfg.Close();
        }

        public void LoadConfig()
        {
            StreamReader cfg =  new StreamReader("config.uwu");
            string[] setts = cfg.ReadToEnd().Split("\n");
            
            xOffset = float.Parse(setts[0]); 
            yOffset = float.Parse(setts[1]);
            zOffset = float.Parse(setts[2]);
            moveSmoothing = float.Parse(setts[3]);
            rotSmoothing = float.Parse(setts[4]);
            rigLerpingMultiplier = float.Parse(setts[5]);
            headLock = bool.Parse(setts[6]);
            nameTagFont = int.Parse(setts[7]);
            scoreOverlay = int.Parse(setts[8]);
            
            cfg.Close();
        }

        #endregion

        #region Camera variables
        
        public Camera camera;
        private List<VRRig> loadedRigs = new List<VRRig>();
        private VRRig target;
        private VRRig offlineRig;
        private bool initialized = false, isUiOpen = true;
        
        private float uiNotificationTimer = 0;
        private string uiNotificationText = "", fetchedVer = "";
        private List<TMP_FontAsset> loadedFonts = new List<TMP_FontAsset>();
        private string roomToJoin = "LUCIO";
        private GUIStyle centeredText = null;
        
        private string team1Name = "TTT";
        private string team2Name = "TSO";
        private int team1Score = 0, team2Score = 0;

        private bool outdatedBuild = false;
        
        #endregion


        #region Setting variables

        private float xOffset = 0, yOffset = 0, zOffset = 0;
        private float moveSmoothing = 0, rotSmoothing = 0, rigLerpingMultiplier = 1;
        private bool headLock = true, cosmeticsHidden = false;

        public bool nametagsEnabled = false;
        public int nameTagFont = 5, scoreOverlay = 0;
        public TMP_FontAsset loadedFont;

        private int overlayX = Screen.width/2, overlayY = Screen.height - 100, timeOfDay = 0;

        #endregion

        private float timeRunning = -10, timeToBeat = -10;
        private bool isTimerRunning = false;
    }
}