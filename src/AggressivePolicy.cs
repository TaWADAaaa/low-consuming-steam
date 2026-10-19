using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Tawop
{
    public static class AggressivePolicy
    {
        public const string Marker = "TAWOP_AGGRESSIVE_V5";
        private const string DiscordPreferences = "spellcheck:!0,contextIsolation:!0,additionalArguments,devTools:ENABLE_DEVTOOLS";
        private const string DiscordWindow = "mainWindowId=(mainWindow=new electron_1.BrowserWindow(mainWindowOptions)).id,global.mainWindowId=mainWindowId,";
        private const string SteamGetter = "get clientSettings(){return this.m_ClientSettings}";
        public const string DiscordRequestPolicy = @"
function tawopDiscordResourceDecision(details) {
  'use strict';
  try {
    if (!details || details.method !== 'GET') return;
    var url = new URL(details.url);
    if (url.protocol !== 'https:' || url.username || url.password || url.port ||
        (url.hostname !== 'cdn.discordapp.com' && url.hostname !== 'media.discordapp.net')) return;
    var path = url.pathname;
    if ((details.resourceType === 'image' || details.resourceType === 'media' || details.resourceType === 'xhr') &&
        (/^\/avatar-decoration-presets\/[a-zA-Z0-9_.-]+\.(?:png|webp|gif|avif)$/.test(path) ||
         /^\/assets\/profile_effects\/[a-zA-Z0-9_./-]+\.(?:png|webp|gif|avif|webm|mp4|json)$/.test(path))) {
      return { cancel: true };
    }
    if (details.resourceType !== 'image' || url.hostname !== 'cdn.discordapp.com') return;
    var identity = /^\/(?:avatars|icons)\/\d{1,22}\/(?:a_)?[a-fA-F0-9]{16,64}\.(?:png|jpe?g|webp|gif|avif)$/.test(path) ||
      /^\/guilds\/\d{1,22}\/users\/\d{1,22}\/avatars\/(?:a_)?[a-fA-F0-9]{16,64}\.(?:png|jpe?g|webp|gif|avif)$/.test(path) ||
      /^\/emojis\/\d{1,22}\.(?:png|jpe?g|webp|gif|avif)$/.test(path);
    if (!identity) return;
    url.pathname = path.replace(/\.[a-zA-Z0-9]+$/, '.webp');
    var requested = Number(url.searchParams.get('size'));
    var size = requested >= 16 && requested <= 64 && (requested & (requested - 1)) === 0 ? requested : 64;
    url.searchParams.set('size', String(size));
    url.searchParams.set('animated', 'false');
    url.searchParams.delete('format');
    var redirected = url.toString();
    if (redirected !== details.url) return { redirectURL: redirected };
  } catch (_) {}
}
";
        public const string SteamSettingsPolicy = @"
var tawopSteamSettingsCache = new WeakMap();
function tawopSteamClientSettings(settings) {
  'use strict';
  if (!settings || typeof settings !== 'object') return settings;
  var cached = tawopSteamSettingsCache.get(settings);
  if (cached) return cached;
  var proxy = new Proxy(settings, {
    get: function (target, key) {
      if (key === 'library_low_bandwidth_mode' || key === 'library_low_perf_mode' ||
          key === 'library_disable_community_content' || key === 'accessibility_reduce_motion') {
        var descriptor = Reflect.getOwnPropertyDescriptor(target, key);
        if (descriptor && !descriptor.configurable &&
            (('value' in descriptor && !descriptor.writable) ||
             (!('value' in descriptor) && descriptor.get === undefined))) return Reflect.get(target, key, target);
        return true;
      }
      return Reflect.get(target, key, target);
    }
  });
  tawopSteamSettingsCache.set(settings, proxy);
  return proxy;
}
";

        public static string PatchDiscordBundle(string original)
        {
            ValidateSource(original, "Discord");
            RequireOne(original, DiscordPreferences, "Discord window preferences");
            RequireOne(original, DiscordWindow, "Discord window construction");
            ValidateDiscordDispatcher(original);
            RequireOne(original, "exports.registerBeforeRequest=function(session,id,handler,urlPatterns)", "Discord request dispatcher");
            RequireOne(original, "for(let{handler}of list.values()){let response=handler(details);if(null!=response)return void callback(response)}callback({})", "Discord request dispatcher callback");
            string preferences = "spellcheck:!1,imageAnimationPolicy:\"noAnimation\",contextIsolation:!0,additionalArguments,devTools:ENABLE_DEVTOOLS";
            string registration = DiscordWindow + "(0,webRequestDispatcher_1.registerBeforeRequest)(mainWindow.webContents.session,\"tawop-aggressive-v5\",tawopDiscordResourceDecision,[\"https://cdn.discordapp.com/*\",\"https://media.discordapp.net/*\"]),";
            return "var tawopEngineVersion = \"" + Marker + "\";\n" + DiscordRequestPolicy + "\n" + original.Replace(DiscordPreferences, preferences).Replace(DiscordWindow, registration);
        }

        public static bool ContainsSteamSettingsStore(string original)
        {
            return original != null && (original.IndexOf("get clientSettings()", StringComparison.Ordinal) >= 0 || original.IndexOf("tawopSteamClientSettings", StringComparison.Ordinal) >= 0);
        }

        public static string PatchSteamBundle(string original)
        {
            ValidateSource(original, "Steam");
            RequireOne(original, SteamGetter, "Steam client settings getter");
            string[] fields = { "library_low_bandwidth_mode:{n:", "library_low_perf_mode:{n:", "library_disable_community_content:{n:", "accessibility_reduce_motion:{n:" };
            foreach (string field in fields)
            {
                if (original.IndexOf(field, StringComparison.Ordinal) < 0)
                {
                    throw new InvalidDataException("Steam's low resource settings are not supported in this application build. No files have been changed.");
                }
            }

            return "var tawopEngineVersion = \"" + Marker + "\";\n" + SteamSettingsPolicy + "\n" + original.Replace(SteamGetter, "get clientSettings(){return tawopSteamClientSettings(this.m_ClientSettings)}");
        }

        private static void ValidateSource(string original, string product)
        {
            if (String.IsNullOrEmpty(original) || original.Length > 32 * 1024 * 1024)
            {
                throw new InvalidDataException(product + " application source is empty or exceeds the supported size.");
            }

            if (original.IndexOf(Marker, StringComparison.Ordinal) >= 0 || original.IndexOf("tawopDiscordResourceDecision", StringComparison.Ordinal) >= 0 || original.IndexOf("tawopSteamClientSettings", StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException(product + " already contains the aggressive patch. Restore its backup before applying again.");
            }
        }

        private static void ValidateDiscordDispatcher(string original)
        {
            MatchCollection bindings = Regex.Matches(original, @"(?<![\w$])webRequestDispatcher_1=__webpack_require__\(([0-9]{1,10})\)", RegexOptions.CultureInvariant);
            if (bindings.Count != 1)
            {
                throw new InvalidDataException("Discord's request dispatcher binding is unsupported or ambiguous. No files have been changed.");
            }

            string header = "(__unused_rspack_module,exports){\"use strict\";Object.defineProperty(exports,\"__esModule\",{value:!0}),exports.registerBeforeRequest=function(session,id,handler,urlPatterns)";
            string pattern = @"(?:\{|,)" + bindings[0].Groups[1].Value + Regex.Escape(header);
            if (Regex.Matches(original, pattern, RegexOptions.CultureInvariant).Count != 1)
            {
                throw new InvalidDataException("Discord's request dispatcher module is not recognized unambiguously. No files have been changed.");
            }
        }

        private static void RequireOne(string source, string anchor, string label)
        {
            int first = source.IndexOf(anchor, StringComparison.Ordinal);
            if (first < 0 || source.IndexOf(anchor, first + anchor.Length, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidDataException(label + " is not recognized unambiguously. This application build needs a compatibility update. No files have been changed.");
            }
        }
    }
}
