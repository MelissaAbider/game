using System;

namespace EchoShift
{
    public static class Config
    {
        /// <summary>FastAPI backend (Gemini intent parser, Gradium voice, image generation). Override with ECHOSHIFT_BACKEND or -backend URL.</summary>
        public static readonly string BackendUrl = Resolve();

        static string Resolve()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-backend") return args[i + 1].TrimEnd('/');
            var env = Environment.GetEnvironmentVariable("ECHOSHIFT_BACKEND");
            if (!string.IsNullOrEmpty(env)) return env.TrimEnd('/');
            // Phone builds: the build script writes the PC's LAN address into Resources/backend_url.txt.
            var baked = UnityEngine.Resources.Load<UnityEngine.TextAsset>("backend_url");
            if (baked && !string.IsNullOrWhiteSpace(baked.text)) return baked.text.Trim().TrimEnd('/');
            return "http://127.0.0.1:8000";
        }

        public static string Http(string path) => path.StartsWith("http") ? path : BackendUrl + path;

        public static string Ws(string path)
        {
            if (BackendUrl.StartsWith("https://")) return "wss://" + BackendUrl.Substring(8) + path;
            if (BackendUrl.StartsWith("http://")) return "ws://" + BackendUrl.Substring(7) + path;
            return BackendUrl + path;
        }
    }
}
