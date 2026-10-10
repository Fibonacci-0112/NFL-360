// Light/dark mode. The server reads the "theme" cookie and renders <html data-theme> and the MudBlazor palette
// to match (see ThemePreference.cs), so switching only has to update the cookie and reload.
(() => {
  const root = document.documentElement;
  const save = (theme) => {
    document.cookie = `theme=${theme}; path=/; max-age=31536000; samesite=lax${location.protocol === "https:" ? "; secure" : ""}`;
  };

  // First visit: adopt the system preference, reloading once if the server guessed differently.
  if (!/(?:^|;\s*)theme=/.test(document.cookie)) {
    const theme = window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
    save(theme);
    if (root.dataset.theme !== theme) {
      location.reload();
    }
  }

  // Delegated so the header toggle keeps working after enhanced navigation replaces it.
  document.addEventListener("click", (event) => {
    if (event.target instanceof Element && event.target.closest("[data-theme-toggle]")) {
      save(root.dataset.theme === "dark" ? "light" : "dark");
      location.reload();
    }
  });
})();
