(function () {
  "use strict";

  function initReviewsToast(root) {
    var msg = (root.getAttribute("data-reviews-toast") || "").trim();
    if (!msg) return;

    var reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    var toast = document.createElement("div");
    toast.className = "reviews-toast" + (reducedMotion ? "" : " reviews-toast--animate");
    toast.setAttribute("role", "status");
    toast.setAttribute("aria-live", "polite");
    toast.setAttribute("aria-atomic", "true");

    var inner = document.createElement("div");
    inner.className = "reviews-toast__inner";

    var icon = document.createElement("span");
    icon.className = "reviews-toast__icon";
    icon.setAttribute("aria-hidden", "true");
    icon.innerHTML =
      '<svg width="20" height="20" viewBox="0 0 20 20" focusable="false"><path fill="currentColor" d="M7.8 13.6 4.3 10l1.4-1.4 2.1 2.1 5.4-5.4L15.7 7 9.2 13.5a1 1 0 0 1-1.4.1Z"/></svg>';

    var text = document.createElement("p");
    text.className = "reviews-toast__text";
    text.textContent = msg;

    var dismiss = document.createElement("button");
    dismiss.type = "button";
    dismiss.className = "reviews-toast__dismiss";
    dismiss.setAttribute("aria-label", "Dismiss notification");
    dismiss.appendChild(document.createTextNode("\u00D7"));

    inner.appendChild(icon);
    inner.appendChild(text);
    inner.appendChild(dismiss);
    toast.appendChild(inner);
    document.body.appendChild(toast);

    var timeoutId = null;

    function removeListeners() {
      document.removeEventListener("keydown", onKeyDown);
    }

    function closeToast() {
      if (timeoutId) {
        clearTimeout(timeoutId);
        timeoutId = null;
      }
      removeListeners();
      if (toast.parentNode) {
        toast.parentNode.removeChild(toast);
      }
    }

    function onKeyDown(e) {
      if (e.key === "Escape") {
        e.preventDefault();
        closeToast();
      }
    }

    dismiss.addEventListener("click", closeToast);
    document.addEventListener("keydown", onKeyDown);

    timeoutId = window.setTimeout(closeToast, 5500);
  }

  function initReviewClampExpand() {
    var pairs = [];
    document.querySelectorAll("[data-review-clamp]").forEach(function (el) {
      var wrap = el.parentElement;
      if (!wrap) return;
      var btn = wrap.querySelector("[data-review-expand]");
      if (!btn) return;
      pairs.push({ el: el, btn: btn });

      function measureOne() {
        if (el.classList.contains("is-expanded")) return;
        if (el.scrollHeight > el.clientHeight + 2) {
          btn.hidden = false;
        } else {
          btn.hidden = true;
        }
      }

      window.requestAnimationFrame(measureOne);

      btn.addEventListener("click", function () {
        var expanded = !el.classList.contains("is-expanded");
        el.classList.toggle("is-expanded", expanded);
        btn.setAttribute("aria-expanded", expanded ? "true" : "false");
        btn.textContent = expanded ? "Show less" : "Read more";
      });
    });

    if (pairs.length === 0) return;

    var resizeTimer = null;
    window.addEventListener("resize", function () {
      window.clearTimeout(resizeTimer);
      resizeTimer = window.setTimeout(function () {
        pairs.forEach(function (p) {
          if (p.el.classList.contains("is-expanded")) return;
          if (p.el.scrollHeight > p.el.clientHeight + 2) {
            p.btn.hidden = false;
          } else {
            p.btn.hidden = true;
          }
        });
      }, 150);
    });
  }

  function initReviewsFormBusy() {
    var form = document.getElementById("reviews-filter-form");
    var panel = document.getElementById("reviews-results-panel");
    if (!form || !panel) return;

    form.addEventListener("submit", function () {
      panel.setAttribute("aria-busy", "true");
      panel.classList.add("reviews-results-panel--busy");
    });
  }

  function initLazyReviewCards() {
    var grid = document.getElementById("reviews-scroll-grid");
    if (!grid) return;

    var cards = Array.prototype.slice.call(grid.querySelectorAll(".review-lazy-card"));
    if (cards.length === 0) return;

    var initialCount = 9;
    var batchCount = 6;
    var visibleCount = initialCount;

    function applyVisibility() {
      cards.forEach(function (card, idx) {
        if (idx >= visibleCount) {
          card.classList.add("review-card-hidden");
        } else {
          card.classList.remove("review-card-hidden");
        }
      });
    }

    applyVisibility();
    if (cards.length <= initialCount) return;

    var sentinel = document.createElement("div");
    sentinel.className = "reviews-sentinel";
    grid.appendChild(sentinel);

    var observer = new IntersectionObserver(
      function (entries) {
        entries.forEach(function (entry) {
          if (!entry.isIntersecting) return;
          visibleCount = Math.min(cards.length, visibleCount + batchCount);
          applyVisibility();
          if (visibleCount >= cards.length) {
            observer.disconnect();
            if (sentinel.parentNode) {
              sentinel.parentNode.removeChild(sentinel);
            }
          }
        });
      },
      { threshold: 0.1 }
    );

    observer.observe(sentinel);
  }

  function init() {
    var root = document.getElementById("reviews-page-root");
    if (!root) return;
    initReviewsToast(root);
    initReviewClampExpand();
    initReviewsFormBusy();
    initLazyReviewCards();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
})();
