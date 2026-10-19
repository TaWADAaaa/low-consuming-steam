"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const projectRoot = path.resolve(__dirname, "..");
const discordCss = fs.readFileSync(path.join(projectRoot, "assets/discord/tawop.css"), "utf8");
const steamCss = fs.readFileSync(path.join(projectRoot, "assets/steam/tawop.css"), "utf8");
const template = fs.readFileSync(path.join(projectRoot, "assets/discord/preload.js"), "utf8");
const preload = template.replace("__TAWOP_STYLESHEET_JSON__", JSON.stringify(discordCss));
let passed = 0;

function video(properties = {}) {
  return Object.assign({
    tagName: "VIDEO",
    isConnected: true,
    autoplay: true,
    loop: true,
    muted: true,
    controls: false,
    srcObject: null,
    duration: 4,
    currentSrc: "https://example.invalid/decoration.webm",
    src: "https://example.invalid/decoration.webm",
    paused: false,
    pauses: 0,
    plays: 0,
    decoration: false,
    closest() {
      return this.decoration ? {} : null;
    },
    pause() {
      this.pauses++;
      this.paused = true;
    },
    play() {
      this.plays++;
      this.paused = false;
      return Promise.resolve();
    }
  }, properties);
}

function harness(videos = [], options = {}) {
  const elements = new Map();
  const listeners = new Map();
  const attributes = new Map();
  const document = {
    hidden: Boolean(options.hidden),
    readyState: options.readyState || "complete",
    videos,
    created: [],
    querySelectorAll(selector) {
      assert.equal(selector, "video[autoplay][loop]");
      return this.videos.filter(item => item.isConnected && item.autoplay && item.loop);
    },
    getElementById(id) {
      return elements.get(id) || null;
    },
    createElement(tagName) {
      this.created.push(tagName);
      return { id: "", textContent: "" };
    },
    addEventListener(name, listener, listenerOptions) {
      if (!listeners.has(name)) listeners.set(name, []);
      listeners.get(name).push({ listener, options: listenerOptions });
    },
    dispatch(name, target) {
      const current = [...(listeners.get(name) || [])];
      for (const entry of current) {
        if (entry.options && entry.options.once) {
          listeners.set(name, listeners.get(name).filter(item => item !== entry));
        }
        entry.listener({ target });
      }
    }
  };
  const container = {
    appendChild(element) {
      elements.set(element.id, element);
    },
    setAttribute(name, value) {
      attributes.set(name, value);
    }
  };
  document.head = options.noHead ? null : container;
  document.documentElement = container;
  const forbidden = () => { throw new Error("Polling and global mutation observers are forbidden."); };
  const context = vm.createContext({
    document,
    setInterval: forbidden,
    setTimeout: forbidden,
    MutationObserver: forbidden
  });
  vm.runInContext(preload, context, { timeout: 1000 });
  return {
    document,
    elements,
    listeners,
    attributes,
    runAgain() {
      vm.runInContext(preload, context, { timeout: 1000 });
    },
    visibility(hidden) {
      document.hidden = hidden;
      document.dispatch("visibilitychange");
    }
  };
}

function test(name, body) {
  body();
  passed++;
  process.stdout.write("PASS " + name + "\n");
}

test("install injects a stylesheet once without a floating wordmark", () => {
  const environment = harness();
  assert.equal(environment.elements.get("tawop-style").textContent, discordCss);
  assert.equal(environment.attributes.get("data-tawop-version"), "TAWOP_MOD_V3");
  assert.deepEqual(environment.document.created, ["style"]);
  environment.runAgain();
  assert.equal(environment.listeners.get("visibilitychange").length, 1);
  assert.equal(environment.listeners.get("play").length, 1);
  assert.equal(environment.listeners.get("play")[0].options, true);
});

