using System;
using UnityEngine;
using UnityEngine.UI;

namespace EchoShift
{
    public sealed class VictoryScreen : MonoBehaviour
    {
        public static void Show(string rank, Color rankColor, float seconds, int commands, int hits, int energySpent, int echoes, Action restart)
        {
            var go = new GameObject("VictoryScreen");
            var screen = go.AddComponent<VictoryScreen>();
            var canvas = Ui.CreateCanvas("Victory Canvas", 60);
            canvas.transform.SetParent(go.transform, false);
            var root = Ui.Stretch("Root", canvas.transform);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var shade = root.gameObject.AddComponent<Image>();
            shade.color = new Color(0.01f, 0.02f, 0.04f, 0.82f);
            var bg = Resources.Load<Texture2D>("Images/home_bg");
            if (bg) Ui.Fill(root, "Backdrop", bg, new Color(1f, 1f, 1f, 0.25f));
            Ui.Fill(root, "ShadeLeft", Ui.Ramp(true, 1.3f), new Color(0.01f, 0.02f, 0.05f, 0.9f));

            // Subject 731 celebrating (the peace-sign pose from the home page).
            var hero = HeroArt.Cutout("Images/hero_fun");
            if (hero)
            {
                Ui.RawImage(root, "Glow", new Vector2(0.5f, 0.5f), new Vector2(560, -420), new Vector2(620, 150), Ui.Radial(), new Color(1f, 0.42f, 0.82f, 0.5f));
                var img = Ui.RawImage(root, "Hero", new Vector2(0.5f, 0.5f), new Vector2(560, -420), new Vector2(640f * hero.width / hero.height, 640f), hero);
                img.rectTransform.pivot = new Vector2(0.5f, 0f);
                img.rectTransform.anchoredPosition = new Vector2(560, -440);
            }

            var card = Ui.Panel(root, new Vector2(0.5f, 0.5f), new Vector2(-260, 0), new Vector2(1180, 820), Ui.PanelBg, "Card");
            Ui.Frame(card, Mats.Green, 34f, 4f);
            var c = card.transform;
            Ui.Label(c, "SUBJECT 731 ESCAPED", Ui.Bold, 26, Mats.Green, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(1100, 36));
            Ui.Glow(Ui.Label(c, "SECTOR CLEARED", Ui.Title, 86, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(1100, 110)), Mats.Green, 5f);
            var rankText = Ui.Label(c, rank, Ui.Title, 240, rankColor, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(600, 280));
            Ui.Label(c, $"TIME {seconds:0}s   ·   COMMANDS {commands}   ·   HITS {hits}   ·   ENERGY {energySpent}   ·   ECHOES {echoes}",
                Ui.Bold, 30, Mats.Cyan, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(1100, 44));
            Ui.Label(c, "S rank: no hits, 20 commands or fewer, under 7 minutes.", Ui.Body, 24, new Color(0.75f, 0.85f, 0.88f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -185), new Vector2(1100, 36));
            var again = Ui.Button(c, "RUN IT AGAIN", new Vector2(0.5f, 0.5f), new Vector2(0, -300), new Vector2(420, 84), Mats.Green, () => restart?.Invoke());
            again.GetComponentInChildren<Text>().font = Ui.Title;
            Tween.Run(screen, 0.8f, k => group.alpha = k);
            rankText.transform.localScale = Vector3.one * 3f;
            Tween.Run(screen, 0.7f, k => rankText.transform.localScale = Vector3.one * Mathf.Lerp(3f, 1f, Tween.OutBack(k)));
        }
    }
}
