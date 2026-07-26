(function () {
  "use strict";

  var root = document.documentElement;
  var header = document.querySelector("[data-site-header]");
  var menuToggle = document.querySelector("[data-menu-toggle]");
  var themeToggle = document.querySelector("[data-theme-toggle]");

  if (menuToggle && header) {
    menuToggle.addEventListener("click", function () {
      var open = header.classList.toggle("is-menu-open");
      menuToggle.setAttribute("aria-expanded", String(open));
      menuToggle.setAttribute("aria-label", open ? "Close navigation" : "Open navigation");
    });

    header.querySelectorAll("a").forEach(function (link) {
      link.addEventListener("click", function () {
        header.classList.remove("is-menu-open");
        menuToggle.setAttribute("aria-expanded", "false");
      });
    });
  }

  if (themeToggle) {
    themeToggle.addEventListener("click", function () {
      var current = root.dataset.theme;
      var systemDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
      var next = current ? (current === "dark" ? "light" : "dark") : (systemDark ? "light" : "dark");
      root.dataset.theme = next;
      localStorage.setItem("xtermsharp-theme", next);
    });
  }

  var docsPage = document.querySelector(".docs-page");
  var openSidebar = document.querySelector("[data-sidebar-open]");
  var closeSidebar = document.querySelector("[data-sidebar-close]");
  var sidebarScrim = document.querySelector("[data-sidebar-scrim]");

  function setSidebar(open) {
    if (!docsPage) return;
    docsPage.classList.toggle("is-sidebar-open", open);
    document.body.style.overflow = open ? "hidden" : "";
  }

  if (openSidebar) openSidebar.addEventListener("click", function () { setSidebar(true); });
  if (closeSidebar) closeSidebar.addEventListener("click", function () { setSidebar(false); });
  if (sidebarScrim) sidebarScrim.addEventListener("click", function () { setSidebar(false); });

  var article = document.querySelector("[data-doc-content]");
  var toc = document.querySelector("[data-page-toc]");

  if (article && toc) {
    var headings = Array.from(article.querySelectorAll("h2, h3"));
    headings.forEach(function (heading) {
      if (!heading.id) return;
      var item = document.createElement("li");
      var link = document.createElement("a");
      link.href = "#" + heading.id;
      link.textContent = heading.textContent;
      if (heading.tagName === "H3") item.className = "is-subsection";
      item.appendChild(link);
      toc.appendChild(item);
    });

    if ("IntersectionObserver" in window && headings.length) {
      var tocLinks = Array.from(toc.querySelectorAll("a"));
      var observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
          if (!entry.isIntersecting) return;
          tocLinks.forEach(function (link) {
            link.classList.toggle("is-visible", link.getAttribute("href") === "#" + entry.target.id);
          });
        });
      }, { rootMargin: "-18% 0px -70% 0px" });
      headings.forEach(function (heading) { observer.observe(heading); });
    }
  }

  document.querySelectorAll("pre").forEach(function (pre) {
    if (!pre.querySelector("code")) return;
    var button = document.createElement("button");
    button.type = "button";
    button.className = "copy-code";
    button.textContent = "Copy";
    button.addEventListener("click", function () {
      var code = pre.querySelector("code").textContent;
      navigator.clipboard.writeText(code).then(function () {
        button.textContent = "Copied";
        window.setTimeout(function () { button.textContent = "Copy"; }, 1400);
      });
    });
    pre.appendChild(button);
  });

  var searchDialog = document.querySelector("[data-search-dialog]");
  var searchInput = document.querySelector("[data-search-input]");
  var searchResults = document.querySelector("[data-search-results]");
  var searchStatus = document.querySelector("[data-search-status]");
  var searchPages = null;

  function loadSearchPages() {
    if (searchPages) return Promise.resolve(searchPages);
    return fetch(searchDialog.dataset.searchIndex)
      .then(function (response) {
        if (!response.ok) throw new Error("Search index unavailable");
        return response.json();
      })
      .then(function (pages) {
        searchPages = pages;
        return pages;
      });
  }

  function renderSearch(query) {
    var terms = query.toLowerCase().trim().split(/\s+/).filter(Boolean);
    searchResults.replaceChildren();

    if (!terms.length) {
      searchStatus.textContent = "Start typing to search the documentation.";
      return;
    }

    loadSearchPages().then(function (pages) {
      var matches = pages.map(function (page) {
        var title = page.title.toLowerCase();
        var description = page.description.toLowerCase();
        var content = page.content.toLowerCase();
        var score = terms.reduce(function (total, term) {
          return total + (title.includes(term) ? 8 : 0) + (description.includes(term) ? 4 : 0) + (content.includes(term) ? 1 : 0);
        }, 0);
        return { page: page, score: score };
      }).filter(function (match) {
        return match.score > 0;
      }).sort(function (a, b) {
        return b.score - a.score || a.page.title.localeCompare(b.page.title);
      }).slice(0, 8);

      searchStatus.textContent = matches.length ? matches.length + (matches.length === 1 ? " result" : " results") : "No matching documentation found.";
      matches.forEach(function (match) {
        var item = document.createElement("li");
        var link = document.createElement("a");
        var title = document.createElement("strong");
        var description = document.createElement("span");
        link.href = match.page.url;
        title.textContent = match.page.title;
        description.textContent = match.page.description || "Open documentation";
        link.append(title, description);
        item.appendChild(link);
        searchResults.appendChild(item);
      });
    }).catch(function () {
      searchStatus.textContent = "Search is temporarily unavailable.";
    });
  }

  if (searchDialog && searchInput && searchResults && searchStatus) {
    document.querySelectorAll("[data-search-open]").forEach(function (button) {
      button.addEventListener("click", function () {
        searchDialog.showModal();
        window.setTimeout(function () { searchInput.focus(); }, 0);
        loadSearchPages().catch(function () {});
      });
    });

    document.querySelector("[data-search-close]").addEventListener("click", function () {
      searchDialog.close();
    });

    searchDialog.addEventListener("click", function (event) {
      if (event.target === searchDialog) searchDialog.close();
    });

    searchInput.addEventListener("input", function () {
      renderSearch(searchInput.value);
    });
  }
}());
