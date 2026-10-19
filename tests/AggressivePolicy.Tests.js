"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const projectRoot = path.resolve(__dirname, "..");
const source = fs.readFileSync(path.join(projectRoot, "src/AggressivePolicy.cs"), "utf8");
let passed = 0;

function embedded(name) {
  const match = source.match(new RegExp("public const string " + name + " = @\"([\\s\\S]*?)\";"));
  assert.ok(match, "Embedded policy missing: " + name);
  return match[1].replaceAll('""', '"');
}

const context = vm.createContext({ URL, WeakMap, Proxy, Reflect });
vm.runInContext(embedded("DiscordRequestPolicy") + embedded("SteamSettingsPolicy"), context, { timeout: 1000 });

function decide(url, overrides = {}) {
  const response = context.tawopDiscordResourceDecision(Object.assign({ url, method: "GET", resourceType: "image" }, overrides));
  return response === undefined ? undefined : JSON.parse(JSON.stringify(response));
}

function test(name, action) {
  action();
  passed++;
  process.stdout.write("PASS " + name + "\n");
}

const hash = "a_0123456789abcdef0123456789abcdef";
const avatar = "https://cdn.discordapp.com/avatars/123456789/" + hash + ".gif?size=1024&animated=true";

test("decorations are canceled before a response body is downloaded", () => {
  for (const url of [
    "https://cdn.discordapp.com/avatar-decoration-presets/a_decoration.png",
    "https://cdn.discordapp.com/assets/profile_effects/effects/example/effect.webp",
    "https://media.discordapp.net/assets/profile_effects/effects/example/effect.webm"
  ]) {
    assert.deepEqual(decide(url), { cancel: true });
  }
  assert.deepEqual(decide("https://cdn.discordapp.com/assets/profile_effects/effects/example/effect.json", { resourceType: "xhr" }), { cancel: true });
});

test("avatars, server icons, guild avatars and custom emoji request small still WebP", () => {
  for (const url of [
    avatar,
    "https://cdn.discordapp.com/icons/123456789/" + hash + ".gif?size=4096",
    "https://cdn.discordapp.com/guilds/123456789/users/987654321/avatars/" + hash + ".webp?animated=true",
    "https://cdn.discordapp.com/emojis/123456789.webp?animated=true&size=512&format=gif"
  ]) {
    const response = decide(url);
    assert.ok(response.redirectURL);
    const redirected = new URL(response.redirectURL);
    assert.ok(redirected.pathname.endsWith(".webp"));
    assert.equal(redirected.searchParams.get("size"), "64");
    assert.equal(redirected.searchParams.get("animated"), "false");
    assert.equal(redirected.searchParams.has("format"), false);
    assert.equal(decide(response.redirectURL), undefined);
  }
});

test("already small image requests are not enlarged", () => {
  for (const size of [16, 32, 64]) {
    const response = decide(avatar.replace("1024", String(size)));
    assert.equal(new URL(response.redirectURL).searchParams.get("size"), String(size));
  }
});

test("unexpected sizes are clamped without redirect loops", () => {
  for (const size of ["0", "17", "-1", "0.5", "NaN", "Infinity", "1073741824"]) {
    const response = decide(avatar.replace("1024", size));
    assert.equal(new URL(response.redirectURL).searchParams.get("size"), "64");
    assert.equal(decide(response.redirectURL), undefined);
  }
});

test("authentication, chat API, attachment and user video requests pass through", () => {
  for (const url of [
    "https://discord.com/api/v10/auth/login",
    "https://discord.com/api/v10/channels/123456789/messages",
    "wss://gateway.discord.gg/?v=10",
    "https://cdn.discordapp.com/attachments/123456789/987654321/user.gif?ex=example&is=example&hm=example",
    "https://media.discordapp.net/attachments/123456789/987654321/user.webm",
    "https://cdn.discordapp.com/stickers/123456789.gif",
    "https://cdn.discordapp.com/banners/123456789/" + hash + ".gif",
    "https://cdn.discordapp.com/embed/avatars/1.png",
    "https://cdn.discordapp.com/app-assets/123456789/icon.png"
  ]) {
    assert.equal(decide(url), undefined);
    assert.equal(decide(url, { resourceType: "media" }), undefined);
  }
});

test("unrecognized origins, methods and resource types pass through", () => {
  for (const url of [
    avatar.replace("https:", "http:"),
    avatar.replace("cdn.discordapp.com", "cdn.discordapp.com.example.invalid"),
    avatar.replace("cdn.discordapp.com", "example.invalid"),
    avatar.replace("cdn.discordapp.com", "name@cdn.discordapp.com"),
    avatar.replace("cdn.discordapp.com", "cdn.discordapp.com:8443"),
    "data:image/gif;base64,example",
    "not a URL"
  ]) assert.equal(decide(url), undefined);
  for (const resourceType of ["script", "xhr", "mainFrame", "subFrame", "webSocket", "media", "other"])
    assert.equal(decide(avatar, { resourceType }), undefined);
  assert.equal(decide(avatar, { method: "POST" }), undefined);
  assert.equal(context.tawopDiscordResourceDecision(null), undefined);
});

