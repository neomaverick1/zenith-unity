// Browser helpers: vibration, the share sheet (or the clipboard), and the screen's pixel density.
mergeInto(LibraryManager.library, {
  ZenithVibrate: function (pattern) {
    try {
      var p = UTF8ToString(pattern).split(',').map(Number);
      if (navigator.vibrate) navigator.vibrate(p.length === 1 ? p[0] : p);
    } catch (e) {}
  },
  ZenithShare: function (title, text, url) {
    var t = UTF8ToString(title), x = UTF8ToString(text), u = UTF8ToString(url);
    try {
      if (navigator.share) navigator.share({ title: t, text: x, url: u }).catch(function () {});
      else if (navigator.clipboard) navigator.clipboard.writeText(x + ' ' + u);
    } catch (e) {}
  },
  ZenithPixelRatio: function () {
    return window.zenithDpr || window.devicePixelRatio || 1;
  },
  ZenithIsMobile: function () {
    return /Android|iPhone|iPad|iPod/i.test(navigator.userAgent) || (navigator.maxTouchPoints > 1 && /Macintosh/.test(navigator.userAgent)) ? 1 : 0;
  },
});
