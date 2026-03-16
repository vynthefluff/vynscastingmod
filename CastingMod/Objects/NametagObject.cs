using System;
using UnityEngine;

namespace vynscastingmod.Objects
{
    public class NametagObject : MonoBehaviour
    {
        public void Awake()
        {
            attachedRig = GetComponentInParent<VRRig>();
        }
        public void Update()
        {
            this.transform.position = attachedRig.transform.position + (Vector3.up * 0.33f);
        }

        public VRRig attachedRig;
    }
}