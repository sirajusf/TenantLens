(() => {
  const loader = document.getElementById("page-loader");
  if (!loader) return;

  let isHiding = false;

  const hideLoader = () => {
    if (isHiding) return;
    isHiding = true;
    loader.classList.add("is-hidden");
    window.setTimeout(() => {
      loader.setAttribute("aria-hidden", "true");
    }, 360);
  };

  const showLoader = () => {
    loader.classList.remove("is-hidden");
    loader.removeAttribute("aria-hidden");
    isHiding = false;
  };

  window.addEventListener("load", hideLoader, { once: true });
  window.addEventListener("pageshow", (event) => {
    if (event.persisted) {
      hideLoader();
    }
  });

  document.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) return;

    const anchor = target.closest("a[href]");
    if (!anchor) return;

    if (anchor.hasAttribute("download")) return;
    if (anchor.target && anchor.target !== "_self") return;
    if (anchor.getAttribute("href")?.startsWith("#")) return;

    const href = anchor.getAttribute("href");
    if (!href) return;

    try {
      const toUrl = new URL(href, window.location.href);
      if (toUrl.origin !== window.location.origin) return;
      if (toUrl.pathname === window.location.pathname && toUrl.search === window.location.search && toUrl.hash) return;
      showLoader();
    } catch {
      // Ignore malformed href values.
    }
  });

  document.addEventListener("submit", (event) => {
    if (event.defaultPrevented) return;
    const form = event.target;
    if (!(form instanceof HTMLFormElement)) return;
    showLoader();
  });
})();

(() => {
  const topButtons = Array.from(document.querySelectorAll("[data-scroll-top]"));
  if (topButtons.length === 0) return;

  const updateVisibility = () => {
    const show = window.scrollY > 260;
    topButtons.forEach((button) => {
      button.classList.toggle("is-visible", show);
    });
  };

  topButtons.forEach((button) => {
    button.addEventListener("click", () => {
      window.scrollTo({ top: 0, behavior: "smooth" });
    });
  });

  window.addEventListener("scroll", updateVisibility, { passive: true });
  updateVisibility();
})();
