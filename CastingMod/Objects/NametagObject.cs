using System;
using System.Collections.Generic;
using System.Reflection;
using GorillaNetworking;
using TMPro;
using UnityEngine;

namespace vynscastingmod.Objects
{
    public class NametagObject : MonoBehaviour
    {
        public void Awake()
        {
            attachedRig = GetComponentInParent<VRRig>();
            textObj = new GameObject().AddComponent<TextMeshPro>();
            textObj.font = Main.instance.loadedFont;
            textObj.fontStyle = FontStyles.Bold;
            textObj.fontSize = 8;
            textObj.alignment = TextAlignmentOptions.CenterGeoAligned;
            textObj.transform.localScale = Vector3.one * 0.2f;
        }

        public void OnDestroy()
        {
            textObj.text = "";
            Destroy(textObj);
            Destroy(textObj.gameObject);
        }

        public void LateUpdate()
        {
            if (!Main.instance.nametagsEnabled)
            {
                textObj.text = "";
                Destroy(textObj);
                Destroy(textObj.gameObject);
                Destroy(this);
                return;
            }

            try
            {
                if (!Main.instance.tags.Contains((this.attachedRig.Creator.UserId, this)))
                    Main.instance.tags.Add((this.attachedRig.Creator.UserId, this));
            }
            catch (Exception) // if this fails, attachedRig is probably destroyed sooo yar
            {
                textObj.text = "";
                Destroy(textObj);
                Destroy(textObj.gameObject);
                Destroy(this);
            }
            
            
            
            // PLAT FORMS!
            
            string plat = "OCULUS";
            
            try
            {
                var field = typeof(VRRig).GetField(
                    "_playerOwnedCosmetics",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
                var cosmetics = (HashSet<string>)field.GetValue(attachedRig);

                cosmetics.ForEach(c =>
                {
                    if (c.ToLower().Contains("first login")) plat = "STEAM";
                    if (c.ToLower().Contains("game-purchase")) plat = "OCULUS PC";
                });
            }
            catch (Exception eee) { }
            
            // PLAT FORMS!
            
            

            textObj.font = Main.instance.loadedFont;
            textObj.transform.position = attachedRig.transform.position + (Vector3.up * 0.4f);
            textObj.transform.rotation = Main.instance.camera.transform.rotation;
            textObj.text = "";
            
            if (Main.instance.nametagFPS == 2)
            {
                var field = typeof(VRRig).GetField("fps", BindingFlags.NonPublic | BindingFlags.Instance);
                textObj.text += "<size=5>" + field.GetValue(attachedRig) + "FPS\n";
                textObj.transform.position += (Vector3.up * 0.06f);
            }
            
            if (Main.instance.nametagPlat == 2)
            {
                
                textObj.text += "<size=5>" + plat+"\n";
                textObj.transform.position += (Vector3.up * 0.06f);
            }
            
            
            textObj.text += "<size=8>" + attachedRig.playerNameVisible;
            
            
            if (Main.instance.nametagFPS == 1)
            {
                var field = typeof(VRRig).GetField("fps", BindingFlags.NonPublic | BindingFlags.Instance);
                textObj.text += "<size=5>\n" + field.GetValue(attachedRig) + "FPS";
                textObj.transform.position += (Vector3.up * 0.06f);
            }
            
            if (Main.instance.nametagPlat == 1)
            {
                
                textObj.text += "<size=5>\n" + plat;
                textObj.transform.position += (Vector3.up * 0.06f);
            }
            
            
            textObj.color = attachedRig.playerColor;
        }

        public TextMeshPro textObj;
        public VRRig attachedRig;
    }
}