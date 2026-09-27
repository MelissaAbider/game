using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoShift
{
    /// <summary>Starts the game in any scene (the whole world is generated from code).</summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (Object.FindFirstObjectByType<GameDirector>()) return;
            new GameObject("EchoShift").AddComponent<GameDirector>();
        }

        public static void Restart()
        {
            Time.timeScale = 1f;
            Barrier.All.Clear();
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
                return;
            }
            // Unsaved scene (pressed Play in an empty editor scene): rebuild everything in place.
            foreach (var root in scene.GetRootGameObjects())
                if (root.name != "Main Camera") Object.Destroy(root);
            MainThread.Post(() => new GameObject("EchoShift").AddComponent<GameDirector>());
        }
    }
}
