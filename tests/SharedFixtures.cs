using System.IO;
using System.Text;

namespace Tawop.Tests
{
    public static class SharedFixtures
    {
        internal const string SteamBundle = "class Settings{get clientSettings(){return this.m_ClientSettings}};var fields={library_low_bandwidth_mode:{n:1},library_low_perf_mode:{n:2},library_disable_community_content:{n:3},accessibility_reduce_motion:{n:4}};";

        internal const string DiscordBundle = "var webRequestDispatcher_1=__webpack_require__(6228);var syntheticModules={6228(__unused_rspack_module,exports){\"use strict\";Object.defineProperty(exports,\"__esModule\",{value:!0}),exports.registerBeforeRequest=function(session,id,handler,urlPatterns){}}};var settings={spellcheck:!0,contextIsolation:!0,additionalArguments,devTools:ENABLE_DEVTOOLS};mainWindowId=(mainWindow=new electron_1.BrowserWindow(mainWindowOptions)).id,global.mainWindowId=mainWindowId,ready=true;function dispatch(){for(let{handler}of list.values()){let response=handler(details);if(null!=response)return void callback(response)}callback({})}";

        internal static void WriteSteamBundle(string root)
        {
            Directory.CreateDirectory(Path.Combine(root, "steamui"));
            File.WriteAllText(Path.Combine(root, "steamui", "sp.js"), SteamBundle, new UTF8Encoding(false));
        }
    }
}
