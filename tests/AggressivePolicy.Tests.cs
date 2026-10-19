using System;
using System.IO;

namespace Tawop.Tests
{
    public static class AggressivePolicyTests
    {
        public static void Run()
        {
            string discord = AggressivePolicy.PatchDiscordBundle(SharedFixtures.DiscordBundle);
            Check(discord.Contains("imageAnimationPolicy:\"noAnimation\""), "Animated images must use the engine's still-image policy.");
            Check(discord.Contains("spellcheck:!1"), "The aggressive policy must disable spellchecking.");
            Check(discord.Contains("tawop-aggressive-v5"), "The native dispatcher must register the resource policy.");
            Check(discord.Contains("for(let{handler}of list.values()){let response=handler(details);if(null!=response)return void callback(response)}callback({})"), "Existing dispatcher callback behavior must be preserved.");
            Check(!discord.Contains("disableHardwareAcceleration") && !discord.Contains("disable-gpu") && !discord.Contains("no-sandbox"), "The aggressive policy must preserve GPU and sandbox decisions.");
            Expect<InvalidOperationException>(delegate { AggressivePolicy.PatchDiscordBundle(discord); });
            Expect<InvalidDataException>(delegate { AggressivePolicy.PatchDiscordBundle(SharedFixtures.DiscordBundle + SharedFixtures.DiscordBundle); });
            Expect<InvalidDataException>(delegate { AggressivePolicy.PatchDiscordBundle(SharedFixtures.DiscordBundle.Replace("devTools:ENABLE_DEVTOOLS", "devTools:false")); });
            Expect<InvalidDataException>(delegate { AggressivePolicy.PatchDiscordBundle(SharedFixtures.DiscordBundle.Replace("exports.registerBeforeRequest", "exports.unsupportedRequest")); });
            Expect<InvalidDataException>(delegate { AggressivePolicy.PatchDiscordBundle(SharedFixtures.DiscordBundle.Replace("callback({})", "callback(null)")); });
            Expect<InvalidDataException>(delegate { AggressivePolicy.PatchDiscordBundle(SharedFixtures.DiscordBundle.Replace("webRequestDispatcher_1", "webRequestDispatcher_2")); });
            Expect<InvalidDataException>(delegate { AggressivePolicy.PatchDiscordBundle(SharedFixtures.DiscordBundle.Replace("__webpack_require__(6228)", "__webpack_require__(6229)")); });
            Expect<InvalidDataException>(delegate { AggressivePolicy.PatchDiscordBundle(SharedFixtures.DiscordBundle.Replace("{6228(", "{16228(")); });
            Expect<InvalidDataException>(delegate { AggressivePolicy.PatchDiscordBundle("var webRequestDispatcher_1=__webpack_require__(6228);" + SharedFixtures.DiscordBundle); });

            string steam = AggressivePolicy.PatchSteamBundle(SharedFixtures.SteamBundle);
            Check(steam.Contains("return tawopSteamClientSettings(this.m_ClientSettings)"), "The native Steam getter must use the cached policy.");
            Check(steam.Contains("library_disable_community_content"), "The policy must activate native community-content suppression.");
            Check(!steam.Contains("localStorage") && !steam.Contains("SetSetting("), "The policy must not write stored account preferences.");
            Expect<InvalidOperationException>(delegate { AggressivePolicy.PatchSteamBundle(steam); });
            Expect<InvalidDataException>(delegate { AggressivePolicy.PatchSteamBundle(SharedFixtures.SteamBundle + SharedFixtures.SteamBundle); });
            Expect<InvalidDataException>(delegate { AggressivePolicy.PatchSteamBundle(SharedFixtures.SteamBundle.Replace("accessibility_reduce_motion:{n:", "unsupported_field:{n:")); });
            Check(!AggressivePolicy.ContainsSteamSettingsStore("window.unrelated = true;"), "Unrelated Steam chunks must not be patched.");
        }

        private static void Check(bool value, string message)
        {
            if (!value)
            {
                throw new Exception("Aggressive policy test failed: " + message);
            }
        }

        private static void Expect<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            throw new Exception("Aggressive policy test expected " + typeof(T).Name + ".");
        }
    }
}
