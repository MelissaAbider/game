using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EchoShift
{
    /// <summary>
    /// On-screen controls for phones and tablets: a 4-way pad on the left, JUMP / CROUCH on the right.
    /// Voice stays the main controller (hold the mic orb); these are the manual fallback.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        public static Vector2 Move { get; private set; }
        public static bool CrouchHeld { get; private set; }
        static bool jumpQueued, crouchQueued;

        Vector2 held;

        public static bool ConsumeJump()
        {
            bool v = jumpQueued;
            jumpQueued = false;
            return v;
        }

        public static bool ConsumeCrouchDown()
        {
            bool v = crouchQueued;
            crouchQueued = false;
            return v;
        }

        public static TouchControls Create()
        {
            var go = new GameObject("TouchControls");
            var tc = go.AddComponent<TouchControls>();
            tc.Build();
            return tc;
        }

        void Build()
        {
            var canvas = Ui.CreateCanvas("Touch Canvas", 20);
            canvas.transform.SetParent(transform, false);
            var root = Ui.Stretch("Root", canvas.transform);

            // Left pad sits above the voice panel.
            var pad = Ui.Rect("Pad", root, new Vector2(0, 0), new Vector2(60, 320), new Vector2(330, 330));
            PadButton(pad, 90f, new Vector2(110, 220), Vector2.up);
            PadButton(pad, -90f, new Vector2(110, 0), Vector2.down);
            PadButton(pad, 180f, new Vector2(0, 110), Vector2.left);
            PadButton(pad, 0f, new Vector2(220, 110), Vector2.right);

            // Right action buttons.
            ActionButton(root, "JUMP", new Vector2(-60, 470), Mats.Red, () => jumpQueued = true, null);
            ActionButton(root, "CROUCH", new Vector2(-60, 300), Mats.Amber, () => { crouchQueued = true; CrouchHeld = true; }, () => CrouchHeld = false);
        }

        void PadButton(RectTransform pad, float angle, Vector2 pos, Vector2 dir)
        {
            var img = Ui.Panel(pad, new Vector2(0, 0), pos, new Vector2(110, 110), new Color(0.03f, 0.08f, 0.13f, 0.72f), "Pad " + angle);
            Ui.Border(img, Mats.Cyan, 2f);
            img.raycastTarget = true;
            // Chevron made of two bars, rotated to point in the pad direction.
            var chevron = Ui.Rect("Chevron", img.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
            chevron.localRotation = Quaternion.Euler(0, 0, angle);
            foreach (var side in new[] { -1f, 1f })
            {
                var bar = Ui.Panel(chevron, new Vector2(0.5f, 0.5f), new Vector2(-4f, side * 11f), new Vector2(36, 9), Mats.Cyan, "Bar");
                bar.raycastTarget = false;
                bar.rectTransform.localRotation = Quaternion.Euler(0, 0, -side * 45f);
            }
            Bind(img, () => { held += dir; Move = Vector2.ClampMagnitude(held, 1f); img.color = new Color(0.1f, 0.35f, 0.45f, 0.85f); },
                () => { held -= dir; Move = Vector2.ClampMagnitude(held, 1f); img.color = new Color(0.03f, 0.08f, 0.13f, 0.72f); });
        }

        void ActionButton(RectTransform root, string label, Vector2 pos, Color color, Action down, Action up)
        {
            var img = Ui.Panel(root, new Vector2(1, 0), pos, new Vector2(170, 140), new Color(0.03f, 0.08f, 0.13f, 0.72f), "Btn " + label);
            Ui.Border(img, color, 3f);
            img.raycastTarget = true;
            Ui.Label(img.transform, label, Ui.Title, 30, color, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170, 140));
            Bind(img, () => { down?.Invoke(); img.color = new Color(color.r, color.g, color.b, 0.4f); },
                () => { up?.Invoke(); img.color = new Color(0.03f, 0.08f, 0.13f, 0.72f); });
        }

        static void Bind(Image img, Action down, Action up)
        {
            var trigger = img.gameObject.AddComponent<EventTrigger>();
            var d = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            d.callback.AddListener(_ => down());
            var u = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            u.callback.AddListener(_ => up());
            trigger.triggers.Add(d);
            trigger.triggers.Add(u);
        }

        void OnDestroy()
        {
            Move = Vector2.zero;
            CrouchHeld = false;
            jumpQueued = crouchQueued = false;
        }
    }
}
