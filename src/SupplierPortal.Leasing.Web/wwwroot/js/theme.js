// Portal-wide light/dark theme. Sets both our own `data-theme` (custom .di-* chrome) and Bootstrap's
// native `data-bs-theme` (v5.3 components), and persists the choice in localStorage. The initial value is
// applied by a tiny inline script in <head> (see App.razor) so there is no flash of the wrong theme.
window.diTheme = {
    get: function () {
        return document.documentElement.getAttribute('data-theme') || 'light';
    },
    set: function (t) {
        var theme = (t === 'dark') ? 'dark' : 'light';
        var root = document.documentElement;
        root.setAttribute('data-theme', theme);
        root.setAttribute('data-bs-theme', theme);
        try { localStorage.setItem('di-theme', theme); } catch (e) { /* storage blocked */ }
    },
    toggle: function () {
        this.set(this.get() === 'dark' ? 'light' : 'dark');
    }
};