test("loading document waits for DOMContentLoaded and supports a missing head", () => {
  const environment = harness([], { readyState: "loading", noHead: true });
  assert.equal(environment.elements.size, 0);
  environment.document.dispatch("DOMContentLoaded");
  environment.document.dispatch("DOMContentLoaded");
  assert.equal(environment.elements.size, 1);
  assert.equal(environment.listeners.get("visibilitychange").length, 1);
});

test("hidden muted decorative loop pauses and resumes once", () => {
  const loop = video();
  const environment = harness([loop]);
  assert.equal(loop.pauses, 0);
  environment.visibility(true);
  environment.visibility(true);
  assert.equal(loop.pauses, 1);
  environment.visibility(false);
  environment.visibility(false);
  assert.equal(loop.plays, 1);
});

test("hidden at install pauses only eligible loops", () => {
  const loop = video();
  harness([loop], { hidden: true });
  assert.equal(loop.pauses, 1);
});

test("known decoration videos stop even while the window is visible", () => {
  const decoration = video({ decoration: true });
  const userVideo = video();
  const environment = harness([decoration, userVideo]);
  assert.equal(decoration.pauses, 1);
  environment.document.dispatch("play", decoration);
  assert.equal(decoration.pauses, 1);
  assert.equal(userVideo.pauses, 0);
  environment.visibility(true);
  environment.visibility(false);
  assert.equal(decoration.plays, 0);
  assert.equal(userVideo.plays, 1);
});

test("late metadata pauses eligible hidden loops without polling", () => {
  const loop = video({ duration: NaN });
  const environment = harness([loop], { hidden: true });
  assert.equal(loop.pauses, 0);
  loop.duration = 4;
  environment.document.dispatch("loadedmetadata", loop);
  assert.equal(loop.pauses, 1);
});

test("voice and video streams, controls, audio and live media keep playing", () => {
  const media = [
    video({ srcObject: {} }),
    video({ controls: true }),
    video({ muted: false }),
    video({ duration: Infinity }),
    video({ duration: NaN }),
    video({ duration: 0 }),
    video({ autoplay: false }),
    video({ loop: false }),
    video({ tagName: "AUDIO" })
  ];
  const environment = harness(media);
  environment.visibility(true);
  environment.visibility(false);
  for (const item of media) {
    assert.equal(item.pauses, 0);
    assert.equal(item.plays, 0);
  }
});

test("previously paused media stays paused", () => {
  const loop = video({ paused: true });
  const environment = harness([loop]);
  environment.visibility(true);
  environment.visibility(false);
  assert.equal(loop.pauses, 0);
  assert.equal(loop.plays, 0);
});

test("a hidden play event pauses a new decorative loop without polling", () => {
  const environment = harness([], { hidden: true });
  const loop = video();
  environment.document.videos.push(loop);
  environment.document.dispatch("play", loop);
  assert.equal(loop.pauses, 1);
  environment.visibility(false);
  assert.equal(loop.plays, 1);
});

test("play events from audio, controls and streams stay untouched", () => {
  const environment = harness([], { hidden: true });
  for (const item of [null, {}, video({ tagName: "AUDIO" }), video({ srcObject: {} }), video({ controls: true })]) {
    environment.document.dispatch("play", item);
    if (item && item.pauses !== undefined) assert.equal(item.pauses, 0);
  }
});

test("detached media is not resumed after reconnection", () => {
  const loop = video();
  const environment = harness([loop]);
  environment.visibility(true);
  loop.isConnected = false;
  environment.visibility(false);
  loop.isConnected = true;
  environment.visibility(false);
  assert.equal(loop.plays, 0);
});

test("changed media source is not resumed", () => {
  const loop = video();
  const environment = harness([loop]);
  environment.visibility(true);
  loop.currentSrc = "https://example.invalid/replacement.webm";
  environment.visibility(false);
  assert.equal(loop.plays, 0);
});

test("a loop converted to a call stream is not resumed", () => {
  const loop = video();
  const environment = harness([loop]);
  environment.visibility(true);
  loop.srcObject = {};
  environment.visibility(false);
  assert.equal(loop.plays, 0);
});

