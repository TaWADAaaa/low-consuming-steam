;(function tawopDiscord() {
  "use strict";

  var tawopModVersion = "TAWOP_MOD_V3";
  var styleText = __TAWOP_STYLESHEET_JSON__;
  var pausedVideos = new WeakMap();

  function isBackgroundLoop(video) {
    return video && video.tagName === "VIDEO" && video.isConnected &&
      video.autoplay && video.loop && video.muted && !video.controls &&
      !video.srcObject && isFinite(video.duration) && video.duration > 0;
  }

  function sourceOf(video) {
    return video.currentSrc || video.src || "";
  }

  function pauseLoop(video) {
    if (!document.hidden || !isBackgroundLoop(video) || video.paused) return;

    pausedVideos.set(video, sourceOf(video));
    try {
      video.pause();
    } catch (_) {
      pausedVideos.delete(video);
    }
  }

  function onVisibility() {
    var videos = document.querySelectorAll("video[autoplay][loop]");
    if (document.hidden) {
      for (var i = 0; i < videos.length; i++) pauseLoop(videos[i]);
      return;
    }

    var pending = pausedVideos;
    pausedVideos = new WeakMap();
    for (var j = 0; j < videos.length; j++) {
      var video = videos[j];
      if (!pending.has(video) || !isBackgroundLoop(video) || !video.paused ||
          pending.get(video) !== sourceOf(video)) continue;

      try {
        var result = video.play();
        if (result && typeof result.catch === "function") result.catch(function () {});
      } catch (_) {}
    }
  }

  function install() {
    if (document.getElementById("tawop-style")) return;

    var style = document.createElement("style");
    style.id = "tawop-style";
    style.textContent = styleText;
    (document.head || document.documentElement).appendChild(style);
    document.documentElement.setAttribute("data-tawop", "1");
    document.documentElement.setAttribute("data-tawop-version", tawopModVersion);
    document.addEventListener("visibilitychange", onVisibility);
    document.addEventListener("play", function (event) {
      pauseLoop(event.target);
    }, true);
    onVisibility();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", install, { once: true });
  } else {
    install();
  }
}());