test("native Steam low resource choices are forced without changing the original settings object", () => {
  const settings = {
    library_low_bandwidth_mode: false,
    library_low_perf_mode: false,
    library_disable_community_content: false,
    accessibility_reduce_motion: false,
    unchanged: "first"
  };
  const proxy = context.tawopSteamClientSettings(settings);
  for (const key of Object.keys(settings).filter(key => key !== "unchanged")) {
    assert.equal(proxy[key], true);
    assert.equal(settings[key], false);
  }
  assert.equal(proxy.unchanged, "first");
  settings.unchanged = "updated";
  assert.equal(proxy.unchanged, "updated");
  proxy.unchanged = "written";
  assert.equal(settings.unchanged, "written");
  assert.equal(context.tawopSteamClientSettings(settings), proxy);
  const replaced = { unchanged: "replacement" };
  assert.notEqual(context.tawopSteamClientSettings(replaced), proxy);
  assert.equal(context.tawopSteamClientSettings(null), null);
});

test("other Steam getters preserve their native receiver and reactive reads", () => {
  let reads = 0;
  const settings = {
    value: 7,
    get derived() {
      assert.equal(this, settings);
      reads++;
      return this.value * 2;
    }
  };
  const proxy = context.tawopSteamClientSettings(settings);
  assert.equal(proxy.derived, 14);
  settings.value = 11;
  assert.equal(proxy.derived, 22);
  assert.equal(reads, 2);
  const symbol = Symbol("native setting");
  settings[symbol] = 42;
  assert.equal(proxy[symbol], 42);
});

test("frozen Steam settings respect Proxy invariants and continue working", () => {
  const settings = Object.freeze({ library_low_perf_mode: false, unrelated: "immutable" });
  const proxy = context.tawopSteamClientSettings(settings);
  assert.equal(proxy.library_low_perf_mode, false);
  assert.equal(proxy.unrelated, "immutable");
  assert.equal(proxy.library_low_bandwidth_mode, true);
  const getterless = {};
  Object.defineProperty(getterless, "accessibility_reduce_motion", { configurable: false, get: undefined });
  assert.equal(context.tawopSteamClientSettings(getterless).accessibility_reduce_motion, undefined);
});

test("the Steam native policy prevents synthetic community load and animation work", () => {
  function renderNativeFixture(settings) {
    return {
      additionalCommunityRequests: settings.library_disable_community_content ? 0 : 8,
      screenshotSlides: settings.library_low_bandwidth_mode ? 1 : 12,
      continuousAnimationFrames: settings.library_low_perf_mode || settings.accessibility_reduce_motion ? 0 : 600
    };
  }
  const baseline = renderNativeFixture({});
  const optimized = renderNativeFixture(context.tawopSteamClientSettings({}));
  assert.deepEqual(baseline, { additionalCommunityRequests: 8, screenshotSlides: 12, continuousAnimationFrames: 600 });
  assert.deepEqual(optimized, { additionalCommunityRequests: 0, screenshotSlides: 1, continuousAnimationFrames: 0 });
});

test("a synthetic 120-request workload actually exercises blocked and redirected decisions", () => {
  const requests = [];
  for (let index = 0; index < 40; index++) {
    requests.push({ url: "https://cdn.discordapp.com/avatar-decoration-presets/a_example" + index + ".png", bytes: 256 * 1024 });
    requests.push({ url: avatar.replace("123456789/", String(100000000 + index) + "/"), bytes: 128 * 1024 });
    requests.push({ url: "https://cdn.discordapp.com/attachments/123456789/987654321/user" + index + ".webm", bytes: 1024 * 1024 });
  }
  let canceled = 0;
  let redirected = 0;
  let bodyBytes = 0;
  for (const request of requests) {
    const response = decide(request.url);
    if (response && response.cancel) {
      canceled++;
      continue;
    }
    if (response && response.redirectURL) {
      redirected++;
      bodyBytes += 4096;
      continue;
    }
    bodyBytes += request.bytes;
  }
  const baselineBytes = requests.reduce((sum, request) => sum + request.bytes, 0);
  assert.equal(canceled, 40);
  assert.equal(redirected, 40);
  assert.equal(bodyBytes, 40 * 4096 + 40 * 1024 * 1024);
  process.stdout.write("Synthetic workload only: " + JSON.stringify({ requests: requests.length, canceled, redirected, baselineFixtureBodyBytes: baselineBytes, optimizedFixtureBodyBytes: bodyBytes, clientMemoryMeasured: false }) + "\n");
});

process.stdout.write("Aggressive policy tests passed: " + passed + "\n");
