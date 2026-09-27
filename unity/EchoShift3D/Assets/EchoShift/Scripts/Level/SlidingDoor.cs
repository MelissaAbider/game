using System;
using UnityEngine;

namespace EchoShift
{
    /// <summary>Double sci-fi door in a doorway. Opens for the subject (and echo clones) unless locked.</summary>
    public sealed class SlidingDoor : MonoBehaviour
    {
        public Func<bool> Locked = () => false;
        /// <summary>Open whenever unlocked, even with nobody around (echo door driven by a pressure plate).</summary>
        public bool OpenWhenUnlocked;

        Transform left, right;
        Material statusMat;
        float open, width;
        bool wasOpen, wasLocked = true;

        public bool IsOpen => open > 0.85f;
        public float Openness => Mathf.SmoothStep(0f, 1f, open);

        public static SlidingDoor Build(Transform parent, float z, float floorY, float doorWidth, float doorHeight, Color accent)
        {
            var root = new GameObject("Door").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(0, floorY, z);
            var door = root.gameObject.AddComponent<SlidingDoor>();
            door.width = doorWidth;
            var panel = Mats.Solid(new Color(0.12f, 0.15f, 0.19f), 0.75f, 0.7f);
            var edge = Mats.Emissive(accent, 3f);
            door.left = Prims.Box("PanelL", root, new Vector3(-doorWidth * 0.25f, doorHeight * 0.5f, 0), new Vector3(doorWidth * 0.5f, doorHeight, 0.18f), panel).transform;
            door.right = Prims.Box("PanelR", root, new Vector3(doorWidth * 0.25f, doorHeight * 0.5f, 0), new Vector3(doorWidth * 0.5f, doorHeight, 0.18f), panel).transform;
            Prims.Box("EdgeL", door.left, new Vector3(0.48f, 0, -0.6f), new Vector3(0.04f, 0.9f, 0.2f), edge, false, false);
            Prims.Box("EdgeR", door.right, new Vector3(-0.48f, 0, -0.6f), new Vector3(0.04f, 0.9f, 0.2f), edge, false, false);
            door.statusMat = Mats.Emissive(Mats.Red, 4f);
            Prims.Box("Status", root, new Vector3(0, doorHeight + 0.18f, -0.12f), new Vector3(0.6f, 0.08f, 0.05f), door.statusMat, false, false);
            return door;
        }

        void Update()
        {
            bool locked = Locked();
            bool want = false;
            if (!locked)
            {
                if (OpenWhenUnlocked) want = true;
                else
                {
                    var s = GameDirector.I ? GameDirector.I.Subject : null;
                    if (s && Near(s.transform.position)) want = true;
                    else if (GameDirector.I && GameDirector.I.Echoes && GameDirector.I.Echoes.AnyCloneNear(transform.position, 3.5f)) want = true;
                }
            }
            // Never crush the subject standing in the doorway.
            var subject = GameDirector.I ? GameDirector.I.Subject : null;
            if (!want && subject && InDoorway(subject.transform.position)) want = true;

            open = Mathf.MoveTowards(open, want ? 1f : 0f, Time.deltaTime * 2.8f);
            float slide = Mathf.SmoothStep(0f, 1f, open) * width * 0.5f;
            left.localPosition = new Vector3(-width * 0.25f - slide, left.localPosition.y, 0);
            right.localPosition = new Vector3(width * 0.25f + slide, right.localPosition.y, 0);

            if (locked != wasLocked)
            {
                wasLocked = locked;
                Mats.SetEmission(statusMat, locked ? Mats.Red : Mats.Green, 4f);
            }
            bool isOpen = open > 0.5f;
            if (isOpen != wasOpen)
            {
                wasOpen = isOpen;
                Sfx.PlayAt("door", transform.position + Vector3.up * 1.5f, 0.7f);
            }
        }

        bool Near(Vector3 p) => Mathf.Abs(p.z - transform.position.z) < 4f && Mathf.Abs(p.x) < width + 2f && Mathf.Abs(p.y - transform.position.y) < 2f;
        bool InDoorway(Vector3 p) => Mathf.Abs(p.z - transform.position.z) < 0.7f && Mathf.Abs(p.x) < width * 0.5f + 0.3f;
    }
}