test("changed playback preferences are respected", () => {
  for (const changes of [{ controls: true }, { muted: false }, { autoplay: false }, { loop: false }]) {
    const loop = video();
    const environment = harness([loop]);
    environment.visibility(true);
    Object.assign(loop, changes);
    environment.visibility(false);
    assert.equal(loop.plays, 0);
  }
});

test("failed pause is not added to the resume set", () => {
  const loop = video({ pause() { throw new Error("pause failed"); } });
  const environment = harness([loop]);
  environment.visibility(true);
  loop.paused = true;
  environment.visibility(false);
  assert.equal(loop.plays, 0);
});

test("autoplay errors are caught and rejected playback is handled", () => {
  const throwing = video({ play() { throw new Error("autoplay denied"); } });
  let rejectionHandled = false;
  const rejecting = video({ play() { return { catch(handler) { rejectionHandled = true; handler(new Error("denied")); } }; } });
  const environment = harness([throwing, rejecting]);
  environment.visibility(true);
  environment.visibility(false);
  assert.equal(rejectionHandled, true);
});

test("styles keep loading animations, avatars, media and functional icons", () => {
  for (const css of [discordCss, steamCss]) {
    assert.match(css, /--tawop-mod-version:\s*"TAWOP_MOD_V3"/);
    assert.doesNotMatch(css, /(?:^|[;{\s])(?:animation(?:-[a-z]+)*|opacity)\s*:/);
    assert.doesNotMatch(css, /__TAWOP_LOGO_DATA_URI__|tawop-wordmark|url\(/);
    assert.match(css, /focus-visible/);
    assert.match(css, /forced-colors:\s*active/);
    for (const rule of css.matchAll(/([^{}]+)\{([^{}]*display:\s*none[^{}]*)\}/g)) {
      assert.doesNotMatch(rule[1], /(?:\bvideo\b|\bimg\b|\bsvg\b|\.avatar_|SteamLogoThrobber|spinner|progress)/i);
    }
  }
  assert.match(discordCss, /\.focus-rings-ring/);
  assert.match(steamCss, /\.gpfocus/);
});

function variable(css, name) {
  const value = css.match(new RegExp("--" + name + ":\\s*(#[0-9a-fA-F]{6})\\s*;"));
  assert.ok(value, name + " must be a solid RGB color");
  return value[1];
}

function luminance(hex) {
  const channels = [1, 3, 5].map(index => parseInt(hex.slice(index, index + 2), 16) / 255);
  const linear = channels.map(value => value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4);
  return linear[0] * 0.2126 + linear[1] * 0.7152 + linear[2] * 0.0722;
}

function contrast(foreground, background) {
  const values = [luminance(foreground), luminance(background)].sort((a, b) => b - a);
  return (values[0] + 0.05) / (values[1] + 0.05);
}

test("declared text colors meet 4.5:1 contrast on the declared surfaces", () => {
  const backgrounds = ["background-primary", "background-secondary", "background-floating", "background-surface-highest"];
  const texts = ["text-default", "text-secondary", "text-muted", "text-link"];
  let minimum = Infinity;
  for (const background of backgrounds) {
    for (const text of texts) {
      const ratio = contrast(variable(discordCss, text), variable(discordCss, background));
      assert.ok(ratio >= 4.5, text + " / " + background + " ratio=" + ratio);
      minimum = Math.min(minimum, ratio);
    }
  }
  for (const state of ["brand-500", "brand-560", "brand-600"]) {
    assert.ok(contrast("#ffffff", variable(discordCss, state)) >= 4.5, state + " white label contrast");
  }
  const steamRatio = contrast(variable(steamCss, "tawop-foreground"), variable(steamCss, "tawop-surface"));
  assert.ok(steamRatio >= 4.5);
  process.stdout.write("Declared palette minimum text contrast: " + minimum.toFixed(2) + ":1; Steam surface: " + steamRatio.toFixed(2) + ":1\n");
});

process.stdout.write("Preload and stylesheet tests passed: " + passed + "\n");
