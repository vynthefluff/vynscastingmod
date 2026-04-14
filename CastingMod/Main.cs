using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public const string modVer = "3.0.3";

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

                try
                {
                    if (!File.Exists("config.uwu"))
                    {
                        SaveConfig();
                        Application.OpenURL("https://discord.gg/KPhreBySxr");
                    }else LoadConfig();
                }catch(Exception) {} // sometimes when updating from older versions, loading configs causes errors :p
                

                try
                {
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
                }
                catch (Exception) { }

                try
                {
                    DiscordRPCImpl.InitRPC(); // Discord RPC library from https://github.com/Lachee/discord-rpc-csharp
                }
                catch (Exception) //stopped launching if discord wasnt open
                {
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
                if (GorillaComputer.instance.roomFull)
                {
                    GorillaComputer.instance.roomFull = false;
                    Notify("Room full!");
                }
                
                if (GorillaComputer.instance.roomNotAllowed)
                {
                    GorillaComputer.instance.roomNotAllowed = false;
                    Notify("Not allowed to join room!");
                }
                
                if (loadedRigs.Count > 1)
                {
                    foreach ((string,NametagObject) valueTuple in tags)
                    {
                        var no = valueTuple.Item2;
                        no.textObj.text = "";
                        Destroy(no.textObj);
                        Destroy(no);
                    }
                    
                    loadedRigs.Clear();
                    loadedRigs.Add(offlineRig);
                }
                return;
            }

            if (loadedRigs.Count != PhotonNetwork.CurrentRoom.PlayerCount)
            {
                foreach ((string,NametagObject) valueTuple in tags)
                {
                    var no = valueTuple.Item2;
                    no.textObj.text = "";
                    Destroy(no.textObj);
                    Destroy(no);
                }
                
                loadedRigs.Clear();
                loadedRigs.AddRange(GameObject.FindObjectsOfType<VRRig>());
                loadedRigs.Reverse(); // reverse order so 0 is always our offlineRig.
            }
        }
        
        private void setTarget(VRRig rig)
        {
            if (rig == target) return;
            
            target.lerpValueBody = 0.155f;
            target.lerpValueFingers = 0.155f;

            if (firstPersonEnabled)
            {
                NetworkView view = (NetworkView)typeof(VRRig).GetField("netView",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(disabledCosmeticsRig); // why would you make this internal lemmone :(

                view.SendRPC("RPC_RequestCosmetics", disabledCosmeticsRig.OwningNetPlayer);
            }
            
            target = rig;
            
            if (firstPersonEnabled)
            {
                disabledCosmeticsRig = target;
                        
                disabledCosmeticsRig.LocalUpdateCosmeticsWithTryon(CosmeticsController.CosmeticSet.EmptySet, CosmeticsController.CosmeticSet.EmptySet, false);
            }
        }

        private void HandleTargetSwitching()
        {
            if (target == null) target = offlineRig;
            
            List<VRRig> targets = loadedRigs.ToList();
            if (Keyboard.current.shiftKey.isPressed)
                targets.RemoveAll(rig => rig.lavaParticleSystem.isPlaying || rig.rockParticleSystem.isPlaying); 
            if (Keyboard.current.digit1Key.wasPressedThisFrame) setTarget(targets[1]);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) setTarget(targets[2]);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) setTarget(targets[3]);
            if (Keyboard.current.digit4Key.wasPressedThisFrame) setTarget(targets[4]);
            if (Keyboard.current.digit5Key.wasPressedThisFrame) setTarget(targets[5]);
            if (Keyboard.current.digit6Key.wasPressedThisFrame) setTarget(targets[6]);
            if (Keyboard.current.digit7Key.wasPressedThisFrame) setTarget(targets[7]);
            if (Keyboard.current.digit8Key.wasPressedThisFrame) setTarget(targets[8]);
            if (Keyboard.current.digit9Key.wasPressedThisFrame) setTarget(targets[9]);
            if (Keyboard.current.digit0Key.wasPressedThisFrame) setTarget(targets[0]);
            
            targets.Clear();
            targets = null;
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

            if (isUiOpen) return; // this was really annoying without.
            
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
                perspective++;
                if (perspective > 2) perspective = 0;
                Notify($"Changed perspective!\nPerspective: {perspective}");
            }

            if (Keyboard.current.cKey.wasPressedThisFrame)
            {
                firstPersonEnabled = !firstPersonEnabled;
                Notify(firstPersonEnabled ? "Enabled firstperson!" : "Disabled firstperson!");
                
                if (firstPersonEnabled)
                {
                    disabledCosmeticsRig = target;
                    
                    disabledCosmeticsRig.LocalUpdateCosmeticsWithTryon(CosmeticsController.CosmeticSet.EmptySet, CosmeticsController.CosmeticSet.EmptySet, false);
                }else if (disabledCosmeticsRig != null)
                {
                    NetworkView view = (NetworkView)typeof(VRRig).GetField("netView",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(disabledCosmeticsRig); // why would you make this internal lemmone :(

                    view.SendRPC("RPC_RequestCosmetics", disabledCosmeticsRig.OwningNetPlayer);
                }
            }


            // code looks horrible but idk - makes it easier to change smoothing at high values.
            float smoothing = moveSmoothing + rotSmoothing;
            if (Keyboard.current.minusKey.isPressed) moveSmoothing -= moveSmoothing < 0.8f ? 0.0025f : 0.0005f;
            if (Keyboard.current.equalsKey.isPressed) moveSmoothing += moveSmoothing < 0.8f ? 0.0025f : 0.0005f;
            if (Keyboard.current.leftBracketKey.isPressed) rotSmoothing -= rotSmoothing < 0.8f ? 0.0025f : 0.0005f;
            if (Keyboard.current.rightBracketKey.isPressed) rotSmoothing += rotSmoothing < 0.8f ? 0.0025f : 0.0005f;

            if (Keyboard.current.fKey.wasPressedThisFrame) rotSmoothing = moveSmoothing = 0;
            
            float postSmoothing = moveSmoothing + rotSmoothing;
            
            moveSmoothing = Math.Clamp(moveSmoothing, 0, 1);
            rotSmoothing = Math.Clamp(rotSmoothing, 0, 1);
            if (postSmoothing != smoothing) Notify($"Changed smoothing!\nMovement: {moveSmoothing}\nRotation: {rotSmoothing}");
            
            float lastRiglerp = rigLerpingMultiplierSlow;
            float lastRiglerpFast = rigLerpingMultiplierFast;
            if (Keyboard.current.commaKey.isPressed) rigLerpingMultiplierSlow -= 0.5f * Time.deltaTime;
            if (Keyboard.current.periodKey.isPressed) rigLerpingMultiplierSlow += 0.5f * Time.deltaTime;
            
            if (Keyboard.current.nKey.isPressed) rigLerpingMultiplierFast -= 0.5f * Time.deltaTime;
            if (Keyboard.current.mKey.isPressed) rigLerpingMultiplierFast += 0.5f * Time.deltaTime;
            
            // im sorry but 10 was a WAY TOO HIGH CAP.
            rigLerpingMultiplierSlow = Math.Clamp(rigLerpingMultiplierSlow, 1, 5);
            rigLerpingMultiplierFast = Math.Clamp(rigLerpingMultiplierFast, 1, 5);
            
            if(rigLerpingMultiplierSlow != lastRiglerp) Notify($"Changed rig lerping!\nLerping: {rigLerpingMultiplierSlow}");
            if(rigLerpingMultiplierFast != lastRiglerpFast) Notify($"Changed rig lerping!\nLerping: {rigLerpingMultiplierFast}");

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
                nametagFPS = !nametagFPS;
                Notify(nametagFPS ? "Enabled nametags FPS!" : "Disabled nametags FPS!");
            }
            
            if (Keyboard.current.f4Key.wasPressedThisFrame)
            {
                nametagPlat = !nametagPlat;
                Notify(nametagPlat ? "Enabled nametags Platform!" : "Disabled nametags Platform!");
            }
            
            
            
            if (Keyboard.current.f5Key.wasPressedThisFrame)
            {
                scoreOverlay++;
                if (scoreOverlay > (File.Exists("TeamInfo.png") ? 4 : 3)) scoreOverlay = 0;
                
                if (File.Exists("TeamInfo.png"))
                    Overlays.customScoreboard.LoadImage(File.ReadAllBytes("TeamInfo.png"));
                
                
                Notify($"Set score overlay to: {scoreOverlay}");
            }
            
            if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                leaderboardOverlay++;
                if (leaderboardOverlay > (File.Exists("Scoreboard.png") ? 2 : 1)) leaderboardOverlay = 0;

                if (File.Exists("Scoreboard.png"))
                    Overlays.customLeaderboard.LoadImage(File.ReadAllBytes("Scoreboard.png"));
                
                Notify($"Set leaderboard overlay to: {leaderboardOverlay}");
            }
            
            if (Keyboard.current.f7Key.wasPressedThisFrame)
            {
                timeOfDay++;
                if (timeOfDay > 9) timeOfDay = 0;
                Notify($"Set time of day to: {timeOfDay}");
            }
            
            if (Keyboard.current.f8Key.wasPressedThisFrame)
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
            int overlayPre = overlayX + overlayY;
            int leaderPre = leaderboardY;
            if (Keyboard.current.upArrowKey.isPressed)
            {
                if (Keyboard.current.shiftKey.isPressed) leaderboardY -= 3;
                else overlayY -= 3;
            }

            if (Keyboard.current.downArrowKey.isPressed)
            {
                if (Keyboard.current.shiftKey.isPressed) leaderboardY += 3;
                else overlayY += 3;
            }
            if (Keyboard.current.leftArrowKey.isPressed && !Keyboard.current.shiftKey.isPressed) overlayX -= 3;
            if (Keyboard.current.rightArrowKey.isPressed && !Keyboard.current.shiftKey.isPressed) overlayX += 3;
            
            int overlayPost = overlayX + overlayY;
            if (leaderboardY > Screen.height - 30) leaderboardY = Screen.height - 30;
            if(overlayPost != overlayPre) Notify($"Changed overlay pos!\nPosX: {overlayX}\nPosY: {overlayY}");
            if(leaderboardY != leaderPre) Notify($"Changed leaderboard pos!\nPosY: {leaderboardY}");
            
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

            Vector3 latestVel = target.LatestVelocity();
            latestVel.y = 0;
            if (latestVel.magnitude < 7)
            {
                target.lerpValueBody = (0.155f * rigLerpingMultiplierSlow) * lerpDelta;
                target.lerpValueFingers = (0.155f * rigLerpingMultiplierSlow) * lerpDelta;
            }
            else
            {
                target.lerpValueBody = (0.155f * rigLerpingMultiplierFast) * lerpDelta;
                target.lerpValueFingers = (0.155f * rigLerpingMultiplierFast) * lerpDelta;
            }

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
            Transform targetTransform = perspective == 0 || firstPersonEnabled ? target.head.rigTarget : target.transform;
            Transform cameraTransform = camera.transform;
            
            Vector3 targetPosition = targetTransform.position;
            
            if(perspective == 0 || firstPersonEnabled) targetPosition += targetTransform.up * 0.15f; // actually puts cam at head height when going in head tracker

            float lerpDelta = Time.deltaTime * 120; //always gonna have 120fps-like lerping
            
            if (firstPersonEnabled)
            {
                cameraTransform.position = targetPosition;
                cameraTransform.rotation = Quaternion.Lerp(cameraTransform.rotation, targetTransform.rotation, (1-rotSmoothing) * lerpDelta);
                return;
            }
            
            targetPosition += targetTransform.up * (yOffset * target.scaleFactor);
            targetPosition += targetTransform.right * (xOffset * target.scaleFactor);
            targetPosition += targetTransform.forward * (zOffset * target.scaleFactor);

            Quaternion targetRotation;
            
            targetRotation = targetTransform.rotation;
            if(perspective == 1) targetRotation = Quaternion.LookRotation(targetTransform.position-cameraTransform.position);
            
            
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

        private void RenderTeamInfo()
        {
            int centerX = Screen.width / 2; // useful for overlays and other thingies like idk
            
            if(overlayY == -1) overlayY = Screen.height - 100;
            if(overlayX == -1) overlayX = centerX;
            
            
            labelStyle.normal.textColor = Color.white;
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
                case 3:
                    GUI.DrawTexture(new Rect(meow - 277, overlayY, 277 * 2, 45 * 2), Overlays.vynDefault);
                    break;
                case 4:
                    GUI.DrawTexture(new Rect(meow - 277, overlayY, 277 * 2, 45 * 2), Overlays.customScoreboard);
                    break;

            }


            labelStyle.fontSize = 32;
            labelStyle.alignment = TextAnchor.MiddleLeft;
            DrawOutline(new Rect(meow - 175, overlayY + 2, 173, 50), $"{team1Name}", labelStyle);



            labelStyle.fontSize = 48;
            DrawOutline(new Rect(meow - 225, overlayY + 12, 225, 50), $"{team1Score}", labelStyle);
            
            
            
            labelStyle.fontSize = 32;
            labelStyle.alignment = TextAnchor.MiddleRight;
            DrawOutline(new Rect(meow, overlayY+2, 175, 50), $"{team2Name}", labelStyle);
            
            
            labelStyle.fontSize = 48;
            DrawOutline(new Rect(meow, overlayY+12, 225, 50), $"{team2Score}", labelStyle);


            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.fontSize = 30;

            TimeSpan time;
                
            if (timeRunning > 0)
            {
                time = TimeSpan.FromSeconds(timeRunning);

                try
                {
                    string aaa = $"{time.Minutes}:{time.Seconds}:{time.Milliseconds.ToString().Substring(0, 2)}";
                    DrawOutline(new Rect(meow - 150, overlayY+2, 300, 50), aaa,
                        labelStyle);
                    lastTimeDisplay = aaa;
                }
                catch (Exception)
                {
                    DrawOutline(new Rect(meow - 150, overlayY+2, 300, 50), lastTimeDisplay,
                        labelStyle);
                }
            }else 
                DrawOutline(new Rect(meow-150, overlayY+2, 300, 50), timeRunning.ToString("F1") + 
                                                                     (timeRunning.ToString("F1").Contains("-") ? "-" : ""), labelStyle);

            labelStyle.fontSize = 24;
            
            time = TimeSpan.FromSeconds(timeToBeat);
            
            if (timeToBeat <= 0)
            {
                DrawOutline(new Rect(meow - 300, overlayY+42, 600, 50),
                    $"0:0:00",
                    labelStyle);
                return;
            }
            try
            {
                DrawOutline(new Rect(meow - 300, overlayY+42, 600, 50),
                    $"{time.Minutes}:{time.Seconds}:{time.Milliseconds.ToString().Substring(0, 2)}",
                    labelStyle);

            }
            catch (Exception)
            {
                DrawOutline(new Rect(meow - 300, Screen.height - 58, 600, 50),
                    $"{time.Minutes}:{time.Seconds}", labelStyle);

            }


            labelStyle.fontSize = 18;
        }

        private void RenderLeaderboard()
        {
            if (leaderboardY == -1) leaderboardY = Screen.height-30;
            labelStyle.fontSize = 16;
            labelStyle.alignment = TextAnchor.MiddleLeft;
            int y = 0;
            int num = 0;
            foreach (var plr in loadedRigs)
            {
                // x200 y30

                switch (leaderboardOverlay)
                {
                    case 1:
                        GUI.DrawTexture(new Rect(0, leaderboardY-y, 200, 30), Overlays.leaderboardPart);
                        break;
                    case 2:
                        GUI.DrawTexture(new Rect(0, leaderboardY-y, 200, 30), Overlays.customLeaderboard);
                        break;
                }

                labelStyle.normal.textColor = plr.lavaParticleSystem.isPlaying || plr.rockParticleSystem.isPlaying
                    ? Color.red
                    : plr.playerColor;
                
                GUI.Label(new Rect(7, leaderboardY-y, 23, 30), $"{num}", labelStyle);

                labelStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(30, leaderboardY-y, 170, 30), $"{plr.playerNameVisible}", labelStyle);

                y += 35;
                num++;
            }
        }

        private void RenderOverlays()
        {
            int centerX = Screen.width / 2; // useful for overlays and other thingies like idk

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.fontStyle = FontStyle.Bold;
            }

            if (outdatedBuild)
            {
                labelStyle.fontSize = 12;
                labelStyle.alignment = TextAnchor.UpperRight;
                labelStyle.normal.textColor = Color.red;
                GUI.Label(new Rect(5,5,Screen.width-10,Screen.height-10), $"You are using an outdated build of vyn's casting mod\nLatest version: {fetchedVer}", labelStyle);
            
                labelStyle.normal.textColor = Color.white;
            }
            
            labelStyle.alignment = TextAnchor.UpperCenter;
            labelStyle.fontSize = 18;

            // Notification stuffs
            if (!uiNotificationText.IsNullOrEmpty() && uiNotificationTimer < 3) // only show notif text under 3 secs
            {
                BetterDayNightManager.instance.SetTimeOfDay(timeOfDay); // as stupid as it seems to have this in OnGUI, it's kinda better because
                // it doesn't have the performance hit that doing it every frame on Update does, but calling it just once doesnt work sometimes.
                
                Color color = Color.white;
                color.a = 1 - (uiNotificationTimer / 3);
                labelStyle.normal.textColor = color;
                GUI.Label(new Rect(0, 5, Screen.width, Screen.height - 5), uiNotificationText, labelStyle);
            }else if (!uiNotificationText.IsNullOrEmpty() && !uiNotificationText.Contains("Config") && uiNotificationTimer > 5)
            {
                uiNotificationText = "";
                SaveConfig();
                Notify("Autosaved Config.");
                uiNotificationTimer = 1.5f;
            }
            // Notification stuffs
            
            

            if (scoreOverlay != 0) RenderTeamInfo();

            if (leaderboardOverlay != 0) RenderLeaderboard();
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

            string labelText = modName + " " + modVer + " - discord.gg/KPhreBySxr\n";
            labelText += "Bindings:\n\n";

            labelText += "ESC -> Toggle UI (this)\n";
            labelText += "WASDQE -> Offset Camera Position\n";
            labelText += "R -> Reset Offsets\n";
            labelText += "P -> Toggle Headlock\n";
            labelText += "C -> Toggle FirstPerson\n";
            labelText += "NUMKEYS -> Switch Target, shift makes runners only\n";
            labelText += "-= -> Decrease/Increase movement lerping\n";
            labelText += "[] -> Decrease/Increase rotation lerping\n";
            labelText += "F -> Reset Smoothing\n";
            labelText += ",. -> Decrease/Increase rig lerping (under 7m/s)\n";
            labelText += "NM -> Decrease/Increase rig lerping (over 7m/s)\n";
            labelText += ";' -> Decrease/Increase FOV\n";
            labelText += "V -> Push To Talk\n";
            labelText += "F1 -> Toggle Nametags\n";
            labelText += "F2 -> Change Nametag font\n";
            labelText += "F3 -> Show Nametag FPS\n";
            labelText += "F4 -> Show Nametag Platform\n";
            labelText += "F5 -> Switch Overlays\n";
            labelText += "F6 -> Switch Leaderboards\n";
            labelText += "F7 -> Change Time Of Day\n";
            labelText += "F8 -> Toggle cosmetics\n";
            labelText += "Arrows -> Move Overlay, shift for leaderboard\n\n";
            
            
            labelText += "Space -> Start/lap timer\n";
            labelText += "B -> Reset round (lap & timer)\n";
            labelText += "TY -> Team 1 points\n";
            labelText += "GH -> Team 2 points\n\n\n";

            
            labelText += "Settings:\n\n";

            labelText += $"X Offset: {xOffset}\n";
            labelText += $"Y Offset: {yOffset}\n";
            labelText += $"Z Offset: {zOffset}\n";
            labelText += $"Perspective: {perspective}\n";
            labelText += $"Move Lerping: {moveSmoothing}\n";
            labelText += $"Rot Lerping: {rotSmoothing}\n";
            labelText += $"Slow Rig Lerping: {rigLerpingMultiplierSlow}\n";
            labelText += $"Fast Rig Lerping: {rigLerpingMultiplierFast}\n";
            labelText += $"FOV: {camera.fieldOfView}\n";
            
            GUI.Label(new Rect(5,75, Screen.width-10, Screen.height-75), labelText);
            
            if (GUI.Button(new Rect(210, 40, 200, 30), "Round all vars"))
            {
                xOffset *= 5;
                yOffset *= 5;
                zOffset *= 5;
                
                xOffset = (float)Math.Round(xOffset, 1);
                yOffset = (float)Math.Round(yOffset, 1);
                zOffset = (float)Math.Round(zOffset, 1);
                
                xOffset /= 5;
                yOffset /= 5;
                zOffset /= 5;
                
                
                moveSmoothing *= 10;
                rotSmoothing *= 10;
                
                moveSmoothing = (float)Math.Round(moveSmoothing, 1);
                rotSmoothing = (float)Math.Round(rotSmoothing, 1);
                
                moveSmoothing /= 10;
                rotSmoothing /= 10;
                
                moveSmoothing *= 5;
                rotSmoothing *= 5;
                
                rigLerpingMultiplierSlow = (float)Math.Round(rigLerpingMultiplierSlow, 1);
                rigLerpingMultiplierFast = (float)Math.Round(rigLerpingMultiplierFast, 1);
                
                moveSmoothing /= 5;
                rotSmoothing /= 5;
                
                camera.fieldOfView = (float)Math.Round(camera.fieldOfView, 1);
            }
            
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
            cfg.WriteLine(rigLerpingMultiplierSlow);
            
            cfg.WriteLine(perspective);
            
            cfg.WriteLine(nameTagFont);
            cfg.WriteLine(scoreOverlay);
            cfg.WriteLine(nametagsEnabled);
            
            cfg.WriteLine(timeOfDay);
            
            cfg.WriteLine(overlayX);
            cfg.WriteLine(overlayY);
            
            cfg.WriteLine(leaderboardOverlay);
            cfg.WriteLine(leaderboardY);
            cfg.WriteLine(camera.fieldOfView);
            
            cfg.WriteLine(firstPersonEnabled);
            cfg.WriteLine(rigLerpingMultiplierFast);
            
            cfg.WriteLine(nametagFPS);
            cfg.WriteLine(nametagPlat);
            
            cfg.Close();
        }

        public void LoadConfig()
        {
            StreamReader cfg =  new StreamReader("config.uwu");
            string[] setts = cfg.ReadToEnd().Split("\n");
            cfg.Close();
            
            xOffset = float.Parse(setts[0]); 
            yOffset = float.Parse(setts[1]);
            zOffset = float.Parse(setts[2]);
            moveSmoothing = float.Parse(setts[3]);
            rotSmoothing = float.Parse(setts[4]);
            rigLerpingMultiplierSlow = float.Parse(setts[5]);
            perspective = int.Parse(setts[6]);
            nameTagFont = int.Parse(setts[7]);
            loadedFont = loadedFonts[nameTagFont - 1];
            scoreOverlay = int.Parse(setts[8]);
            nametagsEnabled = bool.Parse(setts[9]);
            
            if (nametagsEnabled)
            {
                loadedRigs.ForEach(rig =>
                {
                    rig.GetOrAddComponent<NametagObject>(out var unused);
                });
            }
            
            timeOfDay = int.Parse(setts[10]);
            overlayX = int.Parse(setts[11]);
            overlayY = int.Parse(setts[12]);
            
            leaderboardOverlay = int.Parse(setts[13]);
            leaderboardY = int.Parse(setts[14]);
            
            camera.fieldOfView = float.Parse(setts[15]);
            firstPersonEnabled = bool.Parse(setts[16]);
            rigLerpingMultiplierFast = float.Parse(setts[17]);
            
            nametagFPS = bool.Parse(setts[18]);
            nametagPlat = bool.Parse(setts[19]);
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
        private GUIStyle labelStyle = null;
        
        private string team1Name = "TTT";
        private string team2Name = "TSO";
        private int team1Score = 0, team2Score = 0;

        private bool outdatedBuild = false;
        private VRRig disabledCosmeticsRig;

        public List<(string, NametagObject)> tags = new List<(string, NametagObject)>(); // unused as of now, but might be usefull sooner or later
        
        #endregion


        #region Setting variables

        private float xOffset = 0, yOffset = 0, zOffset = 0;
        private float moveSmoothing = 0, rotSmoothing = 0, rigLerpingMultiplierSlow = 1, rigLerpingMultiplierFast = 1;
        private bool firstPersonEnabled = false, cosmeticsHidden = false;

        public bool nametagsEnabled = false, nametagFPS = true, nametagPlat = false;
        public int nameTagFont = 5, scoreOverlay = 0, leaderboardOverlay = 0;
        public TMP_FontAsset loadedFont;

        private int overlayX = -1, overlayY = -1, leaderboardY = -1, timeOfDay = 0, perspective = 1;

        #endregion

        private float timeRunning = -10, timeToBeat = -10;
        private bool isTimerRunning = false;
    }
}