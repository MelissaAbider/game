using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace EchoShift
{
    /// <summary>HTTP calls to the FastAPI backend (Gemini intent, generated art).</summary>
    public static class Backend
    {
        public static IEnumerator Get(string path, Action<string> ok, Action<string> fail = null, int timeoutSeconds = 15)
        {
            using (var req = UnityWebRequest.Get(Config.Http(path)))
            {
                req.timeout = timeoutSeconds;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) ok?.Invoke(req.downloadHandler.text);
                else fail?.Invoke(req.error);
            }
        }

        public static IEnumerator PostJson(string path, string json, Action<string> ok, Action<string> fail = null, int timeoutSeconds = 30)
        {
            using (var req = new UnityWebRequest(Config.Http(path), "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = timeoutSeconds;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) ok?.Invoke(req.downloadHandler.text);
                else fail?.Invoke($"{req.error} {req.downloadHandler?.text}");
            }
        }

        public static IEnumerator Texture(string pathOrUrl, Action<Texture2D> ok, Action<string> fail = null)
        {
            using (var req = UnityWebRequestTexture.GetTexture(Config.Http(pathOrUrl)))
            {
                req.timeout = 30;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) ok?.Invoke(DownloadHandlerTexture.GetContent(req));
                else fail?.Invoke(req.error);
            }
        }

        /// <summary>
        /// Asks the backend for its Gemini asset manifest (generating missing art on the fly)
        /// and registers every texture it knows how to use.
        /// </summary>
        public static IEnumerator LoadGeneratedArt(MonoBehaviour host, Action<string> status)
        {
            string manifestText = null;
            status?.Invoke("Asking Gemini to paint the facility…");
            yield return Get("/api/design/assets", t => manifestText = t, e => status?.Invoke("Design service offline: " + e), 240);
            var manifest = Json.ParseObject(manifestText);
            var assets = manifest.Obj("assets");
            if (assets == null) yield break;

            foreach (var id in new[] { "tex_wall", "tex_floor", "tex_holo", "keyart", "player" })
            {
                var url = assets.Obj(id).Str("url", null);
                if (string.IsNullOrEmpty(url)) continue;
                status?.Invoke($"Loading {id}…");
                yield return Texture(url, tex => Mats.RegisterTexture(id, tex));
            }
            status?.Invoke($"Art by {manifest.Str("image_model", "Gemini")}");
        }
    }
}
